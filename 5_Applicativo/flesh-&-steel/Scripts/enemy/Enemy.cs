using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
	[Export] public float ChaseSpeed = 110f;
	[Export] public float ArriveDistance = 6.5f;

	[Export] public float AttackCooldown = 1.0f;
	[Export] public int Damage = 1;
	[Export] public float AttackFreezeDuration = 1f;
	[Export] public float AttackMoveMultiplier = 0.2f;
	[Export] public float AttackRecoveryRampDuration = 0.6f;

	[Export] public int MaxHealth = 5;

	[Export] public float SpawnFreezeTime = 0.5f;
	[Export] public float SpawnRampDuration = 0.6f;

	[Export] public float KnockbackDecay = 800f;

	public event Action Died;

	private Player _player;
	private Area2D _attackArea;

	private float _attackCooldownTimer = 0f;
	private float _attackSlowTimer = 0f;
	private float _attackRecoveryTimer = 0f;

	private int _currentHealth;
	private float _spawnFreezeTimer;
	private float _spawnRampTimer = 0f;
	private Vector2 _knockbackVelocity = Vector2.Zero;

	public override void _Ready()
	{
		_currentHealth = MaxHealth;
		_spawnFreezeTimer = SpawnFreezeTime;

		_player = GetNodeOrNull<Player>("/root/Main/Player");
		_attackArea = GetNodeOrNull<Area2D>("AttackArea");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_player == null)
			return;

		float d = (float)delta;

		if (_spawnFreezeTimer > 0f)
		{
			_spawnFreezeTimer -= d;
			if (_spawnFreezeTimer <= 0f)
				_spawnRampTimer = SpawnRampDuration;
			return;
		}

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

		float speed = ChaseSpeed;
		float speedMultiplier = 1.0f;

		if (_spawnRampTimer > 0f)
		{
			_spawnRampTimer -= d;
			float t = 1.0f - Mathf.Clamp(_spawnRampTimer / SpawnRampDuration, 0f, 1f);
			t = t * t * (3f - 2f * t);
			speedMultiplier = t;
		}

		bool isInAttackSlowWindow = canAttackNow || _attackSlowTimer > 0f;
		if (isInAttackSlowWindow)
		{
			speedMultiplier *= AttackMoveMultiplier;
		}
		else if (_attackRecoveryTimer > 0f && AttackRecoveryRampDuration > 0f)
		{
			float t = 1.0f - (_attackRecoveryTimer / AttackRecoveryRampDuration);
			t = Mathf.Clamp(t, 0f, 1f);
			t = t * t * (3f - 2f * t);
			speedMultiplier *= Mathf.Lerp(AttackMoveMultiplier, 1.0f, t);
		}

		speed *= speedMultiplier;

		MoveTo(_player.GlobalPosition, speed);

		Velocity += _knockbackVelocity;
		if (_knockbackVelocity.Length() > 1f)
			_knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, KnockbackDecay * d);
		else
			_knockbackVelocity = Vector2.Zero;

		MoveAndSlide();
	}

	public void TakeDamage(int amount = 1)
	{
		if (_currentHealth <= 0)
			return;

		_currentHealth -= amount;

		if (_currentHealth <= 0)
		{
			_currentHealth = 0;
			Died?.Invoke();
			QueueFree();
		}
	}

	public void ApplyKnockback(Vector2 direction, float strength)
	{
		_knockbackVelocity = direction.Normalized() * strength;
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
