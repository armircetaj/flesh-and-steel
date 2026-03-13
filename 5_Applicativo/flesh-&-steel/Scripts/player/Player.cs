using Godot;

public partial class Player : CharacterBody2D
{
	[Export] public float MaxSpeed = 180f;
	[Export] public float Acceleration = 800f;
	[Export] public float Friction = 475f;

	[Export] public PackedScene ProjectileScene;
	[Export] public float ShootCooldown = 0.7f;
	[Export] public float ProjectileSpawnOffset = 20f;
	
	private float _shootCooldownTimer = 0f;
	private Vector2 _lastMoveDirection = Vector2.Down;

	public override void _PhysicsProcess(double delta)
	{
		float d = (float)delta;

		HandleMovement(d);
		HandleShooting(d);
	}

	private void HandleMovement(float delta)
	{
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
			Velocity = Velocity.MoveToward(inputDirection * MaxSpeed, Acceleration * delta);
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

		if (shootInput == Vector2.Zero)
			return;

		Vector2 shootDirection;
		if (Mathf.Abs(shootInput.X) > Mathf.Abs(shootInput.Y))
		{
			shootDirection = new Vector2(Mathf.Sign(shootInput.X), 0);
		}
		else
		{
			shootDirection = new Vector2(0, Mathf.Sign(shootInput.Y));
		}

		if (_shootCooldownTimer > 0f)
			return;

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
}
