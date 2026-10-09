using System.Linq;
using ElementSandbox.Core;
using Xunit;
using static ElementSandbox.Tests.TestUtil;

namespace ElementSandbox.Tests;

public class GravityTests
{
    [Fact]
    public void AfterFlippingGravitySandOnTheFloorMovesToTheCeiling()
    {
        var w = new World(40, 40, Seed);
        StoneBox(w, 0, 0, 39, 39);
        w.Fill(1, 33, 38, 38, Mat.Sand);
        Run(w, 50);
        int sand = w.Count(Mat.Sand);

        w.FlipGravity();
        Assert.Equal(-1, w.Gravity);
        Run(w, 200);

        Assert.Equal(sand, w.Count(Mat.Sand));
        foreach (var (x, y) in CellsOf(w, Mat.Sand))
        {
            Mat above = w.Get(x, y - 1);
            Assert.True(above is Mat.Sand or Mat.Stone, $"sand at ({x},{y}) hangs below {above}");
            Assert.True(y <= 6);
        }
    }

    [Fact]
    public void FallingGrainMovesExactlyOneCellPerStepUpward()
    {
        var w = new World(9, 60, Seed);
        w.SetGravity(-1);
        w.Set(4, 59, Mat.Sand);

        for (int step = 1; step < 60; step++)
        {
            w.Step();
            var (x, y) = Single(w, Mat.Sand);
            Assert.Equal(4, x);
            Assert.Equal(59 - step, y);
        }
    }

    [Fact]
    public void LiquidsLevelOutAgainstTheCeiling()
    {
        var w = new World(42, 42, Seed);
        StoneBox(w, 0, 0, 41, 41);
        w.SetGravity(-1);
        w.Fill(1, 10, 15, 36, Mat.Water);
        int water = w.Count(Mat.Water);

        Run(w, 1000);

        var heights = Enumerable.Range(1, 40).Select(x =>
        {
            int h = 0;
            for (int y = 1; y < 41 && w.Get(x, y) == Mat.Water; y++)
            {
                h++;
            }

            return h;
        }).ToArray();
        Assert.Equal(water, heights.Sum());
        Assert.True(heights.Max() - heights.Min() <= 1, string.Join(",", heights));
    }

    [Fact]
    public void OilFloatsOnWaterInFlippedGravity()
    {
        var w = new World(32, 42, Seed);
        StoneBox(w, 0, 0, 31, 41);
        w.SetGravity(-1);
        w.Fill(1, 1, 30, 10, Mat.Oil);   // oil against the "floor" (the ceiling)
        w.Fill(1, 11, 30, 25, Mat.Water);

        Run(w, 1500);

        // With gravity up, "above" means larger y.
        int oilNearestCeiling = CellsOf(w, Mat.Oil).Min(c => c.Y);
        int waterFurthestFromCeiling = CellsOf(w, Mat.Water).Max(c => c.Y);
        Assert.True(oilNearestCeiling > waterFurthestFromCeiling);
    }

    [Fact]
    public void GasesAndFlamesRiseDownwardInFlippedGravity()
    {
        var w = new World(30, 100, Seed);
        w.SetGravity(-1);
        w.Fill(10, 5, 19, 10, Mat.Steam);
        w.Set(5, 5, Mat.Fire);

        w.Step();
        w.Step();
        int fireY = CellsOf(w, Mat.Fire).Single().Y;
        Assert.True(fireY >= 5);

        Run(w, 60);
        Assert.True(CellsOf(w, Mat.Steam).Average(c => c.Y) > 40);
    }

    [Fact]
    public void BurningCellsEmitFlamesOnTheAntiGravitySide()
    {
        var w = new World(30, 30, Seed);
        w.SetGravity(-1);
        w.Fill(5, 10, 24, 12, Mat.Wood);
        for (int x = 5; x <= 24; x++)
        {
            w.Ignite(x, 12);
        }

        bool flameBelow = false;
        for (int i = 0; i < 10; i++)
        {
            w.Step();
            flameBelow |= CellsOf(w, Mat.Fire).Any(c => c.Y > 12);
            Assert.DoesNotContain(CellsOf(w, Mat.Fire), c => c.Y < 10);
        }

        Assert.True(flameBelow);
    }

    [Fact]
    public void SetGravityRejectsInvalidValues()
    {
        var w = new World(4, 4, Seed);
        Assert.Throws<System.ArgumentOutOfRangeException>(() => w.SetGravity(0));
        w.FlipGravity();
        w.FlipGravity();
        Assert.Equal(1, w.Gravity);
    }
}
