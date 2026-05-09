using Godot;
using System.Collections.Generic;

public partial class Gun : Node2D
{
    [Signal]
    public delegate void WeaponChangedEventHandler(string weaponName, int ammoInMagazine, int ammoInReserve);

    [Signal]
    public delegate void AmmoChangedEventHandler(int ammoInMagazine, int ammoInReserve);

    [ExportGroup("Weapon Textures")]
    [Export] private Texture2D _pistolTexture;
    [Export] private Texture2D _smgTexture;
    [Export] private Texture2D _rifleTexture;
    [Export] private Texture2D _sniperTexture;
    [Export] private Texture2D _shotgunTexture;
    [Export] private Texture2D _meleeTexture;

    private readonly Dictionary<WeaponType, WeaponStats> _weaponTable = new();
    private readonly Dictionary<WeaponType, int> _ammoInMagazine = new();
    private readonly Dictionary<WeaponType, int> _ammoInReserve = new();
    private readonly Dictionary<WeaponType, Node2D> _weaponNodes = new();
    private readonly Dictionary<WeaponType, PackedScene> _attackScenes = new();

    private Marker2D _muzzle;
    private Sprite2D _sprite;
    private AnimationPlayer _animationPlayer;

    private WeaponType _currentWeapon = WeaponType.Pistol;
    private WeaponType _previousWeapon = WeaponType.Pistol;
    private float _shotCooldown;
    private float _reloadTimer;
    private float _meleeReturnTimer;
    private bool _isReloading;

    public string CurrentWeaponName => _currentWeapon.ToString();

    public int CurrentAmmoInMagazine => _ammoInMagazine.TryGetValue(_currentWeapon, out var value) ? value : 0;

    public int CurrentAmmoInReserve => _ammoInReserve.TryGetValue(_currentWeapon, out var value) ? value : 0;

    public bool IsReloading => _isReloading;

    public override void _Ready()
    {
        CacheWeaponNodes();
        _animationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
        BuildWeaponTable();
        InitializeAmmoPools();
        LoadAttackScenes();
        LoadWeaponTexturesFromAssets();
        ApplyWeaponVisual(_currentWeapon);
        BroadcastWeaponState();
    }

    private void LoadAttackScenes()
    {
        _attackScenes[WeaponType.Pistol] = GD.Load<PackedScene>("res://Scenes/Bullets/PistolBullet.tscn");
        _attackScenes[WeaponType.Smg] = GD.Load<PackedScene>("res://Scenes/Bullets/SmgBullet.tscn");
        _attackScenes[WeaponType.Rifle] = GD.Load<PackedScene>("res://Scenes/Bullets/RifleBullet.tscn");
        _attackScenes[WeaponType.Sniper] = GD.Load<PackedScene>("res://Scenes/Bullets/SniperBullet.tscn");
        _attackScenes[WeaponType.Shotgun] = GD.Load<PackedScene>("res://Scenes/Bullets/ShotgunBullet.tscn");
        _attackScenes[WeaponType.Melee] = GD.Load<PackedScene>("res://Scenes/Attacks/MeleeSwing.tscn");
    }

    private void CacheWeaponNodes()
    {
        _weaponNodes[WeaponType.Pistol] = GetNode<Node2D>("PistolNode");
        _weaponNodes[WeaponType.Smg] = GetNode<Node2D>("SmgNode");
        _weaponNodes[WeaponType.Rifle] = GetNode<Node2D>("RifleNode");
        _weaponNodes[WeaponType.Sniper] = GetNode<Node2D>("SniperNode");
        _weaponNodes[WeaponType.Shotgun] = GetNode<Node2D>("ShotgunNode");
        _weaponNodes[WeaponType.Melee] = GetNode<Node2D>("MeleeNode");
    }

    private void LoadWeaponTexturesFromAssets()
    {
        // Prefer pixel art variants if present
        _pistolTexture ??= GD.Load<Texture2D>("res://Assets/Pistolpixel.png") ?? GD.Load<Texture2D>("res://Assets/Pistolpixel.png");
        _smgTexture ??= GD.Load<Texture2D>("res://Assets/smgpixel.png") ?? GD.Load<Texture2D>("res://Assets/smgpixel.png");
        _rifleTexture ??= GD.Load<Texture2D>("res://Assets/riflepixel.png") ?? GD.Load<Texture2D>("res://Assets/rifle.png");
        _sniperTexture ??= GD.Load<Texture2D>("res://Assets/sniperpixel.png") ?? GD.Load<Texture2D>("res://Assets/sniper.png");
        _shotgunTexture ??= GD.Load<Texture2D>("res://Assets/shotgunpixel.png") ?? GD.Load<Texture2D>("res://Assets/shotgun.png");
        _meleeTexture ??= GD.Load<Texture2D>("res://Assets/Sword12.png");

        AssignTextureIfPresent(WeaponType.Pistol, _pistolTexture);
        AssignTextureIfPresent(WeaponType.Smg, _smgTexture ?? _rifleTexture ?? _pistolTexture);
        AssignTextureIfPresent(WeaponType.Rifle, _rifleTexture ?? _pistolTexture);
        AssignTextureIfPresent(WeaponType.Sniper, _sniperTexture ?? _pistolTexture);
        AssignTextureIfPresent(WeaponType.Shotgun, _shotgunTexture ?? _pistolTexture);
        AssignTextureIfPresent(WeaponType.Melee, _meleeTexture ?? _shotgunTexture ?? _pistolTexture);
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
            isAutomatic: false,
            bulletLifetime: 1.4f,
            bulletScale: 1.0f,
            bulletGravity: 0f,
            bulletDrag: 0.02f,
            bulletSpinDegreesPerSecond: 0f,
            bulletTint: new Color(1f, 1f, 1f, 1f));

