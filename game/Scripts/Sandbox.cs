using ElementSandbox.Core;
using Godot;

namespace ElementSandbox;

/// <summary>Godot front end: feeds input to the simulation, steps it, uploads its pixels and draws them.</summary>
public partial class Sandbox : Node2D
{
    private const int MinBrush = 1;
    private const int MaxBrush = 20;
    private const int HudFontSize = 15;
    private const int HudOutline = 4;
    private const string HudHelpMouse = "左键放置  右键擦除  滚轮调大小";
    private const string HudHelpKeys = "1石 2沙 3水 4木 5油 6火 7熔岩 8蒸汽 9冰   G翻转重力  空格暂停  T温度视图  R重置  C清空";

    // Number keys 1..N select these.
    private static readonly Mat[] Palette =
    {
        Mat.Stone, Mat.Sand, Mat.Water, Mat.Wood, Mat.Oil, Mat.Fire, Mat.Lava, Mat.Steam, Mat.Ice,
    };

    private World world = null!;
    private byte[] pixels = null!;
    private Image image = null!;
    private ImageTexture texture = null!;
    private Font hudFont = null!;

    private Mat brush = Mat.Sand;
    private int brushRadius = 4;
    private bool paused;
    private RenderMode renderMode = RenderMode.Normal;

    // Last cell the mouse painted at, for interpolating fast strokes. Null when not dragging.
    private Vector2I? lastPaintCell;

    public override void _Ready()
    {
        world = new World((int)GD.Randi());
        DemoScene.Build(world);
        hudFont = new SystemFont { FontNames = new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC" } };

        pixels = new byte[world.Width * world.Height * 4];
        world.Render(pixels);
        image = Image.CreateFromData(world.Width, world.Height, false, Image.Format.Rgba8, pixels);
        texture = ImageTexture.CreateFromImage(image);
    }

    public override void _PhysicsProcess(double delta)
    {
        HandleBrush();

        if (!paused)
        {
            world.Step();
        }

        world.Render(pixels, renderMode);
        image.SetData(world.Width, world.Height, false, Image.Format.Rgba8, pixels);
        texture.Update(image);
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp)
            {
                brushRadius = Mathf.Min(MaxBrush, brushRadius + 1);
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown)
            {
                brushRadius = Mathf.Max(MinBrush, brushRadius - 1);
            }
        }
        else if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            HandleKey(key.Keycode);
        }
    }

    private void HandleKey(Key keycode)
    {
        int slot = (int)(keycode - Key.Key1);
        if (slot < 0 || slot >= Palette.Length)
        {
            slot = (int)(keycode - Key.Kp1);
        }

        if (slot >= 0 && slot < Palette.Length)
        {
            brush = Palette[slot];
            return;
        }

        switch (keycode)
        {
            case Key.G:
                world.FlipGravity();
                break;
            case Key.Space:
                paused = !paused;
                break;
            case Key.T:
                renderMode = renderMode == RenderMode.Normal ? RenderMode.Thermal : RenderMode.Normal;
                break;
            case Key.R:
                DemoScene.Build(world);
                break;
            case Key.C:
                world.Clear();
                break;
        }
    }

    private void HandleBrush()
    {
        bool paint = Input.IsMouseButtonPressed(MouseButton.Left);
        bool erase = Input.IsMouseButtonPressed(MouseButton.Right);
        if (!paint && !erase)
        {
            lastPaintCell = null;
            return;
        }

        Vector2I cell = MouseCell();
        Vector2I from = lastPaintCell ?? cell;
        world.PaintLine(from.X, from.Y, cell.X, cell.Y, brushRadius, erase ? Mat.Empty : brush);
        lastPaintCell = cell;
    }

    private Vector2 CellSize => GetViewportRect().Size / new Vector2(world.Width, world.Height);

    private Vector2I MouseCell()
    {
        Vector2 p = GetLocalMousePosition() / CellSize;
        return new Vector2I(Mathf.FloorToInt(p.X), Mathf.FloorToInt(p.Y));
    }

    public override void _Draw()
    {
        DrawTextureRect(texture, new Rect2(Vector2.Zero, GetViewportRect().Size), false);

        // Brush outline.
        Vector2 cellSize = CellSize;
        Vector2 center = ((Vector2)MouseCell() + new Vector2(0.5f, 0.5f)) * cellSize;
        DrawArc(center, (brushRadius + 0.5f) * cellSize.X, 0f, Mathf.Tau, 48, new Color(1f, 1f, 1f, 0.35f), 1f);

        DrawHud();
    }

    private void DrawHud()
    {
        string gravity = world.Gravity > 0 ? "向下" : "向上";
        string status = $"画笔：{Materials.Get(brush).Name}   大小：{brushRadius}   重力：{gravity}";
        if (paused)
        {
            status += "   [暂停]";
        }

        if (renderMode == RenderMode.Thermal)
        {
            status += "   [温度视图]";
        }

        DrawHudLine(0, status);
        DrawHudLine(1, HudHelpMouse);
        DrawHudLine(2, HudHelpKeys);
    }

    private void DrawHudLine(int line, string text)
    {
        var pos = new Vector2(10f, 22f + line * 22f);
        DrawStringOutline(hudFont, pos, text, HorizontalAlignment.Left, -1f, HudFontSize, HudOutline, Colors.Black);
        DrawString(hudFont, pos, text, HorizontalAlignment.Left, -1f, HudFontSize, Colors.White);
    }
}
