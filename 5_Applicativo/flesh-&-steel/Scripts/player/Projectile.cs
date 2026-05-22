using Godot;

public partial class Projectile : Area2D
{
	[Export] public float Speed = 200f;
	[Export] public PackedScene ExplosionScene;

	private Vector2 _direction = Vector2.Zero;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;

		var notifier = GetNodeOrNull<VisibleOnScreenNotifier2D>("VisibleOnScreenNotifier2D");
		if (notifier != null)
		{
			notifier.ScreenExited += OnScreenExited;
		}
	}

	public void Initialize(Vector2 direction)
	{
		if (direction == Vector2.Zero)
			return;

		_direction = direction.Normalized();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_direction == Vector2.Zero)
			return;

		float d = (float)delta;
		GlobalPosition += _direction * Speed * d;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body is Player)
			return;

		if (body is Enemy enemy)
		{
			enemy.TakeDamage(1);
		}
		else if (body is Ghost ghost)
		{
			ghost.TakeDamage(1);
		}
		else if (body is Warden warden)
		{
			warden.TakeDamage(1, fromDash: false);
		}

		if (ExplosionScene != null)
		{
			var explosion = ExplosionScene.Instantiate<Node2D>();
			explosion.GlobalPosition = GlobalPosition;
			GetTree().CurrentScene.AddChild(explosion);
		}

		QueueFree();
	}

	private void OnScreenExited()
	{
		QueueFree();
	}
}
