using Godot;

public partial class RifleBullet : Bullet
{
    protected override void ApplyTypeModifiers()
    {
        _speed *= 1.08f;
        _drag *= 0.45f;
        _lifeSeconds *= 1.15f;
    }

    protected override Vector2 ModifyVelocity(Vector2 velocity, float dt)
    {
        return velocity.Lerp(_direction * _speed, 0.02f);
    }
}
