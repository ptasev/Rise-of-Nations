using RoNLibrary.Formats.Bh3;
using RoNLibrary.Formats.Bha;
using RoNLibrary.Formats.Gltf;

namespace RoNLibrary.Tests.Formats.Gltf;

public class Bh3GltfConverterTests
{
    [Theory]
    [InlineData("data/ADVFighter.BH3", "data/ADVFighter_attack1.BHa")]
    public void Convert_ReturnsExpectedData(string filePath, string animFilePath)
    {
        // Arrange
        var bh3 = Bh3File.Open(filePath);
        var bha = BhaFile.Open(animFilePath);
        var parameters = new Bh3GltfParameters
        {
            MeshFilePath = filePath,
            AnimFilePath = animFilePath
        };

        var converter = new Bh3GltfConverter();

        // Act
        var result = converter.Convert(bh3, bha, parameters);

        // Assert
        Assert.Single(result.LogicalMeshes);
        Assert.Single(result.LogicalAnimations);
        Assert.Single(result.LogicalMaterials);
        Assert.Single(result.LogicalTextures);
        Assert.Single(result.LogicalImages);
        
        Assert.Equal(parameters.MeshName, result.LogicalMeshes[0].Name);
        Assert.Equal(parameters.AnimName, result.LogicalAnimations[0].Name);
    }
}
