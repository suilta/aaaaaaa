using System.Linq;
using ElementSandbox.Core;
using Xunit;
using static ElementSandbox.Tests.TestUtil;

namespace ElementSandbox.Tests;

public class LavaTests
{
    [Fact]
    public void LavaNextToWaterProducesStoneAndSteam()
    {
        var w = new World(20, 20, Seed);
        w.Set(9, 10, Mat.Lava);
        w.Set(10, 10, Mat.Water);
        // Hold both in place.
        w.Fill(8, 11, 11, 11, Mat.Stone);
        w.Set(8, 10, Mat.Stone);
        w.Set(11, 10, Mat.Stone);

        w.Step();

        Assert.Equal(Mat.Stone, w.Get(9, 10));
        Assert.Equal(1, w.Count(Mat.Steam));
        Assert.Equal(0, w.Count(Mat.Lava));
        Assert.Equal(0, w.Count(Mat.Water));
    }

    [Fact]
    public void LavaPouredIntoAPoolTurnsToStoneAndGivesOffSteam()
    {
        var w = new World(40, 60, Seed);
        StoneBox(w, 0, 0, 39, 59);
        w.Fill(1, 40, 38, 58, Mat.Water);
        w.Fill(15, 5, 24, 14, Mat.Lava);
        int lava = w.Count(Mat.Lava);

        bool sawSteam = false;
        for (int i = 0; i < 1500; i++)
        {
            w.Step();
            sawSteam |= w.Count(Mat.Steam) > 0;
        }

        Assert.True(sawSteam);

        // Box walls are 2*40 + 2*58 = 196 stone cells; the rest is quenched lava. The first lava to
        // hit the pool forms a stone crust, so some lava may end up resting on it, but none of it
        // may still touch water.
        int quenched = w.Count(Mat.Stone) - 196;
        Assert.Equal(lava, quenched + w.Count(Mat.Lava));
        Assert.True(quenched > lava / 2, $"only {quenched} of {lava} lava cells turned to stone");
        foreach (var (x, y) in CellsOf(w, Mat.Lava))
        {
            Assert.DoesNotContain(Mat.Water, new[] { w.Get(x + 1, y), w.Get(x - 1, y), w.Get(x, y + 1), w.Get(x, y - 1) });
        }
    }

    /// <summary>
    /// Intended mechanic (Noita-style): the first lava to reach water becomes a floating stone crust,
    /// and later lava pools on top of it as a lava lake instead of sinking.
    /// </summary>
    [Fact]
    public void LavaOnWaterFormsAStoneCrustThatHoldsALavaLake()
    {
        var w = new World(40, 60, Seed);
        StoneBox(w, 0, 0, 39, 59);
        w.Fill(1, 40, 38, 58, Mat.Water);
        w.Fill(5, 5, 34, 14, Mat.Lava); // far more lava than one crust row needs
        int stoneBefore = w.Count(Mat.Stone);

        Run(w, 1500);

        var lava = CellsOf(w, Mat.Lava);
        var water = CellsOf(w, Mat.Water);
        Assert.True(lava.Count > 0, "a lava lake should remain on top of the crust");
        Assert.True(w.Count(Mat.Stone) > stoneBefore, "lava touching water should have hardened into stone");
        Assert.True(lava.Max(c => c.Y) < water.Min(c => c.Y), "the lava lake sits above the water");

        // Under every lava column there is stone before any water: the crust separates them.
        foreach (int x in lava.Select(c => c.X).Distinct())
        {
            int y = lava.Where(c => c.X == x).Max(c => c.Y) + 1;
            while (w.Get(x, y) is Mat.Lava or Mat.Empty or Mat.Steam)
            {
                y++;
            }

            Assert.Equal(Mat.Stone, w.Get(x, y));
        }
    }

    [Fact]
    public void LavaIgnitesFlammableNeighbors()
    {
        var w = new World(30, 30, Seed);
        w.Fill(0, 29, 29, 29, Mat.Stone);
        w.Fill(5, 26, 24, 28, Mat.Wood);
        w.Fill(10, 20, 19, 25, Mat.Lava);

        Run(w, 200);

        Assert.True(w.BurningCount() > 0 || w.Count(Mat.Wood) < 60, "wood under lava should catch fire");
    }

    [Fact]
    public void LavaBurnsThroughAWoodenPlug()
    {
        var w = new World(30, 60, Seed);
        // A stone cup with a wooden plug in its floor, open air below.
        w.Fill(5, 5, 5, 25, Mat.Stone);
        w.Fill(24, 5, 24, 25, Mat.Stone);
        w.Fill(5, 25, 24, 25, Mat.Stone);
        w.Fill(12, 24, 17, 26, Mat.Wood);
        w.Fill(6, 10, 23, 23, Mat.Lava);
        w.Fill(0, 59, 29, 59, Mat.Stone);

        Run(w, 3000);

        Assert.Equal(0, w.Count(Mat.Wood));
        Assert.Contains(CellsOf(w, Mat.Lava), c => c.Y > 30); // lava escaped through the hole
    }

    [Fact]
    public void LavaIsViscous()
    {
        var lava = new World(80, 20, Seed);
        var water = new World(80, 20, Seed);
        foreach (var (w, m) in new[] { (lava, Mat.Lava), (water, Mat.Water) })
        {
            w.Fill(0, 19, 79, 19, Mat.Stone);
            w.Fill(36, 10, 43, 18, m);
            Run(w, 30);
        }

        int Spread(World w, Mat m) => CellsOf(w, m).Max(c => c.X) - CellsOf(w, m).Min(c => c.X);
        Assert.True(Spread(lava, Mat.Lava) < Spread(water, Mat.Water) / 2);
    }
}
