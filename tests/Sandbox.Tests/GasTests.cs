using System.Linq;
using ElementSandbox.Core;
using Xunit;
using static ElementSandbox.Tests.TestUtil;

namespace ElementSandbox.Tests;

public class GasTests
{
    [Fact]
    public void SteamInASealedBoxEventuallyProducesWater()
    {
        var w = new World(30, 30, Seed);
        StoneBox(w, 0, 0, 29, 29);
        w.Fill(1, 20, 28, 28, Mat.Steam);

        Run(w, 1000);

        Assert.True(w.Count(Mat.Water) > 0, "some steam should condense into water");
        Assert.Equal(0, w.Count(Mat.Steam));
    }

    [Fact]
    public void SteamRisesAndRainsBackDown()
    {
        var w = new World(40, 100, Seed);
        w.Fill(0, 99, 39, 99, Mat.Stone);
        w.Fill(0, 0, 39, 0, Mat.Stone); // ceiling
        w.Fill(10, 90, 29, 98, Mat.Steam);

        Run(w, 100);
        double meanY = CellsOf(w, Mat.Steam).Average(c => c.Y);
        Assert.True(meanY < 40, $"steam should have risen, mean row {meanY}");

        Run(w, 800);
        // Condensed water falls back to the floor.
        Assert.Contains(CellsOf(w, Mat.Water), c => c.Y == 98);
    }

    [Fact]
    public void SmokeRisesAndFadesOut()
    {
        var w = new World(20, 100, Seed);
        w.Fill(5, 90, 14, 95, Mat.Smoke);

        Run(w, 50);
        Assert.True(CellsOf(w, Mat.Smoke).Average(c => c.Y) < 80);

        Run(w, Materials.Get(Mat.Smoke).LifeMax);
        Assert.Equal(0, w.Count(Mat.Smoke));
    }

    [Fact]
    public void WaterPushesSteamUpward()
    {
        var w = new World(10, 30, Seed);
        StoneBox(w, 0, 0, 9, 29);
        w.Fill(1, 20, 8, 28, Mat.Steam);
        w.Fill(1, 10, 8, 14, Mat.Water);

        Run(w, 60);

        int lowestSteam = CellsOf(w, Mat.Steam).Select(c => c.Y).DefaultIfEmpty(0).Max();
        int highestWater = CellsOf(w, Mat.Water).Min(c => c.Y);
        Assert.True(lowestSteam < highestWater, "water should have sunk below the steam");
    }

    [Fact]
    public void GasesOnlyMoveIntoEmptyCells()
    {
        // A sealed, completely full box: lighter steam under heavier smoke. Gases never swap,
        // so nothing can move even though the layering is "wrong".
        var w = new World(10, 10, Seed);
        StoneBox(w, 0, 0, 9, 9);
        w.Fill(1, 1, 8, 4, Mat.Smoke);
        w.Fill(1, 5, 8, 8, Mat.Steam);

        Run(w, 50);

        for (int x = 1; x <= 8; x++)
        {
            for (int y = 1; y <= 8; y++)
            {
                Assert.Equal(y <= 4 ? Mat.Smoke : Mat.Steam, w.Get(x, y));
            }
        }
    }

    [Fact]
    public void FireOnWaterCanBoilItIntoSteam()
    {
        var w = new World(40, 20, Seed);
        StoneBox(w, 0, 0, 39, 19);
        w.Fill(1, 15, 38, 18, Mat.Water);
        int boiled = 0;
        for (int i = 0; i < 100; i++)
        {
            w.Paint(20, 13, 6, Mat.Fire);
            w.Step();
            boiled = System.Math.Max(boiled, w.Count(Mat.Steam));
        }

        Assert.True(boiled > 0);
    }
}
