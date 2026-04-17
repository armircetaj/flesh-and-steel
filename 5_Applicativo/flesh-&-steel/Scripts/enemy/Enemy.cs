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

	[Export] public float SeparationRadius = 45f;
	[Export] public float SeparationStrength = 250f;

	[Export] public float HitFlashDuration = 0.5f;
	[Export] public PackedScene DeathScene;

	public event Action Died;

	private Player _player;
	private Area2D _attackArea;
	private Sprite2D _sprite;

	private float _attackCooldownTimer = 0f;
	private float _attackSlowTimer = 0f;
	private float _attackRecoveryTimer = 0f;

	private int _currentHealth;
	private float _spawnFreezeTimer;
	private float _spawnRampTimer = 0f;
	private Vector2 _knockbackVelocity = Vector2.Zero;

	private Tween _flashTween;
	private Color _defaultModulate;

	public override void _Ready()
	{
		_currentHealth = MaxHealth;
		_spawnFreezeTimer = SpawnFreezeTime;

		_player = GetNodeOrNull<Player>("/root/Main/Player");
		_attackArea = GetNodeOrNull<Area2D>("AttackArea");
		_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");

		if (_sprite != null)
			_defaultModulate = _sprite.Modulate;
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

		Velocity += GetSeparationForce();

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
		FlashRed();

		if (_currentHealth <= 0)
		{
			_currentHealth = 0;
			SpawnDeathEffect();
			Died?.Invoke();
			QueueFree();
		}
	}

	public void ApplyKnockback(Vector2 direction, float strength)
	{
		_knockbackVelocity = direction.Normalized() * strength;
	}

	private void FlashRed()
	{
		if (_sprite == null)
			return;

		_flashTween?.Kill();
		_flashTween = CreateTween();

		Color red = new Color(1f, 0.2f, 0.2f, 1f);

		_flashTween.TweenProperty(_sprite, "modulate", red, 0.05f);
		_flashTween.TweenProperty(_sprite, "modulate", _defaultModulate, HitFlashDuration - 0.05f);
	}

	private void SpawnDeathEffect()
	{
		if (DeathScene == null)
			return;

		var death = DeathScene.Instantiate<Node2D>();
		death.GlobalPosition = GlobalPosition;

		if (_sprite != null)
			death.Scale = _sprite.Scale;

		var parent = GetParent();
		if (parent != null)
			parent.AddChild(death);
		else
			GetTree().CurrentScene.AddChild(death);

		if (death is AnimatedSprite2D anim)
		{
			anim.Modulate = new Color(1f, 0.2f, 0.2f, 1f);
			var tween = anim.CreateTween();
			tween.TweenProperty(anim, "modulate", new Color(1f, 1f, 1f, 1f), 0.3f);

			anim.Play("coaldeath");
			anim.AnimationFinished += () => anim.QueueFree();
		}
	}

	private Vector2 GetSeparationForce()
	{
		Vector2 separation = Vector2.Zero;
		var parent = GetParent();
		if (parent == null)
			return separation;

		foreach (var child in parent.GetChildren())
		{
			if (child is not Enemy other || other == this)
				continue;

			Vector2 diff = GlobalPosition - other.GlobalPosition;
			float dist = diff.Length();

			if (dist > 0f && dist < SeparationRadius)
				separation += diff.Normalized() * (SeparationRadius - dist) / SeparationRadius;
		}

		if (_player != null)
		{
			Vector2 playerDiff = GlobalPosition - _player.GlobalPosition;
			float playerDist = playerDiff.Length();
			float playerPushRadius = SeparationRadius * 0.6f;

			if (playerDist > 0f && playerDist < playerPushRadius)
				separation += playerDiff.Normalized() * (playerPushRadius - playerDist) / playerPushRadius * 0.5f;
		}

		return separation * SeparationStrength;
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
