using Godot;
using System;
using System.Collections.Generic;

public partial class Marketplace : Control
{
    [Export] public PackedScene ListingItemScene;

    private Label _coinsLabel;
    private VBoxContainer _listingsContainer;
    private Panel _postPanel;
    private OptionButton _itemSelector;
    private SpinBox _priceInput;
    private SpinBox _qtyInput;
    private Label _statusLabel;
    private Button _postButton;
    private Button _refreshButton;
    private PriceChart _postChart;
    private TextureRect _postIcon;

    private Panel _historyPanel;
    private VBoxContainer _historyContainer;

    private List<string> _sellableItemIds   = new();
    private List<string> _sellableItemNames = new();

    public override void _Ready()
    {
        _coinsLabel        = GetNodeOrNull<Label>        ("MarginContainer/VBoxContainer/TopBar/CoinsLabel");
        _listingsContainer = GetNodeOrNull<VBoxContainer>("MarginContainer/VBoxContainer/ScrollContainer/ListingsContainer");
        _postPanel         = GetNodeOrNull<Panel>        ("PostPanel");
        _itemSelector      = GetNodeOrNull<OptionButton> ("PostPanel/VBoxContainer/ItemSelectorRow/ItemSelector");
        _postIcon          = GetNodeOrNull<TextureRect>  ("PostPanel/VBoxContainer/ItemSelectorRow/PostItemIcon");
        _priceInput        = GetNodeOrNull<SpinBox>      ("PostPanel/VBoxContainer/PriceRow/PriceInput");
        _qtyInput          = GetNodeOrNull<SpinBox>      ("PostPanel/VBoxContainer/QtyRow/QtyInput");
        _statusLabel       = GetNodeOrNull<Label>        ("MarginContainer/VBoxContainer/TopBar/StatusLabel");
        _postButton        = GetNodeOrNull<Button>       ("MarginContainer/VBoxContainer/TopBar/PostButton");
        _refreshButton     = GetNodeOrNull<Button>       ("MarginContainer/VBoxContainer/TopBar/RefreshButton");
        _postChart         = GetNodeOrNull<PriceChart>   ("PostPanel/VBoxContainer/PostPriceChart");
        _historyPanel      = GetNodeOrNull<Panel>        ("HistoryPanel");
        _historyContainer  = GetNodeOrNull<VBoxContainer>("HistoryPanel/VBoxContainer/ScrollContainer/HistoryContainer");

        if (_postPanel    != null) _postPanel.Visible    = false;
        if (_historyPanel != null) _historyPanel.Visible = false;

        if (_postIcon != null)
            _postIcon.Texture = CosmeticManager.LoadItemIcon("");

        if (_itemSelector != null)
            _itemSelector.ItemSelected += OnPostItemSelected;

        if (MarketplaceManager.Instance != null)
            MarketplaceManager.Instance.DataChanged += Refresh;

        Refresh();
    }

    public override void _ExitTree()
    {
        if (MarketplaceManager.Instance != null)
            MarketplaceManager.Instance.DataChanged -= Refresh;
    }

    public void Refresh()
    {
        UpdateCoinsLabel();
        RebuildListings();
    }

    private void UpdateCoinsLabel()
    {
        if (_coinsLabel == null) return;
        var coins = CosmeticManager.Instance != null ? CosmeticManager.Instance.Coins : 0;
        _coinsLabel.Text = $"Money: {coins}";
    }

    private void RebuildListings()
    {
        if (_listingsContainer == null) return;

        foreach (Node child in _listingsContainer.GetChildren())
            child.QueueFree();

        if (MarketplaceManager.Instance == null) return;
        var listings = MarketplaceManager.Instance.Listings;

        if (listings.Count == 0)
        {
            var emptyLabel = new Label();
            emptyLabel.Text = "No listings yet. Be the first to post!";
            emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _listingsContainer.AddChild(emptyLabel);
            return;
        }

        foreach (var listing in listings)
        {
            if (ListingItemScene == null) continue;
            var item = ListingItemScene.Instantiate<MarketplaceListing>();
            item.OnBuyPressed    += OnListingBuyPressed;
            item.OnCancelPressed += OnListingCancelPressed;
            _listingsContainer.AddChild(item);
            var history = MarketplaceManager.Instance.GetPriceHistory(listing.ItemId);
            item.Setup(listing, history);
        }
    }

