using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Memory;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;
using SixLabors.ImageSharp;
using System.Numerics;

using RoNLibrary.Formats.Bh3;
using RoNLibrary.Formats.Bha;

using GltfMeshBuilder = SharpGLTF.Geometry.MeshBuilder<
    SharpGLTF.Geometry.VertexTypes.VertexPositionNormal,
    SharpGLTF.Geometry.VertexTypes.VertexTexture1,
    SharpGLTF.Geometry.VertexTypes.VertexJoints4>;
using GltfVertexBuilder = SharpGLTF.Geometry.VertexBuilder<
    SharpGLTF.Geometry.VertexTypes.VertexPositionNormal,
    SharpGLTF.Geometry.VertexTypes.VertexTexture1,
    SharpGLTF.Geometry.VertexTypes.VertexJoints4>;

namespace RoNLibrary.Formats.Gltf;

public class Bh3GltfConverter
{
    private const string TrackName = "Default";
    private static readonly Matrix4x4 RotX90N;
    private static readonly Quaternion RotX90NQuat;

    static Bh3GltfConverter()
    {
        RotX90N = Matrix4x4.CreateRotationX(MathF.PI * -0.5f);
        RotX90NQuat = Quaternion.CreateFromRotationMatrix(RotX90N);
    }

    public ModelRoot Convert(Bh3File bh3, BhaFile? bha, Bh3GltfParameters parameters)
    {
        var sceneBuilder = new SceneBuilder();

        var skeletonData = ConvertSkeleton(bh3, bha);
        ConvertMeshes(bh3, sceneBuilder, skeletonData, parameters.MeshFilePath ?? string.Empty,
            parameters.MeshName ?? "MeshName");
        if (bha is not null)
        {
            ConvertAnimation(skeletonData, parameters.AnimName ?? TrackName);
        }

        return sceneBuilder.ToGltf2();
    }

    private record SkeletonData(GltfVertexBuilder[] Vertices, List<BoneData> Bones);
    private record BoneData(NodeBuilder Node, BhaBoneTrack? BoneTrack);
    private static SkeletonData ConvertSkeleton(Bh3File bh3, BhaFile? bha)
    {
        var vertices = new GltfVertexBuilder[bh3.Positions.Count];
        var bones = new List<BoneData>();
        var boneEnumerable = bh3.RootBone.ZipMatchingTreesDepthFirst(bha?.RootBoneTrack);
        var names = new HashSet<string>();

        var parentStack = new Stack<(int ParentIndex, int ChildrenLeft)>();
        foreach ((Bh3Bone bone, BhaBoneTrack? boneTrack) in boneEnumerable)
        {
            var boneName = bone.Name;
            var nameCount = 1;
            while (names.Contains(boneName))
            {
                boneName = bone.Name + nameCount;
                nameCount++;
            }

            names.Add(boneName);
            
            NodeBuilder node;
            var isRoot = bones.Count == 0;
            if (isRoot)
            {
                node = new NodeBuilder(boneName);
            }
            else
            {
                var stack = parentStack.Pop();
                stack.ChildrenLeft--;
                if (stack.ChildrenLeft > 0)
                {
                    parentStack.Push(stack);
                }

                var parent = bones[stack.ParentIndex].Node;
                node = parent.CreateNode(boneName);
            }

            ConvertBone(bone, node, isRoot);
            if (bone.Children.Count > 0)
            {
                parentStack.Push((bones.Count, bone.Children.Count));
            }

            var endIndex = bone.VertexStartIndex + bone.VertexCount;
            for (int i = bone.VertexStartIndex; i < endIndex; i++)
            {
                vertices[i] = GetVertexBuilder(bh3, i, bones.Count, node);
            }
            
            bones.Add(new BoneData(node, boneTrack));
        }

        return new SkeletonData(vertices, bones);
    }

    private static void ConvertBone(Bh3Bone bone, NodeBuilder nodeBuilder, bool adjustCoordSystem)
    {
        // Bh3 -- Left-handed X Left, Y Back, Z Up
        // glTF -- Right-handed X Left, Y Up, Z Forward
        // Convert -- X = X, Y = Z, Z = -Y
        var rot = Quaternion.Inverse(bone.Rotation);
        nodeBuilder.UseTranslation().Value = adjustCoordSystem ? Vector3.Transform(bone.Translation, RotX90N) : bone.Translation;
        nodeBuilder.UseRotation().Value = adjustCoordSystem ? Quaternion.Concatenate(rot, RotX90NQuat) : rot;
    }

