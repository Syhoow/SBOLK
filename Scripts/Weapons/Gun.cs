using Godot;
using System.Collections.Generic;

public partial class Gun : Node2D
{
    [Signal]
    public delegate void WeaponChangedEventHandler(string weaponName, int ammoInMagazine, int ammoInReserve);

    [Signal]
    public delegate void AmmoChangedEventHandler(int ammoInMagazine, int ammoInReserve);

    private const float DefaultBulletLifeSeconds = 2.0f;

    [ExportGroup("Weapon Textures")]
    [Export] private Texture2D _pistolTexture;
    [Export] private Texture2D _smgTexture;
    [Export] private Texture2D _rifleTexture;
    [Export] private Texture2D _sniperTexture;
    [Export] private Texture2D _shotgunTexture;

    private readonly Dictionary<WeaponType, WeaponStats> _weaponTable = new();
    private readonly Dictionary<WeaponType, int> _ammoInMagazine = new();
    private readonly Dictionary<WeaponType, int> _ammoInReserve = new();
    private readonly Dictionary<WeaponType, Node2D> _weaponNodes = new();

    private PackedScene _bulletScene;
    private Marker2D _muzzle;
    private Sprite2D _sprite;

    private WeaponType _currentWeapon = WeaponType.Pistol;
    private float _shotCooldown;
    private float _reloadTimer;
    private bool _isReloading;

    public string CurrentWeaponName => _currentWeapon.ToString();

    public int CurrentAmmoInMagazine => _ammoInMagazine.TryGetValue(_currentWeapon, out var value) ? value : 0;

    public int CurrentAmmoInReserve => _ammoInReserve.TryGetValue(_currentWeapon, out var value) ? value : 0;

    public bool IsReloading => _isReloading;

    public override void _Ready()
    {
        _bulletScene = GD.Load<PackedScene>("res://Scenes/bullet.tscn");

        if (_bulletScene == null)
        {
            GD.PushError("Could not load res://Scenes/bullet.tscn.");
            return;
        }

        CacheWeaponNodes();
        BuildWeaponTable();
        InitializeAmmoPools();
        LoadWeaponTexturesFromAssets();
        ApplyWeaponVisual(_currentWeapon);
        BroadcastWeaponState();
    }

    private void CacheWeaponNodes()
    {
        _weaponNodes[WeaponType.Pistol] = GetNode<Node2D>("PistolNode");
        _weaponNodes[WeaponType.Smg] = GetNode<Node2D>("SmgNode");
        _weaponNodes[WeaponType.Rifle] = GetNode<Node2D>("RifleNode");
        _weaponNodes[WeaponType.Sniper] = GetNode<Node2D>("SniperNode");
        _weaponNodes[WeaponType.Shotgun] = GetNode<Node2D>("ShotgunNode");
    }

    private void LoadWeaponTexturesFromAssets()
    {
        _pistolTexture ??= GD.Load<Texture2D>("res://Assets/pistol.png");
        _rifleTexture ??= GD.Load<Texture2D>("res://Assets/rifle.png");
        _sniperTexture ??= GD.Load<Texture2D>("res://Assets/sniper.png");
        _shotgunTexture ??= GD.Load<Texture2D>("res://Assets/shotgun.png");

        // Temporary fallback until a dedicated SMG texture exists.
        _smgTexture ??= _rifleTexture;

        AssignTextureIfPresent(WeaponType.Pistol, _pistolTexture);
        AssignTextureIfPresent(WeaponType.Smg, _smgTexture ?? _rifleTexture ?? _pistolTexture);
        AssignTextureIfPresent(WeaponType.Rifle, _rifleTexture ?? _pistolTexture);
        AssignTextureIfPresent(WeaponType.Sniper, _sniperTexture ?? _pistolTexture);
        AssignTextureIfPresent(WeaponType.Shotgun, _shotgunTexture ?? _pistolTexture);
    }

    private void AssignTextureIfPresent(WeaponType type, Texture2D texture)
    {
        if (texture == null || !_weaponNodes.TryGetValue(type, out var node))
        {
            return;
        }

        var sprite = node.GetNodeOrNull<Sprite2D>("Sprite2D");
        if (sprite != null)
        {
            sprite.Texture = texture;
        }
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;

        AimAtMouse();
        TickTimers(dt);
        HandleWeaponSelectionInput();
        HandleReloadInput();
        HandleShootInput();
    }

