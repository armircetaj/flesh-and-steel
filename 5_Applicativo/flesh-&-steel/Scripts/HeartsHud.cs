using Godot;

public partial class HeartsHud : CanvasLayer
{
	[Export]
	public int MaxHearts = 5;

	private int _currentHearts;

	public override void _Ready()
	{
		SetHearts(MaxHearts);
	}

	public void TakeDamage(int amount = 1)
	{
		for (int i = 0; i < amount; i++)
		{
			if (_currentHearts > 0)
			{
				LoseOneHeart();
			}
		}
	}

	public void SetHearts(int hearts)
	{
		_currentHearts = Mathf.Clamp(hearts, 0, MaxHearts);
		
		UpdateHeartsVisibility();
	}

	public void ResetHearts()
	{
		SetHearts(MaxHearts);
	}

	/// <summary>
	/// Rimuove visivamente un cuore dall'HUD riproducendo un'animazione fluida di scomparsa verso l'alto.
	/// </summary>
	private void LoseOneHeart()
	{
		if (_currentHearts <= 0)
		{
			return;
		}

		int heartIndex = _currentHearts - 1;
		_currentHearts = heartIndex;

		var heartsContainer = GetNode<Container>("HeartsContainer");
		
		var heartNode = (Control)heartsContainer.GetChild(heartIndex);

		Vector2 startPos = heartNode.Position;
		Vector2 endPos = startPos + new Vector2(0, -150);

		var tween = CreateTween();
		
		tween.TweenProperty(heartNode, "position", endPos, 0.8).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

		tween.Parallel().TweenProperty(heartNode, "modulate:a", 0.0f, 0.8f);

		tween.Finished += () =>
		{
			heartNode.Visible = heartIndex < _currentHearts;
			heartNode.Position = startPos;

			Color mod = heartNode.Modulate;
			mod.A = 1.0f;
			heartNode.Modulate = mod;
		};
	}

	/// <summary>
	/// Sincronizza immediatamente la visibilità dei cuori nell'interfaccia con il valore corrente della salute.
	/// </summary>
	private void UpdateHeartsVisibility()
	{
		var heartsContainer = GetNode<Container>("HeartsContainer");

		for (int i = 0; i < heartsContainer.GetChildCount(); i++)
		{
			var heartNode = (Control)heartsContainer.GetChild(i);
			
			heartNode.Visible = i < _currentHearts;

			Color mod = heartNode.Modulate;
			mod.A = 1.0f;
			heartNode.Modulate = mod;
		}
	}
}
