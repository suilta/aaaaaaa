using System.Linq;
using ElementSandbox.Core;
using Xunit;
using static ElementSandbox.Tests.TestUtil;

namespace ElementSandbox.Tests;

/// <summary>Behaviors that only exist because reactions are driven by temperature, not by contact.</summary>
public class ThermalReactionTests
{
    [Fact]
    public void HeatThroughAStoneWallIgnitesWoodThatNeverTouchesTheLava()
    {
        var w = new World(30, 20, Seed);
        w.Fill(0, 19, 29, 19, Mat.Stone);
        w.Fill(5, 5, 12, 18, Mat.Stone);   // stone basin...
        w.Fill(6, 5, 11, 17, Mat.Lava);    // ...full of lava
        w.Fill(13, 10, 16, 18, Mat.Wood);  // wood leaning on the outside of the right wall

        Assert.Equal(0, w.BurningCount());
        bool burned = false;
        for (int i = 0; i < 2000 && !burned; i++)
        {
            w.Step();
            burned = w.BurningCount() > 0;
        }

        Assert.True(burned, "heat conducted through the stone should light the wood");
    }

    [Fact]
    public void SmotheredFuelDoesNotBurn()
    {
        // Wood sealed inside stone: no air, so a lit cell only smoulders, cools and goes out.
        var w = new World(12, 12, Seed);
        w.Fill(0, 0, 11, 11, Mat.Stone);
        w.Fill(3, 3, 8, 8, Mat.Wood);
        w.Ignite(5, 5);

        Run(w, 300);

        Assert.Equal(0, w.BurningCount());
        Assert.Equal(36, w.Count(Mat.Wood));
    }

    [Fact]
    public void BulkLavaStaysMoltenInOpenAir()
    {
        var w = new World(40, 30, Seed);
        w.Fill(0, 29, 39, 29, Mat.Stone);
        w.Fill(10, 20, 29, 28, Mat.Lava);
        int lava = w.Count(Mat.Lava);

        Run(w, 600);

        // Lava is a heat source, so air never solidifies it. Only a lone drop at the very edge of the
        // flow, sitting on cold stone, may crust over.
        Assert.True(w.Count(Mat.Lava) >= lava * 97 / 100, $"{w.Count(Mat.Lava)} of {lava} lava cells still molten");
        Assert.All(CellsOf(w, Mat.Lava), c => Assert.True(w.TemperatureAt(c.X, c.Y) > 700f));
    }

    [Fact]
    public void WaterNeverGetsHotterThanItsBoilingPointAndBoilsGradually()
    {
        // A stone pot of water sitting in lava.
        var w = new World(30, 30, Seed);
        StoneBox(w, 0, 0, 29, 29);
        w.Fill(1, 20, 28, 28, Mat.Lava);
        w.Fill(8, 10, 21, 19, Mat.Stone);
        w.Fill(9, 10, 20, 18, Mat.Water);
        int water = w.Count(Mat.Water);

        int firstSteam = -1;
        int previous = water;
        for (int i = 1; i <= 400; i++)
        {
            w.Step();
            foreach (var (x, y) in CellsOf(w, Mat.Water))
            {
                Assert.True(w.TemperatureAt(x, y) <= Materials.Get(Mat.Water).HotAbove + 1e-3f);
            }

            int now = w.Count(Mat.Water);
            Assert.True(previous - now <= 4, $"{previous - now} cells boiled in one step");
            previous = now;

            if (firstSteam < 0 && w.Count(Mat.Steam) > 0)
            {
                firstSteam = i;
            }

            if (firstSteam > 0 && i == firstSteam + 30)
            {
                Assert.True(now > water / 2, "latent heat makes boiling gradual, not instant");
            }
        }

        Assert.True(firstSteam > 0, "the pot should boil");
    }

    [Fact]
    public void BurningWoodHeatsUpAndHotStoneComesFromCooledLava()
    {
        var w = new World(30, 30, Seed);
        w.Fill(0, 29, 29, 29, Mat.Stone);
        w.Fill(5, 20, 9, 28, Mat.Wood);
        w.Ignite(5, 20);
        Run(w, 30);
        Assert.True(w.TemperatureAt(5, 20) > Materials.Get(Mat.Wood).IgnitionTemp);

        // Freshly hardened lava keeps its heat as stone.
        var l = new World(20, 12, Seed);
        StoneBox(l, 0, 0, 19, 11);
        l.Fill(1, 1, 6, 10, Mat.Lava);
        l.Fill(7, 1, 18, 10, Mat.Water);
        int walls = l.Count(Mat.Stone);
        Run(l, 20);
        var hardened = CellsOf(l, Mat.Stone).Where(c => c.X > 0 && c.X < 19 && c.Y > 0 && c.Y < 11).ToList();
        Assert.True(l.Count(Mat.Stone) > walls);
        Assert.Contains(hardened, c => l.TemperatureAt(c.X, c.Y) > 200f);
    }
}
