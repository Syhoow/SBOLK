using Godot;
using System;
using System.Collections.Generic;

public partial class MarketplaceListing : PanelContainer
{
    public Action<string> OnBuyPressed;

    private string _listingId;
    private Label _itemNameLabel;
    private Label _sellerLabel;
    private Label _priceLabel;
    private Label _qtyLabel;
    private Button _buyButton;
    private PriceChart _priceChart;

    public override void _Ready()
    {
        _itemNameLabel = GetNodeOrNull<Label>("VBoxContainer/InfoRow/ItemNameLabel");
        _sellerLabel = GetNodeOrNull<Label>("VBoxContainer/InfoRow/SellerLabel");
        _priceLabel = GetNodeOrNull<Label>("VBoxContainer/InfoRow/PriceLabel");
        _qtyLabel = GetNodeOrNull<Label>("VBoxContainer/InfoRow/QtyLabel");
        _buyButton = GetNodeOrNull<Button>("VBoxContainer/InfoRow/BuyButton");
        _priceChart = GetNodeOrNull<PriceChart>("VBoxContainer/PriceChart");

        if (_buyButton != null)
            _buyButton.Pressed += OnBuyButtonPressed;
    }

    public void Setup(MarketplaceManager.Listing listing, List<MarketplaceManager.TradeRecord> history)
    {
        _listingId = listing.Id;

        if (_itemNameLabel != null) _itemNameLabel.Text = listing.ItemName;
        if (_sellerLabel != null) _sellerLabel.Text = listing.SellerEmail;
        if (_priceLabel != null) _priceLabel.Text = $"{listing.Price}c";
        if (_qtyLabel != null) _qtyLabel.Text = $"x{listing.Quantity}";

        if (_buyButton != null)
        {
            var uid = GetCurrentUid();
            bool isSelf = listing.SellerUid == uid;
            bool canAfford = CosmeticManager.Instance != null &&
                             CosmeticManager.Instance.Coins >= listing.Price;
            _buyButton.Text = isSelf ? "Mine" : "Buy";
            _buyButton.Disabled = isSelf || !canAfford;
        }

        if (_priceChart != null && history != null)
        {
            var prices = new List<float>();
            foreach (var record in history)
                prices.Add(record.Price);
            _priceChart.SetPrices(prices);
        }
    }

    private void OnBuyButtonPressed()
    {
        OnBuyPressed?.Invoke(_listingId);
    }

    private string GetCurrentUid()
    {
        var firebase = GetNodeOrNull<Node>("/root/Firebase");
        var auth = firebase?.GetNodeOrNull<Node>("Auth");
        if (auth == null) return "";
        var authVar = auth.Get("auth");
        if (authVar.VariantType != Variant.Type.Dictionary) return "";
        var d = (Godot.Collections.Dictionary)authVar;
        if (d.ContainsKey("localid")) return ((Variant)d["localid"]).AsString();
        if (d.ContainsKey("localId")) return ((Variant)d["localId"]).AsString();
        return "";
    }
}
