using System.Numerics;

namespace RoNLibrary.Formats.Bh3;

public class Bh3Bone : ITreeNode<Bh3Bone>
{
    public int VertexStartIndex { get; set; }
    
    public int VertexCount { get; set; }

    public string Name { get; set; } = string.Empty;
    
    public Quaternion Rotation { get; set; }
    
    public Vector3 Translation { get; set; }

    public List<Bh3Bone> Children { get; set; } = [];
}