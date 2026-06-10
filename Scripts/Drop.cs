using Godot;
public partial class Drop : Area2D
{
    [Export] public float MagnetSpeed = 150f;      
    [Export] public float MaxSpeed = 5000f;        
    [Export] public float LaunchSpeed = 400f;      
    [Export] public new float Gravity = 0f;        
    [Export] public float SettleTime = 0.1f;       
    [Export] public float AccelerationRate = 10000f; 
    public static Drop Instance;
    private CharacterBody2D player;
    private Vector2 velocity = Vector2.Zero;
    private bool isSettled = false;
    private float settleTimer = 0f;
    private float magnetTime = 0f;  
    public override void _Ready()
    {
        Instance = this;
        player = GetNode<CharacterBody2D>("/root/Main/EnemyLayer/Player");
        Vector2 awayDirection = (GlobalPosition - player.GlobalPosition).Normalized();
        awayDirection = (awayDirection + Vector2.Up * 0.5f).Normalized();
        velocity = awayDirection * LaunchSpeed;
        BodyEntered += OnBodyEntered;
    }
    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (!isSettled)
        {
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
            
            magnetTime += dt;
            
            float currentSpeed = Mathf.Min(MagnetSpeed + AccelerationRate * magnetTime, MaxSpeed);
            Vector2 direction = (player.GlobalPosition - GlobalPosition).Normalized();
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