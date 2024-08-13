using System.Diagnostics;
using System.Numerics;
using System.Text;
using RoNLibrary.IO;

namespace RoNLibrary.Formats.Bh3;

public class Bh3File
{
    public List<Vector4> Positions { get; set; }
    
    public List<Vector3> Normals { get; set; }
    
    public List<Vector2> TextureCoordinates { get; set; }
    
    public List<ushort> Indices { get; set; }
    
    public Bh3Bone RootBone { get; set; }

    public Bh3File()
    {
        Positions = new List<Vector4>();
        Normals = new List<Vector3>();
        TextureCoordinates = new List<Vector2>();
        Indices = new List<ushort>();
        RootBone = new Bh3Bone { Name = "root" };
    }

    public static Bh3File Open(string filePath)
    {
        using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Open(fs);
    }

    public static Bh3File Open(Stream stream)
    {
        using var br = new BinaryReader(stream, Encoding.UTF8, true);
        return Open(br);
    }

    public static Bh3File Open(BinaryReader reader)
    {
        var file = new Bh3File();
        file.Read(reader);
        return file;
    }

    private void Read(BinaryReader reader)
    {
        ReadChunk(reader, 0);
    }

    private void ReadChunk(BinaryReader reader, ushort? expectedChunkId = null)
    {
        var (_, chunkId, childCount) = reader.ReadChunkHeader(expectedChunkId);

        switch (chunkId)
        {
            case 0:
            {
                // Root container. Some have two pairs of mesh/skeleton, I believe by mistake
                Debug.Assert(childCount == 2, "childCount == 2");
                ReadChunk(reader, 1);
                RootBone = ReadBone(reader);
                break;
            }
            case 1:
            {
                // Mesh data container
                Debug.Assert(childCount == 4, "childCount == 4");
                ReadChunk(reader, 2);
                ReadChunk(reader, 3);
                ReadChunk(reader, 4);
                ReadChunk(reader, 5);
                break;
            }
            case 2:
            {
                Debug.Assert(childCount == 0, "childCount == 0");
                var numElements = reader.ReadInt32();
                Positions.Capacity = numElements;
                for (var i = 0; i < numElements; ++i)
                {
                    Positions.Add(reader.ReadVector4());
                }

                break;
            }
            case 3:
            {
                Debug.Assert(childCount == 0, "childCount == 0");
                var numElements = reader.ReadInt32();
                Normals.Capacity = numElements;
                for (var i = 0; i < numElements; ++i)
                {
                    Normals.Add(reader.ReadVector3());
                }

                // padding
                reader.ReadBytes(4 * numElements);

                break;
            }
            case 4:
            {
                Debug.Assert(childCount == 0, "childCount == 0");
                var numElements = reader.ReadInt32();
                TextureCoordinates.Capacity = numElements;
                for (var i = 0; i < numElements; ++i)
                {
                    TextureCoordinates.Add(reader.ReadVector2());
                }

                break;
            }
            case 5:
            {
                Debug.Assert(childCount == 0, "childCount == 0");
                var numElements = reader.ReadInt32();
                Indices.Capacity = numElements;
                for (var i = 0; i < numElements; ++i)
                {
                    Indices.Add(reader.ReadUInt16());
                }

                break;
            }
            default:
                throw new NotImplementedException($"Support for chunk id {chunkId} is not implemented.");
        }
    }

    private static Bh3Bone ReadBone(BinaryReader reader)
    {
        var (_, _, childCount) = reader.ReadChunkHeader(6);
        Debug.Assert(childCount >= 1, "childCount >= 1");
        var bone = ReadBoneData(reader);

        for (var i = 1; i < childCount; ++i)
        {
            var childBone = ReadBone(reader);
            bone.Children.Add(childBone);
        }

        return bone;
    }

    private static Bh3Bone ReadBoneData(BinaryReader reader)
    {
        var (_, _, childCount) = reader.ReadChunkHeader(7);
        Debug.Assert(childCount == 0, "childCount == 0");

        var bone = new Bh3Bone
        {
            VertexStartIndex = reader.ReadInt32(),
            VertexCount = reader.ReadInt32(),
            Name = reader.ReadCString(),
            Rotation = reader.ReadQuaternion(),
            Translation = reader.ReadVector3()
        };
        var xRot = reader.ReadSingle();
        Debug.Assert(xRot == bone.Rotation.X, "xRot == bone.Rotation.X");
        return bone;
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
        writer.Write((ushort)2);

        var meshSize = 56 + 40 * Positions.Count + 2 * Indices.Count;
        writer.Write(meshSize);
        writer.Write((ushort)1);
        writer.Write((ushort)4);

        writer.Write(12 + Positions.Count * 16);
        writer.Write((ushort)2);
        writer.Write((ushort)0);
        writer.Write(Positions.Count);
        foreach (var v in Positions)
        {
            writer.Write(v);
        }

        writer.Write(12 + Normals.Count * 16);
        writer.Write((ushort)3);
        writer.Write((ushort)0);
        writer.Write(Normals.Count);
        foreach (var v in Normals)
        {
            writer.Write(v);
        }
        for (var i = 0; i < Normals.Count; ++i)
        {
            writer.Write(0);
        }

        writer.Write(12 + TextureCoordinates.Count * 8);
        writer.Write((ushort)4);
        writer.Write((ushort)0);
        writer.Write(TextureCoordinates.Count);
        foreach (var v in TextureCoordinates)
        {
            writer.Write(v);
        }


        writer.Write(12 + Indices.Count * 2);
        writer.Write((ushort)5);
        writer.Write((ushort)0);
        writer.Write(Indices.Count);
        foreach (var i in Indices)
        {
            writer.Write(i);
        }

        var bonesSize = WriteBone(writer, RootBone);
        writer.BaseStream.Seek(fileSizeOffset, SeekOrigin.Begin);
        writer.Write(8 + meshSize + bonesSize);
    }

    private static int WriteBone(BinaryWriter writer, Bh3Bone bone)
    {
        var boneSizeOffset = writer.BaseStream.Position;
        writer.Write(0);
        writer.Write((ushort)6);
        writer.Write((ushort)(bone.Children.Count + 1));

        var boneSize = 53 + bone.Name.Length;
        writer.Write(boneSize);
        writer.Write((ushort)7);
        writer.Write((ushort)0);

        writer.Write(bone.VertexStartIndex);
        writer.Write(bone.VertexCount);
        writer.WriteCString(bone.Name);
        writer.Write(bone.Rotation);
        writer.Write(bone.Translation);
        writer.Write(bone.Rotation.X);

        foreach (var child in bone.Children)
        {
            boneSize += WriteBone(writer, child);
        }

        boneSize += 8;
        var currentOffset = writer.BaseStream.Position;
        writer.BaseStream.Seek(boneSizeOffset, SeekOrigin.Begin);
        writer.Write(boneSize);
        writer.BaseStream.Seek(currentOffset, SeekOrigin.Begin);
        return boneSize;
    }
}