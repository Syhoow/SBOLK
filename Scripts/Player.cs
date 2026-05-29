using Godot;
using System;

public partial class Player : CharacterBody2D
{
        [Export] public PackedScene EndRunScene;
        public Node2D Gun;
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
        public float maxHealth = 100f;
        public float health = 100f;
        public float GetHealth() => health;
        private ProgressBar healthBar;
        private Control endrunUI;
        public AnimationPlayer animPlayer;
        public AnimationPlayer hitPlayer;
        public Sprite2D Sprite;
        private Sprite2D Spritedeath;
        private Sprite2D Shadow;
        private Sprite2D HatSprite;
        private Camera2D cam;
        public ColorRect damageOverlay;
        

        public override void _Ready()
        {
                Instance = this;
                hitbox = GetNode<CollisionShape2D>("Area2D/CollisionShape2D");
                wallcollision = GetNode<CollisionShape2D>("CollisionShape2D");
                healthBar = GetNode<ProgressBar>("/root/Main/HUD/Health");
                endrunUI = GetNode<Control>("/root/Main/HUD/Control");
                animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
                hitPlayer = GetNode<AnimationPlayer>("AnimationPlayer2");
                animPlayer.Play("Player");
                healthBar.Value = health;
                Sprite = GetNode<Sprite2D>("Sprite2D");
                Spritedeath = GetNode<Sprite2D>("Sprite2D2");
                Shadow = GetNode<Sprite2D>("Shadow");
                HatSprite = GetNode<Sprite2D>("HatSprite2D");
                cam = GetNode<Camera2D>("Camera2D");
                Gun = GetNode<Node2D>("Gun");
                damageOverlay = GetNode<ColorRect>("ColorRect");

                UpdateHatSprite();
                if (CosmeticManager.Instance != null)
                        CosmeticManager.Instance.DataChanged += UpdateHatSprite;
                TreeExiting += OnPlayerExiting;
        }

        private void OnPlayerExiting()
        {
                if (CosmeticManager.Instance != null)
                        CosmeticManager.Instance.DataChanged -= UpdateHatSprite;
        }

        private void UpdateHatSprite()
        {
                if (HatSprite == null) return;
                var equipped = CosmeticManager.Instance?.EquippedHat ?? "";
                if (string.IsNullOrEmpty(equipped))
                {
                        HatSprite.Visible = false;
                }
                else
                {
                        HatSprite.Texture = CosmeticManager.LoadItemIcon(equipped);
                        HatSprite.Visible = true;
                }
        }

        public override void _PhysicsProcess(double delta)
        {
                float dt = (float)delta;

                healthBar.Value = health;

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
                                damageOverlay.Visible = true;

                        if (invincibilityTimer <= 0f)
                        {
                                damageOverlay.Visible = false;
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

                if (health <= 0f)
                {
                        foreach (Node enemy in GetTree().GetNodesInGroup("enemy"))
                                enemy.QueueFree();
                        Arena.Instance.isEliminated = true;
                        Sprite.Visible = false;
                        Gun.Visible = false;
                        Shadow.Visible = false;
                        Spritedeath.Visible = true;
                        damageOverlay.Visible = true;
                        GameControl.Instance.hudLayer.Visible = false;
                        var tween = CreateTween();
                        GameControl.Instance.gameover.Visible = true;
                        tween.TweenProperty(GameControl.Instance.gameover, "modulate:a", 0.95f, 2f);
                        GameControl.Instance.record.Visible = true;
                        GameControl.Instance.ScoreLabel.Text = $"Score: {GameControl.Instance.score}";
                        GameControl.Instance.ScoreLabel.Visible = true;
                        int totalTokens = CosmeticManager.Instance?.TradeTokens ?? 0;
                        if (GameControl.Instance.TokensLabel != null)
                        {
                                GameControl.Instance.TokensLabel.Text = $"Trade Tokens: {totalTokens}";
                                GameControl.Instance.TokensLabel.Visible = true;
                        }
                        GameControl.Instance.SubmitScoreToLeaderboard(GameControl.Instance.score);
                        GD.Print("Player has died!");
                }

                if(!Arena.Instance.isEliminated)
                {
                        if(GetGlobalMousePosition().X > GlobalPosition.X)
                        {
                                Sprite.FlipH = true;
                                Spritedeath.FlipH = true;
                        }
                        else
                        {
                                Sprite.FlipH = false;
                                Spritedeath.FlipH = false;
                        }
                }

                if (HatSprite != null && HatSprite.Visible)
                {
                        HatSprite.Position = Sprite.Position + new Vector2(0, -18f);
                        HatSprite.FlipH = Sprite.FlipH;
                }
                
                MoveAndSlide();
        }

        private void _CheckDash(float dt)
        {
                bool outsideArena = Arena.Instance != null && Arena.Instance.isOutside;
                if (outsideArena) return;
                if (GameControl.Instance?.currentGoal <= 20) return;
                if (dashCooldownTimer > 0f || isDashing) return;

                if(!Arena.Instance.isEliminated)
                {       
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
                
        }

        public void _Movements(float delta)
        {
                bool outsideArena = Arena.Instance != null && Arena.Instance.isOutside;

                var Direction = new Vector2();
                if(!Arena.Instance.isEliminated)
                {
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
                        hitPlayer.Play("hit");
                        Cam.Instance?.ScreenShake(10, 0.2f);
                        isInvincible = true;
                        hitbox.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
                        CallDeferred(nameof(DisableEnemyLayer)); // ← this was missing
                        invincibilityTimer = invincibilityDuration;
                }

                if(area.IsInGroup("enemy_bullet"))
                {
                        if (isInvincible) return;

                        health -= Enemy.Instance.damage;
                        healthBar.Value = health;
                        hitPlayer.Play("hit");
                        Cam.Instance?.ScreenShake(10, 0.2f);
                        isInvincible = true;
                        hitbox.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
                        CallDeferred(nameof(DisableEnemyLayer)); // ← this was missing
                        invincibilityTimer = invincibilityDuration;
                }
                if(area.IsInGroup("portal"))
                {
                        GD.Print("Entered Portal");
                        foreach (Node enemy in GetTree().GetNodesInGroup("enemy"))
                                enemy.QueueFree();
                        if (Enemy.Instance != null)
                        {
                                Enemy.Instance.health *= (int)(Enemy.Instance.health * 1.25f);
                                Enemy.Instance.damage *= (int)(Enemy.Instance.damage * 1.5f);
                        }
                        GameControl.Instance.isPlaying = false;
                        GameControl.Instance.currentGoal = 0;
                        GameControl.Instance.goalBar.Value = 0;
                        GameControl.Instance.goalReached = false;
                        GameControl.Instance.DifficultyMultiplier *= 1.5f;
                GameControl.Instance.EnemyMultiplier *= 1.25f;
                        GameControl.Instance.goal = GameControl.Instance.WaveGoal;
                GameControl.Instance.goalBar.MaxValue = GameControl.Instance.goal;
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
                healthBar.MaxValue = maxHealth;
                healthBar.Value = health;
        }
}