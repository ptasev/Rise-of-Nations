using System.Numerics;

namespace RoNLibrary.Formats.Bha;

public class BhaBoneTrackKey
{
    public float Time { get; set; }
    
    public Quaternion Rotation { get; set; } = Quaternion.Identity;
    
    public Vector3 Translation { get; set; }
}