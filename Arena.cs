using Godot;

public partial class Arena : Node2D
{
    [Export] public Vector2 ArenaSize = new Vector2(1000, 1000);
    [Export] public Color FloorColor = new Color(0.15f, 0.15f, 0.15f);

    public override void _Ready()
    {
        CreateFloor();
        CreateKillZone();
    }

    private void CreateFloor()
    {
        var floor = new ColorRect();
        floor.Size = ArenaSize;
        floor.Position = -ArenaSize / 2;
        floor.Color = FloorColor;
        AddChild(floor);
    }

    private void CreateKillZone()
    {
        var killZone = new Area2D();
        var collision = new CollisionShape2D();
        var shape = new RectangleShape2D();

        shape.Size = ArenaSize + new Vector2(200, 200);
        collision.Shape = shape;

        killZone.AddChild(collision);
        AddChild(killZone);

        killZone.BodyExited += OnBodyExitedArena;
    }

    private void OnBodyExitedArena(Node2D body)
    {
        if (body is Player player)
        {
            player.GlobalPosition = Vector2.Zero;
            // or player.Die();
        }
    }
}