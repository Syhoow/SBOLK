using Godot;

public sealed class WeaponStats
{
    public WeaponType Type { get; init; }
    public float FireInterval { get; init; }
    public int MagazineSize { get; init; }
    public int ReserveAmmo { get; init; }
    public float ReloadTime { get; init; }
    public float BulletSpeed { get; init; }
    public float SpreadDegrees { get; init; }
    public int PelletsPerShot { get; init; }
    public bool IsAutomatic { get; init; }
    public float BulletLifetime { get; init; }
    public float BulletScale { get; init; }
    public float BulletGravity { get; init; }
    public float BulletDrag { get; init; }
    public float BulletSpinDegreesPerSecond { get; init; }
    public Color BulletTint { get; init; }

    public WeaponStats(
        WeaponType type,
        float fireInterval,
        int magazineSize,
        int reserveAmmo,
        float reloadTime,
        float bulletSpeed,
        float spreadDegrees,
        int pelletsPerShot,
        bool isAutomatic,
        float bulletLifetime,
        float bulletScale,
        float bulletGravity,
        float bulletDrag,
        float bulletSpinDegreesPerSecond,
        Color bulletTint)
    {
        Type = type;
        FireInterval = fireInterval;
        MagazineSize = magazineSize;
        ReserveAmmo = reserveAmmo;
        ReloadTime = reloadTime;
        BulletSpeed = bulletSpeed;
        SpreadDegrees = spreadDegrees;
        PelletsPerShot = pelletsPerShot;
        IsAutomatic = isAutomatic;
        BulletLifetime = bulletLifetime;
        BulletScale = bulletScale;
        BulletGravity = bulletGravity;
        BulletDrag = bulletDrag;
        BulletSpinDegreesPerSecond = bulletSpinDegreesPerSecond;
        BulletTint = bulletTint;
    }

    public float SpreadRadians => Mathf.DegToRad(SpreadDegrees);
}
