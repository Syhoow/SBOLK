using Godot;
using System;

public partial class ShopItem : PanelContainer
{
    public Action OnPurchased;
    private CosmeticManager.Cosmetic cosmetic;

    public void Setup(CosmeticManager.Cosmetic c)
    {
        cosmetic = c;
        GetNode<Label>("VBoxContainer/Label").Text = c.Name;
        var ownedCount = CosmeticManager.Instance.GetOwnedCount(c.Id);
        GetNode<Label>("VBoxContainer/Label2").Text = $"{c.Price} coins (Owned: {ownedCount}/{CosmeticManager.MaxStack})";

        var btn = GetNode<Button>("VBoxContainer/Button");
        bool atMax = ownedCount >= CosmeticManager.MaxStack;
        btn.Text = atMax ? "Max" : "Buy";
        btn.Disabled = atMax;

        // Connect the signal in code
        btn.Pressed += _on_Button_pressed;
    }

    private void _on_Button_pressed()
    {
        if (CosmeticManager.Instance.Purchase(cosmetic.Id))
            OnPurchased?.Invoke();
    }
}