using Godot;
using System;

public partial class InventoryItem : PanelContainer
{
    public Action OnEquipped;
    private string cosmeticId;

    public void Setup(string id)
    {
        cosmeticId = id;
        GetNode<Label>("VBoxContainer/Label").Text = id;

        var btn = GetNode<Button>("VBoxContainer/Button");
        btn.Text = CosmeticManager.Instance.EquippedHat == id ? "Equipped" : "Equip";
    }

    private void _on_button_pressed()
    {
        CosmeticManager.Instance.Equip(cosmeticId);
        OnEquipped?.Invoke();
    }
}