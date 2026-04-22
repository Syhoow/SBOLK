using Godot;
using System;

public partial class Cam : Camera2D
{
    private Vector2 desiredOffset; 
    private float minOffset = -200f; 
    private float maxOffset = 200f;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }
    public override void _Process(double delta) 
    { 
        Input.MouseMode = Input.MouseModeEnum.Confined;

        Node2D player = GetParent().GetNode<Node2D>(".");

        desiredOffset = (GetGlobalMousePosition() - player.GlobalPosition) * 0.5f;

        desiredOffset.X = Mathf.Clamp(desiredOffset.X, minOffset / 4.0f, maxOffset / 4.0f);
        desiredOffset.Y = Mathf.Clamp(desiredOffset.Y, minOffset / 4.0f, maxOffset / 4.0f);

        GlobalPosition = player.GlobalPosition + desiredOffset;
    }
}
