using System.Numerics;
using System.Runtime.CompilerServices;

using SharpGLTF.Runtime;
using SharpGLTF.Schema2;

using RoNLibrary.Formats.Bh3;
using RoNLibrary.Formats.Bha;

namespace RoNLibrary.Formats.Gltf;

public class GltfBh3Converter
{
    private static readonly Quaternion RotX90Quat;
    private static readonly Matrix4x4 RotX90;

    static GltfBh3Converter()
    {
        RotX90 = Matrix4x4.CreateRotationX(MathF.PI * 0.5f);
        RotX90Quat = Quaternion.CreateFromRotationMatrix(RotX90);
    }

    private List<int> GetJoints(Skin skin)
    {
        return GetJointsField(skin);
        
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_joints")]
        extern static ref List<int> GetJointsField(Skin skin);
    }

    public (Bh3File Bh3, BhaFile? Bha) Convert(ModelRoot gltf, GltfBh3Parameters parameters)
    {
        var bh3 = new Bh3File();

        // Pick the chosen scene and animation
        var scene = parameters.SceneIndex < 0 || parameters.SceneIndex >= gltf.LogicalScenes.Count
            ? gltf.DefaultScene
            : gltf.LogicalScenes[parameters.SceneIndex];
        var animIndex = parameters.AnimationIndex;
        if (animIndex < 0 || animIndex >= gltf.LogicalAnimations.Count) animIndex = 0;
        var anim = gltf.LogicalAnimations.Count > 0 ? gltf.LogicalAnimations[animIndex] : null;

        // Find all skinned meshes, get all skins, get all joints, get visual roots distinct
        var skinnedMeshNodes = Node.Flatten(scene).Where(x => x.Mesh is not null && x.Skin is not null).ToArray();
        if (skinnedMeshNodes.Length <= 0)
        {
            throw new InvalidDataException("No skinned meshes were found.");
        }

        var skinNodes = skinnedMeshNodes.SelectMany(x =>
            (x.Skin.Skeleton is null ? GetJoints(x.Skin) : GetJoints(x.Skin).Append(x.Skin.Skeleton.LogicalIndex))
            .Select(idx => x.LogicalParent.LogicalNodes[idx])).ToHashSet();
        var skinRoots = skinNodes.Select(x => x.VisualRoot).Distinct();
        
        // Flatten and skip top-level nodes that have identity transform, are not animated, and are not joints
        var skippedParentIndices = new HashSet<int>();
        var nodes = skinRoots.SelectMany(Node.Flatten).Distinct().Where(x =>
        {
            // Non-root nodes that don't have skipped parents don't need the check
            if (x.VisualParent is not null && !skippedParentIndices.Contains(x.VisualParent.LogicalIndex))
            {
                return true;
            }
            
            // Checking for animation across entire file in case user will convert other animations from same file
            if (skinNodes.Contains(x) ||
                (x.VisualChildren.Any() && (x.WorldMatrix != Matrix4x4.Identity || x.IsTransformAnimated)))
            {
                return true;
            }

            skippedParentIndices.Add(x.LogicalIndex);
            return false;
        });

        var nodeBoneIndexMap = new Dictionary<int, BoneData>();
        var bha = anim is not null && parameters.ConvertAnimations ? new BhaFile() : null;
        ConvertSkeleton(bh3, bha, nodes, nodeBoneIndexMap, skippedParentIndices);

        if (parameters.ConvertMeshes)
        {
            ConvertMeshes(bh3, skinnedMeshNodes, nodeBoneIndexMap);
        }

        if (anim is not null && parameters.ConvertAnimations && bha is not null)
        {
            ConvertAnimation(gltf, anim, nodeBoneIndexMap);
            bha.Prune();
        }

        return (bh3, bha);
    }

