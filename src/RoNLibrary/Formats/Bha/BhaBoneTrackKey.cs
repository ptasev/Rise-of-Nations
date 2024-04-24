using System.Numerics;

namespace RoNLibrary.Formats.Bha;

public class BhaBoneTrackKey
{
    public float Time { get; set; }
    
    public Quaternion Rotation { get; set; }
    
    public Vector3 Translation { get; set; }
}