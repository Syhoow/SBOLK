using Godot;
using System;

public partial class Player : CharacterBody2D
{
	private const float speed = 500;
	private const float dashspeed = 1000;
	private const float acceleration = 5000;
	private const float friction = acceleration/speed;
	private bool isDashing = false;
	public bool isInvisible = false;
	private float dashDisabledDuration = 0.5f;
	private float dashDisabledTimer = 0f;
	private float dashDuration = 0.2f;
	private float dashTimer = 0f;
	private float dashCooldown = 0.5f;
	private float dashCooldownTimer = 0f;
	private Vector2 dashDirection;
	private Vector2 knockback = Vector2.Zero;
	private float kbtimer = 0.0f;
	private CollisionShape2D hitbox;
	public CollisionShape2D wallcollision;
	private float health = 100f;
	private ProgressBar healthBar;
	private Arena arena;

	public override void _Ready()
	{
		hitbox = GetNode<CollisionShape2D>("Area2D/CollisionShape2D");
		wallcollision = GetNode<CollisionShape2D>("CollisionShape2D");
		healthBar = GetNode<ProgressBar>("/root/Main/HUD/Health");
		arena = GetNode<Arena>("/root/Main/ArenaLayer/Arena");
		healthBar.Value = health;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

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
		{
			bool outsideArena = Arena.Instance != null && Arena.Instance.isOutside;
			if (outsideArena)
				Velocity = Velocity.LimitLength(200f); // slow but no friction
			else
				_Friction(dt);
		}

		if (isDashing)
		{
			dashTimer -= dt;
			Velocity = dashDirection * dashspeed;
			if (dashTimer <= 0f) isDashing = false;
		}

		if (isInvisible)
		{
			dashDisabledTimer -= dt;
			hitbox.Disabled = true;
			if (dashDisabledTimer <= 0f)
			{
				isInvisible = false;
				hitbox.Disabled = false;
				CollisionMask |= (1u << 2); // re-enable layer 3
			}
		}

		MoveAndSlide();
	}

	public void _Movements(float delta)
	{
		bool outsideArena = Arena.Instance != null && Arena.Instance.isOutside;

		var Direction = new Vector2();

		if (!outsideArena)
		{
			if(Input.IsActionPressed("ui_up")) Direction.Y -= 1;
			if(Input.IsActionPressed("ui_down")) Direction.Y += 1;
			if(Input.IsActionPressed("ui_left")) Direction.X -= 1;
			if(Input.IsActionPressed("ui_right")) Direction.X += 1;
			if (Input.IsActionJustPressed("dash") && dashCooldownTimer <= 0f && !isDashing)
			{
				isDashing = true;
				isInvisible = true;
				dashTimer = dashDuration;
				dashDisabledTimer = dashDisabledDuration;
				dashCooldownTimer = dashCooldown;
				dashDirection = Direction == Vector2.Zero ? Vector2.Right : Direction;
				CollisionMask &= ~(1u << 2); // disable only layer 3
			}
		}

		if (!isDashing)
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

	private void _on_area_2d_area_entered(Area2D area)
	{
		if(area.IsInGroup("enemy"))
		{
			health -= 5f;
			healthBar.Value = health;
			if (health <= 0f)
			{
				QueueFree();
				GD.Print("Player has died!");
			}
		}
		if(area.IsInGroup("portal"))
		{
			GD.Print("Entered Portal");
		}
	}
}