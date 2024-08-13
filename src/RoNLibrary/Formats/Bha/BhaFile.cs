using System.Diagnostics;
using System.Text;
using RoNLibrary.IO;

namespace RoNLibrary.Formats.Bha;

public class BhaFile
{
    public BhaBoneTrack RootBoneTrack { get; set; } = new();
    
    public static BhaFile Open(string filePath)
    {
        using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Open(fs);
    }

    public static BhaFile Open(Stream stream)
    {
        using var br = new BinaryReader(stream, Encoding.UTF8, true);
        return Open(br);
    }

    public static BhaFile Open(BinaryReader reader)
    {
        var file = new BhaFile();
        file.ReadChunk(reader, 0);
        return file;
    }

    private void ReadChunk(BinaryReader reader, ushort? expectedChunkId = null)
    {
        var (_, chunkId, childCount) = reader.ReadChunkHeader(expectedChunkId);

        switch (chunkId)
        {
            case 0:
            {
                // Root container. Some have multiple roots, I believe by mistake
                Debug.Assert(childCount == 1, "childCount == 1");
                RootBoneTrack = ReadBoneTrack(reader);
                break;
            }
            default:
                throw new NotImplementedException($"Support for chunk id {chunkId} is not implemented.");
        }
    }

    private static BhaBoneTrack ReadBoneTrack(BinaryReader reader)
    {
        var (_, _, childCount) = reader.ReadChunkHeader(8);
        Debug.Assert(childCount >= 1, "childCount >= 1");
        var boneTrack = new BhaBoneTrack
        {
            Keys = ReadBoneTrackKeys(reader)
        };

        for (var i = 1; i < childCount; ++i)
        {
            var childBoneTrack = ReadBoneTrack(reader);
            boneTrack.Children.Add(childBoneTrack);
        }

        return boneTrack;
    }

    private static List<BhaBoneTrackKey> ReadBoneTrackKeys(BinaryReader reader)
    {
        var (_, _, childCount) = reader.ReadChunkHeader(7);
        Debug.Assert(childCount == 0, "childCount == 0");

        var keyCount = reader.ReadInt32();
        var keys = new List<BhaBoneTrackKey>(keyCount);
        for (var i = 0; i < keyCount; ++i)
        {
            var key = new BhaBoneTrackKey
            {
                Time = reader.ReadSingle(),
                Rotation = reader.ReadQuaternion(),
                Translation = reader.ReadVector3()
            };
            keys.Add(key);
            var xRot = reader.ReadSingle();
            Debug.Assert(xRot == key.Rotation.X, "xRot == key.Rotation.X");
        }

        return keys;
    }

    public void Write(string filePath)
    {
        using var fs = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        Write(fs);
    }

    public void Write(Stream stream)
    {
        using var bw = new BinaryWriter(stream, Encoding.UTF8, true);
        Write(bw);
    }

    public void Write(BinaryWriter writer)
    {
        var fileSizeOffset = writer.BaseStream.Position;
        writer.Write(0);
        writer.Write((ushort)0);
        writer.Write((ushort)1);

        var boneTracksSize = WriteBoneTrack(writer, RootBoneTrack);
        writer.BaseStream.Seek(fileSizeOffset, SeekOrigin.Begin);
        writer.Write(8 + boneTracksSize);
    }

    private static int WriteBoneTrack(BinaryWriter writer, BhaBoneTrack boneTrack)
    {
        var boneTrackSizeOffset = writer.BaseStream.Position;
        writer.Write(0);
        writer.Write((ushort)8);
        writer.Write((ushort)(boneTrack.Children.Count + 1));

        var boneTrackSize = 12 + 36 * boneTrack.Keys.Count;
        writer.Write(boneTrackSize);
        writer.Write((ushort)7);
        writer.Write((ushort)0);
        writer.Write(boneTrack.Keys.Count);
        foreach (var key in boneTrack.Keys)
        {
            writer.Write(key.Time);
            writer.Write(key.Rotation);
            writer.Write(key.Translation);
            writer.Write(key.Rotation.X);
        }

        foreach (var child in boneTrack.Children)
        {
            boneTrackSize += WriteBoneTrack(writer, child);
        }

        boneTrackSize += 8;
        var currentOffset = writer.BaseStream.Position;
        writer.BaseStream.Seek(boneTrackSizeOffset, SeekOrigin.Begin);
        writer.Write(boneTrackSize);
        writer.BaseStream.Seek(currentOffset, SeekOrigin.Begin);
        return boneTrackSize;
    }

    /// <summary>
    /// Prunes the animation and updates keys to be valid.
    /// </summary>
    /// <remarks>
    /// The game crashes when any track has 0 keys, or root has less than 2 keys.
    /// The root track keys must sum up to the animation duration.
    /// Remove tracks with no keys and no children.
    /// The hierarchy order must be preserved to match the model's skeleton.
    /// </remarks>
    public void Patch()
    {
        const float minDuration = 1f / 30;
        var duration = minDuration;
        foreach (var track in RootBoneTrack.TraverseDepthFirstParentReverse())
        {
            for (int i = track.Children.Count - 1; i >= 0; i--)
            {
                var child = track.Children[i];
                if (child.Keys.Count > 0)
                {
                    duration = Math.Max(duration, child.Keys.Sum(x => x.Time));
                    continue;
                }

                if (child.Children.Count <= 0 && i >= track.Children.Count - 1)
                {
                    // Delete since this track has no children with keys
                    // And it's the last child, so order will be preserved
                    track.Children.Remove(child);
                }
            }
        }

        var rootDuration = RootBoneTrack.Keys.Sum(x => x.Time);
        duration = Math.Max(duration, rootDuration);
        foreach (var track in RootBoneTrack.TraverseDepthFirst())
        {
            FixKeys(track, duration);
        }

        return;

        static void FixKeys(BhaBoneTrack track, float duration)
        {
            // Add dummy keys since game crashes on tracks with no keys
            // Be extra safe by making sure at least 2 keys are present, and the sum is equal to duration
            // 2 keys is a requirement for root, others require only 1
            if (track.Keys.Count <= 0)
            {
                track.Keys.Add(new BhaBoneTrackKey { Time = duration - minDuration });
                track.Keys.Add(new BhaBoneTrackKey { Time = minDuration });
            }
            else
            {
                var current = track.Keys.Sum(x => x.Time);
                var remaining = duration - current;
                if (remaining >= minDuration || track.Keys.Count == 1)
                {
                    track.Keys.Add(new BhaBoneTrackKey() { Time = remaining });
                }
            }
        }
    }
}
