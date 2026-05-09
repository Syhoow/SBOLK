using Godot;

public partial class MeleeSwing : Node2D
{
    [Export] public float Damage { get; set; } = 90f;
    [Export] public float KnockbackForce { get; set; } = 420f;
    [Export] public float KnockbackDuration { get; set; } = 0.22f;

    private float _lifeRemaining = 0.08f;
    private Vector2 _velocity = Vector2.Zero;

    public void Initialize(Vector2 direction, float travelDistance, float activeTime)
    {
        var safeDirection = direction.Normalized();
        Rotation = safeDirection.Angle();

        // Prefer computing lifetime from an AnimatedSprite2D's frames so the
        // visual animation can complete before the hitbox is freed.
        var animNode = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (animNode != null && animNode.SpriteFrames != null)
        {
            var animName = animNode.Animation;
            if (string.IsNullOrEmpty(animName))
            {
                animName = "default";
            }

            int frameCount = 1;
            double fps = 8.0;
            // Safe calls in case the SpriteFrames resource doesn't have the methods
            try
            {
                frameCount = animNode.SpriteFrames.GetFrameCount(animName);
                fps = animNode.SpriteFrames.GetAnimationSpeed(animName);
            }
            catch
            {
                // Fall back to provided activeTime
            }

            if (frameCount > 0 && fps > 0.001f)
            {
                double duration = (double)frameCount / (double)fps;
                _lifeRemaining = (float)duration;

                // Start on a random frame to add variation to the slash effect
                try
                {
                    var rnd = (int)(GD.Randi() % (uint)frameCount);
                    animNode.Frame = rnd;
                }
                catch
                {
                    // ignore if frame selection fails
                }
            }
            else
            {
                _lifeRemaining = Mathf.Max(0.02f, activeTime);
            }

            animNode.Play(animName);
        }
        else
        {
            _lifeRemaining = Mathf.Max(0.02f, activeTime);
        }

        // Play the slash wave effect (AnimationPlayer handles scale/fade)
        var animPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        animPlayer?.Play("SlashEffect");
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _lifeRemaining -= dt;

        if (_lifeRemaining <= 0f)
        {
            QueueFree();
        }
    }
}
