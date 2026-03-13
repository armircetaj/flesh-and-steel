using Godot;

public partial class Explosion : AnimatedSprite2D
{
	public override void _Ready()
	{
		Play("explode");
		AnimationFinished += OnAnimationFinished;
	}

	private void OnAnimationFinished()
	{
		QueueFree();
	}
}
