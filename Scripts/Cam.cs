using Godot;
using System;

public partial class Cam : Camera2D
{
	private Vector2 desiredOffset; 
	private float minOffset = -200f; 
	private float maxOffset = 200f;
	private float shakeIntensity = 0.0f;
	private float activeShakeTime = 0.0f;
	private float shakeDecay = 5.0f;
	private float shakeTime = 0.0f;
	private float shakeTimeSpeed = 20.0f;
	private FastNoiseLite noise = new FastNoiseLite();
	public static Cam Instance;

	public override void _Ready()
	{
		Instance = this;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _Process(double delta) 
	{ 
		float dt = (float)delta;
		Input.MouseMode = Input.MouseModeEnum.Confined;

		Node2D player = GetParent().GetNode<Node2D>(".");

		desiredOffset = (GetGlobalMousePosition() - player.GlobalPosition) * 0.5f;
		desiredOffset.X = Mathf.Clamp(desiredOffset.X, minOffset / 2.0f, maxOffset / 2.0f);
		desiredOffset.Y = Mathf.Clamp(desiredOffset.Y, minOffset / 2.0f, maxOffset / 2.0f);

		GlobalPosition = player.GlobalPosition + desiredOffset;

		if (Arena.Instance.isOutside)
		{
			desiredOffset = (GetGlobalMousePosition() - player.GlobalPosition) * 0.5f;
			desiredOffset.X = Mathf.Clamp(desiredOffset.X, minOffset / 0.0f, maxOffset / 0.0f);
			desiredOffset.Y = Mathf.Clamp(desiredOffset.Y, minOffset / 0.0f, maxOffset / 0.0f);
			GlobalPosition = player.GlobalPosition + desiredOffset;
		}

		if (Player.Instance.health <= 0f)
		{
			desiredOffset = (GetGlobalMousePosition() - player.GlobalPosition) * 0.5f;
			desiredOffset.X = Mathf.Clamp(desiredOffset.X, minOffset / 1000f, maxOffset / 1000f);
			desiredOffset.Y = Mathf.Clamp(desiredOffset.Y, minOffset / 1000f, maxOffset / 1000f);
			GlobalPosition = player.GlobalPosition + desiredOffset;
			Tween tween = CreateTween();
			tween.Parallel().TweenProperty(this, "zoom", new Vector2(3, 3), 1f);
		}

		// Screen shake
		if (activeShakeTime > 0f)
		{
			shakeTime += dt * shakeTimeSpeed;
			activeShakeTime -= dt;

			Offset = new Vector2(
				noise.GetNoise2D(shakeTime, 0) * shakeIntensity,
				noise.GetNoise2D(0, shakeTime) * shakeIntensity
			);

			shakeIntensity = Mathf.Max(shakeIntensity - shakeDecay * dt, 0f);
		}
		else
		{
			Offset = Offset.Lerp(Vector2.Zero, 10.5f * dt);
		}
	}

	public void ScreenShake(int intensity, float time)
	{
		noise.Seed = (int)GD.Randi();
		noise.Frequency = 2.0f;

		shakeIntensity = intensity;
		activeShakeTime = time;
		shakeTime = 0.0f;
	}
}