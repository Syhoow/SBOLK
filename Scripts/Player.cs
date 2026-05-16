using Godot;
using System;

public partial class Player : CharacterBody2D
{
	[Export] public PackedScene EndRunScene;
	public static Player Instance;
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
	private float invincibilityDuration = 0.5f;
	private float invincibilityTimer = 0f;
	private bool isInvincible = false;
	private Vector2 dashDirection;
	private Vector2 knockback = Vector2.Zero;
	private float kbtimer = 0.0f;
	private CollisionShape2D hitbox;
	public CollisionShape2D wallcollision;
	private float maxHealth = 100f;
	private float health = 100f;
	private ProgressBar healthBar;
	private Control endrunUI;
	private AnimationPlayer animPlayer;
	private Sprite2D Sprite;
	

	public override void _Ready()
	{
		Instance = this;
		hitbox = GetNode<CollisionShape2D>("Area2D/CollisionShape2D");
		wallcollision = GetNode<CollisionShape2D>("CollisionShape2D");
		healthBar = GetNode<ProgressBar>("/root/Main/HUD/Health");
		endrunUI = GetNode<Control>("/root/Main/HUD/Control");
		animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		animPlayer.Play("Player");
		healthBar.Value = health;
		Sprite = GetNode<Sprite2D>("Sprite2D");
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

		// dash input always checked, even during knockback
		_CheckDash(dt);

		if (!isDashing)
		{
			bool outsideArena = Arena.Instance != null && Arena.Instance.isOutside;
			if (outsideArena)
				Velocity = Velocity.LimitLength(200f);
			else
				_Friction(dt);
		}

		if (isDashing)
		{
			dashTimer -= dt;
			Velocity = dashDirection * dashspeed;
			if (dashTimer <= 0f) isDashing = false;
		}

		if (isInvincible)
		{
			// Remove the SetDeferred and CallDeferred lines — they don't belong here
			invincibilityTimer -= dt;
			Modulate = (int)(invincibilityTimer * 10) % 2 == 0 
				? new Color(1, 1, 1, 0.3f) 
				: new Color(1, 1, 1, 1f);
			if (invincibilityTimer <= 0f)
			{
				isInvincible = false;
				hitbox.Disabled = false;
				Modulate = new Color(1, 1, 1, 1f);
				CollisionMask |= (1u << 2);
			}
		}

		if (isInvisible)
		{
			dashDisabledTimer -= dt;
			hitbox.Disabled = true;
			if (dashDisabledTimer <= 0f)
			{
				isInvisible = false;
				hitbox.Disabled = false;
				CollisionMask |= (1u << 2);
				CollisionMask |= (1u << 0);
			}
		}

		if(GetGlobalMousePosition().X > GlobalPosition.X)
			Sprite.FlipH = true;
		else
			Sprite.FlipH = false;

		MoveAndSlide();
	}

	private void _CheckDash(float dt)
	{
		bool outsideArena = Arena.Instance != null && Arena.Instance.isOutside;
		if (outsideArena) return;
		if (GameControl.Instance?.currentGoal <= 20) return;
		if (dashCooldownTimer > 0f || isDashing) return;

		if (Input.IsActionJustPressed("dash"))
		{
			// use last movement direction or default right
			var Direction = new Vector2();
			if (Input.IsActionPressed("ui_up")) Direction.Y -= 1;
			if (Input.IsActionPressed("ui_down")) Direction.Y += 1;
			if (Input.IsActionPressed("ui_left")) Direction.X -= 1;
			if (Input.IsActionPressed("ui_right")) Direction.X += 1;

			isDashing = true;
			isInvisible = true;
			dashTimer = dashDuration;
			dashDisabledTimer = dashDisabledDuration;
			dashCooldownTimer = dashCooldown;
			dashDirection = Direction == Vector2.Zero ? Vector2.Right : Direction;
			CollisionMask &= ~(1u << 2);
			CollisionMask &= ~(1u << 0);
			if (GameControl.Instance != null)
				GameControl.Instance.currentGoal -= 20;
			GameControl.Instance.goalBar.Value = GameControl.Instance.currentGoal;

			// cancel knockback when dashing
			kbtimer = 0f;
			knockback = Vector2.Zero;
		}
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
			if(GameControl.Instance?.currentGoal > 20)
			{
				if (Input.IsActionJustPressed("dash") && dashCooldownTimer <= 0f && !isDashing)
				{
					isDashing = true;
					isInvisible = true;
					dashTimer = dashDuration;
					dashDisabledTimer = dashDisabledDuration;
					dashCooldownTimer = dashCooldown;
					dashDirection = Direction == Vector2.Zero ? Vector2.Right : Direction;
					CollisionMask &= ~(1u << 2); // disable only layer 3
					CollisionMask &= ~(1u << 0);
					if (GameControl.Instance != null)
    				GameControl.Instance.currentGoal -= 10;
					GameControl.Instance.goalBar.Value = GameControl.Instance.currentGoal;
				}
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
		if (area.IsInGroup("enemy"))
		{
			if (isInvincible) return;

			health -= Enemy.Instance.damage;
			healthBar.Value = health;

			isInvincible = true;
			hitbox.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
			CallDeferred(nameof(DisableEnemyLayer)); // ← this was missing
			invincibilityTimer = invincibilityDuration;

			if (health <= 0f)
			{
				QueueFree();
				GD.Print("Player has died!");
			}
		}

		if(area.IsInGroup("enemy_bullet"))
		{
			if (isInvincible) return;

			health -= Enemy.Instance.damage;
			healthBar.Value = health;

			isInvincible = true;
			hitbox.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
			CallDeferred(nameof(DisableEnemyLayer)); // ← this was missing
			invincibilityTimer = invincibilityDuration;

			if (health <= 0f)
			{
				QueueFree();
				GD.Print("Player has died!");
			}
		}
		if(area.IsInGroup("portal"))
		{
			GD.Print("Entered Portal");
			foreach (Node enemy in GetTree().GetNodesInGroup("enemy"))
				enemy.QueueFree();
			
			GameControl.Instance.isPlaying = false;
			GameControl.Instance.currentGoal = 0;
			GameControl.Instance.goalBar.Value = 0;
			GameControl.Instance.goalReached = false;
			GameControl.Instance.CallDeferred(nameof(GameControl.FreezeLayers));
			endrunUI.GetNode<Endrun>(".").OpenShop(); // call OpenShop instead
		}
		
	}

	private void DisableEnemyLayer()
	{
		CollisionMask &= ~(1u << 2);
	}

	public void Heal(float amount)
	{
		health = Mathf.Min(health + amount, maxHealth);
		healthBar.Value = health;
	}

	public void IncreaseMaxHealth(float amount)
	{
		maxHealth += amount;
		health = Mathf.Min(health + amount, maxHealth); // also heals a bit
		healthBar.MaxValue = maxHealth;
		healthBar.Value = health;
	}
}