using ConsoleAppFramework;

using Microsoft.Extensions.Logging;

using RoNLibrary.Formats.Bh3;
using RoNLibrary.Formats.Bha;
using RoNLibrary.Formats.Gltf;

using SharpGLTF.Schema2;

namespace RoNFileConverter;

internal class Commands(Bh3GltfConverter bh3GltfConverter, GltfBh3Converter gltfBh3Converter, ILogger<Commands> logger)
{
    /// <summary>Convert a file.</summary>
    /// <param name="filePath">The file path to convert.</param>
    [Command("")]
    public void Root([Argument] string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        switch (ext)
        {
            case ".bh3":
                {
                    Bh3(filePath);
                    break;
                }
            case ".glb":
            case ".gltf":
                {
                    Gltf(filePath);
                    break;
                }
            default:
                throw new NotSupportedException($"File extension '{ext}' is not supported.");
        }
    }

    /// <summary>Convert bh3 file to glTF.</summary>
    /// <param name="filePath">The bh3 file path.</param>
    /// <param name="animFilePath">-a, The bha file path.</param>
    /// <param name="outputFilePath">-o, The output file path.</param>
    public void Bh3([Argument] string filePath, string? animFilePath = null, string? outputFilePath = null)
    {
        logger.LogInformation("Converting bh3 to glTF...");
        var bh3 = Bh3File.Open(filePath);
        var bha = animFilePath is not null ? BhaFile.Open(animFilePath) : null;

        var parameters = new Bh3GltfParameters
        {
            MeshFilePath = filePath,
            AnimFilePath = animFilePath
        };
        var result = bh3GltfConverter.Convert(bh3, bha, parameters);
        outputFilePath ??= Path.ChangeExtension(filePath, ".glb");
        result.Save(outputFilePath);
    }

    /// <summary>Convert glTF file to bh3/bha.</summary>
    /// <param name="filePath">The glTF file path.</param>
    /// <param name="convertMesh">-cm, Whether to convert the mesh to a bh3 file.</param>
    /// <param name="convertAnim">-ca, Whether to convert the animation to a bha file.</param>
    /// <param name="sceneNameOrIndex">-sn, The name or index of the scene.</param>
    /// <param name="animNameOrIndex">-an, The name or index of the animation.</param>
    /// <param name="modelOutputFilePath">-mo, The output file path for the model.</param>
    /// <param name="animOutputFilePath">-ao, The output file path for the animation.</param>
    public void Gltf(
        [Argument] string filePath,
        bool convertMesh = true,
        bool convertAnim = true,
        string sceneNameOrIndex = "-1",
        string animNameOrIndex = "0",
        string? modelOutputFilePath = null,
        string? animOutputFilePath = null)
    {
        logger.LogInformation("Converting glTF to bh3/bha...");
        var gltf = ModelRoot.Load(filePath);

        var sceneIndex = gltf.LogicalScenes.FirstOrDefault(x =>
            string.Equals(x.Name, sceneNameOrIndex, StringComparison.OrdinalIgnoreCase))?.LogicalIndex ?? -1;
        if (sceneIndex == -1)
        {
            int.TryParse(sceneNameOrIndex, out sceneIndex);
        }

        var animIndex = gltf.LogicalAnimations.FirstOrDefault(x =>
            string.Equals(x.Name, animNameOrIndex, StringComparison.OrdinalIgnoreCase))?.LogicalIndex ?? -1;
        if (animIndex == -1)
        {
            int.TryParse(animNameOrIndex, out animIndex);
        }

        var parameters = new GltfBh3Parameters
        {
            ConvertMeshes = convertMesh,
            ConvertAnimations = convertAnim,
            SceneIndex = sceneIndex,
            AnimationIndex = animIndex,
        };
        var result = gltfBh3Converter.Convert(gltf, parameters);

        if (parameters.ConvertMeshes)
        {
            modelOutputFilePath ??= Path.ChangeExtension(filePath, ".bh3");
            result.Bh3.Write(modelOutputFilePath);
        }

        if (parameters.ConvertAnimations && result.Bha is not null)
        {
            animOutputFilePath ??= Path.ChangeExtension(filePath, ".bha");
            result.Bha.Write(animOutputFilePath);
        }
    }
}
