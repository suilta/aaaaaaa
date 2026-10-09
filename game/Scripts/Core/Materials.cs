using System;

namespace ElementSandbox.Core;

/// <summary>Material id stored per cell. Keep it a byte so the grid stays compact.</summary>
public enum Mat : byte
{
    Empty,
    Stone,
    Sand,
    Water,
    Wood,
    Oil,
    Fire,
    Smoke,
    Steam,
    Lava,
    Ash,
    Ice,
}

/// <summary>Decides how a material moves.</summary>
public enum Phase : byte
{
    Empty,
    Solid,
    Powder,
    Liquid,
    Gas,
}

/// <summary>
/// Every behavior of a material comes from these properties. The simulation never asks
/// "is this water?"; it asks "does this extinguish fire?", "is this hot?", "how dense is it?".
/// A new material only needs a new entry in <see cref="Materials"/>.
/// </summary>
public sealed class MaterialDef
{
    public Mat Id { get; init; }
    public string Name { get; init; } = "";
    public Phase Phase { get; init; }

    // ---- movement

    /// <summary>Heavier materials sink below lighter liquids and gases by swapping places.</summary>
    public float Density { get; init; }

    /// <summary>Max cells a liquid flows sideways per frame.</summary>
    public int Dispersion { get; init; }

    /// <summary>Per-frame chance to move at all. Low for viscous materials.</summary>
    public float Fluidity { get; init; } = 1f;

    // ---- fire

    /// <summary>Per-frame chance of catching fire while at or above <see cref="IgnitionTemp"/>. 0 = not flammable.</summary>
    public float Flammability { get; init; }

    /// <summary>Temperature (°C) at which a flammable material can catch fire. Cooling below it puts the fire out.</summary>
    public float IgnitionTemp { get; init; } = float.PositiveInfinity;

    /// <summary>Degrees a burning cell gains per step from its own combustion.</summary>
    public float BurnHeat { get; init; }

    /// <summary>Hottest temperature (°C) combustion alone can bring the cell to.</summary>
    public float BurnTemp { get; init; }

    /// <summary>Frames the material burns once lit.</summary>
    public short BurnTicks { get; init; }

    /// <summary>What a burnt-out cell becomes (the other half of the time it vanishes).</summary>
    public Mat BurnsInto { get; init; }

    /// <summary>Per-frame chance that the cell, while burning or hot, emits a flame on its "up" side.</summary>
    public float FlameChance { get; init; }

    // ---- heat

    /// <summary>Temperature (°C) of a freshly created particle.</summary>
    public float BaseTemp { get; init; } = Materials.AmbientTemp;

    /// <summary>
    /// How readily heat crosses this material's faces (0..0.4). Contact between two cells uses the
    /// geometric mean of both: two insulators barely exchange heat, while a good conductor such as
    /// water dominates its contact with a poor one such as wood.
    /// </summary>
    public float Conductivity { get; init; }

    /// <summary>Heat needed per degree (≥ 1). High capacity heats and cools slowly.</summary>
    public float HeatCapacity { get; init; } = 1f;

    /// <summary>Per-step fraction by which the cell relaxes toward ambient temperature (the open world as a heat sink).</summary>
    public float AmbientRate { get; init; }

    /// <summary>
    /// Heat sources pull themselves toward <see cref="SourceTemp"/> by this fraction per step
    /// (lava stays molten, flames stay hot). 0 = not a source.
    /// </summary>
    public float SourceRate { get; init; }
    public float SourceTemp { get; init; }

    /// <summary>Above this temperature the cell turns into <see cref="HotInto"/> (boiling, melting).</summary>
    public float HotAbove { get; init; } = float.PositiveInfinity;
    public Mat HotInto { get; init; }

    /// <summary>
    /// Latent heat of the hot transition, in degrees of this material. Past the threshold the cell is
    /// held at it and changes with chance (excess ÷ latent) per step, so on average it absorbs this
    /// much extra heat first. 0 = changes at once.
    /// </summary>
    public float HotLatent { get; init; }

    /// <summary>Below this temperature the cell turns into <see cref="ColdInto"/> (solidifying, freezing, going out).</summary>
    public float ColdBelow { get; init; } = float.NegativeInfinity;
    public Mat ColdInto { get; init; }

