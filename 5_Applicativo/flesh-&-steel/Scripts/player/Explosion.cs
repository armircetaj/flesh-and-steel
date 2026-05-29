using Godot;

public partial class Explosion : AnimatedSprite2D
{
	public override void _Ready()
	{
		Play("explode");
		
		AnimationFinished += OnAnimationFinished;
	}

	/// <summary>
	/// Elimina l'istanza dell'esplosione una volta terminata l'animazione, liberando memoria.
	/// </summary>
	private void OnAnimationFinished()
	{
		QueueFree();
	}
}
