using Godot;
using System;

public partial class Titlescreen : Node2D
{
    public void _on_button_pressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/main.tscn");
    }
}
