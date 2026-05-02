using Godot;

public partial class SniperBullet : Bullet
{
    protected override void ApplyTypeModifiers()
    {
        _speed *= 1.18f;
        _drag *= 0.05f;
        _lifeSeconds *= 1.35f;
        _spinDegreesPerSecond *= 0.2f;
    }

    protected override Vector2 ModifyVelocity(Vector2 velocity, float dt)
    {
        return velocity + _direction * 120f * dt;
    }
}
