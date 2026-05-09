using System.Collections;
using Godot;

public partial class Enemy : CharacterBody2D
{
    public enum EnemyType { Basic, Dash, Teleport, Gun, Bomb }
    private enum GunMovementMode { Strafe, Retreat, Rush, KeepDistance, Reposition }
    private enum GunReactionMode { Pause, Flank, Chase, HoldCover }
    private enum GunAttackPattern { Burst, Charge, Spread, Delayed }

    [Export] public EnemyType Type = EnemyType.Basic;

    protected const float speed = 250;
    protected CharacterBody2D player;
    protected AnimationPlayer animation;
    private AnimationPlayer impactframe;
    private Sprite2D sprite;
    
    private Vector2 knockback = Vector2.Zero;
    private float kbtimer = 0.0f;

    // Dash
    private float dashCooldown = 3.0f;
    private float dashTimer = 0f;
    private float dashDuration = 0.2f;
    private float dashDurationTimer = 0f;
    private float dashPauseDuration = 0.5f;  // pause after dash
    private float dashPauseTimer = 0f;
    private bool isDashing = false;
    private bool isPaused = false;
    private Vector2 dashDirection;

    private float dashWindupDuration = 0.4f;  // pause before dash
    private float dashWindupTimer = 0f;
    private bool isWindingUp = false;

    // Dash physics
    private const float dashSpeed = 6000f;
    private const float enemyAcceleration = 3000f;
    private const float enemyFriction = enemyAcceleration / speed;

    // Teleport
    private float teleportCooldown = 4.0f;
    private float teleportTimer = 0f;
    private float teleportWindupDuration = 0.6f;
    private float teleportWindupTimer = 0f;
    private bool isTeleportingWindup = false;

    // Gun
    private float preferredDistance = 200f;
    private float pushDistance = 150f;
    private float strafeTimer = 0f;
    private float strafeDuration = 1.5f;
    private Vector2 strafeDirection = Vector2.Zero;
    private float strafeChangeCooldown = 2.0f;
    private float strafeChangeTimer = 0f;
    private PackedScene gunBulletScene;
    private GunMovementMode gunMovementMode = GunMovementMode.KeepDistance;
    private GunReactionMode gunReactionMode = GunReactionMode.Chase;
    private GunAttackPattern gunAttackPattern = GunAttackPattern.Burst;
    private float gunAttackCooldown = 0f;
    private float gunAttackWindupTimer = 0f;
    private float gunBurstShotTimer = 0f;
    private int gunBurstShotsRemaining = 0;
    private float gunSpreadShotTimer = 0f;
    private int gunSpreadShotsRemaining = 0;
    private Vector2 gunRepositionTarget = Vector2.Zero;
    private float gunRepositionTimer = 0f;
    private float gunPauseTimer = 0f;
    
    private float health = 100;

    private float spawnTimer = 0f;
    private float spawnDuration = 1f;
    private bool isspawning = true;

    private int random = (int)GD.RandRange(0, 8);

    public override void _Ready()
    {
        animation = GetNode<AnimationPlayer>("AnimationPlayer");
        impactframe = GetNode<AnimationPlayer>("Impact");
        player = GetNode<CharacterBody2D>("/root/Main/ArenaLayer/Player");
        impactframe.Stop();
        impactframe.Seek(0, true);
        animation.Play("Running");
        animation.Seek((float)GD.RandRange(0, animation.CurrentAnimationLength), true);
        sprite = GetNode<Sprite2D>("Sprite2D");

        if (Type == EnemyType.Gun)
        {
            gunBulletScene = GD.Load<PackedScene>("res://Scenes/Bullets/PistolBullet.tscn");
            ConfigureGunBehavior();
        }

        switch (Type)
        {
            case EnemyType.Bomb:
                sprite.Frame = 9;
                CollisionLayer = 3|4;
                CollisionMask = 3|4;
                break;
            default:
                sprite.Frame = random;
                break;
        }

        if(Type == EnemyType.Basic)
        {
            CollisionLayer = 3|4;
            CollisionMask = 3|4;
        }
    }