    /// <summary>Latent heat of the cold transition (see <see cref="HotLatent"/>). 0 = changes at once.</summary>
    public float ColdLatent { get; init; }

    /// <summary>
    /// Supplies oxygen: a burning cell only produces heat, throws flames and consumes fuel while it
    /// touches a cell like this. Smothered fuel keeps smouldering but cools off.
    /// </summary>
    public bool SupportsCombustion { get; init; }

    // ---- lifetime

    /// <summary>Lifetime range in frames. 0 = permanent. Only gases and fire use it.</summary>
    public short LifeMin { get; init; }
    public short LifeMax { get; init; }

    /// <summary>On expiry the cell becomes <see cref="DecaysInto"/> with this chance, otherwise Empty.</summary>
    public float DecayChance { get; init; }
    public Mat DecaysInto { get; init; }

    // ---- looks

    public Rgb Color { get; init; }

    /// <summary>Random brightness variation, as a fraction of the base color.</summary>
    public float Jitter { get; init; }

    /// <summary>If set, the color shifts from <see cref="Color"/> toward this as lifetime runs out.</summary>
    public Rgb? AgeColor { get; init; }

    /// <summary>If true, the particle fades into the background as its lifetime runs out.</summary>
    public bool FadesOut { get; init; }

    // ---- derived

    public bool IsLoose => Phase is Phase.Powder or Phase.Liquid or Phase.Gas;
    public bool IsFluid => Phase is Phase.Liquid or Phase.Gas;
    public bool IsFlammable => Flammability > 0f;
    public bool IsHeatSource => SourceRate > 0f;
    public bool HasLifetime => LifeMax > 0;
}

/// <summary>The material property table.</summary>
public static class Materials
{
    /// <summary>Temperature (°C) of the open world: air relaxes toward it, new particles default to it.</summary>
    public const float AmbientTemp = 20f;

    public const float MaxConductivity = 0.4f;
    public const float MinHeatCapacity = 1f;

    public static readonly Rgb Background = new(0.05f, 0.05f, 0.08f);

    /// <summary>Color burning cells flicker toward.</summary>
    public static readonly Rgb BurnGlow = new(1f, 0.55f, 0.1f);

    /// <summary>The particle burning cells emit, and the material the fire brush paints.</summary>
    public const Mat Flame = Mat.Fire;

    private static readonly MaterialDef[] Table = Build();

    public static MaterialDef Get(Mat m) => Table[(int)m];

    public static int Count => Table.Length;