    private static void ConvertMeshes(Bh3File bh3, SceneBuilder sceneBuilder, SkeletonData skeleton,
        string meshFilePath, string meshName)
    {
        var mb = ConvertMesh(bh3, skeleton.Vertices, meshFilePath, meshName);
        var joints = skeleton.Bones.Select(data => (data.Node, data.Node.GetInverseBindMatrix())).ToArray();
        sceneBuilder.AddSkinnedMesh(mb, joints).WithName(mb.Name);
    }

    private static GltfMeshBuilder ConvertMesh(Bh3File bh3, GltfVertexBuilder[] vertices, string meshFilePath,
        string meshName)
    {
        var mb = new GltfMeshBuilder(meshName);

        var material = ConvertMaterials(meshFilePath, meshName);
        var pb = mb.UsePrimitive(material);

        foreach (var tri in bh3.Indices.Chunk(3))
        {
            if (tri.Length != 3)
            {
                throw new InvalidOperationException("Expected indices array to be multiple of 3 to create triangle.");
            }

            // Adjust for handedness
            pb.AddTriangle(vertices[tri[0]], vertices[tri[2]], vertices[tri[1]]);
        }

        return mb;
    }

    private static GltfVertexBuilder GetVertexBuilder(Bh3File bh3, int index, int boneIndex, NodeBuilder node)
    {
        var vb = new GltfVertexBuilder();

        if (!Matrix4x4.Invert(node.WorldMatrix, out var inverse))
        {
            throw new InvalidDataException($"World matrix of node {node.Name} could not be inverted.");
        }
        
        var transposeInverse = Matrix4x4.Transpose(inverse);
        var vert = Vector4.Transform(bh3.Positions[index], node.WorldMatrix);
        var norm = Vector3.TransformNormal(bh3.Normals[index], transposeInverse);

        vb.Geometry = new VertexPositionNormal(new Vector3(vert.X, vert.Y, vert.Z), norm);
        vb.Material.TexCoord = bh3.TextureCoordinates[index];
        vb.Skinning = new VertexJoints4(boneIndex);

        return vb;
    }

    private static MaterialBuilder ConvertMaterials(string meshFilePath, string meshName)
    {
        var mb = new MaterialBuilder(meshName);
        mb.WithMetallicRoughnessShader();
        var cb = mb.UseChannel(KnownChannel.MetallicRoughness);
        cb.Parameters[KnownProperty.MetallicFactor] = 0.1f;
        cb.Parameters[KnownProperty.RoughnessFactor] = 0.5f;
        cb = mb.UseChannel(KnownChannel.BaseColor);
        cb.Parameters[KnownProperty.RGBA] = new Vector4(0.5f, 0.5f, 0.5f, 1);

        var imageBuilder = ConvertTextures(meshFilePath, meshName);
        if (imageBuilder is not null)
        {
            var tb = cb.UseTexture();
            tb.Name = imageBuilder.Name;
            tb.WrapS = TextureWrapMode.REPEAT;
            tb.WrapT = TextureWrapMode.REPEAT;
            tb.WithPrimaryImage(imageBuilder);
        }

        mb.WithAlpha();

        return mb;
    }

    private static ImageBuilder? ConvertTextures(string meshFilePath, string meshName)
    {
        var fileName = Path.Combine(Path.GetDirectoryName(meshFilePath)!, $"{meshName}.tga");
        if (string.IsNullOrWhiteSpace(meshName) || !File.Exists(fileName))
        {
            return null;
        }

        try
        {
            using var image = SixLabors.ImageSharp.Image.Load(fileName);
            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            
            var memImage = new MemoryImage(ms.ToArray());
            var imageBuilder = ImageBuilder.From(memImage, meshName);
            return imageBuilder;
        }
        catch
        {
            // TODO: log exception
            return null;
        }
    }

    private static void ConvertAnimation(SkeletonData skeleton, string animName)
    {
        for (int i = 0; i < skeleton.Bones.Count; ++i)
        {
            var boneData = skeleton.Bones[i];
            NodeBuilder node = boneData.Node;
            BhaBoneTrack? bone = boneData.BoneTrack;
            if (bone is null || bone.Keys.Count <= 0)
            {
                continue;
            }

            var time = 0f;
            var basePos = node.Translation.Value;
            var baseRot = node.Rotation.Value;
            var tbt = node.UseTranslation(animName);
            var tbr = node.UseRotation(animName);
            for (int j = 0; j < bone.Keys.Count; ++j)
            {
                var key = bone.Keys[j];
                time += key.Time;
                tbt.SetPoint(time, Vector3.Transform(key.Translation, baseRot) + basePos);
                tbr.SetPoint(time, Quaternion.Concatenate(Quaternion.Inverse(key.Rotation), baseRot));
            }
        }
    }
}
