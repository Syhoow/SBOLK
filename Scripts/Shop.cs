using Godot;

public partial class Shop : Control
{
    private Label coinsLabel;
    private Label timerLabel;
    private GridContainer itemGrid;
    [Export] public PackedScene ShopItemScene;

    public override void _Ready()
    {
        coinsLabel = GetNode<Label>("CoinsLabel");
        timerLabel = GetNode<Label>("TimerLabel");
        itemGrid = GetNode<GridContainer>("ItemGrid");
        if (CosmeticManager.Instance != null)
        {
            CosmeticManager.Instance.DataChanged += OnDataChanged;
        }
        TreeExiting += OnTreeExiting;
        Refresh();
    }

    public override void _Process(double delta)
    {
        timerLabel.Text = $"Refreshes in: {CosmeticManager.Instance.GetTimeRemainingText()}";
        coinsLabel.Text = $"Money: {CosmeticManager.Instance.Coins}";

        if (CosmeticManager.Instance.OfferTimeRemaining <= 0)
            Refresh();
    }

    public void Refresh()
    {
        foreach (Node child in itemGrid.GetChildren())
            child.QueueFree();

        foreach (int index in CosmeticManager.Instance.CurrentOfferIndices)
        {
            var cosmetic = CosmeticManager.Instance.AllCosmetics[index];
            var item = ShopItemScene.Instantiate<ShopItem>();
            item.Setup(cosmetic);
            item.OnPurchased += Refresh;
            itemGrid.AddChild(item);
        }
    }

    private void _on_button_pressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/titlescreen.tscn");
    }

    private void OnDataChanged()
    {
        Refresh();
    }

    private void OnTreeExiting()
    {
        if (CosmeticManager.Instance != null)
        {
            CosmeticManager.Instance.DataChanged -= OnDataChanged;
        }
    }
}