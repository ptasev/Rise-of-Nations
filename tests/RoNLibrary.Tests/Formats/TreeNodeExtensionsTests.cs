using RoNLibrary.Formats;
using RoNLibrary.Formats.Bh3;

namespace RoNLibrary.Tests.Formats;

public class TreeNodeExtensionsTests
{
    [Fact]
    public void TraverseDepthFirst_works()
    {
        var g = new Bh3Bone() { Name = "G" };
        var f = new Bh3Bone() { Name = "F" };
        var e = new Bh3Bone() { Name = "E", Children = [f, g]};
        var d = new Bh3Bone() { Name = "D" };
        var c = new Bh3Bone() { Name = "C" };
        var b = new Bh3Bone() { Name = "B", Children = [c, d]};
        var a = new Bh3Bone() { Name = "A", Children = [b, e]};
        
        // Act
        var res = a.TraverseDepthFirst();
        
        // Assert
        Assert.Equal([a, b, c, d, e, f, g], res);
    }

    [Fact]
    public void ZipMatchingTreesDepthFirst_works()
    {
        var d1 = new Bh3Bone() { Name = "D1" };
        var c1 = new Bh3Bone() { Name = "C1" };
        var b1 = new Bh3Bone() { Name = "B1", Children = [c1] };
        var a1 = new Bh3Bone() { Name = "A1", Children = [b1, d1]};
        var d2 = new Bh3Bone() { Name = "D2" };
        var c2 = new Bh3Bone() { Name = "C2" };
        var b2 = new Bh3Bone() { Name = "B2", Children = [c2] };
        var a2 = new Bh3Bone() { Name = "A2", Children = [b2, d2]};
        
        // Act
        var res = a1.ZipMatchingTreesDepthFirst(a2);
        
        // Assert
        Assert.Equal([(a1, a2), (b1, b2), (c1, c2), (d1, d2)], res);
    }

    [Fact]
    public void ZipMatchingTreesDepthFirst_ThrowsWhenNotMatching()
    {
        var c = new Bh3Bone() { Name = "C" };
        var b = new Bh3Bone() { Name = "B" };
        var a1 = new Bh3Bone() { Name = "A1", Children = [b, c]};
        var a2 = new Bh3Bone() { Name = "A2", Children = [b]};
        
        // Act/Assert
        Assert.Throws<InvalidOperationException>(() => a1.ZipMatchingTreesDepthFirst(a2).ToArray());
    }
}