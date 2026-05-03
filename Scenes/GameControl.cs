using Godot;

public partial class GameControl : Node2D
{
    [Export] public PackedScene EnemyScene;
    [Export] public float SpawnInterval = 3.0f;
    [Export] public int MaxEnemies = 100;

    private float spawnTimer = 0f;

    public override void _Process(double delta)
    {
        spawnTimer += (float)delta;

        if (spawnTimer >= SpawnInterval)
        {
            spawnTimer = 0f;
            SpawnEnemy();
        }
    }

    private void SpawnEnemy()
    {
        if (GetTree().GetNodesInGroup("enemy").Count >= MaxEnemies) return;
        if (Arena.Instance == null) return;

        var enemy = EnemyScene.Instantiate<Enemy>();

        // Random type
        var types = (Enemy.EnemyType[])System.Enum.GetValues(typeof(Enemy.EnemyType));
        enemy.Type = 0;//types[(int)GD.RandRange(0, types.Length-1)];

        // Random position inside arena
        Vector2 min = Arena.Instance.GlobalPosition;
        Vector2 max = Arena.Instance.GlobalPosition + new Vector2(
            Arena.Instance.ArenaWidth * 35f,
            (Arena.Instance.ArenaHeight - 1) * 35f
        );

        enemy.GlobalPosition = new Vector2(
            (float)GD.RandRange(min.X, max.X),
            (float)GD.RandRange(min.Y, max.Y)
        );

        AddChild(enemy);
        enemy.AddToGroup("enemy");
    }
}