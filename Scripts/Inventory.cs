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
        Refresh();
    }

    public void Refresh()
    {
        equippedLabel.Text = $"Equipped: {CosmeticManager.Instance.EquippedHat}";

        foreach (Node child in itemGrid.GetChildren())
            child.QueueFree();

        foreach (var id in CosmeticManager.Instance.OwnedIds)
        {
            var item = InventoryItemScene.Instantiate<InventoryItem>();
            item.Setup(id);
            item.OnEquipped += Refresh;
            itemGrid.AddChild(item);
        }
    }

    private void _on_button_pressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/titlescreen.tscn");
    }
}