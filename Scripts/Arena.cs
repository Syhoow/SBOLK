using Godot;

public partial class Arena : TileMapLayer
{
    [Export] public int ArenaWidth = 20;
    [Export] public int ArenaHeight = 12;
    public float isoutsideDuration = 3f;
    public float isoutsideTimer = 0f;
    public bool isOutside = false;
    public bool ismouseInside = false;
    public bool isEliminated = false;

    private const int SourceId = 1;
    private Vector2I FloorTile = new Vector2I(1, 1);
    public static Arena Instance;

    public override void _Ready()
    {
        Instance = this;
        GenerateArena();
    }

    public override void _Process(double delta)
    {
        if (isOutside)
        {
            isoutsideTimer -= (float)delta;
            Modulate = new Color(1f, 0f, 0f);
            if(ismouseInside && Input.IsActionJustPressed("dash"))
            {
                var player = GetTree().Root.GetNode<Player>("/root/Main/ArenaLayer/Player");
                player.GlobalPosition = GetGlobalMousePosition();
            }
            if (isoutsideTimer <= 0f)
            {
                isEliminated = true;
                GD.Print("player out!");
            }
        }
        else
        {
            Modulate = new Color(1f, 1f, 1f);
        }

    }

    public void GenerateArena()
    {
        for (int x = 0; x < ArenaWidth; x++)
        {
            for (int y = 0; y < ArenaHeight; y++)
            {
                Vector2I tile;

                if (x == 0 && y == 0)
                    tile = new Vector2I(0, 0);
                else if (x == ArenaWidth - 1 && y == 0)
                    tile = new Vector2I(2, 0);
                else if (x == 0 && y == ArenaHeight - 1)
                    tile = new Vector2I(0, 3);
                else if (x == ArenaWidth - 1 && y == ArenaHeight - 1)
                    tile = new Vector2I(2, 3);
                else if (x == 0 && y == ArenaHeight - 2)
                    tile = new Vector2I(0, 2);
                else if (x == ArenaWidth - 1 && y == ArenaHeight - 2)
                    tile = new Vector2I(2, 2);
                else if (y == ArenaHeight - 2)
                    tile = new Vector2I(1, 2);
                else if (y == 0)
                    tile = new Vector2I(1, 0);
                else if (y == ArenaHeight - 1)
                    tile = new Vector2I(1, 3);
                else if (x == 0)
                    tile = new Vector2I(0, 1);
                else if (x == ArenaWidth - 1)
                    tile = new Vector2I(2, 1);
                else
                    tile = new Vector2I(1, 1);

                SetCell(new Vector2I(x, y), SourceId, tile);
            }
        }

        CreateKillZone();
    }

    private void CreateKillZone()
    {
        var killZone = new Area2D();
        var collision = new CollisionShape2D();
        var shape = new RectangleShape2D();

        Vector2 arenaPixelSize = new Vector2(ArenaWidth * 25f, (ArenaHeight - 1) * 25f);
        shape.Size = arenaPixelSize;

        collision.Shape = shape;
        collision.Position = arenaPixelSize / 2;
        killZone.Position = Vector2.Zero;

        killZone.Monitoring = true;
        killZone.Monitorable = true;
        killZone.InputPickable = true;

        killZone.AddChild(collision);
        AddChild(killZone);

        killZone.BodyExited += OnBodyExitedArena;
        killZone.BodyEntered += OnBodyEnteredArena;
        killZone.MouseEntered += OnMouseEnteredArena;
        killZone.MouseExited += OnMouseExitedArena;
    }

    private void OnBodyExitedArena(Node2D body)
    {
        if (body is Player)
        {
            isOutside = true;
            isoutsideTimer = isoutsideDuration;
        }
    }

    private void OnBodyEnteredArena(Node2D body)
    {
        if (body is Player)
        {
            isOutside = false;
            isoutsideTimer = 0f;
        }
    }

    private void OnMouseEnteredArena()
    {
        ismouseInside = true;
        GD.Print("mouseinside");
    }

    private void OnMouseExitedArena()
    {
        ismouseInside = false;
        GD.Print("mouseoutside");
    }
}