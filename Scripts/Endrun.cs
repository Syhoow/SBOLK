using Godot;
using System;

public partial class Endrun : Control
{
    public static Endrun Instance;
    public override void _Ready()
    {
        Instance = this;
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("ui_accept"))
        {
            GetTree().ReloadCurrentScene();
        }
    }
}
