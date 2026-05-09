using Godot;
using System;

public partial class Cam : Camera2D
{
	private Vector2 desiredOffset; 
	private float minOffset = -200f; 
	private float maxOffset = 200f;
	private float shakeTimer;
	private float shakeDuration;
	private float shakeStrength;
	private Vector2 shakeOffset = Vector2.Zero;

	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}
	public override void _Process(double delta) 
	{ 
		Input.MouseMode = Input.MouseModeEnum.Confined;

		Node2D player = GetParent().GetNode<Node2D>(".");

		desiredOffset = (GetGlobalMousePosition() - player.GlobalPosition) * 0.5f;

	desiredOffset.X = Mathf.Clamp(desiredOffset.X, minOffset / 2.0f, maxOffset / 2.0f);
	desiredOffset.Y = Mathf.Clamp(desiredOffset.Y, minOffset / 2.0f, maxOffset / 2.0f);

		if (shakeTimer > 0f)
		{
			shakeTimer -= (float)delta;
			float progress = Mathf.Clamp(shakeTimer / Mathf.Max(0.01f, shakeDuration), 0f, 1f);
			float currentStrength = shakeStrength * progress;
			shakeOffset = new Vector2(
				(float)GD.RandRange(-currentStrength, currentStrength),
				(float)GD.RandRange(-currentStrength, currentStrength));
		}
		else
		{
			shakeOffset = Vector2.Zero;
			shakeTimer = 0f;
			shakeDuration = 0f;
			shakeStrength = 0f;
		}

		GlobalPosition = player.GlobalPosition + desiredOffset + shakeOffset;
	}

	public void TriggerShake(float duration = 0.12f, float strength = 7f)
	{
		shakeDuration = Mathf.Max(shakeDuration, duration);
		shakeTimer = Mathf.Max(shakeTimer, duration);
		shakeStrength = Mathf.Max(shakeStrength, strength);
	}
}
