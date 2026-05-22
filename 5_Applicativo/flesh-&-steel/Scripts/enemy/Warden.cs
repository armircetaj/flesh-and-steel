using Godot;
using System;

public partial class Warden : CharacterBody2D
{
	public enum BossPhase { Shield, Blade, LastStand }

	[Export] public int MaxHealth = 20;
	[Export] public float Phase2Threshold = 0.6f;  // 60% HP
	[Export] public float Phase3Threshold = 0.25f;  // 25% HP

	// Movement
	[Export] public float ShieldMoveSpeed = 40f;
	[Export] public float BladeMoveSpeed = 60f;
	[Export] public float LastStandMoveSpeed = 50f;

	// Shield phase
	[Export] public float ShieldShootCooldown = 3f;
	[Export] public int ShieldSpreadCount = 3;

	// Blade phase
	[Export] public float ChargeSpeed = 250f;
	[Export] public float ChargeCooldown = 3f;
	[Export] public float ChargeDuration = 0.4f;
	[Export] public int ChargeDamage = 2;
	[Export] public float SlamProjectileSpeed = 150f;

	// Last Stand
	[Export] public float StanceDuration = 6f;
	[Export] public float TransitionVulnerabilityDuration = 1.5f;
	[Export] public float LastStandShootCooldown = 2f;
	[Export] public float LastStandChargeCooldown = 2f;

	// General
	[Export] public float HitFlashDuration = 0.3f;
	[Export] public float KnockbackDecay = 600f;
	[Export] public float SpawnFreezeTime = 1.0f;

	[Export] public PackedScene ProjectileScene;

	public event Action Died;

	private Player _player;
	private Sprite2D _sprite;
	private CollisionShape2D _collisionShape;
	private ProgressBar _bossHpBar;

	private Texture2D _idleTex;
	private Texture2D _shieldTex;
	private Texture2D _bladeTex;

	private BossPhase _currentPhase = BossPhase.Shield;
	private int _currentHealth;
	private float _spawnFreezeTimer;
	private bool _isDead = false;

	// Timers
	private float _shootTimer;
	private float _chargeTimer;
	private float _stanceTimer;
	private float _transitionTimer = 0f;
	private bool _isTransitioning = false;
	private BossPhase _lastStandCurrentStance = BossPhase.Shield;

	// Charge state
	private bool _isCharging = false;
	private Vector2 _chargeDirection;
	private float _chargeDurationTimer;
	private bool _hasSlammed = false;

	// Knockback
	private Vector2 _knockbackVelocity = Vector2.Zero;

	// Flash
	private Tween _flashTween;
	private Color _defaultModulate;

	public override void _Ready()
	{
		_currentHealth = MaxHealth;
		_spawnFreezeTimer = SpawnFreezeTime;

		_player = GetNodeOrNull<Player>("/root/Main/Player");
		_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		_collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		_bossHpBar = GetNodeOrNull<ProgressBar>("/root/Main/UI/BossHPBar");

		if (_player != null)
			AddCollisionExceptionWith(_player);

		if (_sprite != null)
			_defaultModulate = _sprite.Modulate;

		_idleTex = GD.Load<Texture2D>("res://Assets/sprites/Warden_Idle.png");
		_shieldTex = GD.Load<Texture2D>("res://Assets/sprites/Warden_Shield.png");
		_bladeTex = GD.Load<Texture2D>("res://Assets/sprites/Warden_Blade.png");

		_shootTimer = ShieldShootCooldown * 0.5f;
		_chargeTimer = ChargeCooldown;
		_stanceTimer = StanceDuration;

		if (_bossHpBar != null)
		{
			_bossHpBar.MaxValue = MaxHealth;
			_bossHpBar.Value = _currentHealth;
			_bossHpBar.Visible = true;
		}

		UpdateSprite();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_player == null || _isDead)
			return;

		float d = (float)delta;

		if (_spawnFreezeTimer > 0f)
		{
			_spawnFreezeTimer -= d;
			return;
		}