    private record BoneData(Bh3Bone Bone, BhaBoneTrack? BoneTrack, List<int> VertexIndices, Vector3 Scale);
    private static void ConvertSkeleton(Bh3File bh3, BhaFile? bha, IEnumerable<Node> nodes,
        Dictionary<int, BoneData> nodeBoneIndexMap, IReadOnlySet<int> skippedParentIndices)
    {
        bh3.RootBone = new Bh3Bone { Name = "gltfRoot" };
        if (bha is not null)
        {
            bha.RootBoneTrack = new BhaBoneTrack();
        }

        nodeBoneIndexMap.Add(-1, new BoneData(bh3.RootBone, bha?.RootBoneTrack, [], Vector3.One));

        foreach (var node in nodes)
        {
            var bone = new Bh3Bone { Name = node.Name ?? $"node{node.LogicalIndex}" };
            BhaBoneTrack? boneTrack = null;

            var isRoot = node.VisualParent is null || skippedParentIndices.Contains(node.VisualParent.LogicalIndex);
            var parentIndex = isRoot ? -1 : (node.VisualParent?.LogicalIndex ?? -1);
            var parentMap = nodeBoneIndexMap[parentIndex];
            var scale = parentMap.Scale;

            if (isRoot)
            {
                // Rotate root to adjust for difference of world space
                bone.Translation = Vector3.Transform(node.LocalTransform.Translation, RotX90);
                bone.Rotation = Quaternion.Inverse(Quaternion.Concatenate(node.LocalTransform.Rotation, RotX90Quat));
            }
            else
            {
                // BH3 doesn't support scale, bake it in
                var parentNode = node.LogicalParent.LogicalNodes[parentIndex];
                scale *= parentNode.LocalTransform.Scale;
                bone.Translation = node.LocalTransform.Translation * scale;
                bone.Rotation = Quaternion.Inverse(node.LocalTransform.Rotation);
            }

            parentMap.Bone.Children.Add(bone);
            if (bha is not null)
            {
                boneTrack = new BhaBoneTrack();
                parentMap.BoneTrack?.Children.Add(boneTrack);
            }

            nodeBoneIndexMap.Add(node.LogicalIndex, new BoneData(bone, boneTrack, [], scale));
        }

        // Remove custom root if it only has one child
        if (bh3.RootBone.Children.Count == 1)
        {
            nodeBoneIndexMap.Remove(-1);
            bh3.RootBone = bh3.RootBone.Children[0];
            if (bha is not null)
            {
                bha.RootBoneTrack = bha.RootBoneTrack.Children[0];
            }
        }
    }

    private static void ConvertMeshes(
        Bh3File bh3,
        IEnumerable<Node> nodes,
        IReadOnlyDictionary<int, BoneData> nodeBoneIndexMap)
    {
        var matIdTexCoordSetMapping = new Dictionary<int, int>();

        var meshNodes = nodes.Where(n => n.Mesh != null);
        var baseVertexIndex = 0;
        foreach (var inst in meshNodes)
        {
            var mesh = inst.Mesh;

            if (!mesh.AllPrimitivesHaveJoints)
                throw new InvalidDataException($"Mesh ({mesh.Name}) primitives must have a skin.");

            if (mesh.MorphWeights.Count > 0)
                throw new InvalidDataException($"Mesh ({mesh.Name}) cannot have vertex morphs.");

            if (mesh.Primitives.Any(p => p.Material == null))
                throw new NotImplementedException($"Mesh ({mesh.Name}) primitives must not have a null material.");

            var gltfMats = mesh.Primitives.Select(p => p.Material);
            ConvertMaterials(gltfMats, matIdTexCoordSetMapping);
            if (matIdTexCoordSetMapping.Count > 1)
            {
                throw new InvalidDataException("All meshes must use the same material.");
            }

            // Now add a new mesh from mesh builder
            ConvertMesh(bh3, inst, ref baseVertexIndex, nodeBoneIndexMap, matIdTexCoordSetMapping);
        }
        
        // Remap vertices by bones
        var positions = new List<Vector4>();
        var normals = new List<Vector3>();
        var textureCoordinates = new List<Vector2>();
        var indexMap = new Dictionary<int, int>();
        foreach (var boneData in nodeBoneIndexMap.Values)
        {
            boneData.Bone.VertexStartIndex = positions.Count;
            boneData.Bone.VertexCount = boneData.VertexIndices.Count;

            foreach (var index in boneData.VertexIndices)
            {
                indexMap.Add(index, positions.Count);
                positions.Add(bh3.Positions[index]);
                normals.Add(bh3.Normals[index]);
                textureCoordinates.Add(bh3.TextureCoordinates[index]);
            }
        }

        bh3.Positions = positions;
        bh3.Normals = normals;
        bh3.TextureCoordinates = textureCoordinates;
        for (var i = 0; i < bh3.Indices.Count; i++)
        {
            var index = bh3.Indices[i];
            bh3.Indices[i] = System.Convert.ToUInt16(indexMap[index]);
        }
    }

