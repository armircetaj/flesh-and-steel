using Godot;

public partial class GhostProjectile : Area2D
{
	[Export] public float Speed = 120f;
	[Export] public float Lifetime = 3.0f;
	[Export] public PackedScene ExplosionScene;

	private Vector2 _direction = Vector2.Zero;
	private int _damage = 1;
	private float _lifeTimer;

	public override void _Ready()
	{
		_lifeTimer = Lifetime;
		BodyEntered += OnBodyEntered;
	}

	public void Initialize(Vector2 direction, int damage = 1)
	{
		if (direction == Vector2.Zero)
			return;

		_direction = direction.Normalized();
		_damage = damage;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_direction == Vector2.Zero)
			return;

		float d = (float)delta;
		GlobalPosition += _direction * Speed * d;

		_lifeTimer -= d;
		if (_lifeTimer <= 0f)
		{
			SpawnExplosion();
			QueueFree();
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body is Player player)
		{
			player.TakeDamage(_damage, GlobalPosition, Player.DamageType.Ranged);
			SpawnExplosion();
			QueueFree();
		}
		else if (body is Enemy || body is Ghost)
		{
			return;
		}
		else
		{
			SpawnExplosion();
			QueueFree();
		}
	}

	private void SpawnExplosion()
	{
		if (ExplosionScene == null)
			return;

		var explosion = ExplosionScene.Instantiate<Node2D>();
		explosion.GlobalPosition = GlobalPosition;
		explosion.Scale = new Vector2(1.5f, 1.5f);
		GetTree().CurrentScene.AddChild(explosion);

		if (explosion is AnimatedSprite2D anim)
		{
			anim.Play("ghostexplosion");
			anim.AnimationFinished += () => anim.QueueFree();
		}
	}
}
