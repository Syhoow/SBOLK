using Godot;
public partial class Pot : Area2D
{
    [Export] public float HealAmount = 25f;
    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }
    private void OnBodyEntered(Node2D body)
    {
        if (body is Player player)
        {
            player.Heal(HealAmount);
            QueueFree();
        }
    }
}