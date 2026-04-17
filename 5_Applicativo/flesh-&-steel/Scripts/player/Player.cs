using Godot;
using System.Collections.Generic;

public partial class Player : CharacterBody2D
{
	public enum PlayerState { Flesh, Steel }

	[Export] public float FleshMaxSpeed = 180f;
	[Export] public float SteelMaxSpeed = 110f;
	[Export] public float Acceleration = 800f;
	[Export] public float Friction = 475f;

	[Export] public float SlowDuration = 0.5f;
	[Export] public float SlowMultiplier = 0.5f;
	[Export] public float KnockbackStrength = 220f;

	[Export] public int BlinkCount = 6;
	[Export] public float BlinkInterval = 0.05f;

	[Export] public float AimGraceDuration = 0.5f;

	[Export] public int MaxHealth = 5;
	[Export] public float InvincibilityDuration = 1.0f;

	[Export] public PackedScene ProjectileScene;
	[Export] public float ShootCooldown = 0.7f;
	[Export] public float ProjectileSpawnOffset = 20f;

	[Export] public float DashDistance = 80f;
	[Export] public float DashDuration = 0.15f;
	[Export] public float DashCooldown = 0.6f;
	[Export] public int DashDamage = 2;
	[Export] public float DashKnockbackStrength = 300f;

	[Export] public float TransformCooldown = 6.0f;
	[Export] public PackedScene TransformationScene;

	[Export] public float MaxStamina = 100f;
	[Export] public float DashStaminaCost = 25f;
	[Export] public float StaminaRegenRate = 15f;

	private enum FacingDir { Left, Right, Up, Down }

	private PlayerState _currentState = PlayerState.Flesh;
	private int _currentHealth;
	private HeartsHud _heartsHud;
	private Sprite2D _sprite;
	private Color _defaultSpriteModulate = new Color(1, 1, 1, 1);
	private Tween _flashTween;

	private Texture2D _fleshTexLeft;
	private Texture2D _fleshTexRight;
	private Texture2D _fleshTexUp;
	private Texture2D _fleshTexDown;
	private Texture2D _steelTex;

	private float _slowTimer = 0f;
	private float _shootCooldownTimer = 0f;
	private Vector2 _lastMoveDirection = Vector2.Down;
	private FacingDir _aimDirection = FacingDir.Down;
	private float _aimGraceTimer = 0f;
	private bool _shootWasHeld = false;

	private bool _isDashing = false;
	private Vector2 _dashDirection = Vector2.Zero;
	private float _dashTimer = 0f;
	private float _dashCooldownTimer = 0f;
	private Area2D _dashHitbox;
	private HashSet<ulong> _dashHitEnemies = new();

	private float _transformCooldownTimer = 0f;
	private float _currentStamina;
	private ProgressBar _transformBar;
	private float _invincibilityTimer = 0f;
	private bool _isTransforming = false;
	private AnimatedSprite2D _transformAnim = null;

	public override void _Ready()
	{
		_currentHealth = MaxHealth;
		_currentStamina = MaxStamina;
		_heartsHud = GetNodeOrNull<HeartsHud>("../HeartsHUD");
		_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (_sprite != null)
			_defaultSpriteModulate = _sprite.Modulate;

		_fleshTexLeft = GD.Load<Texture2D>("res://Assets/sprites/Fs_Le_Idle.png");
		_fleshTexRight = GD.Load<Texture2D>("res://Assets/sprites/Fs_Ri_Idle.png");
		_fleshTexUp = GD.Load<Texture2D>("res://Assets/sprites/Fs_Ba_Idle.png");
		_fleshTexDown = GD.Load<Texture2D>("res://Assets/sprites/Fs_Fr_Idle.png");
		_steelTex = GD.Load<Texture2D>("res://Assets/sprites/St_Fr_Idle.png");

		_dashHitbox = GetNodeOrNull<Area2D>("DashHitbox");
		if (_dashHitbox != null)
			_dashHitbox.BodyEntered += OnDashHitboxBodyEntered;

		_transformBar = GetNodeOrNull<ProgressBar>("../UI/TransformBar");

		_heartsHud?.SetHearts(_currentHealth);
		UpdateFacingSprite(_lastMoveDirection);
	}

