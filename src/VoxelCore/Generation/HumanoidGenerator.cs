using System;
using System.Collections.Generic;
using Godot;

namespace VoxelCore.Generation;

public static class HumanoidGenerator
{
    public static VoxelGrid BuildWallTorch(
        int plateWidth,
        int plateHeight,
        int plateThickness,
        int bracketLength,
        int torchRadius,
        int torchLength,
        int flameHeight,
        bool includeHandle,
        int handleLength,
        int handleRadius)
    {
        plateWidth = Math.Max(3, plateWidth);
        plateHeight = Math.Max(4, plateHeight);
        plateThickness = Math.Clamp(plateThickness, 1, 4);
        bracketLength = Math.Max(2, bracketLength);
        torchRadius = Math.Max(1, torchRadius);
        torchLength = Math.Max(3, torchLength);
        flameHeight = Math.Max(2, flameHeight);
        handleLength = Math.Max(2, handleLength);
        handleRadius = Math.Max(1, handleRadius);

        int margin = 3;
        int extraLower = includeHandle ? handleLength : 0;
        int sizeX = plateWidth + margin * 2;
        int sizeY = plateHeight + flameHeight + margin * 2 + extraLower;
        int sizeZ = plateThickness + bracketLength + torchLength + margin * 2;

        int centerX = sizeX / 2;
        int baseY = margin + extraLower;
        int plateZ0 = margin;

        VoxelGrid grid = new(sizeX, sizeY, sizeZ, new Vector3I(centerX, baseY + plateHeight / 2, plateZ0));

        int plateX0 = centerX - plateWidth / 2;
        int plateX1 = plateX0 + plateWidth;
        int plateY0 = baseY;
        int plateY1 = plateY0 + plateHeight;
        int plateZ1 = plateZ0 + plateThickness;

        // Back plate
        grid.FillBox(new Vector3I(plateX0, plateY0, plateZ0), new Vector3I(plateX1, plateY1, plateZ1), 1);

        // Bracket
        int bracketW = Math.Max(1, plateWidth / 3);
        int bracketH = Math.Max(2, plateHeight / 5);
        int bracketX0 = centerX - bracketW / 2;
        int bracketX1 = bracketX0 + bracketW;
        int bracketY0 = plateY0 + plateHeight / 2 - bracketH / 2;
        int bracketY1 = bracketY0 + bracketH;
        int bracketZ0 = plateZ1;
        int bracketZ1 = bracketZ0 + bracketLength;
        grid.FillBox(new Vector3I(bracketX0, bracketY0, bracketZ0), new Vector3I(bracketX1, bracketY1, bracketZ1), 1);

        // Torch body
        int bodyW = torchRadius * 2 + 1;
        int bodyH = Math.Max(4, plateHeight / 2);
        int bodyX0 = centerX - bodyW / 2;
        int bodyX1 = bodyX0 + bodyW;
        int bodyY0 = bracketY0 - 1;
        int bodyY1 = bodyY0 + bodyH;
        int bodyZ0 = bracketZ1;
        int bodyZ1 = bodyZ0 + torchLength;
        grid.FillBox(new Vector3I(bodyX0, bodyY0, bodyZ0), new Vector3I(bodyX1, bodyY1, bodyZ1), 1);

        // Handle below torch body
        if (includeHandle)
        {
            int handleW = handleRadius * 2 + 1;
            int handleX0 = centerX - handleW / 2;
            int handleX1 = handleX0 + handleW;
            int handleY1 = bodyY0;
            int handleY0 = Math.Max(1, handleY1 - handleLength);

            int handleZCenter = bodyZ0 + torchLength / 2;
            int handleZ0 = handleZCenter - handleRadius;
            int handleZ1 = handleZCenter + handleRadius + 1;

            grid.FillBox(new Vector3I(handleX0, handleY0, handleZ0), new Vector3I(handleX1, handleY1, handleZ1), 1);
        }

        // Flame (simple taper)
        int flameY0 = bodyY1;
        int flameZ0 = bodyZ0 + torchLength / 4;
        int flameZ1 = bodyZ1 - torchLength / 4;
        if (flameZ1 <= flameZ0)
        {
            flameZ0 = bodyZ0;
            flameZ1 = bodyZ1;
        }

        for (int i = 0; i < flameHeight; i++)
        {
            int radius = Math.Max(1, torchRadius - i / 2);
            int fx0 = centerX - radius;
            int fx1 = centerX + radius + 1;
            int fy0 = flameY0 + i;
            int fy1 = fy0 + 1;
            grid.FillBox(new Vector3I(fx0, fy0, flameZ0), new Vector3I(fx1, fy1, flameZ1), 1);
        }

        return grid;
    }

