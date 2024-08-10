using RoNLibrary.Formats.Gltf;

using SharpGLTF.Schema2;

namespace RoNLibrary.Tests.Formats.Gltf;

public class GltfBh3ConverterTests
{
    [Theory]
    [InlineData("data/ADVFighter_attack1.glb")]
    public Task Convert_ReturnsExpectedData(string filePath)
    {
        // Arrange
        var gltf = ModelRoot.Load(filePath);
        var parameters = new GltfBh3Parameters
        {
            ConvertMeshes = true,
            ConvertAnimations = true
        };

        var converter = new GltfBh3Converter();

        // Act
        var result = converter.Convert(gltf, parameters);

        // Assert
        return Verify(result);
    }
}