		// Handle knockback
		if (_knockbackVelocity.Length() > 1f)
			_knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, KnockbackDecay * d);
		else
			_knockbackVelocity = Vector2.Zero;

		if (_isCharging)
		{
			HandleChargeMovement(d);
			return;
		}

		if (_isTransitioning)
		{
			HandleTransition(d);
			return;
		}

		switch (_currentPhase)
		{
			case BossPhase.Shield:
				HandleShieldPhase(d);
				break;
			case BossPhase.Blade:
				HandleBladePhase(d);
				break;
			case BossPhase.LastStand:
				HandleLastStandPhase(d);
				break;
		}
	}

	// ===== PHASE 1: SHIELD =====
	private void HandleShieldPhase(float d)
	{
		// Slow advance toward player
		MoveTowardPlayer(ShieldMoveSpeed, d);

		// Shoot spread projectiles
		_shootTimer -= d;
		if (_shootTimer <= 0f)
		{
			ShootSpread(ShieldSpreadCount, 30f);
			_shootTimer = ShieldShootCooldown;
		}
	}

	// ===== PHASE 2: BLADE =====
	private void HandleBladePhase(float d)
	{
		// Move toward player between charges
		MoveTowardPlayer(BladeMoveSpeed, d);

		// Charge attack
		_chargeTimer -= d;
		if (_chargeTimer <= 0f)
		{
			StartCharge();
			_chargeTimer = ChargeCooldown;
		}
	}

	// ===== PHASE 3: LAST STAND =====
	private void HandleLastStandPhase(float d)
	{
		_stanceTimer -= d;
		if (_stanceTimer <= 0f)
		{
			StartStanceTransition();
			return;
		}

		if (_lastStandCurrentStance == BossPhase.Shield)
		{
			MoveTowardPlayer(LastStandMoveSpeed, d);

			_shootTimer -= d;
			if (_shootTimer <= 0f)
			{
				ShootSpread(5, 25f);
				_shootTimer = LastStandShootCooldown;
			}
		}
		else // Blade stance in last stand
		{
			MoveTowardPlayer(LastStandMoveSpeed * 1.2f, d);

			_chargeTimer -= d;
			if (_chargeTimer <= 0f)
			{
				StartCharge();
				_chargeTimer = LastStandChargeCooldown;
			}
		}
	}

	private void StartStanceTransition()
	{
		_isTransitioning = true;
		_transitionTimer = TransitionVulnerabilityDuration;
		Velocity = Vector2.Zero;

		if (_sprite != null)
			_sprite.Texture = _idleTex;
	}

	private void HandleTransition(float d)
	{
		_transitionTimer -= d;

		// Blink during transition to signal vulnerability
		if (_sprite != null)
		{
			float blink = Mathf.Sin(_transitionTimer * 20f);
			_sprite.Modulate = blink > 0 ? _defaultModulate : new Color(1f, 1f, 1f, 0.4f);
		}

		if (_transitionTimer <= 0f)
		{
			_isTransitioning = false;
			if (_sprite != null)
				_sprite.Modulate = _defaultModulate;

			// Switch stance
			_lastStandCurrentStance = _lastStandCurrentStance == BossPhase.Shield
				? BossPhase.Blade
				: BossPhase.Shield;

			_stanceTimer = StanceDuration;
			_shootTimer = LastStandShootCooldown * 0.5f;
			_chargeTimer = LastStandChargeCooldown * 0.5f;

			UpdateSprite();
		}

		Velocity = _knockbackVelocity;
		MoveAndSlide();
	}

	// ===== MOVEMENT =====
	private void MoveTowardPlayer(float speed, float d)
	{
		Vector2 dir = (_player.GlobalPosition - GlobalPosition);
		if (dir.Length() > 15f)
			Velocity = dir.Normalized() * speed + _knockbackVelocity;
		else
			Velocity = _knockbackVelocity;

		MoveAndSlide();
	}

	// ===== CHARGE =====
	private void StartCharge()
	{
		_isCharging = true;
		_hasSlammed = false;
		_chargeDirection = (_player.GlobalPosition - GlobalPosition).Normalized();
		_chargeDurationTimer = ChargeDuration;
	}

	private void HandleChargeMovement(float d)
	{
		_chargeDurationTimer -= d;

		if (_chargeDurationTimer <= 0f)
		{
			EndCharge();
			return;
		}

		Velocity = _chargeDirection * ChargeSpeed;
		MoveAndSlide();

		// Check if we hit the player during charge
		float distToPlayer = (GlobalPosition - _player.GlobalPosition).Length();
		if (distToPlayer < 25f)
		{
			_player.TakeDamage(ChargeDamage, GlobalPosition, Player.DamageType.Melee);
		}
	}

	private void EndCharge()
	{
		_isCharging = false;
		Velocity = Vector2.Zero;

		// Ground slam: shoot 4 projectiles in cardinal directions
		if (!_hasSlammed && ProjectileScene != null)
		{
			_hasSlammed = true;
			Vector2[] dirs = { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right };
			foreach (var dir in dirs)
			{
				SpawnProjectile(dir, SlamProjectileSpeed);
			}
		}
	}

	// ===== SHOOTING =====
	private void ShootSpread(int count, float angleDeg)
	{
		if (ProjectileScene == null || _player == null)
			return;

		Vector2 baseDir = (_player.GlobalPosition - GlobalPosition).Normalized();
		float totalAngle = Mathf.DegToRad(angleDeg);

		for (int i = 0; i < count; i++)
		{
			float t = count == 1 ? 0f : (float)i / (count - 1) - 0.5f;
			float angle = t * totalAngle;
			Vector2 dir = baseDir.Rotated(angle);
			SpawnProjectile(dir, 120f);
		}
	}

	private void SpawnProjectile(Vector2 direction, float speed)
	{
		if (ProjectileScene == null)
			return;

		var proj = ProjectileScene.Instantiate<GhostProjectile>();
		proj.GlobalPosition = GlobalPosition + direction * 20f;
		proj.Speed = speed;
		proj.Initialize(direction, 1);
		GetTree().CurrentScene.AddChild(proj);
	}

	// ===== DAMAGE =====
	public void TakeDamage(int amount = 1, bool fromDash = false)
	{
		if (_currentHealth <= 0 || _isDead)
			return;

		// Shield phase: only melee/dash damages
		if (_currentPhase == BossPhase.Shield && !fromDash && !_isTransitioning)
			return;

		// Blade phase: ranged does full, dash does half
		if (_currentPhase == BossPhase.Blade && fromDash && !_isTransitioning)
			amount = Mathf.Max(1, amount / 2);

		// Transition: 2x damage
		if (_isTransitioning)
			amount *= 2;

		_currentHealth -= amount;
		FlashRed();

		if (_bossHpBar != null)
		{
			_bossHpBar.Value = _currentHealth;
		}

		if (_currentHealth <= 0)
		{
			_currentHealth = 0;
			_isDead = true;
			
			if (_bossHpBar != null)
				_bossHpBar.Visible = false;

			Died?.Invoke();
			QueueFree();
			return;
		}

		// Check phase transitions
		float hpRatio = (float)_currentHealth / MaxHealth;
		if (_currentPhase == BossPhase.Shield && hpRatio <= Phase2Threshold)
		{
			EnterPhase(BossPhase.Blade);
		}
		else if (_currentPhase == BossPhase.Blade && hpRatio <= Phase3Threshold)
		{
			EnterPhase(BossPhase.LastStand);
		}
	}

	public void ApplyKnockback(Vector2 direction, float strength)
	{
		_knockbackVelocity = direction.Normalized() * strength * 0.5f;
	}

	private void EnterPhase(BossPhase newPhase)
	{
		_currentPhase = newPhase;
		_isCharging = false;

		switch (newPhase)
		{
			case BossPhase.Blade:
				_chargeTimer = ChargeCooldown * 0.5f;
				break;
			case BossPhase.LastStand:
				_lastStandCurrentStance = BossPhase.Shield;
				_stanceTimer = StanceDuration;
				_shootTimer = LastStandShootCooldown * 0.5f;
				_chargeTimer = LastStandChargeCooldown;
				break;
		}

		UpdateSprite();
	}

	private void UpdateSprite()
	{
		if (_sprite == null)
			return;

		Texture2D tex = _currentPhase switch
		{
			BossPhase.Shield => _shieldTex,
			BossPhase.Blade => _bladeTex,
			_ => _lastStandCurrentStance == BossPhase.Shield ? _shieldTex : _bladeTex
		};

		if (tex != null)
			_sprite.Texture = tex;
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
}
