using Godot;

public partial class Inventory : Control
{
    private GridContainer itemGrid;
    private Label equippedLabel;
    [Export] public PackedScene InventoryItemScene;

    public override void _Ready()
    {
        itemGrid = GetNode<GridContainer>("ItemGrid");
        equippedLabel = GetNode<Label>("EquippedLabel");
        if (CosmeticManager.Instance != null)
        {
            CosmeticManager.Instance.DataChanged += OnDataChanged;
        }
        TreeExiting += OnTreeExiting;
        Refresh();
    }

    public void Refresh()
    {
        equippedLabel.Text = $"Equipped: {CosmeticManager.Instance.EquippedHat}";

        foreach (Node child in itemGrid.GetChildren())
            child.QueueFree();

        foreach (var key in CosmeticManager.Instance.OwnedCounts.Keys)
        {
            var id = key.ToString();
            var count = CosmeticManager.Instance.GetOwnedCount(id);
            if (count <= 0)
            {
                continue;
            }
            var item = InventoryItemScene.Instantiate<InventoryItem>();
            item.Setup(id, count);
            item.OnEquipped += Refresh;
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