using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
	[Export] public float PatrolSpeed = 70f;
	[Export] public float ChaseSpeed = 110f;
	[Export] public float ArriveDistance = 6.5f;
	[Export] public Vector2 PerimeterHalfSize = new Vector2(220, 110);

	[Export] public float AttackCooldown = 1.0f;
	[Export] public int Damage = 1;
	[Export] public float AttackFreezeDuration = 1f;
	[Export] public float AttackMoveMultiplier = 0.2f;
	[Export] public float AttackRecoveryRampDuration = 0.6f;

	private Player _player;
	private Area2D _visionArea;
	private Area2D _attackArea;

	private float _attackCooldownTimer = 0f;
	private float _attackSlowTimer = 0f;
	private float _attackRecoveryTimer = 0f;
	
	private int _patrolIndex = 0;
	private Vector2[] _perimeterPoints = Array.Empty<Vector2>();
	private Vector2 _roomCenter;

	public override void _Ready()
	{
		_player = GetNodeOrNull<Player>("/root/Main/Player");
		_visionArea = GetNodeOrNull<Area2D>("VisionArea");
		_attackArea = GetNodeOrNull<Area2D>("AttackArea");

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

		float d = (float)delta;
		if (_attackCooldownTimer > 0f)
			_attackCooldownTimer -= d;

		float prevAttackSlowTimer = _attackSlowTimer;
		if (_attackSlowTimer > 0f)
			_attackSlowTimer -= d;

		bool canAttackNow = _attackArea != null && _attackArea.OverlapsBody(_player) && _attackCooldownTimer <= 0f;

		if (canAttackNow)
		{
			_attackCooldownTimer = AttackCooldown;
			_attackSlowTimer = AttackFreezeDuration;
			_attackRecoveryTimer = 0f;
			_player.TakeDamage(Damage, GlobalPosition);
		}

		if (prevAttackSlowTimer > 0f && _attackSlowTimer <= 0f && AttackRecoveryRampDuration > 0f)
			_attackRecoveryTimer = AttackRecoveryRampDuration;

		if (_attackRecoveryTimer > 0f)
			_attackRecoveryTimer -= d;

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

		float speedMultiplier = 1.0f;
		bool isInAttackSlowWindow = canAttackNow || _attackSlowTimer > 0f;
		if (isInAttackSlowWindow)
		{
			speedMultiplier = AttackMoveMultiplier;
		}
		else if (_attackRecoveryTimer > 0f && AttackRecoveryRampDuration > 0f)
		{
			float t = 1.0f - (_attackRecoveryTimer / AttackRecoveryRampDuration);
			t = Mathf.Clamp(t, 0f, 1f);
			t = t * t * (3f - 2f * t);
			speedMultiplier = Mathf.Lerp(AttackMoveMultiplier, 1.0f, t);
		}

		speed *= speedMultiplier;

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