    public static VoxelGrid BuildTreasureChest(int width, int depth, int height, int lidHeight, int wallThickness, int bandThickness)
    {
        width = Math.Max(6, width);
        depth = Math.Max(5, depth);
        height = Math.Max(4, height);
        lidHeight = Math.Max(2, lidHeight);
        wallThickness = Math.Max(1, wallThickness);
        bandThickness = Math.Max(1, bandThickness);

        int margin = 4;
        int sizeX = width + margin * 2;
        int sizeZ = depth + margin * 2;
        int sizeY = height + lidHeight + margin * 2;

        int centerX = sizeX / 2;
        int centerZ = sizeZ / 2;
        int baseY0 = margin;
        int baseY1 = baseY0 + height;

        int minX = centerX - width / 2;
        int maxX = minX + width;
        int minZ = centerZ - depth / 2;
        int maxZ = minZ + depth;

        VoxelGrid grid = new(sizeX, sizeY, sizeZ, new Vector3I(centerX, baseY0 + height / 2, centerZ));

        // Base box (wood)
        grid.FillBox(new Vector3I(minX, baseY0, minZ), new Vector3I(maxX, baseY1, maxZ), 1);

        // Hollow interior
        if (wallThickness * 2 < width && wallThickness * 2 < depth)
        {
            grid.CarveBox(
                new Vector3I(minX + wallThickness, baseY0 + wallThickness, minZ + wallThickness),
                new Vector3I(maxX - wallThickness, baseY1 - wallThickness, maxZ - wallThickness));
        }

        // Rim around top of base
        grid.FillBox(new Vector3I(minX, baseY1 - 1, minZ), new Vector3I(maxX, baseY1, maxZ), 2);

        // Corner caps
        int capY0 = baseY0;
        int capY1 = Math.Min(baseY1, baseY0 + Math.Max(2, height / 3));
        grid.FillBox(new Vector3I(minX, capY0, minZ), new Vector3I(minX + 1, capY1, minZ + 1), 2);
        grid.FillBox(new Vector3I(maxX - 1, capY0, minZ), new Vector3I(maxX, capY1, minZ + 1), 2);
        grid.FillBox(new Vector3I(minX, capY0, maxZ - 1), new Vector3I(minX + 1, capY1, maxZ), 2);
        grid.FillBox(new Vector3I(maxX - 1, capY0, maxZ - 1), new Vector3I(maxX, capY1, maxZ), 2);

        // Rounded lid (barrel)
        float halfDepth = (depth - 1) * 0.5f;
        for (int z = minZ; z < maxZ; z++)
        {
            float dz = (z - (minZ + halfDepth)) / Math.Max(1f, halfDepth);
            float radius = Math.Clamp(1f - dz * dz, 0f, 1f);
            int arcHeight = (int)MathF.Ceiling(radius * lidHeight);
            int lidY0 = baseY1;
            int lidY1 = lidY0 + arcHeight;

            grid.FillBox(new Vector3I(minX, lidY0, z), new Vector3I(maxX, lidY1, z + 1), 1);
        }

        // Lid lip (slight overhang)
        int lipY0 = Math.Max(baseY0, baseY1 - 1);
        int lipY1 = Math.Min(baseY1, sizeY);
        int lipFrontZ0 = Math.Max(0, minZ - 1);
        int lipBackZ0 = Math.Min(sizeZ - 1, maxZ);
        if (lipY1 > lipY0)
        {
            grid.FillBox(new Vector3I(minX, lipY0, lipFrontZ0), new Vector3I(maxX, lipY1, minZ), 2);
            grid.FillBox(new Vector3I(minX, lipY0, lipBackZ0), new Vector3I(maxX, lipY1, Math.Min(sizeZ, lipBackZ0 + 1)), 2);

            int lipSideX0 = Math.Max(0, minX - 1);
            int lipSideX1 = Math.Min(sizeX, maxX + 1);
            grid.FillBox(new Vector3I(lipSideX0, lipY0, minZ), new Vector3I(minX, lipY1, maxZ), 2);
            grid.FillBox(new Vector3I(maxX, lipY0, minZ), new Vector3I(lipSideX1, lipY1, maxZ), 2);
        }

        // Metal band around center
        int strapZ0 = Math.Clamp(minZ + depth / 2 - bandThickness / 2, minZ, maxZ - 1);
        int strapZ1 = Math.Min(maxZ, strapZ0 + bandThickness);
        grid.FillBox(new Vector3I(minX, baseY0, strapZ0), new Vector3I(maxX, baseY1 + lidHeight, strapZ1), 2);

        // Hinges on back
        int hingeY0 = lipY0;
        int hingeY1 = Math.Min(baseY1 + lidHeight, hingeY0 + Math.Max(2, lidHeight / 4));
        int hingeZ0 = maxZ - 1;
        int hingeZ1 = maxZ;
        int hingeW = Math.Max(1, width / 6);
        int hingeX0 = minX + hingeW;
        int hingeX1 = hingeX0 + hingeW;
        int hingeX2 = maxX - hingeW * 2;
        int hingeX3 = hingeX2 + hingeW;
        grid.FillBox(new Vector3I(hingeX0, hingeY0, hingeZ0), new Vector3I(hingeX1, hingeY1, hingeZ1), 2);
        grid.FillBox(new Vector3I(hingeX2, hingeY0, hingeZ0), new Vector3I(hingeX3, hingeY1, hingeZ1), 2);

        // Front lock
        int lockW = Math.Max(2, width / 5);
        int lockH = Math.Max(2, height / 3);
        int lockX0 = centerX - lockW / 2;
        int lockX1 = lockX0 + lockW;
        int lockY0 = baseY0 + height / 2 - lockH / 2;
        int lockY1 = lockY0 + lockH;
        int lockZ = Math.Max(0, minZ - 1);
        grid.FillBox(new Vector3I(lockX0, lockY0, lockZ), new Vector3I(lockX1, lockY1, lockZ + 1), 3);

        return grid;
    }

    public static VoxelGrid BuildTable(int width, int depth, int height, int legThickness, int topThickness)
    {
        width = Math.Max(4, width);
        depth = Math.Max(4, depth);
        height = Math.Max(4, height);
        legThickness = Math.Max(1, legThickness);
        topThickness = Math.Max(1, topThickness);

        int sizeX = width + 4;
        int sizeZ = depth + 4;
        int sizeY = height + topThickness + 4;

        int centerX = sizeX / 2;
        int centerZ = sizeZ / 2;
        int topY = sizeY - topThickness - 2;

        VoxelGrid grid = new(sizeX, sizeY, sizeZ, new Vector3I(centerX, topY, centerZ));

        int minX = centerX - width / 2;
        int maxX = minX + width;
        int minZ = centerZ - depth / 2;
        int maxZ = minZ + depth;

        // Tabletop
        grid.FillBox(new Vector3I(minX, topY, minZ), new Vector3I(maxX, topY + topThickness, maxZ), 1);

        // Legs
        int legY0 = 2;
        int legY1 = topY;
        int lx0 = minX;
        int lx1 = minX + legThickness;
        int rx0 = maxX - legThickness;
        int rx1 = maxX;
        int fz0 = minZ;
        int fz1 = minZ + legThickness;
        int bz0 = maxZ - legThickness;
        int bz1 = maxZ;

        grid.FillBox(new Vector3I(lx0, legY0, fz0), new Vector3I(lx1, legY1, fz1), 1);
        grid.FillBox(new Vector3I(rx0, legY0, fz0), new Vector3I(rx1, legY1, fz1), 1);
        grid.FillBox(new Vector3I(lx0, legY0, bz0), new Vector3I(lx1, legY1, bz1), 1);
        grid.FillBox(new Vector3I(rx0, legY0, bz0), new Vector3I(rx1, legY1, bz1), 1);

        return grid;
    }

    public static VoxelGrid BuildHumanoid(int height, int torsoVoxels, int seed)
    {
        torsoVoxels = Math.Max(64, torsoVoxels);

        (int torsoH, int torsoW, int torsoD) = ComputeTorsoDims(torsoVoxels);
        int torsoTrimBack = 2;
        torsoD = Math.Max(4, torsoD - 2);

        int margin = 4;

        int legW = Math.Max(6, (int)MathF.Round(torsoW * 0.34f));
        int legD = Math.Max(6, (int)MathF.Round(torsoD * 0.4f));
        int legGap = Math.Max(2, (int)MathF.Round(torsoW * 0.16f));

        int armLen = Math.Max(8, (int)MathF.Round(torsoH * 0.7f));
        int armThickY = Math.Max(3, (int)MathF.Round(torsoH * 0.18f));
        int armThickZ = Math.Max(3, (int)MathF.Round(torsoD * 0.28f));
        int shoulderPad = Math.Max(2, torsoW / 10);
        int neckH = Math.Max(2, torsoH / 10);
        int neckW = Math.Max(2, Math.Min(torsoW, torsoD) / 6);
        int headH = Math.Max(7, (int)MathF.Round(torsoH * 0.42f));
        int headW = Math.Max(7, (int)MathF.Round(torsoW * 0.65f));
        int headD = Math.Max(7, (int)MathF.Round(torsoD * 0.7f));

        int hipH = 0;
        int hipW = Math.Max(legW * 2 + legGap + 2, (int)MathF.Round(torsoW * 0.85f));
        int hipD = Math.Max(legD + 1, (int)MathF.Round(torsoD * 0.9f));

        int legTotal = Math.Max((int)MathF.Round(torsoH * 1.1f), 18);
        int upperLegH = Math.Max(6, (int)MathF.Round(legTotal * 0.55f));
        int lowerLegH = Math.Max(6, legTotal - upperLegH);
        int footH = 2;
        int footL = Math.Max(5, legD + 2);

        int sizeX = Math.Max(torsoW + margin * 2, hipW + margin * 2);
        sizeX = Math.Max(sizeX, torsoW + margin * 2 + armLen * 2 + shoulderPad * 2);
        int sizeZ = Math.Max(torsoD + margin * 2 + footL, headD + margin * 2);
        int sizeY = Math.Max(height, torsoH + hipH + legTotal + footH + neckH + headH + margin * 2);

        float centerXf = (sizeX - 1) * 0.5f;
        float centerZf = (sizeZ - 1) * 0.5f;
        int centerX = RoundToInt(centerXf);
        int centerZ = RoundToInt(centerZf);

        int pelvisY = margin + legTotal + footH;

        Vector3I min = new(
            RoundToInt(centerXf - torsoW * 0.5f),
            pelvisY + hipH,
            RoundToInt(centerZf - torsoD * 0.5f));
        Vector3I max = new(min.X + torsoW, min.Y + torsoH, min.Z + torsoD);
        min = new Vector3I(min.X, min.Y, min.Z + torsoTrimBack);

        VoxelGrid grid = new(sizeX, sizeY, sizeZ, new Vector3I(centerX, pelvisY, centerZ));
        FillTaperedTorso(grid, min, max);
        SmoothTorsoCohesion(grid, min, max);
        CleanupFrontBellySpikes(grid, min, max);
        SmoothFrontProfile(grid, min, max);
        ClampChestFrontSpikes(grid, min, max);
        AddHipsAndLegs(grid, min, max, pelvisY, hipH, hipW, hipD, legGap, legW, legD, upperLegH, lowerLegH, footH, footL);
        AddShouldersNeckArms(grid, min, max, armLen, armThickY, armThickZ, shoulderPad, neckH, neckW);
        AddHead(grid, min, max, centerXf, centerZf, neckH, headH, headW, headD);
        MirrorXUnion(grid);

        return grid;
    }

