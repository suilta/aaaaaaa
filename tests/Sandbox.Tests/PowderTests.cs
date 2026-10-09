using System;
using ElementSandbox.Core;
using Xunit;
using static ElementSandbox.Tests.TestUtil;

namespace ElementSandbox.Tests;

public class PowderTests
{
    [Fact]
    public void SandDroppedAboveStoneFloorRestsOnTheFloor()
    {
        var w = new World(60, 60, Seed);
        w.Fill(0, 59, 59, 59, Mat.Stone);
        w.Fill(20, 5, 39, 20, Mat.Sand);
        int sand = w.Count(Mat.Sand);

        Run(w, 300);

        Assert.Equal(sand, w.Count(Mat.Sand));
        foreach (var (x, y) in CellsOf(w, Mat.Sand))
        {
            Mat below = w.Get(x, y + 1);
            Assert.True(below is Mat.Sand or Mat.Stone, $"sand at ({x},{y}) is not resting: below is {below}");
        }
    }

    [Fact]
    public void SandPilesIntoASlope()
    {
        var w = new World(80, 60, Seed);
        w.Fill(0, 59, 79, 59, Mat.Stone);
        w.Fill(39, 0, 40, 39, Mat.Sand); // a tall thin column

        Run(w, 400);

        int left = int.MaxValue, right = int.MinValue;
        for (int x = 0; x < 80; x++)
        {
            if (ColumnHeight(w, x, 58, Mat.Sand) > 0)
            {
                left = Math.Min(left, x);
                right = Math.Max(right, x);
            }
        }

        Assert.True(right - left > 10, $"pile should spread out, width was {right - left + 1}");
        for (int x = 1; x < 80; x++)
        {
            int dh = ColumnHeight(w, x, 58, Mat.Sand) - ColumnHeight(w, x - 1, 58, Mat.Sand);
            Assert.InRange(dh, -1, 1);
        }
    }

    [Fact]
    public void StoneNeverMoves()
    {
        var w = new World(20, 20, Seed);
        w.Fill(5, 5, 8, 6, Mat.Stone);

        Run(w, 100);

        Assert.Equal(8, w.Count(Mat.Stone));
        for (int y = 5; y <= 6; y++)
        {
            for (int x = 5; x <= 8; x++)
            {
                Assert.Equal(Mat.Stone, w.Get(x, y));
            }
        }
    }

    [Fact]
    public void FallingGrainMovesExactlyOneCellPerStep()
    {
        var w = new World(9, 60, Seed);
        w.Set(4, 0, Mat.Sand);

        for (int step = 1; step < 60; step++)
        {
            w.Step();
            var (x, y) = Single(w, Mat.Sand);
            Assert.Equal(4, x);
            Assert.Equal(step, y);
        }
    }

    [Fact]
    public void BrushOnlyFillsEmptyCellsAndLooseMaterialIsSparse()
    {
        var w = new World(60, 60, Seed);
        w.Fill(0, 30, 59, 30, Mat.Stone);
        w.Paint(30, 30, 10, Mat.Sand);

        Assert.Equal(60, w.Count(Mat.Stone)); // stone untouched
        int sand = w.Count(Mat.Sand);
        int area = (int)(Math.PI * 10 * 10);
        Assert.InRange(sand, area / 5, area / 2);

        w.Paint(30, 10, 3, Mat.Stone);
        Assert.True(w.Count(Mat.Stone) > 60 + 25, "solid brush fills every empty cell");

        w.Paint(30, 30, 20, Mat.Empty);
        Assert.Equal(0, w.Count(Mat.Sand));
    }
}
