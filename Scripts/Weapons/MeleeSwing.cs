using Godot;

public partial class MeleeSwing : Node2D
{
    [Export] public float Damage { get; set; } = 90f;
    [Export] public float KnockbackForce { get; set; } = 420f;
    [Export] public float KnockbackDuration { get; set; } = 0.22f;

    private float _lifeRemaining = 0.08f;
    private Vector2 _velocity = Vector2.Zero;

    public void Initialize(Vector2 direction, float travelDistance, float activeTime)
    {
        var safeDirection = direction.Normalized();
        Rotation = safeDirection.Angle();
        _lifeRemaining = Mathf.Max(0.02f, activeTime);
        _velocity = safeDirection * (travelDistance / _lifeRemaining);
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _lifeRemaining -= dt;
        GlobalPosition += _velocity * dt;

        if (_lifeRemaining <= 0f)
        {
            QueueFree();
        }
    }
}
