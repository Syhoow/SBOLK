using Godot;

public partial class ShotgunBullet : Bullet
{
    private float _driftTime;
    private float _driftSeed = 1f;

    protected override void ApplyTypeModifiers()
    {
        _gravity *= 1.6f;
        _drag *= 1.4f;
        _spinDegreesPerSecond -= 60f;
        _lifeSeconds *= 0.7f;
        _driftSeed = 0.7f + (Mathf.Abs(_speed) % 60f) / 60f;
    }

    protected override Vector2 ModifyVelocity(Vector2 velocity, float dt)
    {
        _driftTime += dt;
        var sideways = new Vector2(-velocity.Y, velocity.X).Normalized();
        var drift = Mathf.Sin(_driftTime * 14f * _driftSeed) * 20f;
        return velocity + sideways * drift;
    }
}