        _weaponTable[WeaponType.Smg] = new WeaponStats(
            type: WeaponType.Smg,
            fireInterval: 0.08f,
            magazineSize: 30,
            reserveAmmo: 180,
            reloadTime: 1.4f,
            bulletSpeed: 620f,
            spreadDegrees: 4.5f,
            pelletsPerShot: 1,
            isAutomatic: true,
            bulletLifetime: 1.0f,
            bulletScale: 0.85f,
            bulletGravity: 0f,
            bulletDrag: 0.05f,
            bulletSpinDegreesPerSecond: 180f,
            bulletTint: new Color(0.82f, 1f, 0.82f, 1f));

        _weaponTable[WeaponType.Rifle] = new WeaponStats(
            type: WeaponType.Rifle,
            fireInterval: 0.12f,
            magazineSize: 25,
            reserveAmmo: 125,
            reloadTime: 1.8f,
            bulletSpeed: 900f,
            spreadDegrees: 2.0f,
            pelletsPerShot: 1,
            isAutomatic: true,
            bulletLifetime: 1.8f,
            bulletScale: 1.05f,
            bulletGravity: 0f,
            bulletDrag: 0.01f,
            bulletSpinDegreesPerSecond: 0f,
            bulletTint: new Color(0.8f, 0.92f, 1f, 1f));

        _weaponTable[WeaponType.Sniper] = new WeaponStats(
            type: WeaponType.Sniper,
            fireInterval: 0.95f,
            magazineSize: 5,
            reserveAmmo: 25,
            reloadTime: 2.3f,
            bulletSpeed: 1400f,
            spreadDegrees: 0.35f,
            pelletsPerShot: 1,
            isAutomatic: false,
            bulletLifetime: 3.0f,
            bulletScale: 0.7f,
            bulletGravity: 0f,
            bulletDrag: 0f,
            bulletSpinDegreesPerSecond: 0f,
            bulletTint: new Color(1f, 0.95f, 0.75f, 1f));

        _weaponTable[WeaponType.Shotgun] = new WeaponStats(
            type: WeaponType.Shotgun,
            fireInterval: 0.8f,
            magazineSize: 8,
            reserveAmmo: 40,
            reloadTime: 2.0f,
            bulletSpeed: 900f,
            spreadDegrees: 12.0f,
            pelletsPerShot: 8,
            isAutomatic: false,
            bulletLifetime: 0.8f,
            bulletScale: 1.2f,
            bulletGravity: 220f,
            bulletDrag: 0.12f,
            bulletSpinDegreesPerSecond: -90f,
            bulletTint: new Color(1f, 0.85f, 0.85f, 1f));

            _weaponTable[WeaponType.Melee] = new WeaponStats(
                type: WeaponType.Melee,
                fireInterval: 0.42f,
                magazineSize: 1,
                reserveAmmo: 0,
                reloadTime: 0f,
                bulletSpeed: 120f,
                spreadDegrees: 0f,
                pelletsPerShot: 1,
                isAutomatic: false,
                bulletLifetime: 0.25f,
                bulletScale: 1.0f,
                bulletGravity: 0f,
                bulletDrag: 0f,
                bulletSpinDegreesPerSecond: 0f,
                bulletTint: new Color(1f, 0.7f, 0.55f, 1f));
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