	public override void _PhysicsProcess(double delta)
	{
		float d = (float)delta;

		HandleTransformInput(d);
		HandleStamina(d);

		if (_invincibilityTimer > 0f)
			_invincibilityTimer -= d;

		if (_isTransforming)
		{
			Velocity = Vector2.Zero;
			return;
		}

		if (_isDashing)
		{
			HandleDashMovement(d);
			return;
		}

		HandleMovement(d);
		HandleAttack(d);
		UpdateFacingSprite(_lastMoveDirection);
	}

	private void HandleTransformInput(float delta)
	{
		if (_transformCooldownTimer > 0f)
			_transformCooldownTimer -= delta;

		if (Input.IsActionJustPressed("transform") && _transformCooldownTimer <= 0f && !_isDashing && !_isTransforming)
		{
			StartTransformation();
		}
	}

	private void StartTransformation()
	{
		_isTransforming = true;
		_invincibilityTimer = 999f;
		Velocity = Vector2.Zero;

		if (_sprite != null)
			_sprite.Visible = false;

		if (TransformationScene != null)
		{
			_transformAnim = TransformationScene.Instantiate<AnimatedSprite2D>();
			_transformAnim.SpriteFrames.SetAnimationLoop("transformation", false);
			_transformAnim.ZIndex = 12;
			_transformAnim.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
			_transformAnim.Position = Vector2.Zero;
			AddChild(_transformAnim);
			_transformAnim.AnimationFinished += OnTransformAnimFinished;

			if (_currentState == PlayerState.Flesh)
			{
				_transformAnim.Play("transformation");
			}
			else
			{
				int lastFrame = _transformAnim.SpriteFrames.GetFrameCount("transformation") - 1;
				_transformAnim.Frame = lastFrame;
				_transformAnim.FrameProgress = 0.0f;
				_transformAnim.Play("transformation", -1.0f);
			}
		}
		else
		{
			FinishTransformation();
		}
	}

	private void OnTransformAnimFinished()
	{
		FinishTransformation();
	}

	private void FinishTransformation()
	{
		_currentState = _currentState == PlayerState.Flesh ? PlayerState.Steel : PlayerState.Flesh;
		_transformCooldownTimer = TransformCooldown;
		_shootCooldownTimer = 0f;
		_dashCooldownTimer = 0f;
		_isTransforming = false;
		_invincibilityTimer = 0.2f;

		if (_sprite != null)
			_sprite.Visible = true;

		if (_transformAnim != null)
		{
			_transformAnim.AnimationFinished -= OnTransformAnimFinished;
			_transformAnim.QueueFree();
			_transformAnim = null;
		}

		UpdateFacingSprite(_lastMoveDirection);
	}

	private void HandleStamina(float delta)
	{
		if (_currentStamina < MaxStamina)
		{
			_currentStamina = Mathf.Min(_currentStamina + StaminaRegenRate * delta, MaxStamina);
		}

		if (_transformBar != null)
			_transformBar.Value = (_currentStamina / MaxStamina) * 100.0;
	}

	private void HandleMovement(float delta)
	{
		if (_slowTimer > 0f)
			_slowTimer -= delta;

		float maxSpeed = _currentState == PlayerState.Flesh ? FleshMaxSpeed : SteelMaxSpeed;
		float effectiveSpeed = maxSpeed;
		float effectiveAcceleration = Acceleration;
		if (_slowTimer > 0f)
		{
			effectiveSpeed *= SlowMultiplier;
			effectiveAcceleration *= SlowMultiplier;
		}

		Vector2 inputDirection = new Vector2(
			Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left"),
			Input.GetActionStrength("move_down") - Input.GetActionStrength("move_up")
		);

		if (inputDirection != Vector2.Zero)
		{
			if (Mathf.Abs(inputDirection.X) > Mathf.Abs(inputDirection.Y))
				_lastMoveDirection = new Vector2(Mathf.Sign(inputDirection.X), 0);
			else
				_lastMoveDirection = new Vector2(0, Mathf.Sign(inputDirection.Y));

			inputDirection = inputDirection.Normalized();
			Velocity = Velocity.MoveToward(inputDirection * effectiveSpeed, effectiveAcceleration * delta);
		}
		else
		{
			Velocity = Velocity.MoveToward(Vector2.Zero, Friction * delta);
		}

		MoveAndSlide();
	}

