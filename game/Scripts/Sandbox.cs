using ElementSandbox.Core;
using Godot;

namespace ElementSandbox;

/// <summary>Godot front end: steps the simulation, uploads its pixels and draws them.</summary>
public partial class Sandbox : Node2D
{
    private World world = null!;
    private byte[] pixels = null!;
    private Image image = null!;
    private ImageTexture texture = null!;

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
        world.Step();
        world.Render(pixels);
        image.SetData(world.Width, world.Height, false, Image.Format.Rgba8, pixels);
        texture.Update(image);
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawTextureRect(texture, new Rect2(Vector2.Zero, GetViewportRect().Size), false);
    }
}
