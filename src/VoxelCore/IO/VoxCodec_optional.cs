using System;
using System.IO;
using System.Text;
using Godot;

namespace VoxelCore.IO;

public static class VoxCodecOptional
{
    private const int VoxVersion = 150;
    private const string Magic = "VOX ";

    public static void Save(VoxelGrid grid, string path, byte[]? paletteRgba = null, bool swapYZ = true)
    {
        if (grid == null)
        {
            throw new ArgumentNullException(nameof(grid));
        }

        paletteRgba ??= BuildDefaultPalette();
        if (paletteRgba.Length != 256 * 4)
        {
            throw new ArgumentException("Palette must be 1024 bytes (256 RGBA entries).", nameof(paletteRgba));
        }

        int sizeX = grid.SizeX;
        int sizeY = grid.SizeY;
        int sizeZ = grid.SizeZ;

        if (swapYZ)
        {
            sizeY = grid.SizeZ;
            sizeZ = grid.SizeY;
        }

        if (sizeX > 255 || sizeY > 255 || sizeZ > 255)
        {
            throw new InvalidOperationException(".vox supports dimensions up to 255 per axis in the classic format.");
        }

        byte[] sizeChunk = new byte[12];
        Buffer.BlockCopy(BitConverter.GetBytes(sizeX), 0, sizeChunk, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(sizeY), 0, sizeChunk, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(sizeZ), 0, sizeChunk, 8, 4);

        byte[] xyziChunk = BuildXyzi(grid, swapYZ);
        byte[] rgbaChunk = paletteRgba;

        int childrenSize =
            12 + sizeChunk.Length +
            12 + xyziChunk.Length +
            12 + rgbaChunk.Length;

        using FileStream file = File.Create(path);
        using BinaryWriter writer = new(file, Encoding.ASCII, leaveOpen: false);

        writer.Write(Encoding.ASCII.GetBytes(Magic));
        writer.Write(VoxVersion);

        WriteChunkHeader(writer, "MAIN", 0, childrenSize);
        WriteChunk(writer, "SIZE", sizeChunk);
        WriteChunk(writer, "XYZI", xyziChunk);
        WriteChunk(writer, "RGBA", rgbaChunk);
    }

    public static VxmData Load(string path, bool swapYZ = true)
    {
        using FileStream file = File.OpenRead(path);
        using BinaryReader reader = new(file, Encoding.ASCII, leaveOpen: false);

        string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (magic != Magic)
        {
            throw new InvalidDataException("Invalid VOX header.");
        }

        int version = reader.ReadInt32();
        if (version < VoxVersion)
        {
            throw new InvalidDataException($"Unsupported VOX version {version}.");
        }

        VoxState state = new();

        string mainId = Encoding.ASCII.GetString(reader.ReadBytes(4));
        int mainContent = reader.ReadInt32();
        int mainChildren = reader.ReadInt32();

        if (mainId != "MAIN")
        {
            throw new InvalidDataException("Missing MAIN chunk.");
        }

        reader.BaseStream.Position += mainContent;
        long mainEnd = reader.BaseStream.Position + mainChildren;
        ParseChunks(reader, mainEnd, state, swapYZ);

        if (state.Grid == null)
        {
            throw new InvalidDataException("No voxel data found in VOX file.");
        }

        byte[] palette = state.Palette ?? BuildDefaultPalette();
        string metadataJson = "{\"source\":\"vox\"}";

        return new VxmData(state.Grid, palette, metadataJson);
    }