	private void HandleAttack(float delta)
	{
		if (_currentState == PlayerState.Flesh)
			HandleShooting(delta);
		else
			HandleDashAttack(delta);
	}

	private void HandleShooting(float delta)
	{
		if (_shootCooldownTimer > 0f)
			_shootCooldownTimer -= delta;

		Vector2 shootInput = new Vector2(
			Input.GetActionStrength("shoot_right") - Input.GetActionStrength("shoot_left"),
			Input.GetActionStrength("shoot_down") - Input.GetActionStrength("shoot_up")
		);

		bool shootHeld = shootInput != Vector2.Zero;

		if (shootHeld)
		{
			_aimDirection = VectorToFacingDir(shootInput);
			_aimGraceTimer = AimGraceDuration;
		}
		else
		{
			if (_shootWasHeld)
				_aimGraceTimer = AimGraceDuration;

			if (_aimGraceTimer > 0f)
				_aimGraceTimer -= delta;
		}

		_shootWasHeld = shootHeld;

		if (!shootHeld || _shootCooldownTimer > 0f)
			return;

		Vector2 shootDirection;
		if (Mathf.Abs(shootInput.X) > Mathf.Abs(shootInput.Y))
			shootDirection = new Vector2(Mathf.Sign(shootInput.X), 0);
		else
			shootDirection = new Vector2(0, Mathf.Sign(shootInput.Y));

		Shoot(shootDirection);
		_shootCooldownTimer = ShootCooldown;
	}

	private void Shoot(Vector2 direction)
	{
		if (ProjectileScene == null)
			return;

		direction = direction.Normalized();
		if (direction == Vector2.Zero)
			return;

		var projectile = ProjectileScene.Instantiate<Projectile>();
		projectile.GlobalPosition = GlobalPosition + direction * ProjectileSpawnOffset;
		projectile.Initialize(direction);
		GetTree().CurrentScene.AddChild(projectile);
	}

	private void HandleDashAttack(float delta)
	{
		if (_dashCooldownTimer > 0f)
			_dashCooldownTimer -= delta;

		Vector2 dashInput = new Vector2(
			Input.GetActionStrength("shoot_right") - Input.GetActionStrength("shoot_left"),
			Input.GetActionStrength("shoot_down") - Input.GetActionStrength("shoot_up")
		);

		if (dashInput == Vector2.Zero || _dashCooldownTimer > 0f)
			return;

		if (_currentStamina < DashStaminaCost)
			return;

		Vector2 dir;
		if (Mathf.Abs(dashInput.X) > Mathf.Abs(dashInput.Y))
			dir = new Vector2(Mathf.Sign(dashInput.X), 0);
		else
			dir = new Vector2(0, Mathf.Sign(dashInput.Y));

		StartDash(dir);
	}

	private void StartDash(Vector2 direction)
	{
		_isDashing = true;
		_dashDirection = direction.Normalized();
		_dashTimer = DashDuration;
		_dashHitEnemies.Clear();
		_currentStamina -= DashStaminaCost;

		_aimDirection = VectorToFacingDir(direction);
		UpdateFacingSprite(direction);

		if (_dashHitbox != null)
		{
			_dashHitbox.SetDeferred("monitoring", true);
			var shape = _dashHitbox.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
			if (shape != null)
				shape.SetDeferred("disabled", false);
		}
	}

