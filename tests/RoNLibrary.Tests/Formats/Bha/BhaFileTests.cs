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
    public void Patch_works()
    {
        var g = new BhaBoneTrack() { Keys = [new BhaBoneTrackKey()]};
        var f = new BhaBoneTrack();
        var e = new BhaBoneTrack() { Children = [f, g]};
        var d = new BhaBoneTrack();
        var c = new BhaBoneTrack();
        var b = new BhaBoneTrack() { Children = [c, d]};
        var a = new BhaBoneTrack() { Children = [b, e]};
        var bha = new BhaFile() { RootBoneTrack = a };
        
        // Act
        bha.Patch();
        var res = a.TraverseDepthFirst().ToArray();
        
        // Assert
        Assert.Equal([a, b, e, f, g], res);
        var duration = res.Max(x => x.Keys.Sum(k => k.Time));
        foreach (var track in (BhaBoneTrack[])[a, b, e, f, g])
        {
            Assert.Equal(2, track.Keys.Count);
            Assert.Equal(duration, track.Keys.Sum(x => x.Time));
            foreach (var key in track.Keys)
            {
                Assert.Equal(Vector3.Zero, key.Translation);
                Assert.Equal(Quaternion.Identity, key.Rotation);
            }
        }
    }
}