    private static void ConvertMesh(
        Bh3File bh3,
        Node meshNode,
        ref int baseVertexIndex,
        IReadOnlyDictionary<int, BoneData> nodeBoneIndexMap,
        IReadOnlyDictionary<int, int> matIdTexCoordSetMapping)
    {
        // Get bone bindings
        var skinTransforms = new Matrix4x4[meshNode.Skin.JointsCount];
        var skinNormalTransforms = new Matrix4x4[meshNode.Skin.JointsCount];
        for (int i = 0; i < skinTransforms.Length; ++i)
        {
            var skinJoint = meshNode.Skin.GetJoint(i);
            
            // BH3 doesn't support scale, bake it in
            var scale = nodeBoneIndexMap[skinJoint.Joint.LogicalIndex].Scale;
            var skinTransform = skinJoint.InverseBindMatrix *
                                Matrix4x4.CreateScale(scale);
            skinTransforms[i] = skinTransform;
            
            if (!Matrix4x4.Invert(skinTransform, out var inverse))
            {
                throw new InvalidDataException(
                    $"Inverse bind matrix of node {skinJoint.Joint.Name} could not be inverted.");
            }
            
            skinNormalTransforms[i] = Matrix4x4.Transpose(inverse);
        }

        // Export Vertices, Normals, TexCoords, VertexWeights and Faces
        Mesh gltfMesh = meshNode.Mesh;
        var meshDecoder = gltfMesh.Decode();
        foreach (var p in meshDecoder.Primitives)
        {
            // skip primitives that aren't tris
            if (!p.TriangleIndices.Any())
                continue;

            // Get the new material index in grn
            int faceMatId = p.Material.LogicalIndex;
            if (!matIdTexCoordSetMapping.TryGetValue(faceMatId, out var texCoordSet))
            {
                throw new InvalidDataException($"Mesh ({gltfMesh.Name}) has an invalid material id " + faceMatId +
                                               ".");
            }

            // Make sure we have all the necessary data
            if (p.VertexCount < 3)
                throw new InvalidDataException($"Mesh ({gltfMesh.Name}) must have at least 3 positions.");

            if (p.TexCoordsCount <= texCoordSet)
                throw new InvalidDataException($"Mesh ({gltfMesh.Name}) must have tex coord set {texCoordSet}.");

            if (p.JointsWeightsCount < 4)
                throw new InvalidDataException($"Mesh ({gltfMesh.Name}) must have a set of joints.");
            if (p.JointsWeightsCount > 4)
                throw new InvalidOperationException(
                    $"Can't convert mesh ({gltfMesh.Name}) with more than one set of joints/weights (4).");

            // Grab the data
            for (int i = 0; i < p.VertexCount; ++i)
            {
                var pos = p.GetPosition(i);
                var norm = p.GetNormal(i);

                // Adjust vertex and normal by inverse bind matrix since BH3 doesn't store that
                var numAffected = 0;
                var finPos = Vector3.Zero;
                var finNorm = Vector3.Zero;
                var sw = p.GetSkinWeights(i);
                foreach (var iw in sw.GetNormalized().GetIndexedWeights())
                {
                    if (iw.Weight <= 0) continue;
                    numAffected++;

                    finPos += Vector3.Transform(pos, skinTransforms[iw.Index]) * iw.Weight;
                    finNorm += Vector3.TransformNormal(norm, skinNormalTransforms[iw.Index]) * iw.Weight;

                    var vertexIndex = baseVertexIndex + i;
                    if (vertexIndex > UInt16.MaxValue)
                    {
                        throw new InvalidOperationException(
                            $"The number of vertices in the scene must not exceed {UInt16.MaxValue}.");
                    }
                    
                    nodeBoneIndexMap[meshNode.Skin.GetJoint(iw.Index).Joint.LogicalIndex].VertexIndices
                        .Add(vertexIndex);
                }

                if (numAffected > 1)
                {
                    throw new InvalidDataException(
                        $"Each mesh ({gltfMesh.Name}) vertex ({i}) must only be affected by a single bone.");
                }

                finNorm = Vector3.Normalize(finNorm);
                bh3.Positions.Add(new Vector4(finPos, 1));
                bh3.Normals.Add(finNorm);
                bh3.TextureCoordinates.Add(p.GetTextureCoord(i, texCoordSet));
            }

            foreach (var tri in p.TriangleIndices)
            {
                var a = tri.A + baseVertexIndex;
                var b = tri.B + baseVertexIndex;
                var c = tri.C + baseVertexIndex;

                bh3.Indices.Add(System.Convert.ToUInt16(a));
                bh3.Indices.Add(System.Convert.ToUInt16(c));
                bh3.Indices.Add(System.Convert.ToUInt16(b));
            }

            baseVertexIndex += p.VertexCount;
        }
    }

