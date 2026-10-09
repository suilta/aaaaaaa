using System.Linq;
using ElementSandbox.Core;
using Xunit;
using static ElementSandbox.Tests.TestUtil;

namespace ElementSandbox.Tests;

public class FireTests
{
    [Theory]
    [InlineData(Seed)]
    [InlineData(21)]
    [InlineData(22)]
    [InlineData(23)]
    public void LitWoodInOpenAirBurnsAwayOrTurnsToAsh(int seed)
    {
        var w = new World(80, 80, seed);
        w.Fill(35, 40, 44, 49, Mat.Wood);
        Assert.True(w.Ignite(35, 49)); // one corner

        int steps = 0;
        while (w.Count(Mat.Wood) > 0 && steps < 4000)
        {
            w.Step();
            steps++;
        }

        Assert.Equal(0, w.Count(Mat.Wood));
        Run(w, 300);
        Assert.Equal(0, w.BurningCount());
    }

    [Fact]
    public void BurningWoodLeavesAshAndFireLeavesSmoke()
    {
        var w = new World(80, 80, Seed);
        w.Fill(0, 79, 79, 79, Mat.Stone);
        w.Fill(30, 60, 49, 78, Mat.Wood);
        for (int x = 30; x <= 49; x++)
        {
            w.Ignite(x, 60);
        }

        bool sawSmoke = false;
        for (int i = 0; i < 400; i++)
        {
            w.Step();
            sawSmoke |= w.Count(Mat.Smoke) > 0;
        }

        Assert.True(w.Count(Mat.Ash) > 0, "burnt wood should leave ash");
        Assert.True(sawSmoke, "dying flames should leave smoke");
    }

    [Fact]
    public void BurningWoodTouchingWaterStopsBurning()
    {
        // A wooden wall with water filling the container right up to the lid, so nothing can move.
        var w = new World(12, 12, Seed);
        StoneBox(w, 0, 0, 11, 11);
        w.Fill(1, 1, 1, 10, Mat.Wood);
        w.Fill(2, 1, 10, 10, Mat.Water);
        for (int y = 1; y <= 10; y++)
        {
            Assert.True(w.Ignite(1, y));
        }

        Run(w, 5);

        Assert.Equal(0, w.BurningCount());
        Assert.Equal(10, w.Count(Mat.Wood));
    }

    [Fact]
    public void BurningOilOnWaterKeepsBurning()
    {
        // A one-cell oil film: every oil cell touches both the water below and the air above.
        var w = new World(40, 40, Seed);
        StoneBox(w, 0, 0, 39, 39);
        w.Fill(1, 30, 38, 38, Mat.Water);
        w.Fill(1, 29, 38, 29, Mat.Oil);
        for (int x = 1; x <= 38; x++)
        {
            w.Ignite(x, 29);
        }

        Run(w, 15);

        // Oil is a poor conductor and burns hot, so the water under it cannot cool it below its ignition point.
        int burningOnWater = CellsOf(w, Mat.Oil)
            .Count(c => w.IsBurning(c.X, c.Y) && w.Get(c.X, c.Y + 1) == Mat.Water);
        Assert.True(burningOnWater > 10, $"only {burningOnWater} burning oil cells on the water");
    }

    [Fact]
    public void LitOilBurnsOnlyTheOilLayer()
    {
        var w = new World(40, 40, Seed);
        StoneBox(w, 0, 0, 39, 39);
        w.Fill(1, 30, 38, 38, Mat.Water);
        w.Fill(1, 28, 38, 29, Mat.Oil);
        int water = w.Count(Mat.Water);
        int oil = w.Count(Mat.Oil);
        w.Ignite(20, 28);

        Run(w, 600);

        // Most of the film burns. As it thins it breaks into droplets separated by air, and an
        // isolated droplet cannot gather enough heat to ignite, so a few may survive.
        Assert.True(w.Count(Mat.Oil) < oil * 4 / 10, $"{w.Count(Mat.Oil)} of {oil} oil cells left");
        Assert.Equal(0, w.BurningCount());
        // Burning oil is held off the water by its own poor conductivity, and boiling takes latent heat: the pool survives.
        Assert.True(w.Count(Mat.Water) > water * 9 / 10);
    }

    [Fact]
    public void FireTouchingWaterDies()
    {
        var w = new World(10, 10, Seed);
        StoneBox(w, 0, 0, 9, 9);
        w.Fill(1, 5, 8, 8, Mat.Water);
        w.Set(4, 4, Mat.Fire);

        // Water cools the flame below the point where it can burn within a few steps.
        Run(w, 5);

        Assert.Equal(0, w.Count(Mat.Fire));
    }

    [Fact]
    public void FireRisesAndBurnsOut()
    {
        var w = new World(20, 60, Seed);
        w.Set(10, 50, Mat.Fire);

        w.Step();
        w.Step();
        var after = CellsOf(w, Mat.Fire);
        Assert.Single(after);
        Assert.True(after[0].Y <= 50);

        Run(w, 60);
        Assert.Equal(0, w.Count(Mat.Fire));
    }

    [Fact]
    public void NonFlammableMaterialsNeverBurn()
    {
        var w = new World(30, 30, Seed);
        StoneBox(w, 0, 0, 29, 29);
        w.Fill(1, 20, 28, 28, Mat.Sand);
        for (int i = 0; i < 200; i++)
        {
            w.Paint(15, 15, 5, Mat.Fire);
            w.Step();
            Assert.Equal(0, w.BurningCount());
        }
    }

    [Fact]
    public void FireBrushIgnitesFlammableCells()
    {
        var w = new World(30, 30, Seed);
        w.Fill(10, 10, 19, 19, Mat.Wood);

        w.Paint(15, 15, 2, Mat.Fire);

        Assert.True(w.BurningCount() >= 9);
        Assert.Equal(0, w.Count(Mat.Fire)); // no empty cells under the brush, so no flame particles
    }
}
