using Godot;

public sealed class WeaponStats
{
    public WeaponType Type { get; init; }
    public float FireInterval { get; init; }
    public float BulletSpeed { get; init; }
    public float SpreadDegrees { get; init; }
    public int PelletsPerShot { get; init; }
    public bool IsAutomatic { get; init; }

    public WeaponStats(
        WeaponType type,
        float fireInterval,
        float bulletSpeed,
        float spreadDegrees,
        int pelletsPerShot,
        bool isAutomatic)
    {
        Type = type;
        FireInterval = fireInterval;
        BulletSpeed = bulletSpeed;
        SpreadDegrees = spreadDegrees;
        PelletsPerShot = pelletsPerShot;
        IsAutomatic = isAutomatic;
    }

    public float SpreadRadians => Mathf.DegToRad(SpreadDegrees);
}