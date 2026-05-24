using Godot;
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

    private List<string> _sellableItemIds = new();
    private List<string> _sellableItemNames = new();

    public override void _Ready()
    {
        _coinsLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/TopBar/CoinsLabel");
        _listingsContainer = GetNodeOrNull<VBoxContainer>("MarginContainer/VBoxContainer/ScrollContainer/ListingsContainer");
        _postPanel = GetNodeOrNull<Panel>("PostPanel");
        _itemSelector = GetNodeOrNull<OptionButton>("PostPanel/VBoxContainer/ItemSelector");
        _priceInput = GetNodeOrNull<SpinBox>("PostPanel/VBoxContainer/PriceRow/PriceInput");
        _qtyInput = GetNodeOrNull<SpinBox>("PostPanel/VBoxContainer/QtyRow/QtyInput");
        _statusLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/TopBar/StatusLabel");
        _postButton = GetNodeOrNull<Button>("MarginContainer/VBoxContainer/TopBar/PostButton");
        _refreshButton = GetNodeOrNull<Button>("MarginContainer/VBoxContainer/TopBar/RefreshButton");

        if (_postPanel != null) _postPanel.Visible = false;

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
        _coinsLabel.Text = $"Coins: {coins}";
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
            item.OnBuyPressed += OnListingBuyPressed;
            _listingsContainer.AddChild(item);  // _Ready() runs here, initialising all labels
            var history = MarketplaceManager.Instance.GetPriceHistory(listing.ItemId);
            item.Setup(listing, history);        // labels now exist
        }
    }

    private void OnListingBuyPressed(string listingId)
    {
        if (MarketplaceManager.Instance == null) return;
        bool success = MarketplaceManager.Instance.BuyListing(listingId);
        ShowStatus(success ? "Purchased!" : "Cannot buy (check coins or listing).");
        Refresh();
    }

    private void ShowStatus(string msg)
    {
        if (_statusLabel == null) return;
        _statusLabel.Text = msg;
    }

    private void _on_PostButton_pressed()
    {
        if (_postPanel == null) return;
        PopulateItemSelector();
        _postPanel.Visible = true;
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
    }

    private void _on_ConfirmPostButton_pressed()
    {
        if (_sellableItemIds.Count == 0) { ShowStatus("No items to sell."); return; }
        if (_itemSelector == null || _priceInput == null || _qtyInput == null) return;

        int idx = _itemSelector.Selected;
        if (idx < 0 || idx >= _sellableItemIds.Count) return;

        string itemId = _sellableItemIds[idx];
        string itemName = _sellableItemNames[idx];
        int price = (int)_priceInput.Value;
        int qty = (int)_qtyInput.Value;

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
