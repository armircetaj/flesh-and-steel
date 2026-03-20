using Godot;

public partial class Player : CharacterBody2D
{
	[Export] public float MaxSpeed = 180f;
	[Export] public float Acceleration = 800f;
	[Export] public float Friction = 475f;

	[Export] public float SlowDuration = 0.5f;
	[Export] public float SlowMultiplier = 0.5f;
	[Export] public float KnockbackStrength = 220f;

	[Export] public int BlinkCount = 6;
	[Export] public float BlinkInterval = 0.05f;

	[Export] public float AimGraceDuration = 0.5f;

	[Export] public int MaxHealth = 5;

	[Export] public PackedScene ProjectileScene;
	[Export] public float ShootCooldown = 0.7f;
	[Export] public float ProjectileSpawnOffset = 20f;
	
	private enum FacingDir
	{
		Left,
		Right,
		Up,
		Down
	}

	private int _currentHealth;
	private HeartsHud _heartsHud;
	private Sprite2D _sprite;
	private Color _defaultSpriteModulate = new Color(1, 1, 1, 1);
	private Tween _flashTween;
	
	private Texture2D _texLeft;
	private Texture2D _texRight;
	private Texture2D _texUp;
	private Texture2D _texDown;

	private float _slowTimer = 0f;

	private float _shootCooldownTimer = 0f;
	private Vector2 _lastMoveDirection = Vector2.Down;

	private FacingDir _aimDirection = FacingDir.Down;
	private float _aimGraceTimer = 0f;
	private bool _shootWasHeld = false;

	public override void _Ready()
	{
		_currentHealth = MaxHealth;
		_heartsHud = GetNodeOrNull<HeartsHud>("../HeartsHUD");
		_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (_sprite != null)
			_defaultSpriteModulate = _sprite.Modulate;

		_texLeft = GD.Load<Texture2D>("res://Assets/sprites/Fs_Le_Idle.png");
		_texRight = GD.Load<Texture2D>("res://Assets/sprites/Fs_Ri_Idle.png");
		_texUp = GD.Load<Texture2D>("res://Assets/sprites/Fs_Ba_Idle.png");
		_texDown = GD.Load<Texture2D>("res://Assets/sprites/Fs_Fr_Idle.png");

		_heartsHud?.SetHearts(_currentHealth);
		UpdateFacingSprite(_lastMoveDirection);
	}

	public override void _PhysicsProcess(double delta)
	{
		float d = (float)delta;

		HandleMovement(d);
		HandleShooting(d);
		UpdateFacingSprite(_lastMoveDirection);
	}

	private void HandleMovement(float delta)
	{
		if (_slowTimer > 0f)
			_slowTimer -= delta;

		float effectiveSpeed = MaxSpeed;
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

	private void HandleShooting(float delta)
	{
		if (_shootCooldownTimer > 0f)
		{
			_shootCooldownTimer -= delta;
		}

		Vector2 shootInput = new Vector2(
			Input.GetActionStrength("shoot_right") - Input.GetActionStrength("shoot_left"),
			Input.GetActionStrength("shoot_down") - Input.GetActionStrength("shoot_up")
		);

		bool shootHeld = shootInput != Vector2.Zero;

		// Aim direction updates should NOT be blocked by shoot cooldown.
		if (shootHeld)
		{
			_aimDirection = VectorToFacingDir(shootInput);
			_aimGraceTimer = AimGraceDuration;
		}
		else
		{
			// If the player released a shoot direction (tap or stop holding),
			// keep facing the last aim direction for a short grace time.
			if (_shootWasHeld)
				_aimGraceTimer = AimGraceDuration;

			if (_aimGraceTimer > 0f)
				_aimGraceTimer -= delta;
		}

		_shootWasHeld = shootHeld;

		// Fire only if we have shoot input and the cooldown is ready.
		if (!shootHeld)
			return;

		if (_shootCooldownTimer > 0f)
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

	public void TakeDamage(int amount = 1)
	{
		TakeDamage(amount, GlobalPosition);
	}

	public void TakeDamage(int amount, Vector2 sourcePosition)
	{
		if (amount <= 0)
			return;

		if (_currentHealth <= 0)
			return;

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
		return dir switch
		{
			FacingDir.Left => _texLeft,
			FacingDir.Right => _texRight,
			FacingDir.Up => _texUp,
			_ => _texDown
		};
	}

	private void UpdateFacingSprite(Vector2 moveDirection)
	{
		if (_sprite == null)
			return;

		bool aimOverride = _shootWasHeld || _aimGraceTimer > 0f;
		FacingDir dir = aimOverride ? _aimDirection : VectorToFacingDir(moveDirection);

		Texture2D tex = GetTextureForFacing(dir);
		if (tex != null && _sprite.Texture != tex)
			_sprite.Texture = tex;
	}
}
