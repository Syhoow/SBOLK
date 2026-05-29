using Godot;
using System;

public partial class ShopItem : PanelContainer
{
    public Action OnPurchased;
    private CosmeticManager.Cosmetic cosmetic;

    public void Setup(CosmeticManager.Cosmetic c)
    {
        cosmetic = c;

        Color rarityColor = c.Rarity switch
        {
            CosmeticManager.Rarity.Legendary => new Color(1f, 0.78f, 0f),
            CosmeticManager.Rarity.Rare      => new Color(0.3f, 0.55f, 1f),
            _                                => new Color(0.7f, 0.7f, 0.7f),
        };

        string rarityName = c.Rarity switch
        {
            CosmeticManager.Rarity.Legendary => "✦ LEGENDARY ✦",
            CosmeticManager.Rarity.Rare      => "◆ Rare",
            _                                => "Common",
        };

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.08f, 0.08f, 0.12f, 1f);
        style.BorderColor = rarityColor;
        style.BorderWidthTop = style.BorderWidthBottom = style.BorderWidthLeft = style.BorderWidthRight = 3;
        style.ContentMarginTop = style.ContentMarginBottom = style.ContentMarginLeft = style.ContentMarginRight = 6;
        AddThemeStyleboxOverride("panel", style);

        var icon = GetNodeOrNull<TextureRect>("VBoxContainer/TextureRect");
        if (icon != null)
        {
            icon.Texture           = CosmeticManager.LoadItemIcon(c.Id);
            icon.CustomMinimumSize = new Vector2(48, 48);
            icon.ExpandMode        = TextureRect.ExpandModeEnum.IgnoreSize;
            icon.StretchMode       = TextureRect.StretchModeEnum.KeepAspectCentered;
        }

        var rarityLabel = GetNodeOrNull<Label>("VBoxContainer/RarityLabel");
        if (rarityLabel != null)
        {
            rarityLabel.Text = rarityName;
            rarityLabel.AddThemeColorOverride("font_color", rarityColor);
        }

        GetNode<Label>("VBoxContainer/NameLabel").Text = c.Name;

        var ownedCount = CosmeticManager.Instance.GetOwnedCount(c.Id);
        string priceText = c.Rarity == CosmeticManager.Rarity.Legendary
            ? $"{c.Price} Trade Tokens (owned: {ownedCount}/{CosmeticManager.MaxStack})"
            : $"{c.Price} coins (owned: {ownedCount}/{CosmeticManager.MaxStack})";
        GetNode<Label>("VBoxContainer/PriceLabel").Text = priceText;

        bool atMax = ownedCount >= CosmeticManager.MaxStack;
        bool canAfford = c.Rarity == CosmeticManager.Rarity.Legendary
            ? CosmeticManager.Instance.TradeTokens >= c.Price
            : CosmeticManager.Instance.Coins >= c.Price;

        var btn = GetNode<Button>("VBoxContainer/Button");
        btn.Text = atMax ? "Max" : "Buy";
        btn.Disabled = atMax || !canAfford;
        btn.Pressed += _on_Button_pressed;
    }

    private void _on_Button_pressed()
    {
        if (CosmeticManager.Instance.Purchase(cosmetic.Id))
            OnPurchased?.Invoke();
    }
}
