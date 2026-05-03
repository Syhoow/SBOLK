using Godot;

public partial class Arena : TileMapLayer
{
    [Export] public int ArenaWidth = 20;
    [Export] public int ArenaHeight = 12;
    

    private const int SourceId = 1;
    private Vector2I FloorTile = new Vector2I(1, 1);
    public static Arena Instance;

    public override void _Ready()
    {
        Instance = this;
        GenerateArena();
    }

    private void GenerateArena()
    {
        for (int x = 0; x < ArenaWidth; x++)
        {
            for (int y = 0; y < ArenaHeight; y++)
            {
                Vector2I tile;

                if (x == 0 && y == 0)
                {
                    tile = new Vector2I(0, 0); // top-left corner
                }
                else if (x == ArenaWidth - 1 && y == 0)
                {
                    tile = new Vector2I(2, 0); // top-right corner
                }
                else if (x == 0 && y == ArenaHeight - 1)
                {
                    tile = new Vector2I(0, 3); // bottom-left corner
                }
                else if (x == ArenaWidth - 1 && y == ArenaHeight - 1)
                {
                    tile = new Vector2I(2, 3); // bottom-right corner
                }
                else if (x == 0 && y == ArenaHeight - 2)
                {
                    tile = new Vector2I(0, 2); // bottom-left transition
                }
                else if (x == ArenaWidth - 1 && y == ArenaHeight - 2)
                {
                    tile = new Vector2I(2, 2); // bottom-right transition
                }
                else if (y == ArenaHeight - 2)
                {
                    tile = new Vector2I(1, 2); // bottom transition
                }
                else if (y == 0)
                {
                    tile = new Vector2I(1, 0); // top edge
                }
                else if (y == ArenaHeight - 1)
                {
                    tile = new Vector2I(1, 3); // bottom edge
                }
                else if (x == 0)
                {
                    tile = new Vector2I(0, 1); // left edge
                }
                else if (x == ArenaWidth - 1)
                {
                    tile = new Vector2I(2, 1); // right edge
                }
                else
                {
                    tile = new Vector2I(1, 1); // center
                }

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

        Vector2 arenaPixelSize = new Vector2(ArenaWidth * 25f, (ArenaHeight-1) * 25f);
        shape.Size = arenaPixelSize;

        collision.Shape = shape;
        killZone.Position = arenaPixelSize / 2;

        killZone.AddChild(collision);
        AddChild(killZone);

        killZone.BodyExited += OnBodyExitedArena;
    }

    private void OnBodyExitedArena(Node2D body)
    {
        if (body is Player player)
        {
            player.GlobalPosition = new Vector2(ArenaWidth * 25f / 2, ArenaHeight * 25f / 2);
        }
    }
}