using Godot;

public partial class WeaponHud : CanvasLayer
{
   /* [Export]
    public NodePath GunPath { get; set; } = new NodePath("../Player/Gun");

    private Label _weaponLabel;
    private Label _ammoLabel;
    private Label _damageLabel;
    private Gun _gun;
    private string _currentWeaponName = string.Empty;
    private float _damageMessageTimer;

    public override void _Ready()
    {
        _weaponLabel = GetNode<Label>("MarginContainer/VBoxContainer/WeaponLabel");
        _ammoLabel = GetNode<Label>("MarginContainer/VBoxContainer/AmmoLabel");
        _damageLabel = GetNode<Label>("MarginContainer/VBoxContainer/DamageLabel");
        _damageLabel.Visible = false;

        _gun = GetNodeOrNull<Gun>(GunPath);
        if (_gun == null)
        {
            GD.PushError("WeaponHud could not find Gun node. Check GunPath on HUD.");
            return;
        }

        _gun.WeaponChanged += OnWeaponChanged;
        _gun.AmmoChanged += OnAmmoChanged;

        OnWeaponChanged(_gun.CurrentWeaponName, _gun.CurrentAmmoInMagazine, _gun.CurrentAmmoInReserve);
        OnAmmoChanged(_gun.CurrentAmmoInMagazine, _gun.CurrentAmmoInReserve);
    }

    public override void _Process(double delta)
    {
        if (_damageMessageTimer <= 0f)
        {
            return;
        }

        _damageMessageTimer -= (float)delta;
        if (_damageMessageTimer <= 0f)
        {
            _damageLabel.Visible = false;
            _damageLabel.Modulate = Colors.White;
            _damageLabel.Scale = Vector2.One;
        }
    }

    private void OnWeaponChanged(string weaponName, int ammoInMagazine, int ammoInReserve)
    {
        _currentWeaponName = weaponName;
        _weaponLabel.Text = $"Weapon: {weaponName}";

        // Hide ammo display for melee weapons
        var isMelee = string.Equals(weaponName, "Melee", System.StringComparison.OrdinalIgnoreCase);
        _ammoLabel.Visible = !isMelee;
        if (!isMelee)
        {
            _ammoLabel.Text = BuildAmmoText(ammoInMagazine, ammoInReserve);
        }
    }

    private void OnAmmoChanged(int ammoInMagazine, int ammoInReserve)
    {
        if (string.Equals(_currentWeaponName, "Melee", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _ammoLabel.Text = BuildAmmoText(ammoInMagazine, ammoInReserve);
    }

    private string BuildAmmoText(int ammoInMagazine, int ammoInReserve)
    {
        if (_gun != null && _gun.IsReloading)
        {
            return $"Ammo: {ammoInMagazine}/{ammoInReserve} (Reloading)";
        }

        return $"Ammo: {ammoInMagazine}/{ammoInReserve}";
    }

    public void ShowDamageTaken(float damage)
    {
        _damageLabel.Text = $"Hit -{damage:0}";
        _damageLabel.Modulate = new Color(1f, 0.25f, 0.25f, 1f);
        _damageLabel.Scale = Vector2.One * 1.15f;
        _damageLabel.Visible = true;
        _damageMessageTimer = 1.0f;
    }*/
}
