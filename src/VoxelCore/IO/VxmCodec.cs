using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Godot;

namespace VoxelCore.IO;

public sealed class VxmData
{
    public VoxelGrid Grid { get; }
    public byte[] PaletteRgba { get; }
    public string MetadataJson { get; }

    public VxmData(VoxelGrid grid, byte[] paletteRgba, string metadataJson)
    {
        Grid = grid;
        PaletteRgba = paletteRgba;
        MetadataJson = metadataJson;
    }
}

public static class VxmCodec
{
    private const string Magic = "VXM1";
    private const ushort Version = 1;
    private const byte EndianLittle = 0;
    private const int PaletteSize = 256 * 4;

    public static void Save(VoxelGrid grid, string path, byte[]? paletteRgba = null, string? metadataJson = null)
    {
        if (grid == null)
        {
            throw new ArgumentNullException(nameof(grid));
        }

        paletteRgba ??= BuildDefaultPalette();
        if (paletteRgba.Length != PaletteSize)
        {
            throw new ArgumentException("Palette must be 1024 bytes (256 RGBA entries).", nameof(paletteRgba));
        }

        metadataJson ??= "{}";
        byte[] metadataBytes = Encoding.UTF8.GetBytes(metadataJson);

        using FileStream file = File.Create(path);
        using GZipStream gzip = new(file, CompressionLevel.Optimal);
        using BinaryWriter writer = new(gzip, Encoding.UTF8, leaveOpen: false);

        writer.Write(Encoding.ASCII.GetBytes(Magic));
        writer.Write(Version);
        writer.Write(EndianLittle);
        writer.Write((byte)0);
        writer.Write((ushort)grid.SizeX);
        writer.Write((ushort)grid.SizeY);
        writer.Write((ushort)grid.SizeZ);
        writer.Write((short)grid.Origin.X);
        writer.Write((short)grid.Origin.Y);
        writer.Write((short)grid.Origin.Z);

        writer.Write(paletteRgba);

        WriteRle(writer, grid.Voxels);

        writer.Write((uint)metadataBytes.Length);
        writer.Write(metadataBytes);
    }

    public static VxmData Load(string path)
    {
        using FileStream file = File.OpenRead(path);
        using GZipStream gzip = new(file, CompressionMode.Decompress);
        using BinaryReader reader = new(gzip, Encoding.UTF8, leaveOpen: false);

        string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (magic != Magic)
        {
            throw new InvalidDataException($"Invalid magic. Expected {Magic}.");
        }

        ushort version = reader.ReadUInt16();
        if (version != Version)
        {
            throw new InvalidDataException($"Unsupported VXM version {version}.");
        }

        byte endian = reader.ReadByte();
        if (endian != EndianLittle)
        {
            throw new InvalidDataException("Only little-endian VXM is supported.");
        }

        reader.ReadByte();

        int sizeX = reader.ReadUInt16();
        int sizeY = reader.ReadUInt16();
        int sizeZ = reader.ReadUInt16();

        int originX = reader.ReadInt16();
        int originY = reader.ReadInt16();
        int originZ = reader.ReadInt16();

        byte[] palette = reader.ReadBytes(PaletteSize);
        if (palette.Length != PaletteSize)
        {
            throw new InvalidDataException("Palette data truncated.");
        }

        VoxelGrid grid = new(sizeX, sizeY, sizeZ, new Vector3I(originX, originY, originZ));
        ReadRle(reader, grid.Voxels);

        uint metadataLength = reader.ReadUInt32();
        byte[] metadataBytes = reader.ReadBytes((int)metadataLength);
        string metadataJson = Encoding.UTF8.GetString(metadataBytes);

        return new VxmData(grid, palette, metadataJson);
    }

    private static void WriteRle(BinaryWriter writer, byte[] voxels)
    {
        if (voxels.Length == 0)
        {
            return;
        }

        byte current = voxels[0];
        int run = 1;

        for (int i = 1; i < voxels.Length; i++)
        {
            byte value = voxels[i];
            if (value == current && run < ushort.MaxValue)
            {
                run++;
                continue;
            }

            writer.Write(current);
            writer.Write((ushort)run);

            current = value;
            run = 1;
        }

        writer.Write(current);
        writer.Write((ushort)run);
    }

    private static void ReadRle(BinaryReader reader, byte[] voxels)
    {
        int index = 0;
        while (index < voxels.Length)
        {
            byte value;
            ushort run;

            try
            {
                value = reader.ReadByte();
                run = reader.ReadUInt16();
            }
            catch (EndOfStreamException)
            {
                throw new EndOfStreamException("Unexpected end of RLE stream.");
            }

            for (int i = 0; i < run; i++)
            {
                if (index >= voxels.Length)
                {
                    throw new InvalidDataException("RLE stream exceeded voxel buffer length.");
                }

                voxels[index++] = value;
            }
        }
    }

    private static byte[] BuildDefaultPalette()
    {
        byte[] palette = new byte[PaletteSize];

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
}
