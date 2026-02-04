using System;
using System.Collections.Generic;
using Godot;

namespace VoxelCore.Meshing;

public static class GreedyMesher
{
    public static ArrayMesh BuildMesh(VoxelGrid grid, byte[]? paletteRgba = null)
    {
        if (grid == null)
        {
            throw new ArgumentNullException(nameof(grid));
        }

        paletteRgba ??= BuildDefaultPalette();
        if (paletteRgba.Length != 1024)
        {
            throw new ArgumentException("Palette must be 1024 bytes (256 RGBA entries).", nameof(paletteRgba));
        }

        List<Vector3> vertices = new();
        List<Vector3> normals = new();
        List<Color> colors = new();
        List<int> indices = new();

        int[] dims = { grid.SizeX, grid.SizeY, grid.SizeZ };
        MaskCell[] mask = Array.Empty<MaskCell>();

        for (int d = 0; d < 3; d++)
        {
            int u = (d + 1) % 3;
            int v = (d + 2) % 3;

            int sizeU = dims[u];
            int sizeV = dims[v];
            int sizeD = dims[d];

            if (mask.Length != sizeU * sizeV)
            {
                mask = new MaskCell[sizeU * sizeV];
            }

            int[] x = new int[3];
            int[] q = new int[3];
            q[d] = 1;

            for (x[d] = -1; x[d] < sizeD; x[d]++)
            {
                int n = 0;
                for (x[v] = 0; x[v] < sizeV; x[v]++)
                {
                    for (x[u] = 0; x[u] < sizeU; x[u]++, n++)
                    {
                        byte a = x[d] >= 0 ? GetVoxel(grid, x[0], x[1], x[2]) : (byte)0;
                        byte b = x[d] < sizeD - 1 ? GetVoxel(grid, x[0] + q[0], x[1] + q[1], x[2] + q[2]) : (byte)0;

                        if (a == b || (a == 0 && b == 0))
                        {
                            mask[n] = default;
                        }
                        else if (a != 0)
                        {
                            mask[n] = new MaskCell(a, 1);
                        }
                        else
                        {
                            mask[n] = new MaskCell(b, -1);
                        }
                    }
                }

                n = 0;
                for (int j = 0; j < sizeV; j++)
                {
                    for (int i = 0; i < sizeU; i++, n++)
                    {
                        MaskCell cell = mask[n];
                        if (cell.Material == 0)
                        {
                            continue;
                        }

                        int width = 1;
                        while (i + width < sizeU && mask[n + width].Equals(cell))
                        {
                            width++;
                        }

                        int height = 1;
                        bool done = false;
                        while (j + height < sizeV && !done)
                        {
                            int row = n + height * sizeU;
                            for (int k = 0; k < width; k++)
                            {
                                if (!mask[row + k].Equals(cell))
                                {
                                    done = true;
                                    break;
                                }
                            }

                            if (!done)
                            {
                                height++;
                            }
                        }

                        x[u] = i;
                        x[v] = j;

                        int[] du = new int[3];
                        int[] dv = new int[3];
                        du[u] = width;
                        dv[v] = height;

                        int plane = x[d] + 1;
                        Vector3 p0 = ToVector3(d, u, v, plane, x[u], x[v]);
                        Vector3 p1 = ToVector3(d, u, v, plane, x[u] + du[u], x[v] + du[v]);
                        Vector3 p2 = ToVector3(d, u, v, plane, x[u] + du[u] + dv[u], x[v] + du[v] + dv[v]);
                        Vector3 p3 = ToVector3(d, u, v, plane, x[u] + dv[u], x[v] + dv[v]);

                        Vector3 normal = NormalForAxis(d, cell.Normal);
                        Color baseColor = ColorForMaterial(cell.Material, paletteRgba);

                        int u0 = x[u];
                        int v0 = x[v];
                        int u1 = x[u] + width;
                        int v1 = x[v] + height;

                        int voxelD = cell.Normal > 0 ? x[d] : x[d] + 1;
                        int voxelU0 = u0;
                        int voxelU1 = u1 - 1;
                        int voxelV0 = v0;
                        int voxelV1 = v1 - 1;

                        float ao0 = ComputeAo(grid, d, u, v, cell.Normal, voxelD, voxelU0, voxelV0, -1, -1);
                        float ao1 = ComputeAo(grid, d, u, v, cell.Normal, voxelD, voxelU1, voxelV0, 1, -1);
                        float ao2 = ComputeAo(grid, d, u, v, cell.Normal, voxelD, voxelU1, voxelV1, 1, 1);
                        float ao3 = ComputeAo(grid, d, u, v, cell.Normal, voxelD, voxelU0, voxelV1, -1, 1);

                        Color c0 = ApplyShade(baseColor, ao0);
                        Color c1 = ApplyShade(baseColor, ao1);
                        Color c2 = ApplyShade(baseColor, ao2);
                        Color c3 = ApplyShade(baseColor, ao3);

                        if (cell.Normal > 0)
                        {
                            AddQuad(vertices, normals, colors, indices, p0, p3, p2, p1, normal, c0, c3, c2, c1);
                        }
                        else
                        {
                            AddQuad(vertices, normals, colors, indices, p0, p1, p2, p3, normal, c0, c1, c2, c3);
                        }

                        for (int y = 0; y < height; y++)
                        {
                            int row = n + y * sizeU;
                            for (int k = 0; k < width; k++)
                            {
                                mask[row + k] = default;
                            }
                        }

                        i += width - 1;
                        n += width - 1;
                    }
                }
            }
        }

        ArrayMesh mesh = new();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)ArrayMesh.ArrayType.Max);

        arrays[(int)ArrayMesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)ArrayMesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)ArrayMesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)ArrayMesh.ArrayType.Index] = indices.ToArray();

        if (vertices.Count > 0)
        {
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        }

        return mesh;
    }

    private static byte GetVoxel(VoxelGrid grid, int x, int y, int z)
    {
        return grid.GetSafe(x, y, z);
    }

    private static Vector3 ToVector3(int d, int u, int v, int plane, int uValue, int vValue)
    {
        int[] coords = new int[3];
        coords[d] = plane;
        coords[u] = uValue;
        coords[v] = vValue;

        return new Vector3(coords[0], coords[1], coords[2]);
    }

    private static Vector3 NormalForAxis(int axis, int sign)
    {
        return axis switch
        {
            0 => new Vector3(sign, 0, 0),
            1 => new Vector3(0, sign, 0),
            _ => new Vector3(0, 0, sign),
        };
    }

    private static void AddQuad(List<Vector3> vertices, List<Vector3> normals, List<Color> colors, List<int> indices,
        Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 normal,
        Color c0, Color c1, Color c2, Color c3)
    {
        int baseIndex = vertices.Count;

        vertices.Add(v0);
        vertices.Add(v1);
        vertices.Add(v2);
        vertices.Add(v3);

        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);

        colors.Add(c0);
        colors.Add(c1);
        colors.Add(c2);
        colors.Add(c3);

        indices.Add(baseIndex);
        indices.Add(baseIndex + 1);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex + 3);
    }

    private static Color ColorForMaterial(byte material, byte[] palette)
    {
        int offset = material * 4;
        byte r = palette[offset];
        byte g = palette[offset + 1];
        byte b = palette[offset + 2];
        byte a = palette[offset + 3];

        return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
    }

    private static Color ApplyShade(Color color, float shade)
    {
        shade = MathF.Max(0.35f, MathF.Min(1f, shade));
        return new Color(color.R * shade, color.G * shade, color.B * shade, color.A);
    }

    private static float ComputeAo(VoxelGrid grid, int d, int u, int v, int normalSign, int voxelD, int voxelU, int voxelV, int signU, int signV)
    {
        int[] basePos = new int[3];
        basePos[d] = voxelD + normalSign;
        basePos[u] = voxelU;
        basePos[v] = voxelV;

        int[] side1 = new int[3];
        int[] side2 = new int[3];
        int[] corner = new int[3];

        side1[d] = basePos[d];
        side1[u] = basePos[u] + signU;
        side1[v] = basePos[v];

        side2[d] = basePos[d];
        side2[u] = basePos[u];
        side2[v] = basePos[v] + signV;

        corner[d] = basePos[d];
        corner[u] = basePos[u] + signU;
        corner[v] = basePos[v] + signV;

        int occ1 = grid.GetSafe(side1[0], side1[1], side1[2]) != 0 ? 1 : 0;
        int occ2 = grid.GetSafe(side2[0], side2[1], side2[2]) != 0 ? 1 : 0;
        int occCorner = grid.GetSafe(corner[0], corner[1], corner[2]) != 0 ? 1 : 0;

        int occlusion = (occ1 == 1 && occ2 == 1) ? 3 : (occ1 + occ2 + occCorner);
        return 1f - occlusion * 0.18f;
    }

    private static byte[] BuildDefaultPalette()
    {
        byte[] palette = new byte[1024];
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

    private readonly struct MaskCell
    {
        public byte Material { get; }
        public int Normal { get; }

        public MaskCell(byte material, int normal)
        {
            Material = material;
            Normal = normal;
        }

        public bool Equals(MaskCell other)
        {
            return Material == other.Material && Normal == other.Normal;
        }
    }
}
