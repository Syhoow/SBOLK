using Godot;

public partial class SmgBullet : Bullet
{
    private float _waveTime;
    private float _waveSeed = 1f;

    protected override void ApplyTypeModifiers()
    {
        _drag *= 1.2f;
        _spinDegreesPerSecond += 180f;
        _lifeSeconds *= 0.9f;
        _waveSeed = 0.8f + (Mathf.PosMod(_speed, 90f) / 90f) * 0.6f;
    }

    protected override Vector2 ModifyVelocity(Vector2 velocity, float dt)
    {
        _waveTime += dt;
        var sideways = new Vector2(-velocity.Y, velocity.X).Normalized();
        var wobble = Mathf.Sin(_waveTime * 20f * _waveSeed) * 12f;
        return velocity + sideways * wobble;
    }
}
