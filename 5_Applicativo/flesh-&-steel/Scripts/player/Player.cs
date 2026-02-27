using Godot;
using System;

public partial class Player : CharacterBody2D
{
	[Export] public float MaxSpeed = 180f;
	[Export] public float Acceleration = 900f;
	[Export] public float Friction = 1100f;
	public override void _PhysicsProcess(double delta)
	{
		float d = (float)delta;
		Vector2 inputDirection = new Vector2(
			Input.GetActionStrength("ui_right") - Input.GetActionStrength("ui_left"),
			Input.GetActionStrength("ui_down") - Input.GetActionStrength("ui_up")
		);
		inputDirection = inputDirection.Normalized();
		if (inputDirection != Vector2.Zero)
		{
			Velocity = Velocity.MoveToward(inputDirection * MaxSpeed, Acceleration * d);
		}
		else
		{
			Velocity = Velocity.MoveToward(Vector2.Zero, Friction * d);
		}
		MoveAndSlide();
	}
}
