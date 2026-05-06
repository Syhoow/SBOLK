using Godot;

public partial class PistolBullet : Bullet
{
    protected override void ApplyTypeModifiers()
    {
        _drag *= 0.95f;
    }
}