    private static (int height, int width, int depth) ComputeTorsoDims(int targetVoxels)
    {
        const float ratioH = 1.9f;
        const float ratioW = 1.35f;
        const float ratioD = 1.05f;
        const float fillRatio = 0.5f;

        float boxVolume = targetVoxels / fillRatio;
        float baseSize = MathF.Pow(boxVolume / (ratioH * ratioW * ratioD), 1f / 3f);

        int h = Math.Max(5, (int)MathF.Round(baseSize * ratioH));
        int w = Math.Max(4, (int)MathF.Round(baseSize * ratioW));
        int d = Math.Max(4, (int)MathF.Round(baseSize * ratioD));

        return (h, w, d);
    }

    private static void FillTaperedTorso(VoxelGrid grid, Vector3I min, Vector3I max)
    {
        int baseW = max.X - min.X;
        int baseH = max.Y - min.Y;
        int baseD = max.Z - min.Z;

        int centerX = (min.X + max.X) / 2;
        int centerZ = (min.Z + max.Z) / 2;

        float waistScaleW = 0.68f;
        float waistScaleD = 0.75f;
        float shoulderScaleW = 1.25f;
        float shoulderScaleD = 1.05f;

        for (int y = min.Y; y < max.Y; y++)
        {
            float t = baseH <= 1 ? 0f : (float)(y - min.Y) / (baseH - 1);
            float scaleW;
            float scaleD;

            if (t < 0.4f)
            {
                float k = Smoothstep(0f, 0.4f, t);
                scaleW = Lerp(1f, waistScaleW, k);
                scaleD = Lerp(1f, waistScaleD, k);
            }
            else if (t < 0.75f)
            {
                float k = Smoothstep(0.4f, 0.75f, t);
                scaleW = Lerp(waistScaleW, 1f, k);
                scaleD = Lerp(waistScaleD, 1f, k);
            }
            else
            {
                float k = Smoothstep(0.75f, 1f, t);
                scaleW = Lerp(1f, shoulderScaleW, k);
                scaleD = Lerp(1f, shoulderScaleD, k);
            }

            // Taper front/back into the legs near the bottom.
            if (t < 0.25f)
            {
                float k = Smoothstep(0f, 0.25f, t);
                scaleD *= Lerp(0.55f, 1f, k);
            }

            float rx = MathF.Max(1f, (baseW * 0.5f) * scaleW);
            float rzBase = MathF.Max(1f, (baseD * 0.5f) * scaleD);

            float chestFactor = 1f + 0.16f * Smoothstep(0.5f, 0.62f, t) * (1f - Smoothstep(0.7f, 0.82f, t));
            float backFactor = 1f + 0.08f * Smoothstep(0.6f, 0.9f, t);
            if (t > 0.7f)
            {
                float backTaper = Smoothstep(0.7f, 1f, t);
                backFactor *= Lerp(1f, 0.92f, backTaper);
            }

            float rzFront = rzBase * chestFactor;
            float rzBack = rzBase * backFactor;

            float centerZOffset = 0.05f * Smoothstep(0.35f, 0.7f, t);
            float layerCenterZ = centerZ + centerZOffset;

            int x0 = centerX - (int)MathF.Ceiling(rx);
            int x1 = centerX + (int)MathF.Ceiling(rx);
            int z0 = (int)MathF.Floor(layerCenterZ - MathF.Ceiling(rzBack));
            int z1 = (int)MathF.Ceiling(layerCenterZ + MathF.Ceiling(rzFront));

            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - centerX) / rx;
                    float rz = (z + 0.5f >= layerCenterZ) ? rzFront : rzBack;
                    float dz = (z + 0.5f - layerCenterZ) / rz;
                    if (dx * dx + dz * dz <= 1.0f)
                    {
                        grid.Set(x, y, z, 1);
                    }
                }
            }
        }
    }

    private static void SmoothTorsoCohesion(VoxelGrid grid, Vector3I min, Vector3I max)
    {
        int height = max.Y - min.Y;
        if (height <= 0)
        {
            return;
        }

        int startY = min.Y + (int)MathF.Round(height * 0.2f);
        int endY = min.Y + (int)MathF.Round(height * 0.85f);

        List<Vector3I> fill = new();

        for (int y = startY; y < endY; y++)
        {
            for (int x = min.X + 1; x < max.X - 1; x++)
            {
                for (int z = min.Z + 1; z < max.Z - 1; z++)
                {
                    if (grid.GetSafe(x, y, z) != 0)
                    {
                        continue;
                    }

                    bool left = grid.GetSafe(x - 1, y, z) != 0;
                    bool right = grid.GetSafe(x + 1, y, z) != 0;
                    bool back = grid.GetSafe(x, y, z - 1) != 0;
                    bool front = grid.GetSafe(x, y, z + 1) != 0;

                    int count = (left ? 1 : 0) + (right ? 1 : 0) + (back ? 1 : 0) + (front ? 1 : 0);
                    if (count < 3)
                    {
                        continue;
                    }

                    if (!((left && right) || (front && back)))
                    {
                        continue;
                    }

                    bool up = grid.GetSafe(x, y + 1, z) != 0;
                    bool down = grid.GetSafe(x, y - 1, z) != 0;
                    if (!up && !down)
                    {
                        continue;
                    }

                    fill.Add(new Vector3I(x, y, z));
                }
            }
        }

        foreach (Vector3I cell in fill)
        {
            grid.Set(cell.X, cell.Y, cell.Z, 1);
        }
    }

    private static void CleanupFrontBellySpikes(VoxelGrid grid, Vector3I min, Vector3I max)
    {
        int height = max.Y - min.Y;
        if (height <= 0)
        {
            return;
        }

        int startY = min.Y + (int)MathF.Round(height * 0.3f);
        int endY = min.Y + (int)MathF.Round(height * 0.65f);
        int zFrontStart = (min.Z + max.Z) / 2;

        for (int y = startY; y < endY; y++)
        {
            for (int x = min.X; x < max.X; x++)
            {
                int frontZ = FindFrontZ(grid, x, y);
                if (frontZ < zFrontStart)
                {
                    continue;
                }

                int depth = 0;
                int z = frontZ;
                while (z >= 0 && grid.GetSafe(x, y, z) != 0)
                {
                    depth++;
                    z--;
                }

                if (depth > 2)
                {
                    continue;
                }

                int leftFront = x > 0 ? FindFrontZ(grid, x - 1, y) : frontZ;
                int rightFront = x + 1 < grid.SizeX ? FindFrontZ(grid, x + 1, y) : frontZ;
                int neighborMax = Math.Max(leftFront, rightFront);

                if (frontZ > neighborMax)
                {
                    grid.Set(x, y, frontZ, 0);
                }
            }
        }
    }

    private static int FindFrontZ(VoxelGrid grid, int x, int y)
    {
        for (int z = grid.SizeZ - 1; z >= 0; z--)
        {
            if (grid.GetSafe(x, y, z) != 0)
            {
                return z;
            }
        }

        return -1;
    }

    private static void SmoothFrontProfile(VoxelGrid grid, Vector3I min, Vector3I max)
    {
        int height = max.Y - min.Y;
        if (height <= 0)
        {
            return;
        }

        int startY = min.Y + (int)MathF.Round(height * 0.3f);
        int endY = min.Y + (int)MathF.Round(height * 0.7f);

        for (int y = startY; y < endY; y++)
        {
            int width = max.X - min.X;
            if (width <= 0)
            {
                continue;
            }

            int[] front = new int[width];
            for (int i = 0; i < width; i++)
            {
                int x = min.X + i;
                front[i] = FindFrontZ(grid, x, y);
            }

            for (int i = 0; i < width; i++)
            {
                int x = min.X + i;
                int current = front[i];
                if (current < 0)
                {
                    continue;
                }

                int left = front[i == 0 ? 0 : i - 1];
                int right = front[i == width - 1 ? width - 1 : i + 1];
                int neighborMax = Math.Max(left, right);

                int allowed = neighborMax + 1;
                if (current <= allowed)
                {
                    continue;
                }

                for (int z = current; z > allowed; z--)
                {
                    grid.Set(x, y, z, 0);
                }
            }
        }
    }

    private static void ClampChestFrontSpikes(VoxelGrid grid, Vector3I min, Vector3I max)
    {
        int height = max.Y - min.Y;
        if (height <= 0)
        {
            return;
        }

        int startY = min.Y + (int)MathF.Round(height * 0.55f);
        int endY = min.Y + (int)MathF.Round(height * 0.85f);
        int width = max.X - min.X;
        if (width <= 0)
        {
            return;
        }

        for (int y = startY; y < endY; y++)
        {
            int[] front = new int[width];
            int count = 0;
            for (int i = 0; i < width; i++)
            {
                int x = min.X + i;
                int f = FindFrontZ(grid, x, y);
                front[i] = f;
                if (f >= 0)
                {
                    count++;
                }
            }

            if (count == 0)
            {
                continue;
            }

            int[] sorted = new int[count];
            int idx = 0;
            for (int i = 0; i < width; i++)
            {
                if (front[i] >= 0)
                {
                    sorted[idx++] = front[i];
                }
            }

            Array.Sort(sorted);
            int median = sorted[count / 2];
            int allowed = median + 1;

            for (int i = 0; i < width; i++)
            {
                int current = front[i];
                if (current < 0 || current <= allowed)
                {
                    continue;
                }

                int x = min.X + i;
                for (int z = current; z > allowed; z--)
                {
                    grid.Set(x, y, z, 0);
                }
            }
        }
    }

    private static int LerpInt(int a, int b, float t)
    {
        return (int)MathF.Round(a + (b - a) * t);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    private static float Smoothstep(float edge0, float edge1, float t)
    {
        if (MathF.Abs(edge1 - edge0) < 0.0001f)
        {
            return 0f;
        }

        float x = Math.Clamp((t - edge0) / (edge1 - edge0), 0f, 1f);
        return x * x * (3f - 2f * x);
    }

    private static int RoundToInt(float value)
    {
        return (int)MathF.Round(value, MidpointRounding.AwayFromZero);
    }

    private static void AddHipsAndLegs(
        VoxelGrid grid,
        Vector3I torsoMin,
        Vector3I torsoMax,
        int pelvisY,
        int hipH,
        int hipW,
        int hipD,
        int legGap,
        int legW,
        int legD,
        int upperLegH,
        int lowerLegH,
        int footH,
        int footL)
    {
        float centerXf = (torsoMin.X + torsoMax.X) * 0.5f;
        float centerZf = (torsoMin.Z + torsoMax.Z) * 0.5f;
        int centerX = RoundToInt(centerXf);
        int centerZ = RoundToInt(centerZf);

        Vector3I hipMin = new(centerX - hipW / 2, pelvisY, centerZ - hipD / 2);
        Vector3I hipMax = new(hipMin.X + hipW, hipMin.Y + hipH, hipMin.Z + hipD);

        int legH = upperLegH + lowerLegH;

        int halfGapLeft = legGap / 2;
        int halfGapRight = legGap - halfGapLeft;
        int leftX = centerX - halfGapLeft - legW;
        int rightX = centerX + halfGapRight;
        if (rightX <= leftX + legW)
        {
            rightX = leftX + legW + legGap;
        }
        int legZ = RoundToInt(centerZf - legD * 0.5f);
        int legCenterZ = RoundToInt(centerZf);
        int upperW = Math.Max(3, legW - 1);
        int upperD = Math.Max(3, legD - 1);
        int leftCenterX = leftX + legW / 2;
        int rightCenterX = rightX + legW / 2;

        int legTopY = hipMin.Y;
        Vector3I upperMinL = new(leftCenterX - upperW / 2, legTopY - upperLegH, legCenterZ - upperD / 2);
        Vector3I upperMaxL = new(upperMinL.X + upperW, legTopY, upperMinL.Z + upperD);
        Vector3I upperMinR = new(rightCenterX - upperW / 2, legTopY - upperLegH, legCenterZ - upperD / 2);
        Vector3I upperMaxR = new(upperMinR.X + upperW, legTopY, upperMinR.Z + upperD);

        FillLimb(grid, upperMinL, upperMaxL, roundTop: true, taperEnd: 0.88f,
            rootStart: 0.7f, rootEnd: 1.0f, rootAmount: 0.18f,
            kneeStart: 0.7f, kneeEnd: 0.95f, kneeAmount: 0.12f,
            calfStart: 0f, calfEnd: 0f, calfAmount: 0f);
        FillLimb(grid, upperMinR, upperMaxR, roundTop: true, taperEnd: 0.88f,
            rootStart: 0.7f, rootEnd: 1.0f, rootAmount: 0.18f,
            kneeStart: 0.7f, kneeEnd: 0.95f, kneeAmount: 0.12f,
            calfStart: 0f, calfEnd: 0f, calfAmount: 0f);
        TrimUpperLegBack(grid, upperMinL, upperMaxL, trim: 1);
        TrimUpperLegBack(grid, upperMinR, upperMaxR, trim: 1);
        AddGluteBulge(grid, upperMinL, upperMaxL);
        AddGluteBulge(grid, upperMinR, upperMaxR);

        int lowerW = Math.Max(3, upperW - 2);
        int lowerD = Math.Max(3, upperD - 2);

        int upperCenterXL = RoundToInt(upperMinL.X + upperW * 0.5f);
        int upperCenterXR = RoundToInt(upperMinR.X + upperW * 0.5f);
        int upperCenterZ = RoundToInt(upperMinL.Z + upperD * 0.5f);

        Vector3I lowerMinL = new(upperCenterXL - lowerW / 2, upperMinL.Y - lowerLegH, upperCenterZ - lowerD / 2);
        Vector3I lowerMaxL = new(lowerMinL.X + lowerW, upperMinL.Y, lowerMinL.Z + lowerD);
        Vector3I lowerMinR = new(upperCenterXR - lowerW / 2, upperMinR.Y - lowerLegH, upperCenterZ - lowerD / 2);
        Vector3I lowerMaxR = new(lowerMinR.X + lowerW, upperMinR.Y, lowerMinR.Z + lowerD);

        FillLimb(grid, lowerMinL, lowerMaxL, roundTop: false, taperEnd: 0.82f,
            rootStart: 0f, rootEnd: 0f, rootAmount: 0f,
            kneeStart: 0.02f, kneeEnd: 0.22f, kneeAmount: 0.1f,
            calfStart: 0.2f, calfEnd: 0.68f, calfAmount: 0.12f);
        FillLimb(grid, lowerMinR, lowerMaxR, roundTop: false, taperEnd: 0.82f,
            rootStart: 0f, rootEnd: 0f, rootAmount: 0f,
            kneeStart: 0.02f, kneeEnd: 0.22f, kneeAmount: 0.1f,
            calfStart: 0.2f, calfEnd: 0.68f, calfAmount: 0.12f);

        AddKneeCap(grid, upperMinL, upperMaxL, forwardOffset: 1);
        AddKneeCap(grid, upperMinR, upperMaxR, forwardOffset: 1);
        PinchAnkle(grid, lowerMinL, lowerMaxL, layers: 2, inset: 1);
        PinchAnkle(grid, lowerMinR, lowerMaxR, layers: 2, inset: 1);
        TaperLowerLegBottom(grid, lowerMinL, lowerMaxL, layers: 2, insetStart: 1);
        TaperLowerLegBottom(grid, lowerMinR, lowerMaxR, layers: 2, insetStart: 1);

        int footW = Math.Min(lowerW, Math.Max(2, lowerW - 3));
        int footD = Math.Min(lowerD, Math.Max(2, lowerD - 2));
        int footLen = Math.Max(3, footL - 2);

        int footMinZ = lowerMinL.Z + (lowerD - footD) / 2;
        Vector3I footMinL = new(lowerMinL.X + (lowerW - footW) / 2, lowerMinL.Y - footH + 1, footMinZ);
        Vector3I footMaxL = new(footMinL.X + footW, lowerMinL.Y + 1, footMinL.Z + footLen);
        int footMinZR = lowerMinR.Z + (lowerD - footD) / 2;
        Vector3I footMinR = new(lowerMinR.X + (lowerW - footW) / 2, lowerMinR.Y - footH + 1, footMinZR);
        Vector3I footMaxR = new(footMinR.X + footW, lowerMinR.Y + 1, footMinR.Z + footLen);

        FillRoundedFoot(grid, footMinL, footMaxL);
        FillRoundedFoot(grid, footMinR, footMaxR);

        int footMinZActualL = FindFilledMinZ(grid, footMinL, footMaxL, footH);
        int footMinZActualR = FindFilledMinZ(grid, footMinR, footMaxR, footH);
        ClampLowerLegRearToFoot(grid, lowerMinL, lowerMaxL, footMinZActualL);
        ClampLowerLegRearToFoot(grid, lowerMinR, lowerMaxR, footMinZActualR);
        TrimUpperLegRearToFoot(grid, upperMinL, upperMaxL, footMinZActualL, layers: 2);
        TrimUpperLegRearToFoot(grid, upperMinR, upperMaxR, footMinZActualR, layers: 2);
    }

    private static void AddShouldersNeckArms(VoxelGrid grid, Vector3I torsoMin, Vector3I torsoMax, int armLen, int armThickY, int armThickZ, int shoulderPad, int neckH, int neckW)
    {
        float centerXf = (torsoMin.X + torsoMax.X) * 0.5f;
        float centerZf = (torsoMin.Z + torsoMax.Z) * 0.5f;
        int centerX = RoundToInt(centerXf);
        int centerZ = RoundToInt(centerZf);

        int torsoH = torsoMax.Y - torsoMin.Y;
        int shoulderY = torsoMax.Y - Math.Max(2, torsoH / 6);
        int shoulderPadH = Math.Max(2, torsoH / 10);
        int shoulderPadD = Math.Max(2, armThickZ);

        Vector3I padMinL = new(torsoMin.X - shoulderPad, shoulderY - shoulderPadH / 2, centerZ - shoulderPadD / 2);
        Vector3I padMaxL = new(torsoMin.X, shoulderY + (shoulderPadH + 1) / 2, centerZ + (shoulderPadD + 1) / 2);
        Vector3I padMinR = new(torsoMax.X, shoulderY - shoulderPadH / 2, centerZ - shoulderPadD / 2);
        Vector3I padMaxR = new(torsoMax.X + shoulderPad, shoulderY + (shoulderPadH + 1) / 2, centerZ + (shoulderPadD + 1) / 2);
        grid.FillBox(padMinL, padMaxL, 1);
        grid.FillBox(padMinR, padMaxR, 1);

        int capW = Math.Max(armThickY + 2, shoulderPad + 3);
        int capH = Math.Max(armThickY + 2, shoulderPadH + 2);
        int capD = Math.Max(armThickZ + 2, shoulderPadD + 2);

        Vector3I capMinL = new(torsoMin.X - capW + 1, shoulderY - capH / 2, centerZ - capD / 2);
        Vector3I capMaxL = new(torsoMin.X + 1, shoulderY + (capH + 1) / 2, centerZ + (capD + 1) / 2);
        Vector3I capMinR = new(torsoMax.X - 1, shoulderY - capH / 2, centerZ - capD / 2);
        Vector3I capMaxR = new(torsoMax.X - 1 + capW, shoulderY + (capH + 1) / 2, centerZ + (capD + 1) / 2);

        FillEllipsoid(grid, capMinL, capMaxL);
        FillEllipsoid(grid, capMinR, capMaxR);

        Vector3I neckMin = new(centerX - neckW / 2, torsoMax.Y, centerZ - neckW / 2);
        Vector3I neckMax = new(neckMin.X + neckW, torsoMax.Y + neckH, neckMin.Z + neckW);
        FillLimb(grid, neckMin, neckMax, roundTop: false, taperEnd: 0.9f,
            rootStart: 0f, rootEnd: 0f, rootAmount: 0f,
            kneeStart: 0f, kneeEnd: 0f, kneeAmount: 0f,
            calfStart: 0f, calfEnd: 0f, calfAmount: 0f);

        int flareW = neckW + 2;
        int flareD = neckW + 2;
        int flareH = Math.Max(2, neckH / 2);
        Vector3I flareMin = new(centerX - flareW / 2, torsoMax.Y + neckH - flareH, centerZ - flareD / 2);
        Vector3I flareMax = new(flareMin.X + flareW, torsoMax.Y + neckH, flareMin.Z + flareD);
        FillEllipsoid(grid, flareMin, flareMax);

        int upperLen = Math.Max(4, (int)MathF.Round(armLen * 0.55f));
        int lowerLen = Math.Max(4, armLen - upperLen);

        int armCenterY = shoulderY;
        int armCenterZ = centerZ;

        Vector3I upperLMin = new(torsoMin.X - upperLen, armCenterY - armThickY / 2, armCenterZ - armThickZ / 2);
        Vector3I upperLMax = new(torsoMin.X, armCenterY + (armThickY + 1) / 2, armCenterZ + (armThickZ + 1) / 2);
        Vector3I upperRMin = new(torsoMax.X, armCenterY - armThickY / 2, armCenterZ - armThickZ / 2);
        Vector3I upperRMax = new(torsoMax.X + upperLen, armCenterY + (armThickY + 1) / 2, armCenterZ + (armThickZ + 1) / 2);

        FillLimbAlongX(grid, upperLMin, upperLMax, roundStart: true, taperEnd: 0.93f);
        FillLimbAlongX(grid, upperRMin, upperRMax, roundStart: true, taperEnd: 0.93f);

        int lowerThickY = Math.Max(2, armThickY - 2);
        int lowerThickZ = Math.Max(2, armThickZ - 2);

        Vector3I lowerLMin = new(upperLMin.X - lowerLen, armCenterY - lowerThickY / 2, armCenterZ - lowerThickZ / 2);
        Vector3I lowerLMax = new(upperLMin.X, armCenterY + (lowerThickY + 1) / 2, armCenterZ + (lowerThickZ + 1) / 2);
        Vector3I lowerRMin = new(upperRMax.X, armCenterY - lowerThickY / 2, armCenterZ - lowerThickZ / 2);
        Vector3I lowerRMax = new(upperRMax.X + lowerLen, armCenterY + (lowerThickY + 1) / 2, armCenterZ + (lowerThickZ + 1) / 2);

        FillLimbAlongX(grid, lowerLMin, lowerLMax, roundStart: false, taperEnd: 0.75f);
        FillLimbAlongX(grid, lowerRMin, lowerRMax, roundStart: false, taperEnd: 0.75f);

        int handLen = Math.Max(4, lowerLen / 3 + 1);
        int handThickY = Math.Min(lowerThickY + 1, lowerThickY + 2);
        int handThickZ = Math.Min(lowerThickZ + 1, lowerThickZ + 2);

        Vector3I handLMin = new(lowerLMin.X - handLen, armCenterY - handThickY / 2, armCenterZ - handThickZ / 2);
        Vector3I handLMax = new(lowerLMin.X, armCenterY + (handThickY + 1) / 2, armCenterZ + (handThickZ + 1) / 2);
        Vector3I handRMin = new(lowerRMax.X, armCenterY - handThickY / 2, armCenterZ - handThickZ / 2);
        Vector3I handRMax = new(lowerRMax.X + handLen, armCenterY + (handThickY + 1) / 2, armCenterZ + (handThickZ + 1) / 2);

        FillRoundedHand(grid, handLMin, handLMax);
        FillRoundedHand(grid, handRMin, handRMax);
    }

    private static void AddHead(VoxelGrid grid, Vector3I torsoMin, Vector3I torsoMax, float centerXf, float centerZf, int neckH, int headH, int headW, int headD)
    {
        int headMinY = torsoMax.Y + neckH;
        int headMaxY = headMinY + headH;

        Vector3I headMin = new(
            RoundToInt(centerXf - headW * 0.5f),
            headMinY,
            RoundToInt(centerZf - headD * 0.5f) + 2);
        Vector3I headMax = new(headMin.X + headW, headMaxY, headMin.Z + headD);

        FillEllipsoid(grid, headMin, headMax);
    }

    private static void FillLimb(
        VoxelGrid grid,
        Vector3I min,
        Vector3I max,
        bool roundTop,
        float taperEnd,
        float rootStart,
        float rootEnd,
        float rootAmount,
        float kneeStart,
        float kneeEnd,
        float kneeAmount,
        float calfStart,
        float calfEnd,
        float calfAmount)
    {
        int w = max.X - min.X;
        int d = max.Z - min.Z;
        int h = max.Y - min.Y;
        if (w < 2 || d < 2 || h <= 0)
        {
            grid.FillBox(min, max, 1);
            return;
        }

        int centerX = (min.X + max.X) / 2;
        int centerZ = (min.Z + max.Z) / 2;

        for (int y = min.Y; y < max.Y; y++)
        {
            float t = h <= 1 ? 0f : (float)(y - min.Y) / (h - 1);
            float scale = Lerp(1f, taperEnd, t);
            if (roundTop && t < 0.2f)
            {
                float k = Smoothstep(0f, 0.2f, t);
                scale = Lerp(0.7f, scale, k);
            }

            if (rootAmount > 0f && rootEnd > rootStart)
            {
                float root = Smoothstep(rootStart, rootEnd, t);
                scale *= 1f + rootAmount * root;
            }

            if (kneeAmount > 0f && kneeEnd > kneeStart)
            {
                float knee = Bell(t, kneeStart, (kneeStart + kneeEnd) * 0.5f, kneeEnd);
                scale *= 1f + kneeAmount * knee;
            }

            if (calfAmount > 0f && calfEnd > calfStart)
            {
                float calf = Bell(t, calfStart, (calfStart + calfEnd) * 0.5f, calfEnd);
                scale *= 1f + calfAmount * calf;
            }

            float rx = MathF.Max(1f, (w * 0.5f) * scale);
            float rz = MathF.Max(1f, (d * 0.5f) * scale);

            int x0 = centerX - (int)MathF.Ceiling(rx);
            int x1 = centerX + (int)MathF.Ceiling(rx);
            int z0 = centerZ - (int)MathF.Ceiling(rz);
            int z1 = centerZ + (int)MathF.Ceiling(rz);

            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - centerX) / rx;
                    float dz = (z + 0.5f - centerZ) / rz;
                    if (dx * dx + dz * dz <= 1.0f)
                    {
                        grid.Set(x, y, z, 1);
                    }
                }
            }
        }
    }

    private static float Bell(float t, float start, float mid, float end)
    {
        if (end <= start)
        {
            return 0f;
        }

        float rise = Smoothstep(start, mid, t);
        float fall = 1f - Smoothstep(mid, end, t);
        return rise * fall;
    }

    private static void FillRoundedFoot(VoxelGrid grid, Vector3I min, Vector3I max)
    {
        int w = max.X - min.X;
        int d = max.Z - min.Z;
        int h = max.Y - min.Y;
        if (w <= 0 || d <= 0 || h <= 0)
        {
            return;
        }

        for (int z = min.Z; z < max.Z; z++)
        {
            float tz = d <= 1 ? 0f : (float)(z - min.Z) / (d - 1);
            float widthScale = Lerp(1f, 0.35f, Smoothstep(0.2f, 1f, tz));
            float heightScale = Lerp(1f, 0.55f, Smoothstep(0.4f, 1f, tz));

            int sliceW = Math.Clamp((int)MathF.Round(w * widthScale), 1, w);
            int x0 = min.X + (w - sliceW) / 2;
            int x1 = x0 + sliceW - 1;
            int yMax = min.Y + Math.Max(1, (int)MathF.Round(h * heightScale));

            for (int y = min.Y; y < yMax; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    grid.Set(x, y, z, 1);
                }
            }
        }
    }

    private static void AddKneeCap(VoxelGrid grid, Vector3I upperMin, Vector3I upperMax, int forwardOffset)
    {
        int w = upperMax.X - upperMin.X;
        int d = upperMax.Z - upperMin.Z;
        int h = upperMax.Y - upperMin.Y;
        if (w <= 0 || d <= 0 || h <= 0)
        {
            return;
        }

        int kneeY = upperMin.Y + Math.Max(1, (int)MathF.Round(h * 0.1f));
        int centerX = (upperMin.X + upperMax.X) / 2;
        int centerZ = (upperMin.Z + upperMax.Z) / 2 + forwardOffset;

        int capW = Math.Max(2, w - 1);
        int capD = Math.Max(2, d - 1);
        int capH = 3;

        Vector3I capMin = new(centerX - capW / 2, kneeY - capH / 2, centerZ - capD / 2);
        Vector3I capMax = new(capMin.X + capW, kneeY + (capH + 1) / 2, capMin.Z + capD);
        FillEllipsoid(grid, capMin, capMax);
    }

    private static void TrimUpperLegBack(VoxelGrid grid, Vector3I upperMin, Vector3I upperMax, int trim)
    {
        if (trim <= 0)
        {
            return;
        }

        int z0 = upperMin.Z;
        int z1 = Math.Min(upperMax.Z, upperMin.Z + trim);

        for (int y = upperMin.Y; y < upperMax.Y; y++)
        {
            for (int z = z0; z < z1; z++)
            {
                for (int x = upperMin.X; x < upperMax.X; x++)
                {
                    grid.Set(x, y, z, 0);
                }
            }
        }
    }

    private static void AddGluteBulge(VoxelGrid grid, Vector3I upperMin, Vector3I upperMax)
    {
        int w = upperMax.X - upperMin.X;
        int d = upperMax.Z - upperMin.Z;
        int h = upperMax.Y - upperMin.Y;
        if (w <= 1 || d <= 1 || h <= 1)
        {
            return;
        }

        int gluteW = Math.Max(2, w - 3);
        int gluteH = Math.Max(2, h / 4) + 2;
        int gluteD = Math.Max(2, d / 3 - 1);

        int centerX = (upperMin.X + upperMax.X) / 2;
        int topY = upperMax.Y + 2;

        int minX = centerX - gluteW / 2;
        int maxX = minX + gluteW;
        int minY = topY - gluteH;
        int maxY = topY;

        int minZ = upperMin.Z - gluteD + 2;
        int maxZ = upperMin.Z + 2;

        FillEllipsoid(grid, new Vector3I(minX, minY, minZ), new Vector3I(maxX, maxY, maxZ));

        // Clamp glute bulge so it doesn't extend too far behind the upper leg back edge.
        int rearCut = upperMin.Z - 1;
        if (minZ < rearCut)
        {
            grid.CarveBox(new Vector3I(minX, minY, minZ), new Vector3I(maxX, maxY, rearCut));
        }
    }

    private static void PinchAnkle(VoxelGrid grid, Vector3I lowerMin, Vector3I lowerMax, int layers, int inset)
    {
        int w = lowerMax.X - lowerMin.X;
        int d = lowerMax.Z - lowerMin.Z;
        int h = lowerMax.Y - lowerMin.Y;
        if (w <= inset * 2 || d <= inset * 2 || h <= 0)
        {
            return;
        }

        int y0 = lowerMin.Y;
        int y1 = Math.Min(lowerMax.Y, lowerMin.Y + Math.Max(1, layers));

        for (int y = y0; y < y1; y++)
        {
            for (int z = lowerMin.Z; z < lowerMax.Z; z++)
            {
                for (int x = lowerMin.X; x < lowerMax.X; x++)
                {
                    if (x <= lowerMin.X + inset - 1 ||
                        x >= lowerMax.X - inset ||
                        z <= lowerMin.Z + inset - 1 ||
                        z >= lowerMax.Z - inset)
                    {
                        grid.Set(x, y, z, 0);
                    }
                }
            }
        }
    }

    private static void TaperLowerLegBottom(VoxelGrid grid, Vector3I lowerMin, Vector3I lowerMax, int layers, int insetStart)
    {
        int w = lowerMax.X - lowerMin.X;
        int d = lowerMax.Z - lowerMin.Z;
        int h = lowerMax.Y - lowerMin.Y;
        if (w <= insetStart * 2 || d <= insetStart * 2 || h <= 0)
        {
            return;
        }

        int maxLayers = Math.Min(layers, h);
        for (int i = 0; i < maxLayers; i++)
        {
            int inset = insetStart + i;
            if (w <= inset * 2 || d <= inset * 2)
            {
                break;
            }

            int y = lowerMin.Y + i;
            for (int z = lowerMin.Z; z < lowerMax.Z; z++)
            {
                for (int x = lowerMin.X; x < lowerMax.X; x++)
                {
                    if (x <= lowerMin.X + inset - 1 ||
                        x >= lowerMax.X - inset ||
                        z <= lowerMin.Z + inset - 1 ||
                        z >= lowerMax.Z - inset)
                    {
                        grid.Set(x, y, z, 0);
                    }
                }
            }
        }
    }

    private static void ClampLowerLegRearToFoot(VoxelGrid grid, Vector3I lowerMin, Vector3I lowerMax, int footMinZ)
    {
        int h = lowerMax.Y - lowerMin.Y;
        if (h <= 0)
        {
            return;
        }

        int zCut = Math.Clamp(footMinZ, lowerMin.Z, lowerMax.Z);
        int zStart = Math.Max(0, lowerMin.Z - 2);
        for (int y = lowerMin.Y; y < lowerMax.Y; y++)
        {
            for (int z = zStart; z < zCut; z++)
            {
                for (int x = lowerMin.X; x < lowerMax.X; x++)
                {
                    grid.Set(x, y, z, 0);
                }
            }
        }
    }

    private static int FindFilledMinZ(VoxelGrid grid, Vector3I min, Vector3I max, int yLayers)
    {
        int minZ = max.Z;
        int y1 = Math.Min(max.Y, min.Y + Math.Max(1, yLayers));

        for (int z = min.Z; z < max.Z; z++)
        {
            for (int y = min.Y; y < y1; y++)
            {
                for (int x = min.X; x < max.X; x++)
                {
                    if (grid.GetSafe(x, y, z) != 0)
                    {
                        return z;
                    }
                }
            }
        }

        return minZ;
    }

    private static void TrimUpperLegRearToFoot(VoxelGrid grid, Vector3I upperMin, Vector3I upperMax, int footMinZ, int layers)
    {
        int h = upperMax.Y - upperMin.Y;
        if (h <= 0)
        {
            return;
        }

        int y1 = Math.Min(upperMax.Y, upperMin.Y + Math.Max(1, layers));
        int zCut = Math.Clamp(footMinZ, upperMin.Z, upperMax.Z);

        for (int y = upperMin.Y; y < y1; y++)
        {
            for (int z = upperMin.Z; z < zCut; z++)
            {
                for (int x = upperMin.X; x < upperMax.X; x++)
                {
                    grid.Set(x, y, z, 0);
                }
            }
        }
    }


    private static void FillRoundedHand(VoxelGrid grid, Vector3I min, Vector3I max)
    {
        int w = max.X - min.X;
        int h = max.Y - min.Y;
        int d = max.Z - min.Z;
        if (w <= 0 || h <= 0 || d <= 0)
        {
            return;
        }

        float centerY = min.Y + h * 0.5f;
        float centerZ = min.Z + d * 0.5f;
        float ry = MathF.Max(1f, h * 0.5f);
        float rz = MathF.Max(1f, d * 0.5f);

        for (int x = min.X; x < max.X; x++)
        {
            for (int z = min.Z; z < max.Z; z++)
            {
                for (int y = min.Y; y < max.Y; y++)
                {
                    float dy = (y + 0.5f - centerY) / ry;
                    float dz = (z + 0.5f - centerZ) / rz;
                    if (dy * dy + dz * dz <= 1.0f)
                    {
                        grid.Set(x, y, z, 1);
                    }
                }
            }
        }

        int thumbZ0 = max.Z;
        int thumbZ1 = Math.Min(max.Z + 1, grid.SizeZ);
        if (thumbZ1 > thumbZ0)
        {
            int thumbX0 = min.X + Math.Max(0, w / 2 - 1);
            int thumbX1 = Math.Min(max.X, thumbX0 + 2);
            int thumbY0 = min.Y + Math.Max(0, h / 2 - 1);
            int thumbY1 = Math.Min(max.Y, thumbY0 + 2);

            for (int z = thumbZ0; z < thumbZ1; z++)
            {
                for (int y = thumbY0; y < thumbY1; y++)
                {
                    for (int x = thumbX0; x < thumbX1; x++)
                    {
                        grid.Set(x, y, z, 1);
                    }
                }
            }
        }
    }

    private static void FillEllipsoid(VoxelGrid grid, Vector3I min, Vector3I max)
    {
        int w = max.X - min.X;
        int h = max.Y - min.Y;
        int d = max.Z - min.Z;
        if (w <= 0 || h <= 0 || d <= 0)
        {
            return;
        }

        float centerX = min.X + w * 0.5f;
        float centerY = min.Y + h * 0.5f;
        float centerZ = min.Z + d * 0.5f;
        float rx = MathF.Max(1f, w * 0.5f);
        float ry = MathF.Max(1f, h * 0.5f);
        float rz = MathF.Max(1f, d * 0.5f);

        for (int y = min.Y; y < max.Y; y++)
        {
            for (int z = min.Z; z < max.Z; z++)
            {
                for (int x = min.X; x < max.X; x++)
                {
                    float dx = (x + 0.5f - centerX) / rx;
                    float dy = (y + 0.5f - centerY) / ry;
                    float dz = (z + 0.5f - centerZ) / rz;
                    if (dx * dx + dy * dy + dz * dz <= 1.0f)
                    {
                        grid.Set(x, y, z, 1);
                    }
                }
            }
        }
    }

    private static void MirrorXUnion(VoxelGrid grid)
    {
        int sizeX = grid.SizeX;
        int sizeY = grid.SizeY;
        int sizeZ = grid.SizeZ;

        for (int z = 0; z < sizeZ; z++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    int mx = sizeX - 1 - x;
                    if (x > mx)
                    {
                        continue;
                    }

                    byte a = grid.GetSafe(x, y, z);
                    byte b = grid.GetSafe(mx, y, z);
                    byte val = a > b ? a : b;
                    if (val == 0)
                    {
                        continue;
                    }

                    grid.Set(x, y, z, val);
                    grid.Set(mx, y, z, val);
                }
            }
        }
    }

    private static void FillLimbAlongX(VoxelGrid grid, Vector3I min, Vector3I max, bool roundStart, float taperEnd)
    {
        int w = max.X - min.X;
        int h = max.Y - min.Y;
        int d = max.Z - min.Z;
        if (w <= 0 || h <= 0 || d <= 0)
        {
            return;
        }

        float centerY = min.Y + h * 0.5f;
        float centerZ = min.Z + d * 0.5f;

        for (int x = min.X; x < max.X; x++)
        {
            float t = w <= 1 ? 0f : (float)(x - min.X) / (w - 1);
            float scale = Lerp(1f, taperEnd, t);
            if (roundStart && t < 0.2f)
            {
                float k = Smoothstep(0f, 0.2f, t);
                scale = Lerp(0.7f, scale, k);
            }

            float ry = MathF.Max(1f, (h * 0.5f) * scale);
            float rz = MathF.Max(1f, (d * 0.5f) * scale);

            int y0 = (int)MathF.Floor(centerY - ry);
            int y1 = (int)MathF.Ceiling(centerY + ry);
            int z0 = (int)MathF.Floor(centerZ - rz);
            int z1 = (int)MathF.Ceiling(centerZ + rz);

            for (int z = z0; z <= z1; z++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    float dy = (y + 0.5f - centerY) / ry;
                    float dz = (z + 0.5f - centerZ) / rz;
                    if (dy * dy + dz * dz <= 1.0f)
                    {
                        grid.Set(x, y, z, 1);
                    }
                }
            }
        }
    }

    private static void CarveLegSockets(VoxelGrid grid, Vector3I hipMin, Vector3I hipMax, int centerX, int centerZ, int legGap, int legW, int legD)
    {
        int socketH = Math.Max(1, (hipMax.Y - hipMin.Y) / 2);
        int socketY0 = hipMin.Y;
        int socketY1 = hipMin.Y + socketH;

        int halfGapLeft = legGap / 2;
        int halfGapRight = legGap - halfGapLeft;
        int leftX = centerX - halfGapLeft - legW;
        int rightX = centerX + halfGapRight;
        if (rightX <= leftX + legW)
        {
            rightX = leftX + legW + legGap;
        }

        int legZ = centerZ - legD / 2;
        int inset = 1;

        grid.CarveBox(
            new Vector3I(leftX + inset, socketY0, legZ + inset),
            new Vector3I(leftX + legW - inset, socketY1, legZ + legD - inset));

        grid.CarveBox(
            new Vector3I(rightX + inset, socketY0, legZ + inset),
            new Vector3I(rightX + legW - inset, socketY1, legZ + legD - inset));
    }
}