    private void BuildWeaponTable()
    {
        _weaponTable[WeaponType.Pistol] = new WeaponStats(
            type: WeaponType.Pistol,
            fireInterval: 0.25f,
            magazineSize: 12,
            reserveAmmo: 72,
            reloadTime: 1.1f,
            bulletSpeed: 700f,
            spreadDegrees: 1.5f,
            pelletsPerShot: 1,
            isAutomatic: false);

        _weaponTable[WeaponType.Smg] = new WeaponStats(
            type: WeaponType.Smg,
            fireInterval: 0.08f,
            magazineSize: 30,
            reserveAmmo: 180,
            reloadTime: 1.4f,
            bulletSpeed: 620f,
            spreadDegrees: 4.5f,
            pelletsPerShot: 1,
            isAutomatic: true);

        _weaponTable[WeaponType.Rifle] = new WeaponStats(
            type: WeaponType.Rifle,
            fireInterval: 0.12f,
            magazineSize: 25,
            reserveAmmo: 125,
            reloadTime: 1.8f,
            bulletSpeed: 900f,
            spreadDegrees: 2.0f,
            pelletsPerShot: 1,
            isAutomatic: true);

        _weaponTable[WeaponType.Sniper] = new WeaponStats(
            type: WeaponType.Sniper,
            fireInterval: 0.95f,
            magazineSize: 5,
            reserveAmmo: 25,
            reloadTime: 2.3f,
            bulletSpeed: 1400f,
            spreadDegrees: 0.35f,
            pelletsPerShot: 1,
            isAutomatic: false);

        _weaponTable[WeaponType.Shotgun] = new WeaponStats(
            type: WeaponType.Shotgun,
            fireInterval: 0.8f,
            magazineSize: 8,
            reserveAmmo: 40,
            reloadTime: 2.0f,
            bulletSpeed: 600f,
            spreadDegrees: 12.0f,
            pelletsPerShot: 8,
            isAutomatic: false);
    }

    private void InitializeAmmoPools()
    {
        foreach (var entry in _weaponTable)
        {
            _ammoInMagazine[entry.Key] = entry.Value.MagazineSize;
            _ammoInReserve[entry.Key] = entry.Value.ReserveAmmo;
        }
    }

    private void AimAtMouse()
    {
        LookAt(GetGlobalMousePosition());

        var wrappedDegrees = Mathf.Wrap(RotationDegrees, 0f, 360f);
        RotationDegrees = wrappedDegrees;

        var scaled = Scale;
        scaled.Y = (wrappedDegrees > 90f && wrappedDegrees < 270f) ? -Mathf.Abs(scaled.Y) : Mathf.Abs(scaled.Y);
        Scale = scaled;
    }

    private void TickTimers(float delta)
    {
        if (_shotCooldown > 0f)
        {
            _shotCooldown -= delta;
        }

        if (!_isReloading)
        {
            return;
        }

        _reloadTimer -= delta;
        if (_reloadTimer <= 0f)
        {
            FinishReload();
        }
    }

    private void HandleWeaponSelectionInput()
    {
        if (Input.IsKeyPressed(Key.Key1))
        {
            SwitchWeapon(WeaponType.Pistol);
        }
        else if (Input.IsKeyPressed(Key.Key2))
        {
            SwitchWeapon(WeaponType.Smg);
        }
        else if (Input.IsKeyPressed(Key.Key3))
        {
            SwitchWeapon(WeaponType.Rifle);
        }
        else if (Input.IsKeyPressed(Key.Key4))
        {
            SwitchWeapon(WeaponType.Sniper);
        }
        else if (Input.IsKeyPressed(Key.Key5))
        {
            SwitchWeapon(WeaponType.Shotgun);
        }
    }

    private void HandleReloadInput()
    {
        if (Input.IsKeyPressed(Key.R))
        {
            StartReload();
        }
    }