    private void OnListingBuyPressed(string listingId)
    {
        if (MarketplaceManager.Instance == null) return;
        bool success = MarketplaceManager.Instance.BuyListing(listingId);
        ShowStatus(success ? "Purchased!" : "Cannot buy (check coins or listing).");
        Refresh();
    }

    private void OnListingCancelPressed(string listingId)
    {
        if (MarketplaceManager.Instance == null) return;
        bool success = MarketplaceManager.Instance.CancelListing(listingId);
        ShowStatus(success ? "Listing cancelled — item returned to inventory." : "Could not cancel listing.");
        Refresh();
    }

    private void ShowStatus(string msg)
    {
        if (_statusLabel == null) return;
        _statusLabel.Text = msg;
    }

    // ─── Post panel ──────────────────────────────────────────────────────────

    private void _on_PostButton_pressed()
    {
        if (_postPanel == null) return;
        PopulateItemSelector();
        _postPanel.Visible = true;
        RefreshPostChart();
    }

    private void PopulateItemSelector()
    {
        _sellableItemIds.Clear();
        _sellableItemNames.Clear();
        if (_itemSelector == null) return;
        _itemSelector.Clear();

        if (CosmeticManager.Instance == null) return;
        foreach (var cosmetic in CosmeticManager.Instance.AllCosmetics)
        {
            int owned = CosmeticManager.Instance.GetOwnedCount(cosmetic.Id);
            if (owned <= 0) continue;
            _sellableItemIds.Add(cosmetic.Id);
            _sellableItemNames.Add(cosmetic.Name);
            _itemSelector.AddItem($"{cosmetic.Name} (x{owned})");
        }

        if (_sellableItemIds.Count == 0)
            _itemSelector.AddItem("Nothing to sell");

        UpdatePostIcon();
    }

    private void OnPostItemSelected(long index)
    {
        RefreshPostChart();
        UpdatePostIcon();
    }

    private void UpdatePostIcon()
    {
        if (_postIcon == null) return;
        int idx = _itemSelector != null ? _itemSelector.Selected : -1;
        string id = (idx >= 0 && idx < _sellableItemIds.Count) ? _sellableItemIds[idx] : "";
        _postIcon.Texture = CosmeticManager.LoadItemIcon(id);
    }

    private void RefreshPostChart()
    {
        if (_postChart == null || MarketplaceManager.Instance == null) return;
        int idx = _itemSelector != null ? _itemSelector.Selected : -1;
        if (idx < 0 || idx >= _sellableItemIds.Count)
        {
            _postChart.SetPrices(new List<float>());
            return;
        }
        string itemId = _sellableItemIds[idx];
        var history   = MarketplaceManager.Instance.GetPriceHistory(itemId);
        var prices    = new List<float>();
        foreach (var record in history)
            prices.Add(record.Price);
        _postChart.SetPrices(prices);
    }

    private void _on_ConfirmPostButton_pressed()
    {
        if (_sellableItemIds.Count == 0) { ShowStatus("No items to sell."); return; }
        if (_itemSelector == null || _priceInput == null || _qtyInput == null) return;
        int idx = _itemSelector.Selected;
        if (idx < 0 || idx >= _sellableItemIds.Count) return;

        string itemId   = _sellableItemIds[idx];
        string itemName = _sellableItemNames[idx];
        int price       = (int)_priceInput.Value;
        int qty         = (int)_qtyInput.Value;

        if (MarketplaceManager.Instance == null) return;
        bool success = MarketplaceManager.Instance.PostListing(itemId, itemName, price, qty);
        ShowStatus(success ? "Listing posted!" : "Failed to post (check quantity/price).");
        if (_postPanel != null) _postPanel.Visible = false;
        Refresh();
    }

    private void _on_CancelPostButton_pressed()
    {
        if (_postPanel != null) _postPanel.Visible = false;
    }

