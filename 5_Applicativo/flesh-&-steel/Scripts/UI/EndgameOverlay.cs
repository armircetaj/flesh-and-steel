using Godot;
using System;

public partial class EndgameOverlay : CanvasLayer
{
	private ColorRect _bgRect;
	private Control _centerContainer;
	private Label _titleLabel;
	private Button _menuButton;

	public override void _Ready()
	{
		_bgRect = GetNodeOrNull<ColorRect>("ColorRect");
		_centerContainer = GetNodeOrNull<Control>("CenterContainer");
		_titleLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/TitleLabel");
		_menuButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/MenuButton");

		if (_menuButton != null)
		{
			_menuButton.Pressed += OnMenuPressed;
		}
	}

	public void TriggerGameOver()
	{
		GetTree().Paused = true;
		
		if (_titleLabel != null)
		{
			_titleLabel.Text = "DEAD";
			_titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.2f, 0.2f));
		}
		
		PlayFadeIn();
		
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	public void TriggerVictory()
	{
		GetTree().Paused = true;
		
		if (_titleLabel != null)
		{
			_titleLabel.Text = "THE WARDEN FALLS";
			_titleLabel.AddThemeColorOverride("font_color", new Color(0.2f, 0.6f, 1f));
		}

		PlayFadeIn();
		
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	private void PlayFadeIn()
	{
		Visible = true;
		
		if (_bgRect != null)
		{
			var tween = CreateTween();
			tween.SetPauseMode(Tween.TweenPauseMode.Process); // So it runs while paused
			tween.TweenProperty(_bgRect, "color", new Color(0, 0, 0, 0.9f), 1.5f);
		}
		
		if (_centerContainer != null)
		{
			var tween2 = CreateTween();
			tween2.SetPauseMode(Tween.TweenPauseMode.Process);
			// Delay text fade slightly
			tween2.TweenInterval(0.5f);
			tween2.TweenProperty(_centerContainer, "modulate", new Color(1, 1, 1, 1f), 1.5f);
		}
	}

	private void OnMenuPressed()
	{
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu.tscn");
	}
}
