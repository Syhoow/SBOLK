using Godot;

public partial class EnemyBullet : Node2D
{
    private float _speed = 400f;
    private float _lifeSeconds = 3f;
    private Vector2 _direction = Vector2.Right;
    public float Damage = 10f;

    public override void _Ready()
    {
        AddToGroup("enemy_bullet");
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

   private void _on_area_2d_area_entered(Area2D area)
    {
        if (area.IsInGroup("enemy")) return; // ignore enemies
        
        if (area.IsInGroup("player"))
        {
            Player player = area.GetParent<Player>();
            Vector2 knockbackDirection = (area.GlobalPosition - GlobalPosition).Normalized();
            player.ApplyKnockback(knockbackDirection, 300f, 0.1f);
            QueueFree();
        }
    }
}