        var flip = (wrappedDegrees > 90f && wrappedDegrees < 270f) ? -1f : 1f;
        var scaled = Scale;
        scaled.Y = Mathf.Abs(scaled.Y) * flip;
        Scale = scaled;
    }

    private void TickTimers(float delta)
    {
        if (_shotCooldown > 0f)
        {
            _shotCooldown -= delta;
        }

        if (_meleeReturnTimer > 0f)
        {
            _meleeReturnTimer -= delta;
            if (_meleeReturnTimer <= 0f)
            {
                SwitchWeapon(_previousWeapon);
            }
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
        // Weapon selection is now handled via mouse scroll wheel in _Input.
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp)
            {
                SwitchToPreviousWeapon();
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown)
            {
                SwitchToNextWeapon();
            }
        }
        else if (@event is InputEventKey kb && kb.Pressed && !kb.Echo && kb.Keycode == Key.Space)
        {
            if (_isReloading || _shotCooldown > 0f)
            {
                return;
            }

            // Store the current weapon as previous if not already in a melee attack
            if (_currentWeapon != WeaponType.Melee && _meleeReturnTimer <= 0f)
            {
                _previousWeapon = _currentWeapon;
            }

            if (_currentWeapon != WeaponType.Melee)
            {
                SwitchWeapon(WeaponType.Melee);
            }

            var stats = _weaponTable[_currentWeapon];
            if (_attackScenes.TryGetValue(_currentWeapon, out var attackScene) && attackScene != null)
            {
                PerformMeleeAttack(stats, attackScene);
                // Set timer to return to previous weapon after melee cooldown
                _meleeReturnTimer = stats.FireInterval;
            }
        }
    }

    private void SwitchToNextWeapon()
    {
        var values = (WeaponType[])System.Enum.GetValues(typeof(WeaponType));
        var idx = System.Array.IndexOf(values, _currentWeapon);
        idx = (idx + 1) % values.Length;
        SwitchWeapon(values[idx]);
    }

    private void SwitchToPreviousWeapon()
    {
        var values = (WeaponType[])System.Enum.GetValues(typeof(WeaponType));
        var idx = System.Array.IndexOf(values, _currentWeapon);
        idx = (idx - 1 + values.Length) % values.Length;
        SwitchWeapon(values[idx]);
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

        if (!_attackScenes.TryGetValue(_currentWeapon, out var attackScene) || attackScene == null)
        {
            GD.PushError($"No attack scene assigned for {_currentWeapon}.");
            return;
        }

        if (_currentWeapon == WeaponType.Melee)
        {
            PerformMeleeAttack(stats, attackScene);
            return;
        }

        FireCurrentWeapon(stats, attackScene);
    }

    private void FireCurrentWeapon(WeaponStats stats, PackedScene bulletScene)
    {
        if (_muzzle == null || bulletScene == null)
        {
            return;
        }

        for (var pelletIndex = 0; pelletIndex < stats.PelletsPerShot; pelletIndex++)
        {
            SpawnBullet(stats, bulletScene);
        }

        _ammoInMagazine[_currentWeapon] -= 1;
        _shotCooldown = stats.FireInterval;

        EmitSignal(SignalName.AmmoChanged, _ammoInMagazine[_currentWeapon], _ammoInReserve[_currentWeapon]);
    }

    private void SpawnBullet(WeaponStats stats, PackedScene bulletScene)
    {
        var spreadOffset = stats.PelletsPerShot <= 1
            ? 0f
            : (float)GD.RandRange(-stats.SpreadRadians * 0.5f, stats.SpreadRadians * 0.5f);

        var direction = _muzzle.GlobalTransform.X.Rotated(spreadOffset).Normalized();

        var bullet = bulletScene.Instantiate<Bullet>();
        
        // Add to ArenaLayer (same container as enemies) for proper rendering
        var arenaLayer = GetTree().CurrentScene.GetNode<Node>("ArenaLayer");
        arenaLayer.AddChild(bullet);

        // Spawn at muzzle's global position
        bullet.GlobalPosition = _muzzle.GlobalPosition;
        
        bullet.Initialize(
            speed: stats.BulletSpeed,
            lifeSeconds: stats.BulletLifetime,
            direction: direction,
            scale: stats.BulletScale,
            gravity: stats.BulletGravity,
            drag: stats.BulletDrag,
            spinDegreesPerSecond: stats.BulletSpinDegreesPerSecond,
            tint: stats.BulletTint);
    }

    private void PerformMeleeAttack(WeaponStats stats, PackedScene attackScene)
    {
        if (_muzzle == null || attackScene == null)
        {
            return;
        }

        var swing = attackScene.Instantiate<MeleeSwing>();

        var arenaLayer = GetTree().CurrentScene.GetNode<Node>("ArenaLayer");
        arenaLayer.AddChild(swing);

        swing.GlobalPosition = _muzzle.GlobalPosition;
        var direction = _muzzle.GlobalTransform.X.Normalized();
        swing.Initialize(direction, stats.BulletSpeed, stats.BulletLifetime);

        // Play melee swing animation on held weapon
        if (_animationPlayer != null)
        {
            _animationPlayer.Play("MeleeSwing");
        }

        _shotCooldown = stats.FireInterval;
        EmitSignal(SignalName.AmmoChanged, _ammoInMagazine[_currentWeapon], _ammoInReserve[_currentWeapon]);
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
