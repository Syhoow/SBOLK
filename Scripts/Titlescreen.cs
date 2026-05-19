using Godot;
using System;

public partial class Titlescreen : Node2D
{
    public void _on_button_pressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/main.tscn");
    }

    public void _on_button_6_pressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/shop.tscn");
    }

    public void _on_button_7_pressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/inventory.tscn");
    }
}
