using Godot;

public partial class GameControl : Node2D
{
    public static GameControl Instance;
    [Export] public PackedScene EnemyScene;
    [Export] public PackedScene Marker;
    [Export] public PackedScene Portal;
    [Export] public PackedScene HealingPotScene;
    [Export] public float HealingPotInterval = 15f;
    [Export] private float healingPotTimer = 0f;
    [Export] public int MaxHealingPots = 2;
    [Export] public float SpawnInterval = 0.5f;
    [Export] public float MarkerWarningTime = 1.5f; // how long marker shows before enemy spawns
    [Export] public int MaxEnemies = 10;
    public float goal = 300;
    public float currentGoal = 0;
    private CharacterBody2D player;
    private Arena arena;
    public ProgressBar goalBar;
    private CanvasLayer enemyLayer;
    private CanvasLayer arenaLayer;

    private float spawnTimer = 0f;

    public override void _Ready()
    {
        Instance = this;
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
             // reset goal for next round
        }

        healingPotTimer += (float)delta;
        if (healingPotTimer >= HealingPotInterval)
        {
            healingPotTimer = 0f;
            SpawnHealingPot();
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
        enemyLayer.AddChild(marker);
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

    private void SpawnHealingPot()
    {
        if (HealingPotScene == null || Arena.Instance == null) return;
        if (GetTree().GetNodesInGroup("healing_pot").Count >= MaxHealingPots) return; // ← limit check
        int side = (int)GD.RandRange(0, 4);
        Vector2 arenaPos = Arena.Instance.GlobalPosition;
        float w = Arena.Instance.ArenaWidth * 35f;
        float h = Arena.Instance.ArenaHeight * 35f;
        float offset = (float)GD.RandRange(100f, 200f);
        Vector2 spawnPos = side switch
        {
            0 => new Vector2((float)GD.RandRange(arenaPos.X, arenaPos.X + w), arenaPos.Y - offset),
            1 => new Vector2((float)GD.RandRange(arenaPos.X, arenaPos.X + w), arenaPos.Y + h + offset),
            2 => new Vector2(arenaPos.X - offset, (float)GD.RandRange(arenaPos.Y, arenaPos.Y + h)),
            _ => new Vector2(arenaPos.X + w + offset, (float)GD.RandRange(arenaPos.Y, arenaPos.Y + h)),
        };
        var pot = HealingPotScene.Instantiate<Node2D>();
        pot.GlobalPosition = spawnPos;
        pot.AddToGroup("healing_pot"); // ← tag it
        arenaLayer.AddChild(pot);
    }

    private void OnEnemyKilled(Enemy enemy)
    {
        //currentGoal += 5f; // Increase goal when enemy is killed
        goalBar.Value = currentGoal;
    }

    public void OnDropCollected()
    {
        currentGoal += 5f;
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
        if(currentGoal >= goal)
        {
            portal.GlobalPosition = position;
            arenaLayer.AddChild(portal);
            GD.Print("Portal spawned at: " + position);
        }
        
    }
}