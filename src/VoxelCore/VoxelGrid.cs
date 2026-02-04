using System;
using Godot;

namespace VoxelCore;

public sealed class VoxelGrid
{
    public int SizeX { get; }
    public int SizeY { get; }
    public int SizeZ { get; }

    public Vector3I Origin { get; }
    public byte[] Voxels { get; }

    public int VoxelCount => Voxels.Length;

    public VoxelGrid(int sizeX, int sizeY, int sizeZ, Vector3I? origin = null)
    {
        if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeX), "Grid dimensions must be positive.");
        }

        SizeX = sizeX;
        SizeY = sizeY;
        SizeZ = sizeZ;
        Origin = origin ?? Vector3I.Zero;
        Voxels = new byte[sizeX * sizeY * sizeZ];
    }

    public bool InBounds(int x, int y, int z)
    {
        return x >= 0 && x < SizeX && y >= 0 && y < SizeY && z >= 0 && z < SizeZ;
    }

    public byte Get(int x, int y, int z)
    {
        if (!InBounds(x, y, z))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Coordinates are out of bounds.");
        }

        return Voxels[Index(x, y, z)];
    }

    public byte GetSafe(int x, int y, int z)
    {
        return InBounds(x, y, z) ? Voxels[Index(x, y, z)] : (byte)0;
    }

    public void Set(int x, int y, int z, byte material)
    {
        if (!InBounds(x, y, z))
        {
            return;
        }

        Voxels[Index(x, y, z)] = material;
    }

    public int Index(int x, int y, int z)
    {
        return x + y * SizeX + z * SizeX * SizeY;
    }

    public void FillBox(Vector3I min, Vector3I maxExclusive, byte material)
    {
        for (int z = min.Z; z < maxExclusive.Z; z++)
        {
            for (int y = min.Y; y < maxExclusive.Y; y++)
            {
                for (int x = min.X; x < maxExclusive.X; x++)
                {
                    Set(x, y, z, material);
                }
            }
        }
    }

    public void CarveBox(Vector3I min, Vector3I maxExclusive)
    {
        FillBox(min, maxExclusive, 0);
    }
}
