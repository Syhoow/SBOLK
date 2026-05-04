using Godot;

public partial class Bullet : Node2D
{
    private float _speed = 300f;
    private float _lifeSeconds = 2f;
    private Vector2 _direction = Vector2.Right;

    public override void _Ready()
    {
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
