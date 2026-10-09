using System;
using System.Collections.Generic;
using ElementSandbox.Core;

namespace ElementSandbox.Tests;

internal static class TestUtil
{
    public const int Seed = 12345;

    /// <summary>Draws a 1-cell stone border around the inclusive rectangle (x0,y0)-(x1,y1).</summary>
    public static void StoneBox(World w, int x0, int y0, int x1, int y1)
    {
        w.Fill(x0, y0, x1, y0, Mat.Stone);
        w.Fill(x0, y1, x1, y1, Mat.Stone);
        w.Fill(x0, y0, x0, y1, Mat.Stone);
        w.Fill(x1, y0, x1, y1, Mat.Stone);
    }

    public static void Run(World w, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            w.Step();
        }
    }

    public static List<(int X, int Y)> CellsOf(World w, Mat m)
    {
        var list = new List<(int, int)>();
        for (int y = 0; y < w.Height; y++)
        {
            for (int x = 0; x < w.Width; x++)
            {
                if (w.Get(x, y) == m)
                {
                    list.Add((x, y));
                }
            }
        }

        return list;
    }

    /// <summary>Height of <paramref name="m"/> stacked on the floor of column x (gravity down).</summary>
    public static int ColumnHeight(World w, int x, int floorY, Mat m)
    {
        int h = 0;
        for (int y = floorY; y >= 0 && w.Get(x, y) == m; y--)
        {
            h++;
        }

        return h;
    }

    public static (int X, int Y) Single(World w, Mat m)
    {
        var cells = CellsOf(w, m);
        if (cells.Count != 1)
        {
            throw new InvalidOperationException($"Expected exactly one {m}, found {cells.Count}.");
        }

        return cells[0];
    }
}
