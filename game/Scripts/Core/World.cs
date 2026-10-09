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
    private readonly float[] temp;

    private readonly Rng rng;

    // Per-material thermal properties copied into flat arrays for the conduction hot loop.
    // Contact conductance between two materials is the geometric mean of their conductivities:
    // two poor conductors barely exchange heat, while a good one (water) dominates its contact
    // with a poor one (wood). It never exceeds the larger conductivity, which keeps steps stable.
    private static readonly float[] ContactK = BuildContactTable();
    private static readonly float[] InvCapOf = BuildTable(d => 1f / d.HeatCapacity);
    private static readonly float[] AmbientRateOf = BuildTable(d => d.AmbientRate);

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
        temp = new float[n];
        Array.Fill(temp, Materials.AmbientTemp);
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

    /// <summary>Temperature (°C) at (x, y). Outside the grid reads as ambient.</summary>
    public float TemperatureAt(int x, int y) => InBounds(x, y) ? temp[y * Width + x] : Materials.AmbientTemp;

    public void SetTemperature(int x, int y, float celsius)
    {
        if (InBounds(x, y))
        {
            temp[y * Width + x] = celsius;
        }
    }

    /// <summary>Total heat content, sum of capacity × temperature. Conduction alone conserves it.</summary>
    public double ThermalEnergy()
    {
        double e = 0;
        for (int i = 0; i < cells.Length; i++)
        {
            e += (double)temp[i] / InvCapOf[(int)cells[i]];
        }

        return e;
    }

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
        Array.Fill(temp, Materials.AmbientTemp);
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

        Conduct();

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

        // 1. Reactions, all driven by temperature. Each returns false when the particle was transformed.
        if (!UpdateThermalPhase(i, def))
        {
            return;
        }

        if (def.IsFlammable && !UpdateCombustion(i, def, x, y))
        {
            return;
        }

        if (def.IsHeatSource)
        {
            temp[i] += (def.SourceTemp - temp[i]) * def.SourceRate;
            EmitFlame(x, y, def.FlameChance);
        }

        // 2. Lifetime.
        if (def.HasLifetime && --life[i] <= 0)
        {
            Transform(i, rng.Chance(def.DecayChance) ? def.DecaysInto : Mat.Empty, keepTemp: true);
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

    /// <summary>
    /// Boiling, melting, solidifying, freezing, going out. Past a threshold the cell is held at it
    /// and changes form with chance (excess ÷ latent heat); the new material keeps the heat.
    /// </summary>
    private bool UpdateThermalPhase(int i, MaterialDef def)
    {
        float t = temp[i];
        if (t > def.HotAbove)
        {
            temp[i] = def.HotAbove;
            if (def.HotLatent <= 0f || rng.Chance((t - def.HotAbove) / def.HotLatent))
            {
                Transform(i, def.HotInto, keepTemp: true);
                return false;
            }
        }
        else if (t < def.ColdBelow)
        {
            temp[i] = def.ColdBelow;
            if (def.ColdLatent <= 0f || rng.Chance((def.ColdBelow - t) / def.ColdLatent))
            {
                Transform(i, def.ColdInto, keepTemp: true);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Flammable cells catch fire once hot enough. While it touches air, a burning cell heats itself,
    /// throws flames upward and burns down; smothered, it only smoulders. Anything that cools it
    /// below its ignition point (water) puts it out. Fire spreads purely through conducted heat.
    /// </summary>
    private bool UpdateCombustion(int i, MaterialDef def, int x, int y)
    {
        if (burn[i] == 0)
        {
            if (temp[i] >= def.IgnitionTemp && rng.Chance(def.Flammability))
            {
                burn[i] = Math.Max((short)1, def.BurnTicks);
            }

            return true;
        }

        if (temp[i] < def.IgnitionTemp)
        {
            burn[i] = 0;
            return true;
        }

        if (!TouchesAir(x, y))
        {
            return true;
        }

        if (temp[i] < def.BurnTemp)
        {
            temp[i] = Math.Min(def.BurnTemp, temp[i] + def.BurnHeat);
        }

        EmitFlame(x, y, def.FlameChance);
        if (--burn[i] > 0)
        {
            return true;
        }

        Transform(i, rng.CoinFlip() ? def.BurnsInto : Mat.Empty, keepTemp: true);
        return false;
    }

    private bool TouchesAir(int x, int y) =>
        SupportsCombustion(x, y - 1) || SupportsCombustion(x - 1, y) ||
        SupportsCombustion(x + 1, y) || SupportsCombustion(x, y + 1);

    private bool SupportsCombustion(int x, int y) =>
        InBounds(x, y) && Materials.Get(cells[y * Width + x]).SupportsCombustion;

    /// <summary>Lights cell i as if by a flame: brings it above its ignition point and starts the burn.</summary>
    private bool Ignite(int i)
    {
        var def = Materials.Get(cells[i]);
        if (!def.IsFlammable || burn[i] > 0)
        {
            return false;
        }

        temp[i] = Math.Max(temp[i], def.IgnitionTemp + 50f);
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

    /// <summary>
    /// Replaces cell i during a step; the new particle counts as already updated. With
    /// <paramref name="keepTemp"/> it keeps the old particle's heat (matter changing form).
    /// </summary>
    private void Transform(int i, Mat m, bool keepTemp = false)
    {
        float t = temp[i];
        Create(i, m);
        if (keepTemp)
        {
            temp[i] = t;
        }

        clock[i] = stamp;
    }

    // ---------------------------------------------------------------- heat

    /// <summary>
    /// Exchanges heat across every right and down face once per step, then lets materials with an
    /// ambient rate (air) relax toward ambient. Each exchange moves both cells toward each other by
    /// less than their difference, so the pass is stable and, without ambient relaxation, conserves
    /// total heat.
    /// </summary>
    private void Conduct()
    {
        int w = Width;
        for (int y = 0; y < Height; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                int i = row + x;
                int mi = (int)cells[i];
                if (x + 1 < w)
                {
                    Exchange(i, i + 1, mi);
                }

                if (y + 1 < Height)
                {
                    Exchange(i, i + w, mi);
                }
            }
        }

        for (int i = 0; i < temp.Length; i++)
        {
            float rate = AmbientRateOf[(int)cells[i]];
            if (rate > 0f)
            {
                temp[i] += (Materials.AmbientTemp - temp[i]) * rate;
            }
        }
    }

    private void Exchange(int i, int j, int mi)
    {
        float diff = temp[i] - temp[j];
        if (diff is > -0.01f and < 0.01f)
        {
            return;
        }

        int mj = (int)cells[j];
        float q = ContactK[mi * Materials.Count + mj] * diff;
        temp[i] -= q * InvCapOf[mi];
        temp[j] += q * InvCapOf[mj];
    }

    private static float[] BuildContactTable()
    {
        int n = Materials.Count;
        var table = new float[n * n];
        for (int a = 0; a < n; a++)
        {
            for (int b = 0; b < n; b++)
            {
                table[a * n + b] = MathF.Sqrt(Materials.Get((Mat)a).Conductivity * Materials.Get((Mat)b).Conductivity);
            }
        }

        return table;
    }

    private static float[] BuildTable(Func<MaterialDef, float> f)
    {
        var table = new float[Materials.Count];
        for (int m = 0; m < table.Length; m++)
        {
            table[m] = f(Materials.Get((Mat)m));
        }

        return table;
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
        (temp[i], temp[j]) = (temp[j], temp[i]);
        clock[i] = stamp;
        clock[j] = stamp;
    }

    // ---------------------------------------------------------------- rendering

    /// <summary>Writes the grid as RGBA8, 4 bytes per cell, row-major.</summary>
    public void Render(byte[] rgba, RenderMode mode = RenderMode.Normal)
    {
        if (rgba.Length < cells.Length * 4)
        {
            throw new ArgumentException("Buffer too small for the grid.", nameof(rgba));
        }

        for (int i = 0; i < cells.Length; i++)
        {
            Rgb c = mode == RenderMode.Thermal ? ThermalColor(temp[i]) : CellColor(i);
            c.WriteRgba(rgba, i * 4);
        }
    }

    // Thermal view ramp: cold blue, ambient near-black, then red, orange, yellow, white-hot.
    private static readonly (float T, Rgb C)[] ThermalRamp =
    {
        (-40f, new Rgb(0.55f, 0.75f, 1f)),
        (0f, new Rgb(0.10f, 0.25f, 0.70f)),
        (Materials.AmbientTemp, new Rgb(0.04f, 0.04f, 0.08f)),
        (100f, new Rgb(0.55f, 0.05f, 0.35f)),
        (300f, new Rgb(0.90f, 0.15f, 0.05f)),
        (700f, new Rgb(1f, 0.60f, 0.05f)),
        (1200f, new Rgb(1f, 1f, 0.85f)),
    };

    public static Rgb ThermalColor(float t)
    {
        if (t <= ThermalRamp[0].T)
        {
            return ThermalRamp[0].C;
        }

        for (int k = 1; k < ThermalRamp.Length; k++)
        {
            if (t <= ThermalRamp[k].T)
            {
                var (t0, c0) = ThermalRamp[k - 1];
                var (t1, c1) = ThermalRamp[k];
                return Rgb.Lerp(c0, c1, (t - t0) / (t1 - t0));
            }
        }

        return ThermalRamp[^1].C;
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
        if (def.Phase != Phase.Gas && temp[i] > Materials.GlowStart)
        {
            float glow = Math.Min(0.85f, (temp[i] - Materials.GlowStart) / Materials.GlowSpan);
            color = Rgb.Lerp(color, Materials.HeatGlow, glow);
        }

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

    /// <summary>Puts a brand-new particle into cell i: fresh shade, lifetime, base temperature and no fire.</summary>
    private void Create(int i, Mat m)
    {
        var def = Materials.Get(m);
        cells[i] = m;
        temp[i] = def.BaseTemp;
        burn[i] = 0;
        shade[i] = (byte)rng.Next(256);
        life[i] = def.HasLifetime ? (short)rng.Range(def.LifeMin, def.LifeMax) : (short)0;
    }
}

public enum RenderMode
{
    /// <summary>Material colors.</summary>
    Normal,

    /// <summary>Heat map of every cell's temperature.</summary>
    Thermal,
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
