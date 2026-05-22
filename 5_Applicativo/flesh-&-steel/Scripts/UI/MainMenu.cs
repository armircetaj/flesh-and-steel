using Godot;
using System;

public partial class MainMenu : Control
{
	public override void _Ready()
	{
		var playBtn = GetNodeOrNull<Button>("VBoxContainer/PlayButton");
		var quitBtn = GetNodeOrNull<Button>("VBoxContainer/QuitButton");

		if (playBtn != null)
			playBtn.Pressed += OnPlayPressed;

		if (quitBtn != null)
			quitBtn.Pressed += OnQuitPressed;
			
		// Make sure mouse is visible in the menu if it was hidden in game
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	private void OnPlayPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
	}

	private void OnQuitPressed()
	{
		GetTree().Quit();
	}
}