	private void HandleDashMovement(float delta)
	{
		_dashTimer -= delta;

		if (_dashTimer <= 0f)
		{
			EndDash();
			return;
		}

		float progress = 1.0f - (_dashTimer / DashDuration);
		float speedCurve = 2.0f * (1.0f - progress);
		float dashSpeed = (DashDistance / DashDuration) * speedCurve;

		Velocity = _dashDirection * dashSpeed;
		MoveAndSlide();
	}

	private void EndDash()
	{
		_isDashing = false;
		_dashCooldownTimer = DashCooldown;
		Velocity = Vector2.Zero;

		if (_dashHitbox != null)
		{
			_dashHitbox.SetDeferred("monitoring", false);
			var shape = _dashHitbox.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
			if (shape != null)
				shape.SetDeferred("disabled", true);
		}
	}

	private void OnDashHitboxBodyEntered(Node2D body)
	{
		if (body is not Enemy enemy)
			return;

		if (_dashHitEnemies.Contains(enemy.GetInstanceId()))
			return;

		_dashHitEnemies.Add(enemy.GetInstanceId());
		enemy.TakeDamage(DashDamage);

		Vector2 knockDir = (enemy.GlobalPosition - GlobalPosition).Normalized();
		enemy.ApplyKnockback(knockDir, DashKnockbackStrength);
	}

	public void TakeDamage(int amount = 1)
	{
		TakeDamage(amount, GlobalPosition);
	}

	public void TakeDamage(int amount, Vector2 sourcePosition)
	{
		if (amount <= 0 || _currentHealth <= 0)
			return;

		if (_isDashing || _isTransforming || _invincibilityTimer > 0f)
			return;

		_invincibilityTimer = InvincibilityDuration;

		int actualDamage = Mathf.Min(amount, _currentHealth);
		_currentHealth -= actualDamage;

		_heartsHud?.TakeDamage(actualDamage);
		ApplyHitEffects(sourcePosition);

		if (_currentHealth <= 0)
		{
			_currentHealth = MaxHealth;
			_heartsHud?.ResetHearts();
		}
	}

	private void ApplyHitEffects(Vector2 sourcePosition)
	{
		_slowTimer = SlowDuration;

		Vector2 dir = GlobalPosition - sourcePosition;
		if (dir != Vector2.Zero)
			Velocity += dir.Normalized() * KnockbackStrength;

		BlinkDamage();
	}

	private void BlinkDamage()
	{
		if (_sprite == null)
			return;

		_flashTween?.Kill();
		_flashTween = CreateTween();

		Color baseCol = _defaultSpriteModulate;
		Color offCol = new Color(baseCol.R, baseCol.G, baseCol.B, 0f);
		Color onCol = new Color(baseCol.R, baseCol.G, baseCol.B, 1f);

		for (int i = 0; i < BlinkCount; i++)
		{
			_flashTween.TweenProperty(_sprite, "modulate", offCol, BlinkInterval);
			_flashTween.TweenProperty(_sprite, "modulate", onCol, BlinkInterval);
		}
	}

	private FacingDir VectorToFacingDir(Vector2 v)
	{
		if (Mathf.Abs(v.X) > Mathf.Abs(v.Y))
			return v.X < 0 ? FacingDir.Left : FacingDir.Right;
		return v.Y < 0 ? FacingDir.Up : FacingDir.Down;
	}

	private Texture2D GetTextureForFacing(FacingDir dir)
	{
		if (_currentState == PlayerState.Steel)
			return _steelTex;

		return dir switch
		{
			FacingDir.Left => _fleshTexLeft,
			FacingDir.Right => _fleshTexRight,
			FacingDir.Up => _fleshTexUp,
			_ => _fleshTexDown
		};
	}

	private void UpdateFacingSprite(Vector2 moveDirection)
	{
		if (_sprite == null)
			return;

		bool aimOverride = _shootWasHeld || _aimGraceTimer > 0f || _isDashing;
		FacingDir dir = aimOverride ? _aimDirection : VectorToFacingDir(moveDirection);

		Texture2D tex = GetTextureForFacing(dir);
		if (tex != null && _sprite.Texture != tex)
			_sprite.Texture = tex;
	}
}
