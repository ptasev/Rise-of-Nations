using System.Numerics;
using System.Text;

namespace RoNLibrary.IO;

public static class BinaryWriterExtensions
{
    public static void Write(this BinaryWriter writer, Vector2 v)
    {
        writer.Write(v.X);
        writer.Write(v.Y);
    }
    
    public static void Write(this BinaryWriter writer, Vector3 v)
    {
        writer.Write(v.X);
        writer.Write(v.Y);
        writer.Write(v.Z);
    }
    
    public static void Write(this BinaryWriter writer, Vector4 v)
    {
        writer.Write(v.X);
        writer.Write(v.Y);
        writer.Write(v.Z);
        writer.Write(v.W);
    }
    
    public static void Write(this BinaryWriter writer, Quaternion v)
    {
        writer.Write(v.X);
        writer.Write(v.Y);
        writer.Write(v.Z);
        writer.Write(v.W);
    }

    public static void WriteCString(this BinaryWriter writer, string s)
    {
        var bytes = Encoding.ASCII.GetBytes(s);
        writer.Write(bytes.Length + 1);
        writer.Write(bytes);
        writer.Write((byte)0);
    }
}