    public override void _Process(double delta)
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
            spawnTimer += (float)delta;
            if(spawnTimer >= spawnDuration)
            {
                _Movement(dt);
            }
            else
            {
                Velocity -= Velocity * enemyFriction * dt;
            }
            
        }

        MoveAndSlide();
        _HandleTypeBehavior(dt);

        if (Type == EnemyType.Gun)
        {
            if (player.Position.X > Position.X)
                sprite.FlipH = true;
            else
                sprite.FlipH = false;
        }
        else
        {
            if (Velocity.X > 0)
                sprite.FlipH = true;
            else if (Velocity.X < 0)
                sprite.FlipH = false;
        }

        if(health <= 0)
        {
            QueueFree();
        }
        
    }

    private void _Movement(float delta)
    {
        switch (Type)
        {
            case EnemyType.Gun:
                UpdateGunMovement(delta);
                break;

            case EnemyType.Dash:

                Vector2 chaseDirection = (player.Position - Position).Normalized();

                if (isDashing)
                {
                    Velocity += dashDirection * enemyAcceleration * delta * 5f;
                    Velocity = Velocity.LimitLength(dashSpeed);
                }
                else if (isPaused || isWindingUp)
                {
                    // Stop moving - just apply friction
                    Velocity -= Velocity * enemyFriction * delta;
                }
                else
                {
                    Velocity += chaseDirection * enemyAcceleration * delta;
                    Velocity = Velocity.LimitLength(speed);
                    Velocity -= Velocity * enemyFriction * delta;
                }
                break;

            case EnemyType.Bomb:

                var directionbomb = (player.Position - Position).Normalized();
                Velocity = directionbomb * speed * 1.2f;
                break;

            default:
                        
                if (isTeleportingWindup)
                {
                    // Stop moving during windup
                    Velocity = Velocity.MoveToward(Vector2.Zero, speed);
                    break;
                }
                var direction = (player.Position - Position).Normalized();
                Velocity = direction * speed;
                    break;
        }
    }

    private void _HandleTypeBehavior(float delta)
    {
        switch (Type)
        {
            case EnemyType.Gun:
                UpdateGunAttack(delta);
                break;

            case EnemyType.Dash:
                if (isPaused)
                {
                    dashPauseTimer -= delta;
                    if (dashPauseTimer <= 0f)
                    {
                        isPaused = false;
                        dashTimer = 0f;
                    }
                    break;
                }

                if (isDashing)
                {
                    dashDurationTimer -= delta;
                    if (dashDurationTimer <= 0f)
                    {
                        isDashing = false;
                        isPaused = true;
                        dashPauseTimer = dashPauseDuration;
                    }
                    break;
                }

                if (isWindingUp)
                {
                    dashWindupTimer -= delta;
                    if (dashWindupTimer <= 0f)
                    {
                        // Windup done - now actually dash
                        isWindingUp = false;
                        isDashing = true;
                        dashDurationTimer = dashDuration;
                        dashDirection = (player.Position - Position).Normalized();
                    }
                    break;
                }

                dashTimer += delta;
                if (dashTimer >= dashCooldown)
                {
                    isWindingUp = true;
                    dashWindupTimer = dashWindupDuration;
                    dashCooldown = (float)GD.RandRange(2.0, 5.0);
                }
                break;

            case EnemyType.Teleport:

                if (isTeleportingWindup)
                {
                    teleportWindupTimer -= delta;
                    if (teleportWindupTimer <= 0f)
                    {
                        // Windup done - teleport
                        isTeleportingWindup = false;
                        Vector2 behindPlayer = player.Position + (player.Position - Position).Normalized() * 400f;
                        GlobalPosition = behindPlayer;
                        teleportTimer = 0f;
                    }
                    break;
                }

                teleportTimer += delta;
                if (teleportTimer >= teleportCooldown)
                {
                    // Start windup instead of teleporting immediately
                    isTeleportingWindup = true;
                    teleportWindupTimer = teleportWindupDuration;
                }
                break;
        }
    }

    public void ApplyKnockback(Vector2 direction, float force, float duration)
    {
        knockback = direction * force;
        kbtimer = duration;
    }

    private void ConfigureGunBehavior()
    {
        gunMovementMode = (GunMovementMode)Mathf.FloorToInt(GD.Randf() * 5f);
        gunReactionMode = (GunReactionMode)Mathf.FloorToInt(GD.Randf() * 4f);
        gunAttackPattern = (GunAttackPattern)Mathf.FloorToInt(GD.Randf() * 4f);

        preferredDistance = (float)GD.RandRange(190f, 340f);
        pushDistance = preferredDistance * 0.72f;
        strafeDuration = (float)GD.RandRange(0.8f, 2.0f);
        strafeChangeCooldown = strafeDuration;
        strafeDirection = Vector2.Zero;
        gunRepositionTarget = GlobalPosition;
        gunRepositionTimer = (float)GD.RandRange(0.8f, 2.2f);
    }

    private void UpdateGunMovement(float delta)
    {
        if (player == null)
        {
            return;
        }

        float dist = GlobalPosition.DistanceTo(player.GlobalPosition);
        Vector2 toPlayer = (player.GlobalPosition - GlobalPosition).Normalized();
        Vector2 perpendicular = new Vector2(-toPlayer.Y, toPlayer.X);

        strafeTimer += delta;
        if (strafeDirection == Vector2.Zero || strafeTimer >= strafeChangeCooldown)
        {
            strafeDirection = GD.Randf() < 0.5f ? perpendicular : -perpendicular;
            strafeTimer = 0f;
            strafeChangeCooldown = (float)GD.RandRange(0.75f, 2.2f);
        }

        gunRepositionTimer -= delta;
        if (gunMovementMode == GunMovementMode.Reposition &&
            (gunRepositionTimer <= 0f || GlobalPosition.DistanceTo(gunRepositionTarget) < 24f))
        {
            var side = GD.Randf() < 0.5f ? perpendicular : -perpendicular;
            var backward = -toPlayer;
            gunRepositionTarget = player.GlobalPosition + side * (float)GD.RandRange(160f, 260f) + backward * (float)GD.RandRange(40f, 120f);
            gunRepositionTimer = (float)GD.RandRange(1.1f, 2.6f);
        }

        Vector2 desired = Vector2.Zero;
        switch (gunMovementMode)
        {
            case GunMovementMode.Strafe:
                desired = strafeDirection;
                if (dist < pushDistance)
                {
                    desired += -toPlayer * 0.8f;
                }
                else if (dist > preferredDistance)
                {
                    desired += toPlayer * 0.35f;
                }
                break;

            case GunMovementMode.Retreat:
                desired = -toPlayer * 1.2f + strafeDirection * 0.6f;
                if (dist > preferredDistance)
                {
                    desired += toPlayer * 0.25f;
                }
                break;

            case GunMovementMode.Rush:
                desired = toPlayer * 1.35f + strafeDirection * 0.25f;
                if (dist < pushDistance)
                {
                    desired *= 0.55f;
                }
                break;

            case GunMovementMode.KeepDistance:
                if (dist < pushDistance)
                {
                    desired = -toPlayer * 1.1f + strafeDirection * 0.3f;
                }
                else if (dist > preferredDistance)
                {
                    desired = toPlayer * 0.9f + strafeDirection * 0.35f;
                }
                else
                {
                    desired = strafeDirection;
                }
                break;

            case GunMovementMode.Reposition:
                desired = (gunRepositionTarget - GlobalPosition).Normalized();
                if (dist < pushDistance)
                {
                    desired += -toPlayer * 0.45f;
                }
                break;
        }

        switch (gunReactionMode)
        {
            case GunReactionMode.Pause:
                if (gunAttackWindupTimer > 0f || gunPauseTimer > 0f)
                {
                    desired = Vector2.Zero;
                }
                break;

            case GunReactionMode.Flank:
                desired += perpendicular * 0.7f;
                desired += toPlayer * 0.15f;
                break;

            case GunReactionMode.Chase:
                desired += toPlayer * 0.45f;
                break;

            case GunReactionMode.HoldCover:
                if (dist <= preferredDistance)
                {
                    desired *= 0.2f;
                    if (gunAttackCooldown > 0.2f)
                    {
                        desired = Vector2.Zero;
                    }
                }
                break;
        }

        if (gunAttackWindupTimer > 0f)
        {
            desired = Vector2.Zero;
        }

        if (desired.LengthSquared() > 0.001f)
        {
            Velocity = desired.Normalized() * speed;
        }
        else
        {
            Velocity -= Velocity * enemyFriction * delta;
        }
    }

    private void UpdateGunAttack(float delta)
    {
        if (player == null || gunBulletScene == null)
        {
            return;
        }

        if (gunPauseTimer > 0f)
        {
            gunPauseTimer -= delta;
        }

        if (gunAttackCooldown > 0f)
        {
            gunAttackCooldown -= delta;
        }

        if (gunBurstShotsRemaining > 0)
        {
            gunBurstShotTimer -= delta;
            if (gunBurstShotTimer <= 0f)
            {
                FireGunShot(0f, 1.0f, 14f, 0f, 0.55f);
                gunBurstShotsRemaining--;
                gunBurstShotTimer = gunBurstShotsRemaining > 0 ? 0.14f : 0f;
                gunPauseTimer = 0.08f;
            }

            return;
        }

        if (gunSpreadShotsRemaining > 0)
        {
            gunSpreadShotTimer -= delta;
            if (gunSpreadShotTimer <= 0f)
            {
                FireGunShot((gunSpreadShotsRemaining - 2) * 0.13f, 1.0f, 11f, 0f, 0.4f);
                gunSpreadShotsRemaining--;
                gunSpreadShotTimer = gunSpreadShotsRemaining > 0 ? 0.05f : 0f;
                gunPauseTimer = 0.05f;
            }

            return;
        }

        if (gunAttackWindupTimer > 0f)
        {
            gunAttackWindupTimer -= delta;
            if (gunAttackWindupTimer > 0f)
            {
                return;
            }

            switch (gunAttackPattern)
            {
                case GunAttackPattern.Charge:
                    FireGunShot(0f, 1.35f, 34f, 0f, 0.25f);
                    gunPauseTimer = 0.22f;
                    break;

                case GunAttackPattern.Delayed:
                    FireGunShot(0f, 1.0f, 20f, 0f, 0.35f);
                    gunPauseTimer = 0.18f;
                    break;
            }

            return;
        }

        if (gunAttackCooldown > 0f)
        {
            return;
        }

        float distance = GlobalPosition.DistanceTo(player.GlobalPosition);
        if (distance > preferredDistance * 2.2f)
        {
            return;
        }

        switch (gunAttackPattern)
        {
            case GunAttackPattern.Burst:
                gunBurstShotsRemaining = 3;
                gunBurstShotTimer = 0f;
                FireGunShot(0f, 0.95f, 12f, 0f, 0.5f);
                gunBurstShotsRemaining--;
                gunBurstShotTimer = 0.13f;
                gunAttackCooldown = 1.2f;
                gunPauseTimer = 0.06f;
                break;

            case GunAttackPattern.Charge:
                gunAttackWindupTimer = 0.65f;
                gunAttackCooldown = 1.8f;
                gunPauseTimer = 0.1f;
                break;

            case GunAttackPattern.Spread:
                gunSpreadShotsRemaining = 5;
                gunSpreadShotTimer = 0f;
                FireGunShot(-0.26f, 1.0f, 10f, 0f, 0.35f);
                gunSpreadShotsRemaining--;
                gunSpreadShotTimer = 0.05f;
                gunAttackCooldown = 1.6f;
                gunPauseTimer = 0.05f;
                break;

            case GunAttackPattern.Delayed:
                gunAttackWindupTimer = 0.9f;
                gunAttackCooldown = 1.5f;
                gunPauseTimer = 0.2f;
                break;
        }
    }

    private void FireGunShot(float spreadOffset, float speedMultiplier, float damage, float lifetimeBonus, float scaleMultiplier)
    {
        if (gunBulletScene == null || player == null)
        {
            return;
        }

        var bullet = gunBulletScene.Instantiate<Bullet>();
        var arenaLayer = GetTree().CurrentScene.GetNode<Node>("ArenaLayer");
        arenaLayer.AddChild(bullet);

        bullet.GlobalPosition = GlobalPosition;
        var aimDirection = (player.GlobalPosition - GlobalPosition).Normalized().Rotated(spreadOffset);
        bullet.SetHostile();
        bullet.Initialize(
            speed: 700f * speedMultiplier,
            lifeSeconds: 1.0f + lifetimeBonus,
            direction: aimDirection,
            scale: 0.75f * scaleMultiplier,
            gravity: 0f,
            drag: 0.03f,
            spinDegreesPerSecond: 0f,
            tint: new Color(1f, 0.55f, 0.55f, 1f),
            damage: damage);
    }

    private void _on_area_2d_area_entered(Area2D area)
    {
        if (area.IsInGroup("player"))
        {
            Vector2 knockbackDirection = (area.GlobalPosition - GlobalPosition).Normalized();
            Player player = area.GetParent<Player>();
            player.ApplyKnockback(knockbackDirection, 500.0f, .15f);

            if(Type == EnemyType.Bomb)
            {
                player.ApplyKnockback(knockbackDirection, 1000.0f, .15f);
                QueueFree();
            }
            
        }
        if(area.IsInGroup("bullet"))
        {
            var bulletNode = area.GetParent() as Bullet;
            if (bulletNode?.IsHostile == true)
            {
                return;
            }

            health -= bulletNode?.Damage ?? 50f;
            impactframe.Stop();
            impactframe.Seek(0, true);
            impactframe.Play("impact");
            GD.Print("enemy got damaged"+health);

            if (bulletNode != null && !bulletNode.ConsumePenetration())
            {
                bulletNode.QueueFree();
            }
        }
        if (area.IsInGroup("melee_hit"))
        {
            var swing = area.GetParent() as MeleeSwing;
            Vector2 knockbackDirection = (area.GlobalPosition - GlobalPosition).Normalized();
            ApplyKnockback(knockbackDirection, swing?.KnockbackForce ?? 420f, swing?.KnockbackDuration ?? 0.22f);
            health -= swing?.Damage ?? 90f;
            impactframe.Stop();
            impactframe.Seek(0, true);
            impactframe.Play("impact");
            GD.Print("enemy got melee damaged" + health);
        }
    }
}