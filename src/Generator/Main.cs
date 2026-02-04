using System;
using System.IO;
using Godot;
using VoxelCore;
using VoxelCore.IO;

public partial class Main : Node
{
    public override void _Ready()
    {
        string[] args = OS.GetCmdlineUserArgs();

        if (HasFlag(args, "--help") || HasFlag(args, "-h"))
        {
            PrintUsage();
            GetTree().Quit();
            return;
        }

        string? seedText = GetArgValue(args, "--seed");
        string? outPath = GetArgValue(args, "--out");

        if (string.IsNullOrWhiteSpace(seedText) || string.IsNullOrWhiteSpace(outPath))
        {
            PrintUsage();
            GetTree().Quit(1);
            return;
        }

        int seed = int.Parse(seedText);
        int height = ParseIntOrDefault(GetArgValue(args, "--height"), 64);
        int torsoVoxels = ParseIntOrDefault(GetArgValue(args, "--torso_voxels"), 800);
        string style = GetArgValue(args, "--style") ?? "chunky";

        VoxelGrid grid = BuildTorsoOnly(height, torsoVoxels, seed);
        string metadataJson = $"{{\"seed\":{seed},\"height_vox\":{height},\"torso_voxels\":{torsoVoxels},\"style\":\"{style}\"}}";

        string outputDir = Path.GetDirectoryName(outPath) ?? ".";
        Directory.CreateDirectory(outputDir);

        VxmCodec.Save(grid, outPath, metadataJson: metadataJson);

        GD.Print($"Saved {outPath} with {grid.VoxelCount} voxels.");
        GetTree().Quit();
    }

