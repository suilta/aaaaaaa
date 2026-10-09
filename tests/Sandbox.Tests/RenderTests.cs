using ElementSandbox.Core;
using Xunit;

namespace ElementSandbox.Tests;

public class RenderTests
{
    [Fact]
    public void EmptyWorldRendersOpaqueBackground()
    {
        var world = new World(8, 4, seed: 1);
        var rgba = new byte[8 * 4 * 4];
        world.Render(rgba);

        var bg = new byte[4];
        Materials.Background.WriteRgba(bg, 0);
        for (int i = 0; i < rgba.Length; i += 4)
        {
            Assert.Equal(bg[0], rgba[i]);
            Assert.Equal(bg[1], rgba[i + 1]);
            Assert.Equal(bg[2], rgba[i + 2]);
            Assert.Equal(255, rgba[i + 3]);
        }
    }

    [Fact]
    public void DefaultWorldIs320By180()
    {
        var world = new World(seed: 1);
        Assert.Equal(320, world.Width);
        Assert.Equal(180, world.Height);
    }

    [Fact]
    public void EveryMaterialHasADefinition()
    {
        foreach (var m in System.Enum.GetValues<Mat>())
        {
            Assert.Equal(m, Materials.Get(m).Id);
        }
    }
}
