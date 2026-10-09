using ElementSandbox.Core;
using Xunit;
using static ElementSandbox.Tests.TestUtil;

namespace ElementSandbox.Tests;

public class IceTests
{
    [Fact]
    public void WaterCooledBelowFreezingTurnsToIce()
    {
        var w = new World(10, 10, Seed);
        StoneBox(w, 0, 0, 9, 9);
        w.Fill(1, 5, 8, 8, Mat.Water);
        // Cold enough to pay freezing's latent heat for every cell (a one-off chill to just below
        // zero would only bring the water to its freezing point and leave it there as water).
        for (int y = 0; y <= 9; y++)
        {
            for (int x = 0; x <= 9; x++)
            {
                w.SetTemperature(x, y, -80f);
            }
        }

        Run(w, 30);

        Assert.Equal(0, w.Count(Mat.Water));
        Assert.Equal(32, w.Count(Mat.Ice));
    }

    [Fact]
    public void IceMeltsInWarmWater()
    {
        var w = new World(12, 12, Seed);
        StoneBox(w, 0, 0, 11, 11);
        w.Fill(1, 1, 10, 10, Mat.Water);
        for (int y = 1; y <= 10; y++)
        {
            for (int x = 1; x <= 10; x++)
            {
                w.SetTemperature(x, y, 60f);
            }
        }

        w.Set(5, 5, Mat.Ice);

        Run(w, 300);

        Assert.Equal(0, w.Count(Mat.Ice));
        Assert.Equal(100, w.Count(Mat.Water));
    }

    [Fact]
    public void WaterPouredOntoAnIceSlabFreezes()
    {
        // Like rain landing on ice: a little water, a lot of cold.
        var w = new World(30, 20, Seed);
        StoneBox(w, 0, 0, 29, 19);
        w.Fill(1, 12, 28, 18, Mat.Ice);
        w.Fill(1, 11, 28, 11, Mat.Water);

        Run(w, 300);

        Assert.Equal(0, w.Count(Mat.Water));
    }

    [Fact]
    public void AnIceBlockInAWarmPoolMeltsInsteadOfFreezingIt()
    {
        var w = new World(30, 20, Seed);
        StoneBox(w, 0, 0, 29, 19);
        w.Fill(1, 10, 28, 18, Mat.Water);
        w.Fill(10, 7, 19, 9, Mat.Ice); // resting on the water's surface

        Run(w, 3000);

        Assert.Equal(0, w.Count(Mat.Ice));
    }

    [Fact]
    public void IceNearLavaMeltsAndTheMeltwaterHardensTheLava()
    {
        var w = new World(30, 20, Seed);
        StoneBox(w, 0, 0, 29, 19);
        w.Fill(1, 12, 28, 18, Mat.Lava);
        w.Fill(10, 6, 19, 11, Mat.Ice);
        int stone = w.Count(Mat.Stone);

        Run(w, 400);

        Assert.True(w.Count(Mat.Ice) < 60, "lava melts the ice");
        Assert.True(w.Count(Mat.Stone) > stone, "the cold meltwater hardens some lava");
    }

    [Fact]
    public void IceDoesNotFlickerAroundTheFreezingPoint()
    {
        // Between -2 and 2 °C both forms are stable, so a cell sitting at 0 °C stays what it is.
        var w = new World(4, 1, Seed);
        w.Set(0, 0, Mat.Ice);
        w.Set(3, 0, Mat.Water);
        w.Fill(0, 0, 0, 0, Mat.Ice);
        w.SetTemperature(0, 0, 0f);
        w.SetTemperature(3, 0, 0f);
        w.Fill(1, 0, 2, 0, Mat.Stone);
        w.SetTemperature(1, 0, 0f);
        w.SetTemperature(2, 0, 0f);

        Run(w, 5);

        Assert.Equal(Mat.Ice, w.Get(0, 0));
        Assert.Equal(Mat.Water, w.Get(3, 0));
    }
}
