using Godot;
using System;

public partial class MainMenu : Control
{
	private Button _playBtn;
	private Button _quitBtn;
	private Button _btnHelp;
	private Button _btnCloseHelp;
	private Control _controlsPopup;

	public override void _Ready()
	{
		_playBtn = GetNodeOrNull<Button>("VBoxContainer/PlayButton");
		_quitBtn = GetNodeOrNull<Button>("VBoxContainer/QuitButton");
		_btnHelp = GetNodeOrNull<Button>("BtnHelp");
		_controlsPopup = GetNodeOrNull<Control>("ControlsPopup");
		_btnCloseHelp = GetNodeOrNull<Button>("ControlsPopup/VBoxContainer/HBoxContainer/BtnCloseHelp");

		if (_playBtn != null)
		{
			_playBtn.Pressed += OnPlayPressed;
			_playBtn.GrabFocus();
		}

		if (_quitBtn != null)
			_quitBtn.Pressed += OnQuitPressed;

		if (_btnHelp != null)
			_btnHelp.Pressed += OnHelpPressed;

		if (_btnCloseHelp != null)
			_btnCloseHelp.Pressed += OnCloseHelpPressed;
			
		// Make sure mouse is visible in the menu if it was hidden in game
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	private void OnHelpPressed()
	{
		if (_controlsPopup != null)
		{
			_controlsPopup.Visible = true;
			if (_btnCloseHelp != null)
				_btnCloseHelp.GrabFocus();
		}
	}

	private void OnCloseHelpPressed()
	{
		if (_controlsPopup != null)
		{
			_controlsPopup.Visible = false;
			if (_playBtn != null)
				_playBtn.GrabFocus();
		}
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
