using System;

namespace ElementSandbox.Core;

/// <summary>Material id stored per cell. Keep it a byte so the grid stays compact.</summary>
public enum Mat : byte
{
    Empty,
    Stone,
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

    /// <summary>Heavier materials sink below lighter liquids and gases by swapping places.</summary>
    public float Density { get; init; }

    /// <summary>Per-frame chance of catching fire when next to something hot. 0 = not flammable.</summary>
    public float Flammability { get; init; }

    /// <summary>Frames the material burns once lit.</summary>
    public short BurnTicks { get; init; }

    /// <summary>What a burnt-out cell becomes (or Empty).</summary>
    public Mat BurnsInto { get; init; }

    /// <summary>Lifetime range in frames. 0 = permanent.</summary>
    public short LifeMin { get; init; }
    public short LifeMax { get; init; }

    /// <summary>Max cells a liquid flows sideways per frame.</summary>
    public int Dispersion { get; init; }

    /// <summary>Per-frame chance to move at all. Low for viscous materials.</summary>
    public float Fluidity { get; init; } = 1f;

    public Rgb Color { get; init; }

    /// <summary>Random brightness variation, as a fraction of the base color.</summary>
    public float Jitter { get; init; }

    public bool IsLoose => Phase is Phase.Powder or Phase.Liquid or Phase.Gas;
    public bool IsFluid => Phase is Phase.Liquid or Phase.Gas;
    public bool HasLifetime => LifeMax > 0;
}

/// <summary>The material property table.</summary>
public static class Materials
{
    public static readonly Rgb Background = new(0.05f, 0.05f, 0.08f);

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
            },
            new()
            {
                Id = Mat.Stone, Name = "石", Phase = Phase.Solid, Density = 3f,
                Color = new Rgb(0.42f, 0.42f, 0.47f), Jitter = 0.08f,
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
        }

        return table;
    }
}
