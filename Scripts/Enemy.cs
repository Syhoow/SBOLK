using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
    private float speed = 200;
    CharacterBody2D player;
    public override void _Ready()
    {
        player =  GetNode<CharacterBody2D>("/root/Main/Player");
        var hitbox = GetNode<Hitbox>("Area2D");
        hitbox.Hit += OnHit;
    }

    private void OnHit(Node2D body)
    {
        GD.Print("enemy hitbox worked");
    }

    public override void _Process(double delta)
    {
        _Movement((float)delta);
        MoveAndSlide();
    }

    public void _Movement(float delta)
    {
        
        var Direction = (player.Position - Position).Normalized();
        Velocity = Direction * speed;
        
    }
}
