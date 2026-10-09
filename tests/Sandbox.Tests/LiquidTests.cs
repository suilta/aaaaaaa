using System.Linq;
using ElementSandbox.Core;
using Xunit;
using static ElementSandbox.Tests.TestUtil;

namespace ElementSandbox.Tests;

public class LiquidTests
{
    [Theory]
    [InlineData(Seed)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void WaterInClosedContainerEndsWithFlatSurface(int seed)
    {
        var w = new World(42, 42, seed);
        StoneBox(w, 0, 0, 41, 41);
        w.Fill(1, 5, 15, 30, Mat.Water); // a tall slab against the left wall
        int water = w.Count(Mat.Water);

        Run(w, 1000);

        Assert.Equal(water, w.Count(Mat.Water));
        var heights = Enumerable.Range(1, 40).Select(x => ColumnHeight(w, x, 40, Mat.Water)).ToArray();
        Assert.Equal(water, heights.Sum()); // every water cell sits in a solid column from the floor
        Assert.True(heights.Max() - heights.Min() <= 1, $"surface not flat: {string.Join(",", heights)}");
    }

    [Fact]
    public void SandPlacedOnWaterSinksBelowIt()
    {
        var w = new World(32, 42, Seed);
        StoneBox(w, 0, 0, 31, 41);
        w.Fill(1, 20, 30, 40, Mat.Water);
        w.Fill(1, 15, 30, 17, Mat.Sand);
        int sand = w.Count(Mat.Sand);
        int water = w.Count(Mat.Water);

        Run(w, 600);

        Assert.Equal(sand, w.Count(Mat.Sand));
        Assert.Equal(water, w.Count(Mat.Water));
        foreach (var (x, y) in CellsOf(w, Mat.Sand))
        {
            Mat below = w.Get(x, y + 1);
            Assert.True(below is Mat.Sand or Mat.Stone, $"sand at ({x},{y}) sits on {below}");
        }

        int lowestWater = CellsOf(w, Mat.Water).Max(c => c.Y);
        int highestSand = CellsOf(w, Mat.Sand).Min(c => c.Y);
        Assert.True(highestSand > lowestWater - 3, "sand should be at the bottom of the pool");
    }

    [Fact]
    public void SandAndWaterConserveMassOver1000Steps()
    {
        var w = new World(64, 64, Seed);
        StoneBox(w, 0, 0, 63, 63);
        w.Fill(5, 5, 30, 20, Mat.Water);
        w.Fill(33, 2, 58, 25, Mat.Sand);
        w.Fill(20, 30, 40, 31, Mat.Stone); // a ledge to pour over
        int sand = w.Count(Mat.Sand);
        int water = w.Count(Mat.Water);

        for (int i = 0; i < 10; i++)
        {
            Run(w, 100);
            Assert.Equal(sand, w.Count(Mat.Sand));
            Assert.Equal(water, w.Count(Mat.Water));
        }
    }

    [Fact]
    public void WaterSpreadsSidewaysFasterThanOneCellPerStep()
    {
        var w = new World(60, 10, Seed);
        w.Fill(0, 9, 59, 9, Mat.Stone);
        w.Fill(28, 6, 31, 8, Mat.Water);

        Run(w, 6);

        int minX = CellsOf(w, Mat.Water).Min(c => c.X);
        int maxX = CellsOf(w, Mat.Water).Max(c => c.X);
        Assert.True(maxX - minX > 4 + 2 * 6, $"water spread only to [{minX},{maxX}]");
    }
}
