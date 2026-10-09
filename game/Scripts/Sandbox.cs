using ElementSandbox.Core;
using Godot;

namespace ElementSandbox;

/// <summary>Godot front end: feeds input to the simulation, steps it, uploads its pixels and draws them.</summary>
public partial class Sandbox : Node2D
{
    private const int MinBrush = 1;
    private const int MaxBrush = 20;

    // Number keys 1..N select these.
    private static readonly Mat[] Palette =
    {
        Mat.Stone, Mat.Sand, Mat.Water, Mat.Wood, Mat.Oil, Mat.Fire, Mat.Steam,
    };

    private World world = null!;
    private byte[] pixels = null!;
    private Image image = null!;
    private ImageTexture texture = null!;

    private Mat brush = Mat.Sand;
    private int brushRadius = 4;
    private bool paused;

    // Last cell the mouse painted at, for interpolating fast strokes. Null when not dragging.
    private Vector2I? lastPaintCell;

    public override void _Ready()
    {
        world = new World((int)GD.Randi());
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

        world.Render(pixels);
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
        if (slot >= 0 && slot < Palette.Length)
        {
            brush = Palette[slot];
            return;
        }

        switch (keycode)
        {
            case Key.Space:
                paused = !paused;
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
    }
}
