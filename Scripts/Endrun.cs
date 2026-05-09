using Godot;
using System;

public partial class Endrun : Control
{
    public static Endrun Instance;
    public override void _Ready()
    {
        Instance = this;
    }

    public void ShowEndScreen()
    {
        Visible = true;
    }
}
