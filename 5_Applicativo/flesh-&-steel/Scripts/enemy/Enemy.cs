using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
	[Export] public float PatrolSpeed = 70f;
	[Export] public float ChaseSpeed = 100f;
	[Export] public float ArriveDistance = 6.5f;
	[Export] public Vector2 PerimeterHalfSize = new Vector2(220, 110);

	private Player _player;
	private Area2D _visionArea;
	
	private int _patrolIndex = 0;
	private Vector2[] _perimeterPoints = Array.Empty<Vector2>();
	private Vector2 _roomCenter;

	public override void _Ready()
	{
		_player = GetNodeOrNull<Player>("/root/Main/Player");
		_visionArea = GetNodeOrNull<Area2D>("VisionArea");

		_roomCenter = (GetParent() is Node2D p) ? p.GlobalPosition : Vector2.Zero;

		_perimeterPoints = new Vector2[]
		{
			_roomCenter + new Vector2(-PerimeterHalfSize.X, -PerimeterHalfSize.Y),
			_roomCenter + new Vector2(+PerimeterHalfSize.X, -PerimeterHalfSize.Y),
			_roomCenter + new Vector2(+PerimeterHalfSize.X, +PerimeterHalfSize.Y),
			_roomCenter + new Vector2(-PerimeterHalfSize.X, +PerimeterHalfSize.Y),
		};
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_player == null)
			return;

		bool seePlayer = _visionArea != null && _visionArea.OverlapsBody(_player);

		Vector2 target;
		float speed;

		if (seePlayer)
		{
			target = _player.GlobalPosition;
			speed = ChaseSpeed;
		}
		else
		{
			target = GetNextPatrolTarget();
			speed = PatrolSpeed;
		}

		MoveTo(target, speed);
		MoveAndSlide();
	}

	private Vector2 GetNextPatrolTarget()
	{
		if (_perimeterPoints.Length > 0)
		{
			Vector2 target = _perimeterPoints[_patrolIndex];
			if (GlobalPosition.DistanceTo(target) <= ArriveDistance)
			{
				_patrolIndex = (_patrolIndex + 1) % _perimeterPoints.Length;
				target = _perimeterPoints[_patrolIndex];
			}
			return target;
		}
		return GlobalPosition;
	}

	private void MoveTo(Vector2 targetGlobalPos, float speed)
	{
		Vector2 dir = targetGlobalPos - GlobalPosition;
		if (dir.Length() <= ArriveDistance)
			Velocity = Vector2.Zero;
		else
			Velocity = dir.Normalized() * speed;
	}
}