    // ─── History panel ───────────────────────────────────────────────────────

    private void _on_HistoryButton_pressed()
    {
        if (_historyPanel == null) return;
        RebuildHistory();
        _historyPanel.Visible = true;
    }

    private void _on_HistoryCloseButton_pressed()
    {
        if (_historyPanel != null) _historyPanel.Visible = false;
    }

    private void RebuildHistory()
    {
        if (_historyContainer == null || MarketplaceManager.Instance == null) return;

        foreach (Node child in _historyContainer.GetChildren())
            child.QueueFree();

        string myUid     = MarketplaceManager.Instance.MyUid;
        var    allHistory = MarketplaceManager.Instance.PriceHistory;

        // Build flat list of only THIS player's trades, sorted newest-first
        var myTrades = new List<(string ItemId, string ItemName, int Price, double Timestamp, string Role)>();
        foreach (var kv in allHistory)
        {
            string itemId   = kv.Key;
            string itemName = itemId;
            if (CosmeticManager.Instance != null)
            {
                foreach (var c in CosmeticManager.Instance.AllCosmetics)
                    if (c.Id == itemId) { itemName = c.Name; break; }
            }
            foreach (var record in kv.Value)
            {
                bool isBuyer  = !string.IsNullOrEmpty(myUid) && record.BuyerUid  == myUid;
                bool isSeller = !string.IsNullOrEmpty(myUid) && record.SellerUid == myUid;

                if (isBuyer)
                    myTrades.Add((itemId, itemName, record.Price, record.Timestamp, "Bought"));
                else if (isSeller)
                    myTrades.Add((itemId, itemName, record.Price, record.Timestamp, "Sold"));
            }
        }

        if (myTrades.Count == 0)
        {
            var empty = new Label();
            empty.Text = "You have no trade history yet.";
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            _historyContainer.AddChild(empty);
            return;
        }

        myTrades.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));

        // Header row
        var header = MakeHistoryRow("", "Item", "Price", "Date / Time", "Type", true);
        _historyContainer.AddChild(header);

        foreach (var trade in myTrades)
        {
            var    dt      = DateTimeOffset.FromUnixTimeSeconds((long)trade.Timestamp).ToLocalTime();
            string dateStr = dt.ToString("yyyy-MM-dd  HH:mm");
            var    row     = MakeHistoryRow(trade.ItemId, trade.ItemName, $"{trade.Price}c", dateStr, trade.Role, false);
            _historyContainer.AddChild(row);
        }
    }

    private static HBoxContainer MakeHistoryRow(string itemId, string item, string price, string date, string role, bool isHeader)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);

        var icon = new TextureRect();
        icon.CustomMinimumSize = new Vector2(24, 24);
        icon.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        if (!isHeader)
            icon.Texture = CosmeticManager.LoadItemIcon(itemId);
        row.AddChild(icon);

        Label MakeLabel(string text, float grow)
        {
            var l = new Label();
            l.Text = text;
            l.SizeFlagsHorizontal = (SizeFlags)(grow > 0 ? (int)SizeFlags.Expand | (int)SizeFlags.Fill : (int)SizeFlags.Fill);
            if (isHeader) l.AddThemeFontSizeOverride("font_size", 12);
            return l;
        }

        var roleLabel = MakeLabel(role, 1);
        if (!isHeader)
        {
            roleLabel.AddThemeColorOverride("font_color",
                role == "Bought" ? new Color(0.2f, 0.9f, 0.4f) : new Color(1f, 0.6f, 0.1f));
        }

        row.AddChild(MakeLabel(item,  3));
        row.AddChild(MakeLabel(price, 1));
        row.AddChild(MakeLabel(date,  2));
        row.AddChild(roleLabel);
        return row;
    }

    // ─── Toolbar buttons ─────────────────────────────────────────────────────

    private void _on_RefreshButton_pressed()
    {
        ShowStatus("Refreshing...");
        MarketplaceManager.Instance?.TryLoadFromCloud();
    }

    private void _on_CloseButton_pressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/titlescreen.tscn");
    }
}