    private static VoxelGrid BuildTorsoOnly(int height, int torsoVoxels, int seed)
    {
        torsoVoxels = Math.Max(64, torsoVoxels);

        (int torsoH, int torsoW, int torsoD) = ComputeTorsoDims(torsoVoxels);

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
        int footL = Math.Max(6, legD + 4);

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

        VoxelGrid grid = new(sizeX, sizeY, sizeZ, new Vector3I(centerX, pelvisY, centerZ));
        FillTaperedTorso(grid, min, max);
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

        int legTopY = hipMin.Y;
        Vector3I upperMinL = new(leftX, legTopY - upperLegH, legZ);
        Vector3I upperMaxL = new(leftX + legW, legTopY, legZ + legD);
        Vector3I upperMinR = new(rightX, legTopY - upperLegH, legZ);
        Vector3I upperMaxR = new(rightX + legW, legTopY, legZ + legD);

        FillLimb(grid, upperMinL, upperMaxL, roundTop: true, taperEnd: 0.88f,
            kneeStart: 0.7f, kneeEnd: 0.95f, kneeAmount: 0.12f,
            calfStart: 0f, calfEnd: 0f, calfAmount: 0f);
        FillLimb(grid, upperMinR, upperMaxR, roundTop: true, taperEnd: 0.88f,
            kneeStart: 0.7f, kneeEnd: 0.95f, kneeAmount: 0.12f,
            calfStart: 0f, calfEnd: 0f, calfAmount: 0f);

        int lowerW = Math.Max(3, legW - 2);
        int lowerD = Math.Max(3, legD - 2);

        int upperCenterXL = RoundToInt(upperMinL.X + legW * 0.5f);
        int upperCenterXR = RoundToInt(upperMinR.X + legW * 0.5f);
        int upperCenterZ = RoundToInt(legZ + legD * 0.5f);

        Vector3I lowerMinL = new(upperCenterXL - lowerW / 2, upperMinL.Y - lowerLegH, upperCenterZ - lowerD / 2);
        Vector3I lowerMaxL = new(lowerMinL.X + lowerW, upperMinL.Y, lowerMinL.Z + lowerD);
        Vector3I lowerMinR = new(upperCenterXR - lowerW / 2, upperMinR.Y - lowerLegH, upperCenterZ - lowerD / 2);
        Vector3I lowerMaxR = new(lowerMinR.X + lowerW, upperMinR.Y, lowerMinR.Z + lowerD);

        FillLimb(grid, lowerMinL, lowerMaxL, roundTop: false, taperEnd: 0.82f,
            kneeStart: 0.02f, kneeEnd: 0.22f, kneeAmount: 0.1f,
            calfStart: 0.18f, calfEnd: 0.7f, calfAmount: 0.18f);
        FillLimb(grid, lowerMinR, lowerMaxR, roundTop: false, taperEnd: 0.82f,
            kneeStart: 0.02f, kneeEnd: 0.22f, kneeAmount: 0.1f,
            calfStart: 0.18f, calfEnd: 0.7f, calfAmount: 0.18f);

        int footW = Math.Max(3, lowerW);
        int footD = Math.Max(3, lowerD);

        Vector3I footMinL = new(lowerMinL.X + (lowerW - footW) / 2, lowerMinL.Y - footH, lowerMinL.Z + (lowerD - footD) / 2);
        Vector3I footMaxL = new(footMinL.X + footW, lowerMinL.Y, footMinL.Z + footL);
        Vector3I footMinR = new(lowerMinR.X + (lowerW - footW) / 2, lowerMinR.Y - footH, lowerMinR.Z + (lowerD - footD) / 2);
        Vector3I footMaxR = new(footMinR.X + footW, lowerMinR.Y, footMinR.Z + footL);

        FillRoundedFoot(grid, footMinL, footMaxL);
        FillRoundedFoot(grid, footMinR, footMaxR);
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

        Vector3I neckMin = new(centerX - neckW / 2, torsoMax.Y, centerZ - neckW / 2);
        Vector3I neckMax = new(neckMin.X + neckW, torsoMax.Y + neckH, neckMin.Z + neckW);
        FillLimb(grid, neckMin, neckMax, roundTop: false, taperEnd: 0.9f,
            kneeStart: 0f, kneeEnd: 0f, kneeAmount: 0f,
            calfStart: 0f, calfEnd: 0f, calfAmount: 0f);

        int upperLen = Math.Max(4, (int)MathF.Round(armLen * 0.55f));
        int lowerLen = Math.Max(4, armLen - upperLen);

        int armCenterY = shoulderY;
        int armCenterZ = centerZ;

        Vector3I upperLMin = new(torsoMin.X - upperLen, armCenterY - armThickY / 2, armCenterZ - armThickZ / 2);
        Vector3I upperLMax = new(torsoMin.X, armCenterY + (armThickY + 1) / 2, armCenterZ + (armThickZ + 1) / 2);
        Vector3I upperRMin = new(torsoMax.X, armCenterY - armThickY / 2, armCenterZ - armThickZ / 2);
        Vector3I upperRMax = new(torsoMax.X + upperLen, armCenterY + (armThickY + 1) / 2, armCenterZ + (armThickZ + 1) / 2);

        FillLimbAlongX(grid, upperLMin, upperLMax, roundStart: true, taperEnd: 0.9f);
        FillLimbAlongX(grid, upperRMin, upperRMax, roundStart: true, taperEnd: 0.9f);

        int lowerThickY = Math.Max(2, armThickY - 1);
        int lowerThickZ = Math.Max(2, armThickZ - 1);

        Vector3I lowerLMin = new(upperLMin.X - lowerLen, armCenterY - lowerThickY / 2, armCenterZ - lowerThickZ / 2);
        Vector3I lowerLMax = new(upperLMin.X, armCenterY + (lowerThickY + 1) / 2, armCenterZ + (lowerThickZ + 1) / 2);
        Vector3I lowerRMin = new(upperRMax.X, armCenterY - lowerThickY / 2, armCenterZ - lowerThickZ / 2);
        Vector3I lowerRMax = new(upperRMax.X + lowerLen, armCenterY + (lowerThickY + 1) / 2, armCenterZ + (lowerThickZ + 1) / 2);

        FillLimbAlongX(grid, lowerLMin, lowerLMax, roundStart: false, taperEnd: 0.85f);
        FillLimbAlongX(grid, lowerRMin, lowerRMax, roundStart: false, taperEnd: 0.85f);

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
            RoundToInt(centerZf - headD * 0.5f));
        Vector3I headMax = new(headMin.X + headW, headMaxY, headMin.Z + headD);

        FillEllipsoid(grid, headMin, headMax);
    }

    private static void FillLimb(
        VoxelGrid grid,
        Vector3I min,
        Vector3I max,
        bool roundTop,
        float taperEnd,
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

        float centerX = min.X + w * 0.5f;
        float centerZ = min.Z + d * 0.5f;
        float rx = MathF.Max(1f, w * 0.5f);
        float rz = MathF.Max(1f, d * 0.5f);

        for (int y = min.Y; y < max.Y; y++)
        {
            for (int z = min.Z; z < max.Z; z++)
            {
                for (int x = min.X; x < max.X; x++)
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

    private static bool HasFlag(string[] args, string flag)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetArgValue(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static int ParseIntOrDefault(string? text, int fallback)
    {
        return int.TryParse(text, out int value) ? value : fallback;
    }

    private static void PrintUsage()
    {
        GD.Print("Usage: --seed <int> --out <path> [--height <int>] [--torso_voxels <int>] [--style chunky|slender]");
    }
}