    private void HandleShootInput()
    {
        var stats = _weaponTable[_currentWeapon];

        if (_isReloading || _shotCooldown > 0f)
        {
            return;
        }

        var wantsToShoot = stats.IsAutomatic ? Input.IsActionPressed("fire") : Input.IsActionJustPressed("fire");
        if (!wantsToShoot)
        {
            return;
        }

        if (_ammoInMagazine[_currentWeapon] <= 0)
        {
            StartReload();
            return;
        }

        FireCurrentWeapon(stats);
    }

    private void FireCurrentWeapon(WeaponStats stats)
    {
        if (_bulletScene == null || _muzzle == null)
        {
            return;
        }

        for (var pelletIndex = 0; pelletIndex < stats.PelletsPerShot; pelletIndex++)
        {
            SpawnBullet(stats);
        }

        _ammoInMagazine[_currentWeapon] -= 1;
        _shotCooldown = stats.FireInterval;

        EmitSignal(SignalName.AmmoChanged, _ammoInMagazine[_currentWeapon], _ammoInReserve[_currentWeapon]);
    }

    private void SpawnBullet(WeaponStats stats)
    {
        var spreadOffset = stats.PelletsPerShot <= 1
            ? 0f
            : (float)GD.RandRange(-stats.SpreadRadians * 0.5f, stats.SpreadRadians * 0.5f);

        var direction = _muzzle.GlobalTransform.X.Rotated(spreadOffset).Normalized();

        var bullet = _bulletScene.Instantiate<Bullet>();
        GetTree().CurrentScene.AddChild(bullet);

        bullet.GlobalPosition = _muzzle.GlobalPosition;
        bullet.Initialize(stats.BulletSpeed, DefaultBulletLifeSeconds, direction);
    }

    private void StartReload()
    {
        if (_isReloading)
        {
            return;
        }

        if (_ammoInMagazine[_currentWeapon] >= _weaponTable[_currentWeapon].MagazineSize)
        {
            return;
        }

        if (_ammoInReserve[_currentWeapon] <= 0)
        {
            return;
        }

        _isReloading = true;
        _reloadTimer = _weaponTable[_currentWeapon].ReloadTime;
    }

    private void FinishReload()
    {
        var stats = _weaponTable[_currentWeapon];
        var needed = stats.MagazineSize - _ammoInMagazine[_currentWeapon];
        var fromReserve = Mathf.Min(needed, _ammoInReserve[_currentWeapon]);

        _ammoInMagazine[_currentWeapon] += fromReserve;
        _ammoInReserve[_currentWeapon] -= fromReserve;

        _isReloading = false;
        _reloadTimer = 0f;

        EmitSignal(SignalName.AmmoChanged, _ammoInMagazine[_currentWeapon], _ammoInReserve[_currentWeapon]);
    }

    private void SwitchWeapon(WeaponType weapon)
    {
        if (!_weaponTable.ContainsKey(weapon) || weapon == _currentWeapon)
        {
            return;
        }

        _currentWeapon = weapon;
        _isReloading = false;
        _reloadTimer = 0f;
        _shotCooldown = 0f;

        ApplyWeaponVisual(_currentWeapon);

        BroadcastWeaponState();
    }

    private void ApplyWeaponVisual(WeaponType weapon)
    {
        foreach (var pair in _weaponNodes)
        {
            pair.Value.Visible = pair.Key == weapon;
        }

        if (!_weaponNodes.TryGetValue(weapon, out var activeNode))
        {
            return;
        }

        _sprite = activeNode.GetNodeOrNull<Sprite2D>("Sprite2D");
        _muzzle = activeNode.GetNodeOrNull<Marker2D>("Marker2D");

        if (_sprite == null || _muzzle == null)
        {
            GD.PushError($"Weapon node '{activeNode.Name}' is missing Sprite2D or Marker2D child.");
        }
    }

    private void BroadcastWeaponState()
    {
        EmitSignal(SignalName.WeaponChanged, _currentWeapon.ToString(), _ammoInMagazine[_currentWeapon], _ammoInReserve[_currentWeapon]);
        EmitSignal(SignalName.AmmoChanged, _ammoInMagazine[_currentWeapon], _ammoInReserve[_currentWeapon]);
    }
}
