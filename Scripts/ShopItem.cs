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
        GetNode<Label>("VBoxContainer/Label2").Text = $"{c.Price} coins";

        var btn = GetNode<Button>("VBoxContainer/Button");
        bool owned = CosmeticManager.Instance.OwnedIds.Contains(c.Id);
        btn.Text = owned ? "Owned" : "Buy";
        btn.Disabled = owned;

        // Connect the signal in code
        btn.Pressed += _on_Button_pressed;
    }

    private void _on_Button_pressed()
    {
        if (CosmeticManager.Instance.Purchase(cosmetic.Id))
            OnPurchased?.Invoke();
    }
}