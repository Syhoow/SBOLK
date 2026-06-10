using Godot;
using System;
using System.Collections.Generic;

public partial class MarketplaceListing : PanelContainer
{
    public Action<string> OnBuyPressed;
    public Action<string> OnCancelPressed;

    private string _listingId;
    private double _expiresAt;

    private Label _itemNameLabel;
    private Label _sellerLabel;
    private Label _priceLabel;
    private Label _qtyLabel;
    private Label _timerLabel;
    private TextureRect _itemIcon;
    private Button _buyButton;
    private Button _cancelButton;
    private Button _chartToggleButton;
    private PriceChart _priceChart;

    public override void _Ready()
    {
        _itemNameLabel     = GetNodeOrNull<Label>      ("VBoxContainer/InfoRow/ItemNameLabel");
        _sellerLabel       = GetNodeOrNull<Label>      ("VBoxContainer/InfoRow/SellerLabel");
        _priceLabel        = GetNodeOrNull<Label>      ("VBoxContainer/InfoRow/PriceLabel");
        _qtyLabel          = GetNodeOrNull<Label>      ("VBoxContainer/InfoRow/QtyLabel");
        _timerLabel        = GetNodeOrNull<Label>      ("VBoxContainer/InfoRow/TimerLabel");
        _itemIcon          = GetNodeOrNull<TextureRect>("VBoxContainer/InfoRow/ItemIcon");
        _buyButton         = GetNodeOrNull<Button>     ("VBoxContainer/InfoRow/BuyButton");
        _cancelButton      = GetNodeOrNull<Button>     ("VBoxContainer/InfoRow/CancelButton");
        _chartToggleButton = GetNodeOrNull<Button>     ("VBoxContainer/InfoRow/ChartToggleButton");
        _priceChart        = GetNodeOrNull<PriceChart> ("VBoxContainer/PriceChart");

        if (_buyButton != null)
            _buyButton.Pressed += OnBuyButtonPressed;

        if (_cancelButton != null)
            _cancelButton.Pressed += OnCancelButtonPressed;

        if (_chartToggleButton != null)
            _chartToggleButton.Pressed += OnChartTogglePressed;

        if (_priceChart != null)
            _priceChart.Visible = false;

        UpdateToggleLabel();
    }

    public override void _Process(double delta)
    {
        if (_timerLabel == null || _expiresAt <= 0) return;

        double nowUtc   = MarketplaceManager.DateTimeToUnix(DateTime.UtcNow);
        double secsLeft = _expiresAt - nowUtc;

        if (secsLeft <= 0)
        {
            _timerLabel.Text = "Expired";
            _timerLabel.Modulate = new Color(1f, 0.3f, 0.3f);
        }
        else
        {
            int hours = (int)(secsLeft / 3600);
            int mins  = (int)((secsLeft % 3600) / 60);
            int secs  = (int)(secsLeft % 60);

            if (hours > 0)
                _timerLabel.Text = $"{hours}h {mins:D2}m";
            else if (mins > 0)
                _timerLabel.Text = $"{mins}m {secs:D2}s";
            else
                _timerLabel.Text = $"{secs}s";
            if (secsLeft < 600)
                _timerLabel.Modulate = new Color(1f, 0.3f, 0.3f);
            else if (secsLeft < 3600)
                _timerLabel.Modulate = new Color(1f, 0.85f, 0.2f);
            else
                _timerLabel.Modulate = new Color(0.7f, 1f, 0.7f);
        }
    }

    public void Setup(MarketplaceManager.Listing listing, List<MarketplaceManager.TradeRecord> history)
    {
        _listingId = listing.Id;
        _expiresAt = listing.ExpiresAt;

        if (_itemNameLabel != null) _itemNameLabel.Text = listing.ItemName;
        if (_sellerLabel   != null) _sellerLabel.Text   = listing.SellerEmail;
        if (_priceLabel    != null) _priceLabel.Text    = $"{listing.Price}c";
        if (_qtyLabel      != null) _qtyLabel.Text      = $"x{listing.Quantity}";
        if (_itemIcon      != null) _itemIcon.Texture   = CosmeticManager.LoadItemIcon(listing.ItemId);

        var uid     = GetCurrentUid();
        bool isSelf = listing.SellerUid == uid;

        if (_buyButton != null)
        {
            bool canAfford = CosmeticManager.Instance != null &&
                             CosmeticManager.Instance.Coins >= listing.Price;
            _buyButton.Text     = isSelf ? "Mine" : "Buy";
            _buyButton.Disabled = isSelf || !canAfford;
        }

        if (_cancelButton != null)
            _cancelButton.Visible = isSelf;

        if (_priceChart != null && history != null)
        {
            var prices = new List<float>();
            foreach (var record in history)
                prices.Add(record.Price);
            _priceChart.SetPrices(prices);
        }
    }

    public void SetButtonsDisabled(bool disabled)
    {
        if (_buyButton    != null) _buyButton.Disabled    = disabled;
        if (_cancelButton != null) _cancelButton.Disabled = disabled;
    }

    private void OnBuyButtonPressed()    => OnBuyPressed?.Invoke(_listingId);
    private void OnCancelButtonPressed() => OnCancelPressed?.Invoke(_listingId);

    private void OnChartTogglePressed()
    {
        if (_priceChart == null) return;
        _priceChart.Visible = !_priceChart.Visible;
        UpdateToggleLabel();
    }

    private void UpdateToggleLabel()
    {
        if (_chartToggleButton == null) return;
        bool visible = _priceChart != null && _priceChart.Visible;
        _chartToggleButton.Text = visible ? "Hide Chart" : "Show Chart";
    }

    private string GetCurrentUid()
    {
        var firebase = GetNodeOrNull<Node>("/root/Firebase");
        var auth     = firebase?.GetNodeOrNull<Node>("Auth");
        if (auth == null) return "";
        var authVar = auth.Get("auth");
        if (authVar.VariantType != Variant.Type.Dictionary) return "";
        var d = (Godot.Collections.Dictionary)authVar;
        if (d.ContainsKey("localid")) return ((Variant)d["localid"]).AsString();
        if (d.ContainsKey("localId")) return ((Variant)d["localId"]).AsString();
        return "";
    }
}
