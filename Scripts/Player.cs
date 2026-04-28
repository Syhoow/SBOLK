using Godot;
using System;

public partial class Player : CharacterBody2D
{

	private const float speed = 500;
	private const float acceleration = 5000;
	private const float friction = acceleration/speed;
	

	public override void _Ready()
	{
		var hitbox = GetNode<Hitbox>("Area2D");
		hitbox.Hit += OnHit;
	}

	private void OnHit(Node2D body)
	{
		GD.Print("player hitbox worked");
	}

	public override void _PhysicsProcess(double delta)
	{
		_Movements((float)delta);
		_Friction((float)delta); 
		MoveAndSlide();
	}

	public void _Movements(float delta)
	{
		var velocity = Velocity;
		Velocity = velocity;

		var Direction = new Vector2();

		if(Input.IsActionPressed("ui_up"))
		{
			Direction.Y -= 1;
		}
		if(Input.IsActionPressed("ui_down"))
		{
			Direction.Y += 1;
		}
		if(Input.IsActionPressed("ui_left"))
		{
			Direction.X -= 1;
		}
		if(Input.IsActionPressed("ui_right"))
		{
			Direction.X += 1;
		}

		Direction = Direction.Normalized();
		Velocity += Direction * acceleration * delta;

	}

	public void _Friction(float delta)
	{
		Velocity -= Velocity * friction * delta;
	}
}
