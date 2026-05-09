using Godot;
public partial class Drop : Area2D
{
    [Export] public float MagnetSpeed = 150f;      // Initial magnet speed
    [Export] public float MaxSpeed = 5000f;          // Maximum magnet speed
    [Export] public float LaunchSpeed = 400f;        // How fast it flies away on spawn
    [Export] public float Gravity = 0f;            // Gravity during launch phase
    [Export] public float SettleTime = 0.1f;         // Time before magnet kicks in
    [Export] public float AccelerationRate = 10000f;   // How fast it gains speed over time
    public static Drop Instance;
    private CharacterBody2D player;
    private Vector2 velocity = Vector2.Zero;
    private bool isSettled = false;
    private float settleTimer = 0f;
    private float magnetTime = 0f;  // How long it's been magneting
    public override void _Ready()
    {
        Instance = this;
        player = GetNode<CharacterBody2D>("/root/Main/ArenaLayer/Player");
        // Launch away from the player
        Vector2 awayDirection = (GlobalPosition - player.GlobalPosition).Normalized();
        // Add a slight upward arc to make it feel more natural
        awayDirection = (awayDirection + Vector2.Up * 0.5f).Normalized();
        velocity = awayDirection * LaunchSpeed;
        BodyEntered += OnBodyEntered;
    }
    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (!isSettled)
        {
            // Launch phase: apply gravity and move freely
            velocity.Y += Gravity * dt;
            GlobalPosition += velocity * dt;
            settleTimer += dt;
            if (settleTimer >= SettleTime)
            {
                isSettled = true;
                velocity = Vector2.Zero;
            }
        }
        else
        {
            // Magnet phase: accelerate toward player over time
            magnetTime += dt;
            // Speed increases the longer it hasn't been picked up
            float currentSpeed = Mathf.Min(MagnetSpeed + AccelerationRate * magnetTime, MaxSpeed);
            Vector2 direction = (player.GlobalPosition - GlobalPosition).Normalized();
            // Smoothly steer velocity toward player
            Vector2 targetVelocity = direction * currentSpeed;
            velocity = velocity.Lerp(targetVelocity, dt * 8f);
            GlobalPosition += velocity * dt;
        }
    }
    private void OnBodyEntered(Node2D body)
    {
        if (body is Player)
        {
            GameControl.Instance?.OnDropCollected();
            GD.Print("coin collected!");
            QueueFree();
        }
    }
}