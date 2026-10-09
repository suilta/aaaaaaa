using System;

namespace ElementSandbox.Core;

/// <summary>
/// The prebuilt starting scene. It is laid out to show chain reactions as soon as something is lit:
/// an oil slick on a pool, a sand pile held up by wood, and lava sealed above a pool by a wooden plug.
/// </summary>
public static class DemoScene
{
    // Layout is authored for 320 x 180 and scaled to the actual world size.
    private const int RefWidth = 320;
    private const int RefHeight = 180;

    public static void Build(World w)
    {
        w.Clear();
        w.SetGravity(1);

        var s = new Scaler(w);

        // Stone floor across the bottom.
        s.Fill(0, 170, 319, 179, Mat.Stone);

        // Left: stone-walled pool of water with a layer of oil floating on top.
        s.Fill(8, 100, 10, 169, Mat.Stone);
        s.Fill(98, 100, 100, 169, Mat.Stone);
        s.Fill(11, 132, 97, 169, Mat.Water);
        s.Fill(11, 120, 97, 131, Mat.Oil);

        // Middle: a wooden box on wooden legs, holding a pile of sand.
        s.Fill(134, 112, 137, 169, Mat.Wood);  // legs
        s.Fill(182, 112, 185, 169, Mat.Wood);
        s.Fill(130, 108, 189, 111, Mat.Wood);  // bottom
        s.Fill(130, 66, 133, 107, Mat.Wood);   // walls
        s.Fill(186, 66, 189, 107, Mat.Wood);
        s.Fill(134, 80, 185, 107, Mat.Sand);
        for (int row = 0; row < 14; row++)     // mound on top of the pile
        {
            s.Fill(136 + row * 2, 79 - row, 183 - row * 2, 79 - row, Mat.Sand);
        }

        // Right: a lava box sealed by a wooden plug, with a pool of water below.
        s.Fill(230, 15, 232, 55, Mat.Stone);
        s.Fill(288, 15, 290, 55, Mat.Stone);
        s.Fill(230, 50, 290, 55, Mat.Stone);
        s.Fill(255, 50, 264, 55, Mat.Wood);    // the plug: thick enough to take several seconds
        s.Fill(233, 33, 287, 49, Mat.Lava);

        s.Fill(222, 118, 224, 169, Mat.Stone);
        s.Fill(296, 118, 298, 169, Mat.Stone);
        s.Fill(225, 132, 295, 169, Mat.Water);
    }

    /// <summary>Maps reference coordinates to the world, keeping 1-cell features at least 1 cell.</summary>
    private readonly struct Scaler
    {
        private readonly World world;

        public Scaler(World world) => this.world = world;

        public void Fill(int x0, int y0, int x1, int y1, Mat m)
        {
            world.Fill(X(x0), Y(y0), Math.Max(X(x0), X(x1 + 1) - 1), Math.Max(Y(y0), Y(y1 + 1) - 1), m);
        }

        private int X(int x) => x * world.Width / RefWidth;

        private int Y(int y) => y * world.Height / RefHeight;
    }
}
