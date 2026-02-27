using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
	[Export] public float MaxSpeed = 110f;
	[Export] public float Acceleration = 500f;
	[Export] public float Friction = 1200f;

	private Player _player;
	private Area2D _attackArea;
	private bool _isTouchingPlayer = false;

	public override void _Ready()
	{
		_player = GetTree().Root.GetNode<Player>("Main/Player");
		_attackArea = GetNode<Area2D>("AttackArea");

		_attackArea.BodyEntered += OnBodyEntered;
		_attackArea.BodyExited += OnBodyExited;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_player == null)
			return;

		float d = (float)delta;

		Vector2 direction = (_player.GlobalPosition - GlobalPosition).Normalized();

		if (_isTouchingPlayer)
		{
			Velocity = Velocity.MoveToward(Vector2.Zero, Friction * d);
		}
		else
		{
			Vector2 targetVelocity = direction * MaxSpeed;
			Velocity = Velocity.MoveToward(targetVelocity, Acceleration * d);
		}

		MoveAndSlide();
	}

	private void OnBodyEntered(Node body)
	{
		if (body is Player)
			_isTouchingPlayer = true;
	}

	private void OnBodyExited(Node body)
	{
		if (body is Player)
			_isTouchingPlayer = false;
	}
}
