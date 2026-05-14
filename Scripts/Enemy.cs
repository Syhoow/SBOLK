using System.Collections;
using Godot;

public partial class Enemy : CharacterBody2D
{
    public enum EnemyType { Basic, Dash, Teleport, Gun, Bomb }
    public static Enemy Instance;
    [Export] public EnemyType Type = EnemyType.Basic;
    [Export] public PackedScene DropScene;

    [Export] public PackedScene EnemyBulletScene;
    private float shootCooldown = 5f;
    private float shootInterval = 5f;

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
    
    public float health = 100;
    public float damage = 5;
    private float spawnTimer = 0f;
    private float spawnDuration = 1f;
    private bool isspawning = true;

    public CanvasLayer arenaLayer;

    private int random = (int)GD.RandRange(0, 8);

    public override void _Ready()
    {
        Instance = this;
        animation = GetNode<AnimationPlayer>("AnimationPlayer");
        impactframe = GetNode<AnimationPlayer>("Impact");
        player = GetNode<CharacterBody2D>("/root/Main/ArenaLayer/Player");
        impactframe.Stop();
        impactframe.Seek(0, true);
        animation.Play("Running");
        animation.Seek((float)GD.RandRange(0, animation.CurrentAnimationLength), true);
        sprite = GetNode<Sprite2D>("Sprite2D");
        arenaLayer = GetNode<CanvasLayer>("/root/Main/ArenaLayer");

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

        if(Arena.Instance.ArenaWidth >= 100 && Arena.Instance.ArenaHeight >= 100)
        {
            
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
            if (DropScene != null)
            {
                var drop = DropScene.Instantiate<Drop>();
                drop.GlobalPosition = GlobalPosition;
                GetParent().AddChild(drop);
            }
        }
        
    }

    private void _Movement(float delta)
    {
        switch (Type)
        {
            case EnemyType.Gun:
                
                float dist = Position.DistanceTo(player.Position);
                Vector2 toPlayer = (player.Position - Position).Normalized();
                Vector2 perpendicular = new Vector2(-toPlayer.Y, toPlayer.X); // sideways direction
                CollisionLayer = 3|4;
                CollisionMask = 3|4;
                // Change strafe direction every few seconds
                strafeChangeTimer += delta;
                if (strafeChangeTimer >= strafeChangeCooldown)
                {
                    // Randomly strafe left or right
                    float randomChoice = (float)GD.RandRange(0, 1);
                    strafeDirection = randomChoice > 0.5f ? perpendicular : -perpendicular;

                    // Randomize how long and how soon they change direction
                    strafeChangeCooldown = (float)GD.RandRange(1.0f, 3.0f);
                    strafeChangeTimer = 0f;
                }

                if (dist < pushDistance)
                    Velocity = (-toPlayer + strafeDirection).Normalized() * GetSpeed();
                else if (dist > preferredDistance)
                    Velocity = (toPlayer + strafeDirection).Normalized() * GetSpeed();
                else
                    Velocity = strafeDirection * GetSpeed();
                break;

            case EnemyType.Dash:

                Vector2 chaseDirection = (player.Position - Position).Normalized();
                CollisionLayer = 3|4;
                CollisionMask = 3|4;
                if (isDashing)
                {
                    Velocity += dashDirection * enemyAcceleration * delta * 5f;
                    Velocity = Velocity.LimitLength(GetSpeed() * 10f);
                }
                else if (isPaused || isWindingUp)
                {
                    // Stop moving - just apply friction
                    Velocity -= Velocity * enemyFriction * delta;
                }
                else
                {
                    Velocity += chaseDirection * enemyAcceleration * delta;
                    Velocity = Velocity.LimitLength(GetSpeed());
                    Velocity -= Velocity * enemyFriction * delta;
                }
                break;

            case EnemyType.Bomb:

                var directionbomb = (player.Position - Position).Normalized();
                Velocity = directionbomb * GetSpeed() * 1.2f;
                break;

            default:
                        
                if (isTeleportingWindup)
                {
                    // Stop moving during windup
                    Velocity = Velocity.MoveToward(Vector2.Zero, speed);
                    break;
                }
                var direction = (player.Position - Position).Normalized();
                Velocity = direction * GetSpeed();
                break;
        }
    }

    private void _HandleTypeBehavior(float delta)
    {
        switch (Type)
        {
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

            case EnemyType.Gun:
                shootCooldown -= delta;
                if (shootCooldown <= 0f && player != null)
                {
                    shootCooldown = shootInterval;
                    ShootAtPlayer();
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

    private float GetSpeed()
    {
        if (Arena.Instance == null) return speed;
        float arenaSize = Arena.Instance.ArenaWidth + Arena.Instance.ArenaHeight;
        if (arenaSize >= 50f)
        {
            float bonus = (arenaSize - 50f) * 0.5f; // 0.5 speed per tile over 50
            return speed + bonus;
        }
        return speed;
    }

    private void ShootAtPlayer()
    {
        if (EnemyBulletScene == null) return;

        var bullet = EnemyBulletScene.Instantiate<EnemyBullet>();
        var direction = (player.GlobalPosition - GlobalPosition).Normalized();
        bullet.GlobalPosition = GlobalPosition;
        bullet.Initialize(400f, 3f, direction); // slower than player bullets
        arenaLayer.AddChild(bullet);
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
                player.ApplyKnockback(knockbackDirection, 3000.0f, .15f);
                QueueFree();
            }
            if(Type == EnemyType.Dash)
            {
                player.ApplyKnockback(knockbackDirection, 1000.0f, .15f);
            }
            
        }
        if(area.IsInGroup("bullet") && !area.IsInGroup("enemy_bullet"))
        {
            health -= Bullet.Damage;
            impactframe.Stop();
            impactframe.Seek(0, true);
            impactframe.Play("impact");
        }
    }
}