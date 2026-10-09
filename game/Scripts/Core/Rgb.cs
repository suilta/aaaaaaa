using System;

namespace ElementSandbox.Core;

/// <summary>Small engine-independent color (components 0..1). Core code must not use Godot.Color.</summary>
public readonly struct Rgb
{
    public readonly float R;
    public readonly float G;
    public readonly float B;

    public Rgb(float r, float g, float b)
    {
        R = r;
        G = g;
        B = b;
    }

    public Rgb Scale(float k) => new(R * k, G * k, B * k);

    public static Rgb Lerp(Rgb a, Rgb b, float t) =>
        new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);

    /// <summary>Writes the color as opaque RGBA8 at <paramref name="offset"/>.</summary>
    public void WriteRgba(byte[] buffer, int offset)
    {
        buffer[offset] = ToByte(R);
        buffer[offset + 1] = ToByte(G);
        buffer[offset + 2] = ToByte(B);
        buffer[offset + 3] = 255;
    }

    private static byte ToByte(float v) => (byte)(Math.Clamp(v, 0f, 1f) * 255f + 0.5f);

    public override string ToString() => $"({R:0.###}, {G:0.###}, {B:0.###})";
}