    private static void ConvertAnimation(
        ModelRoot gltf,
        Animation gltfAnim,
        IReadOnlyDictionary<int, BoneData> nodeBoneIndexMap)
    {
        foreach (var pair in nodeBoneIndexMap)
        {
            // Skip root created earlier
            if (pair.Key == -1)
            {
                continue;
            }
            
            var node = gltf.LogicalNodes[pair.Key];
            var boneTrack = pair.Value.BoneTrack;
            if (boneTrack is null)
            {
                continue;
            }

            // Get translation animation data
            var translationSampler = gltfAnim.FindTranslationChannel(node)?.GetTranslationSampler();
            var translationCurve = translationSampler?.CreateCurveSampler();
            var translationTimes = translationSampler is null
                ? []
                : (translationSampler.InterpolationMode == AnimationInterpolationMode.CUBICSPLINE
                    ? translationSampler.GetCubicKeys().Select(x => x.Key)
                    : translationSampler.GetLinearKeys().Select(x => x.Key));
            
            var rotationSampler = gltfAnim.FindRotationChannel(node)?.GetRotationSampler();
            var rotationCurve = rotationSampler?.CreateCurveSampler();
            var rotationTimes = rotationSampler is null
                ? []
                : (rotationSampler.InterpolationMode == AnimationInterpolationMode.CUBICSPLINE
                    ? rotationSampler.GetCubicKeys().Select(x => x.Key)
                    : rotationSampler.GetLinearKeys().Select(x => x.Key));
            
            var scaleSampler = gltfAnim.FindScaleChannel(node)?.GetScaleSampler();
            var scaleTimes = scaleSampler is null
                ? []
                : (scaleSampler.InterpolationMode == AnimationInterpolationMode.CUBICSPLINE
                    ? scaleSampler.GetCubicKeys().Select(x => x.Key)
                    : scaleSampler.GetLinearKeys().Select(x => x.Key));

            var prevTime = 0f;
            var basePos = node.LocalTransform.Translation * pair.Value.Scale;
            var baseRotInv = Quaternion.Inverse(node.LocalTransform.Rotation);
            var times = translationTimes.Concat(rotationTimes).Concat(scaleTimes).Distinct().Order();
            foreach (var time in times)
            {
                var key = new BhaBoneTrackKey { Time = time - prevTime };
                prevTime = time;

                // BH3 doesn't support scale, bake it in
                key.Translation = translationCurve is null
                    ? Vector3.Zero
                    : Vector3.Transform(translationCurve.GetPoint(time) * GetScale(node, time) - basePos, baseRotInv);

                key.Rotation = rotationCurve is null
                    ? Quaternion.Identity
                    : Quaternion.Inverse(Quaternion.Concatenate(rotationCurve.GetPoint(time), baseRotInv));
                boneTrack.Keys.Add(key);
            }
        }

        Vector3 GetScale(Node node, float time)
        {
            // Loop through all parents that are getting exported and calculate the scale at time
            var scale = Vector3.One;
            var parent = node.VisualParent;
            while (parent is not null && nodeBoneIndexMap.ContainsKey(parent.LogicalIndex))
            {
                var scaleSampler = gltfAnim.FindScaleChannel(parent)?.GetScaleSampler();
                var scaleCurve = scaleSampler?.CreateCurveSampler();
                scale *= scaleCurve?.GetPoint(time) ?? parent.LocalTransform.Scale;
                parent = parent.VisualParent;
            }

            return scale;
        }
    }

    private static void ConvertMaterials(IEnumerable<Material?> gltfMats, Dictionary<int, int> matIdTexCoordSetMapping)
    {
        foreach (var gltfMat in gltfMats)
        {
            if (gltfMat is null)
            {
                continue;
            }

            int actualMatId = gltfMat.LogicalIndex;
            if (matIdTexCoordSetMapping.ContainsKey(actualMatId))
            {
                continue;
            }

            ConvertMaterial(gltfMat, out var texCoordSet);
            matIdTexCoordSetMapping.Add(actualMatId, texCoordSet);
        }
    }

    private static void ConvertMaterial(Material gltfMat, out int texCoordSet)
    {
        texCoordSet = GetDiffuseBaseColorTexCoord(gltfMat);

        static int GetDiffuseBaseColorTexCoord(Material srcMaterial)
        {
            var channel = srcMaterial.FindChannel("Diffuse");
            if (channel.HasValue) return channel.Value.TextureCoordinate;

            channel = srcMaterial.FindChannel("BaseColor");
            if (channel.HasValue) return channel.Value.TextureCoordinate;

            return 0;
        }
    }
}
