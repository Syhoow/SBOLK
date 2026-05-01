using Godot;
using System;

public partial class Player : CharacterBody2D
{

	private const float speed = 500;
	private const float acceleration = 5000;
	private const float friction = acceleration/speed;
	private Vector2 knockback = Vector2.Zero;
    private float kbtimer = 0.0f;
	

	public override void _Ready()
	{

	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (kbtimer > 0.0f)
        {
            Velocity = knockback;
            kbtimer -= dt;
            if (kbtimer <= 0.0f) knockback = Vector2.Zero;
        }
        else
        {
            _Movements((float)delta);
        }
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

	public void ApplyKnockback(Vector2 direction, float force, float duration)
    {
        knockback = direction * force;
        kbtimer = duration;
    }

	public void _Friction(float delta)
	{
		Velocity -= Velocity * friction * delta;
	}
}
