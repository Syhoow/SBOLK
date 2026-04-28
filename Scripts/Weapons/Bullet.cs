using Godot;

public partial class Bullet : Node2D
{
    private float _speed = 300f;
    private float _lifeSeconds = 2f;
    private Vector2 _direction = Vector2.Right;

    public override void _Process(double delta)
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
}
