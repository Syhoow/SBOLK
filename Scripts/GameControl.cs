using Godot;

public partial class GameControl : Node2D
{
    [Export] public PackedScene EnemyScene;
    [Export] public PackedScene Marker;
    [Export] public PackedScene Portal;
    [Export] public float SpawnInterval = 0.5f;
    [Export] public float MarkerWarningTime = 1.5f; // how long marker shows before enemy spawns
    [Export] public int MaxEnemies = 10;
    public float goal = 300;
    public float currentGoal = 0;
    private CharacterBody2D player;
    private Arena arena;
    private ProgressBar goalBar;
    private CanvasLayer enemyLayer;
    private CanvasLayer arenaLayer;

    private float spawnTimer = 0f;

    public override void _Ready()
    {
        player = GetNode<CharacterBody2D>("/root/Main/ArenaLayer/Player");
        arena = GetNode<Arena>("/root/Main/ArenaLayer/Arena");
        goalBar = GetNode<ProgressBar>("HUD/Goal");
        enemyLayer = GetNode<CanvasLayer>("/root/Main/EnemyLayer");
        arenaLayer = GetNode<CanvasLayer>("/root/Main/ArenaLayer");

        player.GlobalPosition = arena.ToGlobal(new Vector2(arena.ArenaWidth * 25f / 2, (arena.ArenaHeight - 1) * 25f / 2));
    }

    public override void _Process(double delta)
    {
        spawnTimer += (float)delta;
        if (spawnTimer >= SpawnInterval)
        {
            spawnTimer = 0f;
            SpawnWithWarning();
        }
        if(currentGoal >= goal)
        {
            GD.Print("Goal reached! You win!");
            // Random position
            Vector2 min = Arena.Instance.GlobalPosition + new Vector2(35f, 35f); // avoid spawning on walls
            Vector2 max = Arena.Instance.GlobalPosition + new Vector2(
                Arena.Instance.ArenaWidth * 35f,
                (Arena.Instance.ArenaHeight - 1) * 35f
            );

            Vector2 spawnPos = new Vector2(
                (float)GD.RandRange(min.X, max.X),
                (float)GD.RandRange(min.Y, max.Y)
            );
            SpawnPortal(spawnPos);
            currentGoal = 0; // reset goal for next round
        }

    }

    private async void SpawnWithWarning()
    {
        if (GetTree().GetNodesInGroup("enemy").Count >= MaxEnemies) return;
        if (Arena.Instance == null) return;

        // Random position
        Vector2 min = Arena.Instance.GlobalPosition + new Vector2(35f, 35f); // avoid spawning on walls
        Vector2 max = Arena.Instance.GlobalPosition + new Vector2(
            Arena.Instance.ArenaWidth * 35f,
            (Arena.Instance.ArenaHeight - 1) * 35f
        );

        Vector2 spawnPos = new Vector2(
            (float)GD.RandRange(min.X, max.X),
            (float)GD.RandRange(min.Y, max.Y)
        );

        // Spawn marker first
        var marker = Marker.Instantiate();
        ((Node2D)marker).GlobalPosition = spawnPos;
        AddChild(marker);
        marker.AddToGroup("marker");

        // Wait before spawning enemy
        await ToSignal(GetTree().CreateTimer(MarkerWarningTime), "timeout");

        if (!IsInstanceValid(this)) return;

        var enemy = EnemyScene.Instantiate<Enemy>();
        var types = (Enemy.EnemyType[])System.Enum.GetValues(typeof(Enemy.EnemyType));
        enemy.Type = types[(int)GD.RandRange(0,types.Length - 1)];
        enemy.GlobalPosition = spawnPos;

        enemyLayer.AddChild(enemy);
        enemy.AddToGroup("enemy");

        enemy.TreeExited += () => OnEnemyKilled(enemy);

        // Remove marker after enemy spawns
        if (IsInstanceValid(marker))
            ((Node2D)marker).QueueFree();
    }

    private void OnEnemyKilled(Enemy enemy)
    {
        currentGoal += 5f; // Increase goal when enemy is killed
        goalBar.Value = currentGoal;
    }

    private void SpawnPortal(Vector2 position)
    {
        if (Portal == null)
        {
            GD.PrintErr("Portal scene not assigned!");
            return;
        }
        var portal = Portal.Instantiate<Node2D>();
        if (portal == null)
        {
            GD.PrintErr("Failed to instantiate portal!");
            return;
        }
        portal.GlobalPosition = position;
        arenaLayer.AddChild(portal);
        GD.Print("Portal spawned at: " + position);
    }
}