using Godot;
using System;

public partial class InventoryItem : PanelContainer
{
    public Action OnEquipped;
    private string cosmeticId;
    private bool isEquipped;

    public void Setup(string id, int count)
    {
        cosmeticId = id;
        isEquipped = CosmeticManager.Instance.EquippedHat == id;

        string displayName = id;
        foreach (var c in CosmeticManager.Instance.AllCosmetics)
        {
            if (c.Id == id) { displayName = c.Name; break; }
        }
        GetNode<Label>("VBoxContainer/Label").Text = $"{displayName} x{count}";

        var icon = GetNodeOrNull<TextureRect>("VBoxContainer/TextureRect");
        if (icon != null)
            icon.Texture = CosmeticManager.LoadItemIcon(id);

        var btn = GetNode<Button>("VBoxContainer/Button");
        btn.Text = isEquipped ? "Unequip" : "Equip";
    }

    private void _on_button_pressed()
    {
        if (isEquipped)
            CosmeticManager.Instance.Unequip(cosmeticId);
        else
            CosmeticManager.Instance.Equip(cosmeticId);

        OnEquipped?.Invoke();
    }
}