    private static byte[] BuildXyzi(VoxelGrid grid, bool swapYZ)
    {
        int voxelCount = 0;
        foreach (byte value in grid.Voxels)
        {
            if (value != 0)
            {
                voxelCount++;
            }
        }

        byte[] chunk = new byte[4 + voxelCount * 4];
        Buffer.BlockCopy(BitConverter.GetBytes(voxelCount), 0, chunk, 0, 4);

        int sizeX = grid.SizeX;
        int sizeY = grid.SizeY;
        int sizeZ = grid.SizeZ;

        int offset = 4;
        int index = 0;
        for (int z = 0; z < sizeZ; z++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++, index++)
                {
                    byte value = grid.Voxels[index];
                    if (value == 0)
                    {
                        continue;
                    }

                    byte vx = (byte)x;
                    byte vy = (byte)y;
                    byte vz = (byte)z;

                    if (swapYZ)
                    {
                        vy = (byte)z;
                        vz = (byte)y;
                    }

                    chunk[offset++] = vx;
                    chunk[offset++] = vy;
                    chunk[offset++] = vz;
                    chunk[offset++] = value;
                }
            }
        }

        return chunk;
    }

    private static void ParseChunks(BinaryReader reader, long endPosition, VoxState state, bool swapYZ)
    {
        while (reader.BaseStream.Position < endPosition)
        {
            string id = Encoding.ASCII.GetString(reader.ReadBytes(4));
            int contentSize = reader.ReadInt32();
            int childrenSize = reader.ReadInt32();

            long contentStart = reader.BaseStream.Position;

            switch (id)
            {
                case "SIZE":
                    state.PendingSizeX = reader.ReadInt32();
                    state.PendingSizeY = reader.ReadInt32();
                    state.PendingSizeZ = reader.ReadInt32();
                    state.PendingSizeValid = true;
                    break;
                case "XYZI":
                    ReadXyzi(reader, contentSize, state, swapYZ);
                    break;
                case "RGBA":
                    state.Palette = reader.ReadBytes(256 * 4);
                    break;
                case "PACK":
                    state.ExpectedModels = reader.ReadInt32();
                    break;
                default:
                    break;
            }

            reader.BaseStream.Position = contentStart + contentSize;
            long childrenEnd = reader.BaseStream.Position + childrenSize;

            if (childrenSize > 0)
            {
                ParseChunks(reader, childrenEnd, state, swapYZ);
            }

            reader.BaseStream.Position = childrenEnd;
        }
    }

    private static void ReadXyzi(BinaryReader reader, int contentSize, VoxState state, bool swapYZ)
    {
        if (contentSize < 4)
        {
            return;
        }

        int voxelCount = reader.ReadInt32();

        if (!state.PendingSizeValid)
        {
            reader.BaseStream.Position += voxelCount * 4;
            return;
        }

        int sizeX = state.PendingSizeX;
        int sizeY = state.PendingSizeY;
        int sizeZ = state.PendingSizeZ;

        if (swapYZ)
        {
            (sizeY, sizeZ) = (sizeZ, sizeY);
        }

        if (state.Grid == null)
        {
            state.Grid = new VoxelGrid(sizeX, sizeY, sizeZ, Vector3I.Zero);
        }

        for (int i = 0; i < voxelCount; i++)
        {
            byte vx = reader.ReadByte();
            byte vy = reader.ReadByte();
            byte vz = reader.ReadByte();
            byte color = reader.ReadByte();

            int x = vx;
            int y = vy;
            int z = vz;

            if (swapYZ)
            {
                y = vz;
                z = vy;
            }

            if (state.Grid.InBounds(x, y, z))
            {
                state.Grid.Set(x, y, z, color);
            }
        }

        state.PendingSizeValid = false;
        state.ModelIndex++;
    }

    private static void WriteChunk(BinaryWriter writer, string id, byte[] content)
    {
        WriteChunkHeader(writer, id, content.Length, 0);
        writer.Write(content);
    }

    private static void WriteChunkHeader(BinaryWriter writer, string id, int contentSize, int childrenSize)
    {
        writer.Write(Encoding.ASCII.GetBytes(id));
        writer.Write(contentSize);
        writer.Write(childrenSize);
    }

    private static byte[] BuildDefaultPalette()
    {
        byte[] palette = new byte[256 * 4];
        for (int i = 0; i < 256; i++)
        {
            int offset = i * 4;
            if (i == 0)
            {
                palette[offset] = 0;
                palette[offset + 1] = 0;
                palette[offset + 2] = 0;
                palette[offset + 3] = 0;
            }
            else
            {
                palette[offset] = 230;
                palette[offset + 1] = 230;
                palette[offset + 2] = 230;
                palette[offset + 3] = 255;
            }
        }

        return palette;
    }

    private sealed class VoxState
    {
        public VoxelGrid? Grid;
        public byte[]? Palette;
        public int PendingSizeX;
        public int PendingSizeY;
        public int PendingSizeZ;
        public bool PendingSizeValid;
        public int ModelIndex;
        public int ExpectedModels;
    }
}