    private static MaterialDef[] Build()
    {
        var defs = new MaterialDef[]
        {
            new()
            {
                Id = Mat.Empty, Name = "空", Phase = Phase.Empty, Color = Background,
                Conductivity = 0.0005f, HeatCapacity = 1f, AmbientRate = 0.05f, SupportsCombustion = true,
            },
            new()
            {
                Id = Mat.Stone, Name = "石", Phase = Phase.Solid, Density = 3f,
                Conductivity = 0.1f, HeatCapacity = 2f,
                Color = new Rgb(0.42f, 0.42f, 0.47f), Jitter = 0.08f,
            },
            new()
            {
                Id = Mat.Sand, Name = "沙", Phase = Phase.Powder, Density = 1.6f,
                Conductivity = 0.05f, HeatCapacity = 1.5f,
                Color = new Rgb(0.86f, 0.74f, 0.45f), Jitter = 0.1f,
            },
            new()
            {
                Id = Mat.Water, Name = "水", Phase = Phase.Liquid, Density = 1f, Dispersion = 5,
                Conductivity = 0.4f, HeatCapacity = 8f,
                HotAbove = 100f, HotInto = Mat.Steam, HotLatent = 300f,
                ColdBelow = -2f, ColdInto = Mat.Ice, ColdLatent = 45f,
                Color = new Rgb(0.18f, 0.38f, 0.85f), Jitter = 0.04f,
            },
            new()
            {
                Id = Mat.Wood, Name = "木", Phase = Phase.Solid, Density = 1.2f,
                Conductivity = 0.015f, HeatCapacity = 1.5f,
                Flammability = 0.05f, IgnitionTemp = 300f, BurnHeat = 14f, BurnTemp = 800f,
                BurnTicks = 180, BurnsInto = Mat.Ash, FlameChance = 0.25f,
                Color = new Rgb(0.45f, 0.28f, 0.12f), Jitter = 0.1f,
            },
            new()
            {
                Id = Mat.Oil, Name = "油", Phase = Phase.Liquid, Density = 0.8f, Dispersion = 3, Fluidity = 0.9f,
                Conductivity = 0.05f, HeatCapacity = 1f,
                Flammability = 0.25f, IgnitionTemp = 200f, BurnHeat = 40f, BurnTemp = 900f,
                BurnTicks = 45, BurnsInto = Mat.Smoke, FlameChance = 0.4f,
                Color = new Rgb(0.28f, 0.20f, 0.10f), Jitter = 0.06f,
            },
            new()
            {
                Id = Mat.Fire, Name = "火", Phase = Phase.Gas, Density = 0.01f, Fluidity = 0.7f,
                BaseTemp = 900f, Conductivity = 0.25f, HeatCapacity = 1f,
                SourceTemp = 900f, SourceRate = 0.15f, ColdBelow = 400f, ColdInto = Mat.Empty, SupportsCombustion = true,
                LifeMin = 15, LifeMax = 40, DecayChance = 0.25f, DecaysInto = Mat.Smoke,
                Color = new Rgb(1f, 0.9f, 0.3f), AgeColor = new Rgb(0.85f, 0.15f, 0.05f), Jitter = 0.1f,
            },
            new()
            {
                Id = Mat.Smoke, Name = "烟", Phase = Phase.Gas, Density = 0.1f, Fluidity = 0.7f,
                Conductivity = 0.02f, HeatCapacity = 1f, AmbientRate = 0.02f, SupportsCombustion = true,
                LifeMin = 150, LifeMax = 300,
                Color = new Rgb(0.30f, 0.30f, 0.32f), Jitter = 0.08f, FadesOut = true,
            },
            new()
            {
                Id = Mat.Steam, Name = "蒸汽", Phase = Phase.Gas, Density = 0.05f, Fluidity = 0.7f,
                BaseTemp = 110f, Conductivity = 0.05f, HeatCapacity = 1f, AmbientRate = 0.02f,
                LifeMin = 200, LifeMax = 400, DecayChance = 0.3f, DecaysInto = Mat.Water,
                Color = new Rgb(0.75f, 0.80f, 0.86f), Jitter = 0.05f, FadesOut = true,
            },
            new()
            {
                Id = Mat.Lava, Name = "熔岩", Phase = Phase.Liquid, Density = 2.5f, Dispersion = 2, Fluidity = 0.25f,
                BaseTemp = 1200f, Conductivity = 0.02f, HeatCapacity = 1f,
                SourceTemp = 1200f, SourceRate = 0.03f, ColdBelow = 700f, ColdInto = Mat.Stone, FlameChance = 0.02f,
                Color = new Rgb(0.95f, 0.35f, 0.05f), Jitter = 0.12f,
            },
            new()
            {
                Id = Mat.Ash, Name = "灰", Phase = Phase.Powder, Density = 0.5f,
                Conductivity = 0.03f, HeatCapacity = 1f,
                Color = new Rgb(0.35f, 0.33f, 0.32f), Jitter = 0.1f,
            },
            new()
            {
                // Painted ice is deep-frozen so a block of it can freeze the water it touches.
                Id = Mat.Ice, Name = "冰", Phase = Phase.Solid, Density = 0.9f,
                BaseTemp = -60f, Conductivity = 0.3f, HeatCapacity = 4f,
                HotAbove = 2f, HotInto = Mat.Water, HotLatent = 45f,
                Color = new Rgb(0.70f, 0.86f, 0.95f), Jitter = 0.05f,
            },
        };

        var table = new MaterialDef[Enum.GetValues<Mat>().Length];
        foreach (var d in defs)
        {
            table[(int)d.Id] = d;
        }

        for (int i = 0; i < table.Length; i++)
        {
            if (table[i] == null)
            {
                throw new InvalidOperationException($"Material {(Mat)i} has no definition.");
            }

            // Keeps every pairwise heat exchange a contraction (k/capA + k/capB <= 1), so conduction can never overshoot.
            if (table[i].Conductivity is < 0f or > MaxConductivity || table[i].HeatCapacity < MinHeatCapacity)
            {
                throw new InvalidOperationException($"Material {(Mat)i} has out-of-range thermal properties.");
            }
        }

        return table;
    }
}
