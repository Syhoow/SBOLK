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
    private const float TileSize = 37.5f; 
    private Vector2I FloorTile = new Vector2I(1, 1);
    public static Arena Instance;
    private CollisionShape2D killZoneCollision;

    public override void _Ready()
    {
        Instance = this;
        GenerateArena();
        CreateKillZone();
    }

    public override void _Process(double delta)
    {
        Vector2 mouse = GetGlobalMousePosition();
        Vector2 arenaPixelSize = new Vector2(ArenaWidth * TileSize, (ArenaHeight - 1) * TileSize);
        ismouseInside = mouse.X >= GlobalPosition.X &&
                        mouse.Y >= GlobalPosition.Y &&
                        mouse.X <= GlobalPosition.X + arenaPixelSize.X &&
                        mouse.Y <= GlobalPosition.Y + arenaPixelSize.Y;

        if (isOutside)
        {
		    Player.Instance.animPlayer.Play("PlayerOutside");
            isoutsideTimer -= (float)delta;
            Modulate = new Color(1f, 0f, 0f);
            if (ismouseInside && Input.IsActionJustPressed("dash"))
            {
                var player = GetTree().Root.GetNode<Player>("/root/Main/EnemyLayer/Player");
                player.GlobalPosition = GetGlobalMousePosition();
                Player.Instance.damageOverlay.Visible = true;
                var tween = CreateTween();
                tween.TweenInterval(0.2f); 
                tween.TweenCallback(Callable.From(() => Player.Instance.damageOverlay.Visible = false));
                Player.Instance.health -= 5; 
            }
            
            if (isoutsideTimer <= 0f)
            {
                isEliminated = true;
                Player.Instance.health = 0; 
                GD.Print("player out!");
            }
            
        }
        else
        {
            Player.Instance.animPlayer.Play("Player");
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

    }

    private void CreateKillZone()
    {
        var killZone = new Area2D();
        killZoneCollision = new CollisionShape2D();
        var shape = new RectangleShape2D();

        Vector2 arenaPixelSize = new Vector2(ArenaWidth * TileSize, (ArenaHeight - 1) * TileSize);
        shape.Size = arenaPixelSize;

        killZoneCollision.Shape = shape;
        killZoneCollision.Position = arenaPixelSize / 2;

        killZone.GlobalPosition = GlobalPosition;
        killZone.Monitoring = true;
        killZone.Monitorable = true;
        killZone.InputPickable = false;

        killZone.AddChild(killZoneCollision);
        killZone.Ready += () => killZone.GlobalPosition = GlobalPosition;
        GetParent().CallDeferred("add_child", killZone);

        killZone.BodyExited += OnBodyExitedArena;
        killZone.BodyEntered += OnBodyEnteredArena;
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

    public void Rebuild()
    {
        Clear();
        GenerateArena();

        Vector2 arenaPixelSize = new Vector2(ArenaWidth * TileSize, (ArenaHeight - 1) * TileSize);
        var shape = new RectangleShape2D();
        shape.Size = arenaPixelSize;
        killZoneCollision.Shape = shape;
        killZoneCollision.Position = arenaPixelSize / 2;
    }
}