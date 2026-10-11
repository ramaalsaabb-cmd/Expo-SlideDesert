using System;

/// <summary>Deterministic desert relief outside the authored terrain; no per-frame noise.</summary>
public static class ThumamahLandscapeProfile
{
    public static float Smooth(float start, float end, float value)
    {
        float t = Math.Max(0f, Math.Min(1f, (value - start) / (end - start)));
        return t * t * (3f - 2f * t);
    }

    public static float Noise(float x, float z, int seed)
    {
        int ix = (int)Math.Floor(x), iz = (int)Math.Floor(z);
        float u = Smooth(0f, 1f, x - ix), v = Smooth(0f, 1f, z - iz);
        float a = Hash(ix, iz, seed), b = Hash(ix + 1, iz, seed);
        float c = Hash(ix, iz + 1, seed), d = Hash(ix + 1, iz + 1, seed);
        return (a + (b - a) * u) * (1f - v) + (c + (d - c) * u) * v;
    }

    static float Hash(int x, int z, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + z * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }
    }

    public static float DistanceOutside(float x, float z, float width, float length)
    {
        float dx = Math.Max(0f, Math.Max(-x, x - width));
        float dz = Math.Max(0f, Math.Max(-z, z - length));
        return (float)Math.Sqrt(dx * dx + dz * dz);
    }

    public static float Relief(float x, float z, float width, float length,
        float extent, float mountainHeight, int seed, out float rock)
    {
        float distance = DistanceOutside(x, z, width, length);
        // Long wind-shaped sand crests, with broad windward slopes and narrower lee slopes.
        // Only low-frequency variation changes the silhouette; no rocky surface bumps.
        float eastCenter = width + extent * (0.62f + 0.10f * (float)Math.Sin(z / 260f + seed));
        float northCenter = length + extent * (0.66f + 0.09f * (float)Math.Sin(x / 280f + seed * 0.3f));
        float eastDelta = x - eastCenter;
        float northDelta = z - northCenter;
        float eastOffset = eastDelta / (extent * (eastDelta < 0f ? 0.68f : 0.42f));
        float northOffset = northDelta / (extent * (northDelta < 0f ? 0.72f : 0.46f));
        float east = (float)Math.Exp(-eastOffset * eastOffset)
            * (0.34f + 0.66f * Noise(z / 105f, 2f, seed + 11));
        float north = (float)Math.Exp(-northOffset * northOffset) * 0.78f
            * (0.38f + 0.62f * Noise(x / 115f, 6f, seed + 17));
        float massif = (1f - (1f - east) * (1f - north)) * mountainHeight * 0.85f;
        // Additional broad dunes have independent heights, widths and staggered positions.
        // Smoothly combine their profiles instead of introducing hard joins between peaks.
        float extra = 0f;
        for (int i = 0; i < 14; i++)
        {
            int side = i % 4;
            float along = (i / 4 + 0.35f + Hash(i, 1, seed) * 0.5f) / 4f;
            float away = extent * (0.40f + Hash(i, 2, seed) * 0.44f);
            float centerX = side == 0 ? -away : side == 1 ? width + away : along * width;
            float centerZ = side == 2 ? -away : side == 3 ? length + away : along * length;
            float radius = 85f + Hash(i, 3, seed) * 80f;
            float dx = (x - centerX) / radius;
            float dz = (z - centerZ) / (radius * 1.4f);
            float peak = (float)Math.Exp(-dx * dx - dz * dz)
                * (0.22f + Hash(i, 4, seed) * 0.58f);
            extra = 1f - (1f - extra) * (1f - peak);
        }
        massif += extra * mountainHeight * 0.62f;
        float sandWave = 0.5f + 0.5f * (float)Math.Sin(x / 100f + z / 180f
            + Noise(x / 330f, z / 350f, seed + 23));
        float hills = 3f + 14f * sandWave * sandWave;
        float apron = Smooth(0f, extent * 0.55f, distance);
        rock = 0f;
        return (massif + hills) * apron;
    }
}
