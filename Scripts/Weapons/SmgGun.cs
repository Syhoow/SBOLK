using Godot;

public partial class SmgGun : Node2D
{
    [Signal]
    public delegate void AmmoChangedEventHandler(int ammoInMagazine, int ammoInReserve);

    private int _ammoInMagazine = 30;
    private int _ammoInReserve = 180;
    private float _shotCooldown;
    private float _reloadTimer;
    private bool _isReloading;

    private Marker2D _muzzle;
    private AnimatedSprite2D _animatedSprite;
    private PackedScene _bulletScene;

    public int CurrentAmmoInMagazine => _ammoInMagazine;
    public int CurrentAmmoInReserve => _ammoInReserve;
    public bool IsReloading => _isReloading;

    public override void _Ready()
    {
        _muzzle = GetNode<Marker2D>("Marker2D");
        _animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        _bulletScene = GD.Load<PackedScene>("res://Scenes/Bullets/SmgBullet.tscn");
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;

        AimAtMouse();
        TickTimers(dt);
        HandleReloadInput();
        HandleShootInput();
    }

    private void AimAtMouse()
    {
        var mousePos = GetGlobalMousePosition();
        LookAt(mousePos);

        var flip = RotationDegrees > 90f && RotationDegrees < 270f ? -1f : 1f;
        var scaled = Scale;
        scaled.Y = Mathf.Abs(scaled.Y) * flip;
        Scale = scaled;
    }

    private void TickTimers(float dt)
    {
        if (_shotCooldown > 0f)
        {
            _shotCooldown -= dt;
        }

        if (_isReloading)
        {
            _reloadTimer -= dt;
            if (_reloadTimer <= 0f)
            {
                CompleteReload();
            }
        }
    }

    private void HandleReloadInput()
    {
        if (Input.IsActionJustPressed("reload") && !_isReloading && _ammoInMagazine < 30)
        {
            StartReload();
        }
    }

    private void HandleShootInput()
    {
        if (Input.IsActionPressed("shoot") && _shotCooldown <= 0f && !_isReloading && _ammoInMagazine > 0)
        {
            Fire();
        }
    }

    private void Fire()
    {
        _shotCooldown = 0.08f; // Fire rate
        _ammoInMagazine--;

        var bullet = _bulletScene.Instantiate<Node2D>();
        GetParent().AddChild(bullet);
        bullet.GlobalPosition = _muzzle.GlobalPosition;
        bullet.GlobalRotation = GlobalRotation;

        if (_animatedSprite != null && _animatedSprite.SpriteFrames != null)
        {
            _animatedSprite.Play("fire");
        }

        EmitSignal(SignalName.AmmoChanged, _ammoInMagazine, _ammoInReserve);
    }

    private void StartReload()
    {
        _isReloading = true;
        _reloadTimer = 1.4f; // Reload time
        if (_animatedSprite != null && _animatedSprite.SpriteFrames != null)
        {
            _animatedSprite.Play("reload");
        }
    }

    private void CompleteReload()
    {
        _isReloading = false;
        int ammoNeeded = 30 - _ammoInMagazine;
        int ammoToTransfer = Mathf.Min(ammoNeeded, _ammoInReserve);
        _ammoInMagazine += ammoToTransfer;
        _ammoInReserve -= ammoToTransfer;
        EmitSignal(SignalName.AmmoChanged, _ammoInMagazine, _ammoInReserve);
    }
}
