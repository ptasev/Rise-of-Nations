using System.Numerics;

using RoNLibrary.Formats;
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

    [Fact]
    public void Prune_works()
    {
        var g = new BhaBoneTrack() { Keys = [new BhaBoneTrackKey()]};
        var f = new BhaBoneTrack() {  };
        var e = new BhaBoneTrack() { Children = [f, g]};
        var d = new BhaBoneTrack() {  };
        var c = new BhaBoneTrack() {  };
        var b = new BhaBoneTrack() { Children = [c, d]};
        var a = new BhaBoneTrack() { Children = [b, e]};
        var bha = new BhaFile() { RootBoneTrack = a };
        
        // Act
        bha.Prune();
        var res = a.TraverseDepthFirst();
        
        // Assert
        Assert.Equal([a, b, e, f, g], res);
        Assert.Single(g.Keys);
        foreach (var track in (BhaBoneTrack[])[a, b, e, f])
        {
            Assert.Single(track.Keys);
            Assert.Equal(1f / 30, track.Keys.First().Time);
            Assert.Equal(Vector3.Zero, track.Keys.First().Translation);
            Assert.Equal(Quaternion.Identity, track.Keys.First().Rotation);
        }
    }
}
