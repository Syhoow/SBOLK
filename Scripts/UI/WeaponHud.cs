using Godot;

public partial class WeaponHud : CanvasLayer
{
    [Export]
    public NodePath GunPath { get; set; } = new NodePath("../Player/Gun");

    private Label _weaponLabel;
    private Label _ammoLabel;
    private Gun _gun;

    public override void _Ready()
    {
        _weaponLabel = GetNode<Label>("MarginContainer/VBoxContainer/WeaponLabel");
        _ammoLabel = GetNode<Label>("MarginContainer/VBoxContainer/AmmoLabel");

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

    private void OnWeaponChanged(string weaponName, int ammoInMagazine, int ammoInReserve)
    {
        _weaponLabel.Text = $"Weapon: {weaponName}";
        _ammoLabel.Text = BuildAmmoText(ammoInMagazine, ammoInReserve);
    }

    private void OnAmmoChanged(int ammoInMagazine, int ammoInReserve)
    {
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
}
