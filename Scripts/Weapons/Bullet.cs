using System;
using Godot;

public partial class Bullet : Node2D
{
    private float _speed = 300f;
    private float _lifeSeconds = 2f;
    public static int Damage = 50;
    private Vector2 _direction = Vector2.Right;
    private Sprite2D spritecolor;
    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    public override void _Ready()
    {
        spritecolor = GetNode<Sprite2D>("Sprite2D");
        _rng.Randomize();
        spritecolor.Frame = _rng.RandiRange(0, 4);
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

        Position += _direction * _speed * dt;
    }

    public void Initialize(float speed, float lifeSeconds, Vector2 direction)
    {
        _speed = speed;
        _lifeSeconds = lifeSeconds;
        _direction = direction.Normalized();
        Rotation = _direction.Angle();
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
            enemy.ApplyKnockback(knockbackDirection, 200.0f, .15f);
            QueueFree();
        }
    }

    
}


