using System;

namespace ElementSandbox.Core;

/// <summary>
/// Falling-sand grid. Pure C#, no engine types: the Godot node only feeds it input,
/// calls <see cref="Step"/> and uploads the buffer written by <see cref="Render"/>.
/// </summary>
public sealed class World
{
    public const int DefaultWidth = 320;
    public const int DefaultHeight = 180;

    public int Width { get; }
    public int Height { get; }

    /// <summary>Number of simulation steps run so far.</summary>
    public long Frame { get; private set; }

    // Parallel per-cell arrays, indexed by y * Width + x.
    private readonly Mat[] cells;
    private readonly short[] life;
    private readonly short[] burn;
    private readonly byte[] shade;
    private readonly byte[] clock;

    private readonly Rng rng;

    // Stamp written to clock[] for every cell handled this step. Cycles 1..255; 0 means "never".
    private byte stamp;

    public World(int width, int height, int seed)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "World size must be positive.");
        }

        Width = width;
        Height = height;
        int n = width * height;
        cells = new Mat[n];
        life = new short[n];
        burn = new short[n];
        shade = new byte[n];
        clock = new byte[n];
        rng = new Rng(seed);
    }

    public World(int seed) : this(DefaultWidth, DefaultHeight, seed)
    {
    }

    // ---------------------------------------------------------------- cell access

    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public Mat Get(int x, int y) => InBounds(x, y) ? cells[y * Width + x] : Mat.Stone;

    public short LifeAt(int x, int y) => life[y * Width + x];

    /// <summary>Replaces a cell with a fresh particle of <paramref name="m"/>.</summary>
    public void Set(int x, int y, Mat m)
    {
        if (InBounds(x, y))
        {
            Create(y * Width + x, m);
            clock[y * Width + x] = 0;
        }
    }

    /// <summary>Fills a rectangle (inclusive bounds, clipped to the grid).</summary>
    public void Fill(int x0, int y0, int x1, int y1, Mat m)
    {
        for (int y = Math.Max(0, y0); y <= Math.Min(Height - 1, y1); y++)
        {
            for (int x = Math.Max(0, x0); x <= Math.Min(Width - 1, x1); x++)
            {
                Set(x, y, m);
            }
        }
    }

    public void Clear()
    {
        Array.Clear(cells);
        Array.Clear(life);
        Array.Clear(burn);
        Array.Clear(shade);
        Array.Clear(clock);
    }

    public int Count(Mat m)
    {
        int n = 0;
        foreach (var c in cells)
        {
            if (c == m)
            {
                n++;
            }
        }

        return n;
    }

    // ---------------------------------------------------------------- simulation

    public void Step()
    {
        Frame++;
        stamp = (byte)(stamp == 255 ? 1 : stamp + 1);
    }

    // ---------------------------------------------------------------- rendering

    /// <summary>Writes the grid as RGBA8, 4 bytes per cell, row-major.</summary>
    public void Render(byte[] rgba)
    {
        if (rgba.Length < cells.Length * 4)
        {
            throw new ArgumentException("Buffer too small for the grid.", nameof(rgba));
        }

        for (int i = 0; i < cells.Length; i++)
        {
            CellColor(i).WriteRgba(rgba, i * 4);
        }
    }

    private Rgb CellColor(int i)
    {
        var def = Materials.Get(cells[i]);
        if (def.Phase == Phase.Empty)
        {
            return Materials.Background;
        }

        // shade 0..255 maps to a brightness factor of 1 +/- Jitter.
        float k = 1f + def.Jitter * (shade[i] / 127.5f - 1f);
        return def.Color.Scale(k);
    }

    // ---------------------------------------------------------------- internals

    /// <summary>Puts a brand-new particle into cell i: fresh shade, lifetime and no fire.</summary>
    private void Create(int i, Mat m)
    {
        var def = Materials.Get(m);
        cells[i] = m;
        burn[i] = 0;
        shade[i] = (byte)rng.Next(256);
        life[i] = def.HasLifetime ? (short)rng.Range(def.LifeMin, def.LifeMax) : (short)0;
    }
}

/// <summary>Small deterministic xorshift RNG; faster than System.Random and seedable for tests.</summary>
internal sealed class Rng
{
    private uint state;

    public Rng(int seed)
    {
        // SplitMix-style scramble so nearby seeds diverge and the state is never zero.
        uint z = unchecked((uint)seed + 0x9E3779B9u);
        z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
        z = (z ^ (z >> 13)) * 0xC2B2AE35u;
        z ^= z >> 16;
        state = z == 0 ? 0x6D2B79F5u : z;
    }

    public uint NextUInt()
    {
        uint x = state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        state = x;
        return x;
    }

    /// <summary>Uniform integer in [0, n).</summary>
    public int Next(int n) => (int)((ulong)NextUInt() * (uint)n >> 32);

    /// <summary>Uniform integer in [min, max] inclusive.</summary>
    public int Range(int min, int max) => min + Next(max - min + 1);

    /// <summary>Uniform float in [0, 1).</summary>
    public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

    public bool Chance(float p) => p >= 1f || (p > 0f && NextFloat() < p);

    public bool CoinFlip() => (NextUInt() & 0x80000000u) != 0;
}
