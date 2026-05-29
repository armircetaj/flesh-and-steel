using Godot;
using System;

// Rappresenta il nemico "Ghost", capace di muoversi mantenendo la distanza, // sparare proiettili a ricerca e gestire la propria logica di knockback e separazione (Single Responsibility).
public partial class Ghost : CharacterBody2D
{
	[Export] 
	public float MoveSpeed = 90f;
	
	[Export] 
	public float PreferredDistance = 110f;
	
	[Export] 
	public float MinDistance = 70f;
	
	[Export] 
	public float MaxDistance = 140f;

	[Export] 
	public float ShootCooldown = 2.5f;
	
	[Export] 
	public int Damage = 1;

	[Export] 
	public int MaxHealth = 3;

	[Export] 
	public float SpawnFreezeTime = 0.5f;
	
	[Export] 
	public float SpawnRampDuration = 0.6f;

	[Export] 
	public float KnockbackDecay = 800f;

	[Export] 
	public float SeparationRadius = 45f;
	
	[Export] 
	public float SeparationStrength = 250f;

	[Export] 
	public float HitFlashDuration = 0.5f;
	
	[Export] 
	public PackedScene DeathScene;
	
	[Export] 
	public PackedScene ProjectileScene;

	public event Action Died;

	private Player _player;
	private Sprite2D _sprite;

	private int _currentHealth;
	private float _spawnFreezeTimer;
	private float _spawnRampTimer = 0f;
	private float _shootCooldownTimer = 0f;
	private Vector2 _knockbackVelocity = Vector2.Zero;
	private int _strafeDirection = 1;
	private float _strafeChangeTimer = 0f;

	private Tween _flashTween;
	private Color _defaultModulate;

	public override void _Ready()
	{
		_currentHealth = MaxHealth;
		_spawnFreezeTimer = SpawnFreezeTime;
		_shootCooldownTimer = ShootCooldown * 0.5f;

		_player = GetNodeOrNull<Player>("/root/Main/Player");
		
		_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");

		if (_player != null)
		{
			AddCollisionExceptionWith(_player);
		}

		if (_sprite != null)
		{
			_defaultModulate = _sprite.Modulate;
		}

		if (GD.Randf() > 0.5f)
		{
			_strafeDirection = 1;
		}
		else
		{
			_strafeDirection = -1;
		}
		
		_strafeChangeTimer = (float)GD.RandRange(2.0, 4.0);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_player == null)
		{
			return;
		}

		float d = (float)delta;

		if (_spawnFreezeTimer > 0f)
		{
			_spawnFreezeTimer -= d;
			
			if (_spawnFreezeTimer <= 0f)
			{
				_spawnRampTimer = SpawnRampDuration;
			}
			
			return;
		}

		HandleShooting(d);
		
		HandleMovement(d);

		Velocity += GetSeparationForce();
		Velocity += _knockbackVelocity;
		
