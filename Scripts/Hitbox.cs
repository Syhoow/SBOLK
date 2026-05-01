using Godot;
using System;

public partial class Hitbox : Area2D
{
	[Signal]
	public delegate void HitEventHandler(Node2D body);

	public override void _Ready()
	{
		BodyEntered += (body) => EmitSignal(SignalName.Hit, body);
	}
}
