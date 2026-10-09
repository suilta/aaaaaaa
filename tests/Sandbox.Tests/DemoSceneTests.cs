using System.Linq;
using ElementSandbox.Core;
using Xunit;
using static ElementSandbox.Tests.TestUtil;

namespace ElementSandbox.Tests;

public class DemoSceneTests
{
    private static World Build()
    {
        var w = new World(Seed);
        DemoScene.Build(w);
        return w;
    }

    [Fact]
    public void ContainsEveryIngredient()
    {
        var w = Build();
        foreach (var m in new[] { Mat.Stone, Mat.Sand, Mat.Water, Mat.Wood, Mat.Oil, Mat.Lava })
        {
            Assert.True(w.Count(m) > 100, $"scene has too little {m}");
        }

        Assert.Equal(1, w.Gravity);
    }

    [Fact]
    public void RebuildingResetsGravityAndContents()
    {
        var w = Build();
        w.FlipGravity();
        w.Paint(160, 20, 10, Mat.Stone);
        Run(w, 10);

        DemoScene.Build(w);
        var fresh = Build();

        Assert.Equal(1, w.Gravity);
        foreach (Mat m in System.Enum.GetValues<Mat>())
        {
            Assert.Equal(fresh.Count(m), w.Count(m));
        }
    }

    [Fact]
    public void UntouchedSceneHoldsItsShapeExceptForTheLava()
    {
        var w = Build();
        int sand = w.Count(Mat.Sand);
        int oil = w.Count(Mat.Oil);

        Run(w, 300);

        // The sand pile only slumps inside its wooden box and the oil stays on the pool.
        Assert.Equal(sand, w.Count(Mat.Sand));
        Assert.Equal(oil, w.Count(Mat.Oil));
        Assert.All(CellsOf(w, Mat.Sand), c => Assert.InRange(c.Y, 60, 107));
        Assert.All(CellsOf(w, Mat.Oil), c => Assert.InRange(c.Y, 110, 131));
    }

    [Fact]
    public void LavaBurnsThroughThePlugAndTurnsToStoneInThePool()
    {
        var w = Build();
        int stone = w.Count(Mat.Stone);

        bool sawSteam = false;
        for (int i = 0; i < 3600; i++)
        {
            w.Step();
            sawSteam |= w.Count(Mat.Steam) > 0;
        }

        Assert.True(sawSteam, "lava hitting the pool should give off steam");
        Assert.True(w.Count(Mat.Stone) > stone + 50, "lava should have turned to stone in the pool");
    }

    [Fact]
    public void BurningTheWoodenFrameCollapsesTheSandPile()
    {
        var w = Build();
        for (int y = 112; y <= 169; y++)
        {
            w.Ignite(135, y);
            w.Ignite(183, y);
        }

        Run(w, 3000);

        // Sand has come down to the floor between the legs.
        Assert.Contains(CellsOf(w, Mat.Sand), c => c.Y == 169 && c.X > 125 && c.X < 195);
    }

    [Fact]
    public void LitOilBurnsOnThePoolSurface()
    {
        var w = Build();
        int water = w.Count(Mat.Water);
        w.Paint(50, 118, 3, Mat.Fire);

        Run(w, 1200);

        // Count only the left pool: the lava pool on the right loses water to steam on its own.
        int leftWater = CellsOf(w, Mat.Water).Count(c => c.X < 110);
        Assert.True(w.Count(Mat.Oil) < 50, $"{w.Count(Mat.Oil)} oil cells left");
        Assert.True(leftWater > 87 * 38 * 9 / 10, $"left pool lost too much water: {leftWater}");
        Assert.True(water > 0);
    }

    [Fact]
    public void BuildsInASmallerWorldWithoutThrowing()
    {
        var w = new World(160, 90, Seed);
        DemoScene.Build(w);
        Run(w, 10);
        Assert.True(w.Count(Mat.Water) > 0);
    }
}
