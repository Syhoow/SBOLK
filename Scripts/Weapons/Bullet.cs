using Godot;

public partial class Bullet : Node2D
{
    public virtual float Damage => 50f;
    public virtual float KnockbackForce => 200f;
    public virtual float KnockbackDuration => 0.15f;

    protected float _speed = 300f;
    protected float _lifeSeconds = 2f;
    protected Vector2 _direction = Vector2.Right;
    protected float _gravity;
    protected float _drag;
    protected float _spinDegreesPerSecond;
    protected Vector2 _velocity = Vector2.Right * 300f;
    protected Vector2 _baseVelocity; // Cached base velocity for optimization

    protected Sprite2D _sprite;
    protected AnimatedSprite2D _animatedSprite;

    private void CacheVisualNodes()
    {
        _sprite ??= GetNodeOrNull<Sprite2D>("Sprite2D");
        _animatedSprite ??= GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
    }

    protected virtual void ApplyTypeModifiers()
    {
    }

    protected virtual Vector2 ModifyVelocity(Vector2 velocity, float dt)
    {
        return velocity;
    }


    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;

        _lifeSeconds -= dt;
        if (_lifeSeconds <= 0f)
        {
            QueueFree();
            return;
        }

        // Apply gravity
        _velocity += Vector2.Down * _gravity * dt;
        
        // Apply drag as velocity reduction (simpler and more direct)
        if (_drag > 0f)
        {
            _velocity *= Mathf.Max(0f, 1f - _drag * dt);
        }
        
        _velocity = ModifyVelocity(_velocity, dt);

        GlobalPosition += _velocity * dt;

        if (_velocity.LengthSquared() > 0.001f)
        {
            Rotation = _velocity.Angle();
        }

        if (_spinDegreesPerSecond != 0f)
        {
            Rotate(Mathf.DegToRad(_spinDegreesPerSecond) * dt);
        }
    }

    public override void _Ready()
    {
        CacheVisualNodes();
        Visible = true;
    }

    public void Initialize(
        float speed,
        float lifeSeconds,
        Vector2 direction,
        float scale,
        float gravity,
        float drag,
        float spinDegreesPerSecond,
        Color tint)
    {
        CacheVisualNodes();
        Visible = true;

        _speed = speed;
        _lifeSeconds = lifeSeconds;
        _direction = direction.Normalized();
        _gravity = gravity;
        _drag = drag;
        _spinDegreesPerSecond = spinDegreesPerSecond;
        ApplyTypeModifiers();

        _baseVelocity = _direction * _speed;
        _velocity = _baseVelocity;
        Rotation = _direction.Angle();

        Scale = Vector2.One * scale;

        if (_sprite != null)
        {
            _sprite.Modulate = tint;
            _sprite.Visible = true;
            _sprite.ZIndex = 0;
        }

        if (_animatedSprite != null)
        {
            _animatedSprite.Modulate = tint;
            _animatedSprite.Visible = true;
            _animatedSprite.Play();
            _animatedSprite.SpeedScale = Mathf.Max(0.4f, _speed / 700f);
        }
    }

    private void _on_visible_on_screen_notifier_2d_screen_exited()
    {
        QueueFree();
    }

    public void _on_area_2d_area_entered(Area2D bullet)
    {
        if (bullet.IsInGroup("enemy"))
        {
            Vector2 knockbackDirection = (bullet.GlobalPosition - GlobalPosition).Normalized();
            Enemy enemy = bullet.GetParent<Enemy>();
            enemy.ApplyKnockback(knockbackDirection, KnockbackForce, KnockbackDuration);
            QueueFree();
        }
    }
}
