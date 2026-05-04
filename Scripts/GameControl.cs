using Godot;

public partial class GameControl : Node2D
{
    [Export] public PackedScene EnemyScene;
    [Export] public PackedScene Marker;
    [Export] public float SpawnInterval = 0.5f;
    [Export] public float MarkerWarningTime = 1.5f; // how long marker shows before enemy spawns
    [Export] public int MaxEnemies = 10;
    private CharacterBody2D player;
    private Arena arena;

    private float spawnTimer = 0f;

    public override void _Ready()
    {
        player = GetNode<CharacterBody2D>("/root/Main/Player");
        arena = GetNode<Arena>("/root/Main/Arena");
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

        AddChild(enemy);
        enemy.AddToGroup("enemy");

        // Remove marker after enemy spawns
        if (IsInstanceValid(marker))
            ((Node2D)marker).QueueFree();
    }
}