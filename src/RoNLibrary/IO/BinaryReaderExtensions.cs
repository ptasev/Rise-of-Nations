using System.Numerics;
using System.Text;

namespace RoNLibrary.IO;

public static class BinaryReaderExtensions
{
    public static string ReadCString(this BinaryReader r)
    {
        var charCount = r.ReadInt32();
        var bytes = r.ReadBytes(charCount).AsSpan();
        return Encoding.ASCII.GetString(bytes[..^1]);
    }
    
    public static Vector4 ReadVector4(this BinaryReader r)
    {
        return new Vector4(
            r.ReadSingle(),
            r.ReadSingle(),
            r.ReadSingle(),
            r.ReadSingle());
    }
    
    public static Vector3 ReadVector3(this BinaryReader r)
    {
        return new Vector3(
            r.ReadSingle(),
            r.ReadSingle(),
            r.ReadSingle());
    }
    
    public static Vector2 ReadVector2(this BinaryReader r)
    {
        return new Vector2(
            r.ReadSingle(),
            r.ReadSingle());
    }

    public static Quaternion ReadQuaternion(this BinaryReader r)
    {
        return new Quaternion(
            r.ReadSingle(),
            r.ReadSingle(),
            r.ReadSingle(),
            r.ReadSingle());
    }
    
    public static (uint Size, ushort ChunkId, ushort ChildCount) ReadChunkHeader(
        this BinaryReader reader,
        ushort? expectedChunkId = null)
    {
        var size = reader.ReadUInt32();
        var chunkId = reader.ReadUInt16();
        var childrenCount = reader.ReadUInt16();

        if (expectedChunkId is not null && chunkId != expectedChunkId)
        {
            throw new FileLoadException($"Expected chunk id {expectedChunkId} but was {chunkId}.");
        }

        return (size, chunkId, childrenCount);
    }
}