		if (_knockbackVelocity.Length() > 1f)
		{
			_knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, KnockbackDecay * d);
		}
		else
		{
			_knockbackVelocity = Vector2.Zero;
		}

		MoveAndSlide();
	}

	/// <summary>
	/// Calcola la direzione ottimale di movimento per mantenere la distanza preferita dal giocatore.
	/// Applica inoltre movimenti laterali (strafe) per rendere il nemico più imprevedibile.
	/// </summary>
	private void HandleMovement(float d)
	{
		Vector2 toPlayer = _player.GlobalPosition - GlobalPosition;
		float dist = toPlayer.Length();
		Vector2 dirToPlayer = toPlayer.Normalized();

		Vector2 strafeDir = new Vector2(-dirToPlayer.Y, dirToPlayer.X) * _strafeDirection;

		_strafeChangeTimer -= d;
		
		if (_strafeChangeTimer <= 0f)
		{
			_strafeDirection *= -1;
			
			_strafeChangeTimer = (float)GD.RandRange(2.0, 4.0);
		}

		Vector2 moveDir;

		if (dist < MinDistance)
		{
			float urgency = Mathf.Clamp(1.0f - dist / MinDistance, 0f, 1f);
			
			moveDir = (-dirToPlayer * urgency + strafeDir * (1f - urgency)).Normalized();
		}
		else if (dist > MaxDistance)
		{
			float urgency = Mathf.Clamp((dist - MaxDistance) / 40f, 0f, 1f);
			
			moveDir = (dirToPlayer * urgency + strafeDir * (1f - urgency)).Normalized();
		}
		else
		{
			float center = (MinDistance + MaxDistance) * 0.5f;
			
			float drift = (dist - center) / (MaxDistance - MinDistance) * 0.3f;
			
			moveDir = (strafeDir + dirToPlayer * drift).Normalized();
		}

		float speed = MoveSpeed;
		float speedMultiplier = 1.0f;

		if (_spawnRampTimer > 0f)
		{
			_spawnRampTimer -= d;
			
			float t = 1.0f - Mathf.Clamp(_spawnRampTimer / SpawnRampDuration, 0f, 1f);
			
			t = t * t * (3f - 2f * t);
			speedMultiplier = t;
		}

		Velocity = moveDir * speed * speedMultiplier;
	}

	/// <summary>
	/// Controlla il cooldown di fuoco e istanzia un proiettile puntato verso la posizione attuale del giocatore.
	/// </summary>
	private void HandleShooting(float d)
	{
		if (_shootCooldownTimer > 0f)
		{
			_shootCooldownTimer -= d;
			return;
		}

		if (ProjectileScene == null)
		{
			return;
		}

		Vector2 dirToPlayer = (_player.GlobalPosition - GlobalPosition).Normalized();
		var projectile = ProjectileScene.Instantiate<GhostProjectile>();
		
		projectile.GlobalPosition = GlobalPosition + dirToPlayer * 15f;
		
		projectile.Initialize(dirToPlayer, Damage);
		
		GetTree().CurrentScene.AddChild(projectile);

		_shootCooldownTimer = ShootCooldown;
	}

	public void TakeDamage(int amount = 1)
	{
		if (_currentHealth <= 0)
		{
			return;
		}

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
		{
			return;
		}

		_flashTween?.Kill();
		_flashTween = CreateTween();

		Color red = new Color(1f, 0.2f, 0.2f, 1f);

		_flashTween.TweenProperty(_sprite, "modulate", red, 0.05f);
		
		_flashTween.TweenProperty(_sprite, "modulate", _defaultModulate, HitFlashDuration - 0.05f);
	}

	private void SpawnDeathEffect()
	{
		if (DeathScene == null)
		{
			return;
		}

		var death = DeathScene.Instantiate<Node2D>();
		death.GlobalPosition = GlobalPosition;

		if (_sprite != null)
		{
			death.Scale = _sprite.Scale;
		}

		var parent = GetParent();
		
		if (parent != null)
		{
			parent.AddChild(death);
		}
		else
		{
			GetTree().CurrentScene.AddChild(death);
		}

		if (death is AnimatedSprite2D anim)
		{
			anim.Modulate = new Color(1f, 0.2f, 0.2f, 1f);
			
			var tween = anim.CreateTween();
			
			tween.TweenProperty(anim, "modulate", new Color(1f, 1f, 1f, 1f), 0.3f);

			anim.Play("ghostdeath");
			
			anim.AnimationFinished += () => anim.QueueFree();
		}
	}

	/// <summary>
	/// Genera una forza repulsiva rispetto agli altri nemici vicini per evitare la compenetrazione (Separation).
	/// </summary>
	private Vector2 GetSeparationForce()
	{
		Vector2 separation = Vector2.Zero;
		var parent = GetParent();
		
		if (parent == null)
		{
			return separation;
		}

		foreach (var child in parent.GetChildren())
		{
			if (child == this)
			{
				continue;
			}

			if (child is not CharacterBody2D other)
			{
				continue;
			}

			if (other is not Enemy && other is not Ghost)
			{
				continue;
			}

			Vector2 diff = GlobalPosition - other.GlobalPosition;
			float dist = diff.Length();

			if (dist > 0f && dist < SeparationRadius)
			{
				separation += diff.Normalized() * (SeparationRadius - dist) / SeparationRadius;
			}
		}

		if (_player != null)
		{
			Vector2 playerDiff = GlobalPosition - _player.GlobalPosition;
			float playerDist = playerDiff.Length();
			float playerPushRadius = SeparationRadius * 0.6f;

			if (playerDist > 0f && playerDist < playerPushRadius)
			{
				separation += playerDiff.Normalized() * (playerPushRadius - playerDist) / playerPushRadius * 0.5f;
			}
		}

		return separation * SeparationStrength;
	}
}
