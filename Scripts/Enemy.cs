using Godot;

public partial class Enemy : CharacterBody2D
{
    public enum EnemyType { Basic, Dash, Teleport, Gun }

    [Export] public EnemyType Type = EnemyType.Basic;

    protected const float speed = 200;
    protected CharacterBody2D player;
    protected AnimationPlayer animation;
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
    private float preferredDistance = 300f;
    private float pushDistance = 150f;
    private float strafeTimer = 0f;
    private float strafeDuration = 1.5f;
    private Vector2 strafeDirection = Vector2.Zero;
    private float strafeChangeCooldown = 2.0f;
    private float strafeChangeTimer = 0f;
    

    private int random = (int)GD.RandRange(0, 9);

    public override void _Ready()
    {
        animation = GetNode<AnimationPlayer>("AnimationPlayer");
        player = GetNode<CharacterBody2D>("/root/Main/Player");
        animation.Play("Running");
        animation.Seek((float)GD.RandRange(0, animation.CurrentAnimationLength), true);
        sprite = GetNode<Sprite2D>("Sprite2D");
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
            _Movement(dt);
        }

        MoveAndSlide();
        _HandleTypeBehavior(dt);

        if(Type != EnemyType.Gun)
        {
            if (Velocity.X > 0)
            {
                sprite.FlipH = true;
            }
            else if (Velocity.X < 0)
            {
                sprite.FlipH = false;
            }
        }
        
    }

    private void _Movement(float delta)
    {
        switch (Type)
        {
            case EnemyType.Gun:
                
                sprite.Frame = random;
                animation.Stop();
                float dist = Position.DistanceTo(player.Position);
                Vector2 toPlayer = (player.Position - Position).Normalized();
                Vector2 perpendicular = new Vector2(-toPlayer.Y, toPlayer.X); // sideways direction

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
                {
                    // Too close - back away AND strafe
                    Velocity = (-toPlayer + strafeDirection).Normalized() * speed;
                }
                else if (dist > preferredDistance)
                {
                    // Too far - move closer AND strafe
                    Velocity = (toPlayer + strafeDirection).Normalized() * speed;
                }
                else
                {
                    // In range - just strafe sideways
                    Velocity = strafeDirection * speed;
                }
                break;

            case EnemyType.Dash:

                sprite.Frame = random;

                Vector2 chaseDirection = (player.Position - Position).Normalized();

                if (isDashing)
                {
                    Velocity += dashDirection * enemyAcceleration * delta * 6f;
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
                default:
                    if (Type == EnemyType.Basic)
                    {
                        sprite.Frame = random;
                    }
                        
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

                sprite.Frame = random;

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

    private void _on_area_2d_area_entered(Area2D area)
    {
        if (area.IsInGroup("player"))
        {
            Vector2 knockbackDirection = (area.GlobalPosition - GlobalPosition).Normalized();
            Player player = area.GetParent<Player>();
            player.ApplyKnockback(knockbackDirection, 500.0f, .15f);
            
        }
        if(area.IsInGroup("bullet"))
        {
            GD.Print("enemy got damaged");
        }
    }
}