using RoNLibrary.Formats.Bha;

namespace RoNLibrary.Tests.Formats.Bha;

public class BhaFileTests
{
    [Theory]
    [InlineData("data/ADVFighter_attack1.BHa")]
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
        var file = BhaFile.Open(fsi);
        file.Write(mso);

        // Assert
        mso.Seek(0, SeekOrigin.Begin);
        Assert.Equal(msi.ToArray(), mso.ToArray());
    }
}