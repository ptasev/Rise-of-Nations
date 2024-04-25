namespace RoNLibrary.Formats.Bha;

public class BhaBoneTrack : ITreeNode<BhaBoneTrack>
{
    public List<BhaBoneTrackKey> Keys { get; set; } = [];

    public List<BhaBoneTrack> Children { get; set; } = [];
}