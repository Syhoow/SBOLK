using Godot;
using System;

public partial class Player : CharacterBody2D
{

	private const float speed = 500;
	private const float dashspeed = 1000;
	private const float acceleration = 5000;
	private const float friction = acceleration/speed;
	private bool isDashing = false;
	private float dashDuration = 0.2f;
	private float dashTimer = 0f;
	private float dashCooldown = 5f;
	private float dashCooldownTimer = 0f;
	private Vector2 dashDirection;
	private Vector2 knockback = Vector2.Zero;
    private float kbtimer = 0.0f;
	

	public override void _Ready()
	{

	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		// tick cooldown
		if (dashCooldownTimer > 0f) dashCooldownTimer -= dt;

		if (kbtimer > 0.0f)
		{
			Velocity = knockback;
			kbtimer -= dt;
			if (kbtimer <= 0.0f) knockback = Vector2.Zero;
		}
		else
		{
			_Movements(dt);
		}

		if (!isDashing)
			_Friction(dt);

		// tick dash duration
		if (isDashing)
		{
			dashTimer -= dt;
			Velocity = dashDirection * dashspeed;
			if (dashTimer <= 0f) isDashing = false;
		}

		MoveAndSlide();
	}

	public void _Movements(float delta)
	{
		var velocity = Velocity;
		Velocity = velocity;

		var Direction = new Vector2();
		Direction = Direction.Normalized();

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
		if (Input.IsActionJustPressed("ui_accept") && dashCooldownTimer <= 0f && !isDashing)
		{
			isDashing = true;
			dashTimer = dashDuration;
			dashCooldownTimer = dashCooldown;
			dashDirection = Direction == Vector2.Zero ? Vector2.Right : Direction;
		}

    	if (!isDashing)
		{
			Velocity += Direction * acceleration * delta;
		}
        

		

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
