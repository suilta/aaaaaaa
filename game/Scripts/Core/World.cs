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

    /// <summary>Material at (x, y). Outside the grid reads as stone: the edges behave like walls.</summary>
    public Mat Get(int x, int y) => InBounds(x, y) ? cells[y * Width + x] : Mat.Stone;

    public short LifeAt(int x, int y) => life[y * Width + x];

    public bool IsBurning(int x, int y) => InBounds(x, y) && burn[y * Width + x] > 0;

    /// <summary>Sets a flammable cell on fire. Returns false if it cannot burn or already burns.</summary>
    public bool Ignite(int x, int y) => InBounds(x, y) && Ignite(y * Width + x);

    public int BurningCount()
    {
        int n = 0;
        foreach (var b in burn)
        {
            if (b > 0)
            {
                n++;
            }
        }

        return n;
    }

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

    // ---------------------------------------------------------------- brush

    /// <summary>Fraction of cells a loose-material brush fills, for a natural scattered look.</summary>
    public const float LooseFillChance = 0.35f;

    /// <summary>
    /// Paints a round brush. Only fills empty cells; powders, liquids and gases fill
    /// about 35% of them. <see cref="Mat.Empty"/> erases instead. The flame brush also
    /// ignites the flammable cells it touches.
    /// </summary>
    public void Paint(int cx, int cy, int radius, Mat m)
    {
        var def = Materials.Get(m);
        int r2 = radius * radius + radius; // slightly rounder small brushes
        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                if (dx * dx + dy * dy > r2 || !InBounds(x, y))
                {
                    continue;
                }

                int i = y * Width + x;
                if (m == Mat.Empty)
                {
                    Create(i, Mat.Empty);
                    continue;
                }

                if (m == Materials.Flame && Ignite(i))
                {
                    continue;
                }

                if (cells[i] != Mat.Empty || (def.IsLoose && !rng.Chance(LooseFillChance)))
                {
                    continue;
                }

                Create(i, m);
                clock[i] = 0;
            }
        }
    }

    /// <summary>Paints brush stamps along a segment so fast mouse strokes stay continuous.</summary>
    public void PaintLine(int x0, int y0, int x1, int y1, int radius, Mat m)
    {
        int dx = x1 - x0;
        int dy = y1 - y0;
        int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
        int spacing = Math.Max(1, radius);
        int steps = Math.Max(1, (dist + spacing - 1) / spacing);
        for (int s = 0; s <= steps; s++)
        {
            float t = (float)s / steps;
            Paint((int)MathF.Round(x0 + dx * t), (int)MathF.Round(y0 + dy * t), radius, m);
        }
    }

    // ---------------------------------------------------------------- simulation

    /// <summary>Gravity direction along y: +1 pulls down, -1 pulls up.</summary>
    public int Gravity { get; private set; } = 1;

    /// <summary>Reverses gravity. Every rule is written in terms of <see cref="Gravity"/>, so everything follows.</summary>
    public void FlipGravity() => Gravity = -Gravity;

    public void SetGravity(int direction)
    {
        if (direction != 1 && direction != -1)
        {
            throw new ArgumentOutOfRangeException(nameof(direction), "Gravity is +1 (down) or -1 (up).");
        }

        Gravity = direction;
    }

    public void Step()
    {
        Frame++;
        stamp = (byte)(stamp == 255 ? 1 : stamp + 1);

        // Scan from the gravity-side bottom so falling particles move one cell per step
        // without being visited again; the clock stops anything that moves "ahead" of the scan.
        int g = Gravity;
        int yStart = g > 0 ? Height - 1 : 0;
        int yEnd = g > 0 ? -1 : Height;
        for (int y = yStart; y != yEnd; y -= g)
        {
            // Random horizontal direction per row so liquids do not drift to one side.
            if (rng.CoinFlip())
            {
                for (int x = 0; x < Width; x++)
                {
                    UpdateCell(x, y);
                }
            }
            else
            {
                for (int x = Width - 1; x >= 0; x--)
                {
                    UpdateCell(x, y);
                }
            }
        }
    }

    private void UpdateCell(int x, int y)
    {
        int i = y * Width + x;
        Mat m = cells[i];
        if (m == Mat.Empty || clock[i] == stamp)
        {
            return;
        }

        clock[i] = stamp;
        var def = Materials.Get(m);

        // 1. Reactions. Each returns false when the particle was consumed or transformed.
        if (burn[i] > 0 && !UpdateBurning(i, def, x, y))
        {
            return;
        }

        if (def.IsHot && !UpdateHot(i, def, x, y))
        {
            return;
        }

        // 2. Lifetime.
        if (def.HasLifetime && --life[i] <= 0)
        {
            Transform(i, rng.Chance(def.DecayChance) ? def.DecaysInto : Mat.Empty);
            return;
        }

        // 3. Movement.
        if (def.Fluidity < 1f && !rng.Chance(def.Fluidity))
        {
            return;
        }

        switch (def.Phase)
        {
            case Phase.Powder:
                Fall(i, def, x, y);
                break;
            case Phase.Liquid:
                if (!Fall(i, def, x, y))
                {
                    Flow(i, def, x, y);
                }

                break;
            case Phase.Gas:
                Rise(i, def, x, y);
                break;
        }
    }

    // ---------------------------------------------------------------- reactions

    private static readonly (int Dx, int Dy)[] Neighbors4 = { (0, -1), (-1, 0), (1, 0), (0, 1) };

    private static readonly (int Dx, int Dy)[] Neighbors8 =
    {
        (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1),
    };

    /// <summary>
    /// A burning cell carries a flame: water puts it out (unless the cell is a liquid, so burning
    /// oil keeps burning on water), it throws flames upward, spreads fire, and finally burns out.
    /// </summary>
    private bool UpdateBurning(int i, MaterialDef def, int x, int y)
    {
        if (def.Phase != Phase.Liquid)
        {
            int wet = FindExtinguisher(x, y, Neighbors4);
            if (wet >= 0)
            {
                burn[i] = 0;
                Boil(wet, Materials.Get(Materials.Flame).Heat);
                return true;
            }
        }

        EmitFlame(x, y, def.FlameChance);
        IgniteNeighbors(x, y);

        if (--burn[i] > 0)
        {
            return true;
        }

        Transform(i, rng.CoinFlip() ? def.BurnsInto : Mat.Empty);
        return false;
    }

    /// <summary>
    /// Hot materials (fire, lava) are quenched by extinguishers and otherwise set their
    /// surroundings alight. Wispy gases touch with all 8 neighbors, denser phases with 4.
    /// </summary>
    private bool UpdateHot(int i, MaterialDef def, int x, int y)
    {
        int wet = FindExtinguisher(x, y, def.Phase == Phase.Gas ? Neighbors8 : Neighbors4);
        if (wet >= 0)
        {
            Boil(wet, def.Heat);
            Transform(i, def.QuenchedInto);
            return false;
        }

        IgniteNeighbors(x, y);
        EmitFlame(x, y, def.FlameChance);
        return true;
    }

    private int FindExtinguisher(int x, int y, (int Dx, int Dy)[] offsets)
    {
        foreach (var (dx, dy) in offsets)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (InBounds(nx, ny) && Materials.Get(cells[ny * Width + nx]).Extinguishes)
            {
                return ny * Width + nx;
            }
        }

        return -1;
    }

    /// <summary>Something hot touched extinguisher j: it may boil into its BoilsInto.</summary>
    private void Boil(int j, float chance)
    {
        var def = Materials.Get(cells[j]);
        if (def.BoilsInto != def.Id && rng.Chance(chance))
        {
            Transform(j, def.BoilsInto);
        }
    }

    private void IgniteNeighbors(int x, int y)
    {
        foreach (var (dx, dy) in Neighbors8)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (!InBounds(nx, ny))
            {
                continue;
            }

            int j = ny * Width + nx;
            if (burn[j] == 0 && rng.Chance(Materials.Get(cells[j]).Flammability))
            {
                Ignite(j);
            }
        }
    }

    private bool Ignite(int i)
    {
        var def = Materials.Get(cells[i]);
        if (!def.IsFlammable || burn[i] > 0)
        {
            return false;
        }

        burn[i] = Math.Max((short)1, def.BurnTicks);
        return true;
    }

    /// <summary>Spawns a flame particle in an empty cell on the "up" side (against gravity).</summary>
    private void EmitFlame(int x, int y, float chance)
    {
        if (!rng.Chance(chance))
        {
            return;
        }

        int nx = x + rng.Next(3) - 1;
        int ny = y - Gravity;
        if (InBounds(nx, ny) && cells[ny * Width + nx] == Mat.Empty)
        {
            Transform(ny * Width + nx, Materials.Flame);
        }
    }

    /// <summary>Replaces cell i during a step; the new particle counts as already updated.</summary>
    private void Transform(int i, Mat m)
    {
        Create(i, m);
        clock[i] = stamp;
    }

    // ---------------------------------------------------------------- movement

    /// <summary>Straight down, then a random diagonal, then the other diagonal ("down" = y + g).</summary>
    private bool Fall(int i, MaterialDef def, int x, int y)
    {
        int ny = y + Gravity;
        if (TryMove(i, def, x, ny))
        {
            return true;
        }

        int d = rng.CoinFlip() ? 1 : -1;
        return TryMove(i, def, x + d, ny) || TryMove(i, def, x - d, ny);
    }

    /// <summary>Gases: straight up, then a random diagonal up, then the other, then drift sideways.</summary>
    private bool Rise(int i, MaterialDef def, int x, int y)
    {
        int ny = y - Gravity;
        if (TryMove(i, def, x, ny))
        {
            return true;
        }

        int d = rng.CoinFlip() ? 1 : -1;
        return TryMove(i, def, x + d, ny) || TryMove(i, def, x - d, ny) || TryMove(i, def, x + d, y);
    }

    /// <summary>Sideways liquid flow in a random direction first, then the other.</summary>
    private bool Flow(int i, MaterialDef def, int x, int y)
    {
        int d = rng.CoinFlip() ? 1 : -1;
        return FlowTo(i, def, x, y, d) || FlowTo(i, def, x, y, -d);
    }

    /// <summary>
    /// Walks up to <see cref="MaterialDef.Dispersion"/> cells sideways through empty space,
    /// stopping early at the first spot where the liquid can fall. Swapping with a lighter
    /// fluid is only allowed with the adjacent cell so displaced particles never teleport.
    /// </summary>
    private bool FlowTo(int i, MaterialDef def, int x, int y, int dir)
    {
        int ny = y + Gravity;
        bool belowInBounds = (uint)ny < (uint)Height;
        int target = -1;
        for (int k = 1; k <= def.Dispersion; k++)
        {
            int nx = x + dir * k;
            if ((uint)nx >= (uint)Width)
            {
                break;
            }

            int j = y * Width + nx;
            if (cells[j] != Mat.Empty)
            {
                if (k == 1 && CanEnter(def, j))
                {
                    target = j;
                }

                break;
            }

            target = j;
            if (belowInBounds && CanEnter(def, ny * Width + nx))
            {
                break;
            }
        }

        if (target < 0)
        {
            return false;
        }

        Swap(i, target);
        return true;
    }

    /// <summary>
    /// A mover can enter an empty cell, or swap with a lighter liquid or gas.
    /// Gases only ever move into empty cells.
    /// </summary>
    private bool CanEnter(MaterialDef mover, int j)
    {
        Mat target = cells[j];
        if (target == Mat.Empty)
        {
            return true;
        }

        if (mover.Phase == Phase.Gas)
        {
            return false;
        }

        var t = Materials.Get(target);
        return t.IsFluid && t.Density < mover.Density;
    }

    private bool TryMove(int i, MaterialDef def, int x, int y)
    {
        if (!InBounds(x, y))
        {
            return false;
        }

        int j = y * Width + x;
        if (!CanEnter(def, j))
        {
            return false;
        }

        Swap(i, j);
        return true;
    }

    /// <summary>Swaps two cells with all their per-particle state, and marks both as updated.</summary>
    private void Swap(int i, int j)
    {
        (cells[i], cells[j]) = (cells[j], cells[i]);
        (life[i], life[j]) = (life[j], life[i]);
        (burn[i], burn[j]) = (burn[j], burn[i]);
        (shade[i], shade[j]) = (shade[j], shade[i]);
        clock[i] = stamp;
        clock[j] = stamp;
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
        Rgb color = def.Color;

        if (def.HasLifetime && (def.AgeColor.HasValue || def.FadesOut))
        {
            float remaining = Math.Clamp((float)life[i] / def.LifeMax, 0f, 1f);
            if (def.AgeColor is Rgb aged)
            {
                color = Rgb.Lerp(aged, color, remaining);
            }

            if (def.FadesOut)
            {
                return Rgb.Lerp(Materials.Background, color.Scale(k), remaining);
            }
        }

        color = color.Scale(k);
        if (burn[i] > 0)
        {
            // Flicker: hash of cell and frame, so rendering never touches the simulation RNG.
            uint h = (uint)i * 0x9E3779B1u ^ (uint)Frame * 0x85EBCA77u;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            float flicker = 0.45f + 0.5f * ((h & 0xFF) / 255f);
            color = Rgb.Lerp(color, Materials.BurnGlow, flicker);
        }

        return color;
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
