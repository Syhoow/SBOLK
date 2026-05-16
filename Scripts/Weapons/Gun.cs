using Godot;
using System.Collections.Generic;

public partial class Gun : Node2D
{
    [Signal]
    public delegate void WeaponChangedEventHandler(string weaponName);

    private const float DefaultBulletLifeSeconds = 2.0f;

    [ExportGroup("Weapon Textures")]
    [Export] private Texture2D _pistolTexture;
    [Export] private Texture2D _smgTexture;
    [Export] private Texture2D _rifleTexture;
    [Export] private Texture2D _sniperTexture;
    [Export] private Texture2D _shotgunTexture;

    private readonly Dictionary<WeaponType, WeaponStats> _weaponTable = new();
    private readonly Dictionary<WeaponType, Node2D> _weaponNodes = new();

    private PackedScene _bulletScene;
    private Marker2D _muzzle;
    private Sprite2D _sprite;

    private WeaponType _currentWeapon = WeaponType.Pistol;
    private float _shotCooldown;

    public string CurrentWeaponName => _currentWeapon.ToString();

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
        LoadWeaponTexturesFromAssets();
        ApplyWeaponVisual(_currentWeapon);
        EmitSignal(SignalName.WeaponChanged, _currentWeapon.ToString());
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
        _pistolTexture ??= GD.Load<Texture2D>("res://Assets/Pistolpixel.png");
        _rifleTexture ??= GD.Load<Texture2D>("res://Assets/riflepixel.png");
        _sniperTexture ??= GD.Load<Texture2D>("res://Assets/sniperpixel.png");
        _shotgunTexture ??= GD.Load<Texture2D>("res://Assets/shotgunpixel.png");
        _smgTexture ??= GD.Load<Texture2D>("res://Assets/smgpixel.png");

        AssignTextureIfPresent(WeaponType.Pistol, _pistolTexture);
        AssignTextureIfPresent(WeaponType.Smg, _smgTexture ?? _rifleTexture ?? _pistolTexture);
        AssignTextureIfPresent(WeaponType.Rifle, _rifleTexture ?? _pistolTexture);
        AssignTextureIfPresent(WeaponType.Sniper, _sniperTexture ?? _pistolTexture);
        AssignTextureIfPresent(WeaponType.Shotgun, _shotgunTexture ?? _pistolTexture);
    }

    private void AssignTextureIfPresent(WeaponType type, Texture2D texture)
    {
        if (texture == null || !_weaponNodes.TryGetValue(type, out var node)) return;

        var sprite = node.GetNodeOrNull<Sprite2D>("Sprite2D");
        if (sprite != null)
            sprite.Texture = texture;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        AimAtMouse();
        TickTimers(dt);
        //HandleWeaponSelectionInput();
        HandleShootInput();
    }

    private void BuildWeaponTable()
    {
        _weaponTable[WeaponType.Pistol] = new WeaponStats(
            type: WeaponType.Pistol,
            fireInterval: 0.30f,
            bulletSpeed: 1000f,
            spreadDegrees: 1.5f,
            pelletsPerShot: 1,
            isAutomatic: true);

        _weaponTable[WeaponType.Smg] = new WeaponStats(
            type: WeaponType.Smg,
            fireInterval: 0.07f,
            bulletSpeed: 1500f,
            spreadDegrees: 4.5f,
            pelletsPerShot: 1,
            isAutomatic: true);

        _weaponTable[WeaponType.Rifle] = new WeaponStats(
            type: WeaponType.Rifle,
            fireInterval: 0.15f,
            bulletSpeed: 1500f,
            spreadDegrees: 2.0f,
            pelletsPerShot: 1,
            isAutomatic: true);

        _weaponTable[WeaponType.Sniper] = new WeaponStats(
            type: WeaponType.Sniper,
            fireInterval: 0.95f,
            bulletSpeed: 1400f,
            spreadDegrees: 0.35f,
            pelletsPerShot: 1,
            isAutomatic: false);

        _weaponTable[WeaponType.Shotgun] = new WeaponStats(
            type: WeaponType.Shotgun,
            fireInterval: 0.8f,
            bulletSpeed: 600f,
            spreadDegrees: 12.0f,
            pelletsPerShot: 8,
            isAutomatic: false);
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
            _shotCooldown -= delta;
    }

    private void HandleWeaponSelectionInput()
    {
        if (Input.IsKeyPressed(Key.Key1)) SwitchWeapon(WeaponType.Pistol);
        else if (Input.IsKeyPressed(Key.Key2)) SwitchWeapon(WeaponType.Smg);
        else if (Input.IsKeyPressed(Key.Key3)) SwitchWeapon(WeaponType.Rifle);
        else if (Input.IsKeyPressed(Key.Key4)) SwitchWeapon(WeaponType.Sniper);
        else if (Input.IsKeyPressed(Key.Key5)) SwitchWeapon(WeaponType.Shotgun);
    }

    private void HandleShootInput()
    {
        var stats = _weaponTable[_currentWeapon];
        if (_shotCooldown > 0f) return;

        var wantsToShoot = stats.IsAutomatic
            ? Input.IsActionPressed("fire")
            : Input.IsActionJustPressed("fire");

        if (!wantsToShoot) return;

        FireCurrentWeapon(stats);
    }

    private void FireCurrentWeapon(WeaponStats stats)
    {
        if (_bulletScene == null || _muzzle == null) return;

        for (var i = 0; i < stats.PelletsPerShot; i++)
            SpawnBullet(stats);

        _shotCooldown = stats.FireInterval;
    }

    private void SpawnBullet(WeaponStats stats)
    {
        var spreadOffset = stats.PelletsPerShot <= 1
            ? 0f
            : (float)GD.RandRange(-stats.SpreadRadians * 0.5f, stats.SpreadRadians * 0.5f);

        var direction = _muzzle.GlobalTransform.X.Rotated(spreadOffset).Normalized();

        var bullet = _bulletScene.Instantiate<Bullet>();
        var canvasLayer = GetTree().Root.GetNode<CanvasLayer>("Main/EnemyLayer");
        canvasLayer.AddChild(bullet);

        bullet.GlobalPosition = _muzzle.GlobalPosition;
        bullet.Initialize(stats.BulletSpeed, DefaultBulletLifeSeconds, direction);
    }

    private void SwitchWeapon(WeaponType weapon)
    {
        if (!_weaponTable.ContainsKey(weapon) || weapon == _currentWeapon) return;

        _currentWeapon = weapon;
        _shotCooldown = 0f;

        ApplyWeaponVisual(_currentWeapon);
        EmitSignal(SignalName.WeaponChanged, _currentWeapon.ToString());
    }

    private void ApplyWeaponVisual(WeaponType weapon)
    {
        foreach (var pair in _weaponNodes)
            pair.Value.Visible = pair.Key == weapon;

        if (!_weaponNodes.TryGetValue(weapon, out var activeNode)) return;

        _sprite = activeNode.GetNodeOrNull<Sprite2D>("Sprite2D");
        _muzzle = activeNode.GetNodeOrNull<Marker2D>("Marker2D");

        if (_sprite == null || _muzzle == null)
            GD.PushError($"Weapon node '{activeNode.Name}' is missing Sprite2D or Marker2D child.");
    }

    public void UpgradeWeapon(WeaponType newWeapon)
    {
        if (!_weaponTable.ContainsKey(newWeapon)) return;
        _currentWeapon = newWeapon;
        _shotCooldown = 0f;
        ApplyWeaponVisual(_currentWeapon);
        EmitSignal(SignalName.WeaponChanged, _currentWeapon.ToString());
    }
}