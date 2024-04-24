using RoNLibrary.Formats.Bh3;

namespace RoNLibrary.Tests.Formats.Bh3;

public class Bh3FileTests
{
    [Theory]
    [InlineData("data/ADVFighter.BH3")]
    public void ReadWrite_DoesNotChangeData(string filePath)
    {
        // Arrange
        using var fsi = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var msi = new MemoryStream();
        using var mso = new MemoryStream();

        fsi.CopyTo(msi);
        fsi.Seek(0, SeekOrigin.Begin);
        msi.Seek(0, SeekOrigin.Begin);

        // Act
        var file = Bh3File.Open(fsi);
        file.Write(mso);

        // Assert
        mso.Seek(0, SeekOrigin.Begin);
        Assert.Equal(msi.ToArray(), mso.ToArray());
    }
}