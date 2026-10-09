using System;
using ElementSandbox.Core;
using Xunit;
using static ElementSandbox.Tests.TestUtil;

namespace ElementSandbox.Tests;

public class TemperatureTests
{
    [Fact]
    public void ConductionConservesHeatInAClosedSolid()
    {
        // No air anywhere, so nothing relaxes toward ambient: conduction alone must conserve heat.
        var w = new World(30, 30, Seed);
        w.Fill(0, 0, 29, 29, Mat.Stone);
        for (int y = 10; y < 15; y++)
        {
            for (int x = 10; x < 15; x++)
            {
                w.SetTemperature(x, y, 1000f);
            }
        }

        double before = w.ThermalEnergy();
        Run(w, 300);

        Assert.Equal(before, w.ThermalEnergy(), before * 1e-5);
        Assert.InRange(w.TemperatureAt(12, 12), 21f, 999f);   // the hot spot cooled
        Assert.True(w.TemperatureAt(20, 12) > 21f);            // its surroundings warmed
    }

    [Fact]
    public void HeatOnlyFlowsFromHotToCold()
    {
        var w = new World(10, 1, Seed);
        w.Fill(0, 0, 9, 0, Mat.Stone);
        w.SetTemperature(0, 0, 500f);

        float previousFar = w.TemperatureAt(9, 0);
        for (int i = 0; i < 500; i++)
        {
            w.Step();
            for (int x = 1; x < 10; x++)
            {
                Assert.True(w.TemperatureAt(x - 1, 0) >= w.TemperatureAt(x, 0) - 1e-3f, "temperature must fall away from the source");
            }

            Assert.True(w.TemperatureAt(9, 0) >= previousFar - 1e-3f);
            previousFar = w.TemperatureAt(9, 0);
        }
    }

    [Fact]
    public void InsulatorsSlowHeatDown()
    {
        // Two identical stone bars, one with a gap of ash (a poor conductor) in the middle.
        var solid = new World(21, 1, Seed);
        var insulated = new World(21, 1, Seed);
        solid.Fill(0, 0, 20, 0, Mat.Stone);
        insulated.Fill(0, 0, 20, 0, Mat.Stone);
        insulated.Set(10, 0, Mat.Ash);
        foreach (var w in new[] { solid, insulated })
        {
            w.SetTemperature(0, 0, 1000f);
            Run(w, 400);
        }

        Assert.True(insulated.TemperatureAt(20, 0) < solid.TemperatureAt(20, 0));
    }

    [Fact]
    public void AirRelaxesTowardAmbient()
    {
        var w = new World(5, 5, Seed);
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                w.SetTemperature(x, y, 400f);
            }
        }

        Run(w, 200);

        Assert.InRange(w.TemperatureAt(2, 2), Materials.AmbientTemp - 0.5f, Materials.AmbientTemp + 0.5f);
    }

    [Fact]
    public void NewParticlesStartAtTheirBaseTemperature()
    {
        var w = new World(10, 10, Seed);
        w.Set(1, 1, Mat.Lava);
        w.Set(2, 2, Mat.Sand);

        Assert.Equal(Materials.Get(Mat.Lava).BaseTemp, w.TemperatureAt(1, 1));
        Assert.Equal(Materials.AmbientTemp, w.TemperatureAt(2, 2));
    }

    [Fact]
    public void TemperatureTravelsWithTheParticle()
    {
        var w = new World(5, 40, Seed);
        w.Set(2, 0, Mat.Sand);
        w.SetTemperature(2, 0, 500f);

        Run(w, 20);

        var (x, y) = Single(w, Mat.Sand);
        Assert.Equal(20, y);
        Assert.True(w.TemperatureAt(x, y) > 250f, "the grain carries its heat while it falls");
        Assert.True(w.TemperatureAt(2, 0) < 100f, "no heat is left behind at the start");
    }

    [Fact]
    public void HotStoneGlows()
    {
        var w = new World(2, 1, Seed);
        w.Fill(0, 0, 1, 0, Mat.Stone);
        w.SetTemperature(1, 0, 900f);
        var rgba = new byte[2 * 4];

        w.Render(rgba);

        int coldRedness = rgba[0] - rgba[2];
        int hotRedness = rgba[4] - rgba[6];
        Assert.True(hotRedness > coldRedness + 100, $"hot stone should glow red-orange ({coldRedness} vs {hotRedness})");
    }

    [Fact]
    public void ThermalViewShowsHeatDifferences()
    {
        var w = new World(3, 1, Seed);
        w.Fill(0, 0, 2, 0, Mat.Stone);
        w.SetTemperature(0, 0, -30f);
        w.SetTemperature(2, 0, 1000f);
        var rgba = new byte[3 * 4];

        w.Render(rgba, RenderMode.Thermal);

        Assert.True(rgba[2] > rgba[0], "cold reads blue");
        Assert.True(rgba[8] > 200 && rgba[9] > rgba[10], "hot reads yellow-white");
        Assert.True(Math.Max(rgba[4], Math.Max(rgba[5], rgba[6])) < 60, "ambient reads dark");
    }
}
