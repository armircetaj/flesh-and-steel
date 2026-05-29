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
		{
			_quitBtn.Pressed += OnQuitPressed;
		}

		if (_btnHelp != null)
		{
			_btnHelp.Pressed += OnHelpPressed;
		}

		if (_btnCloseHelp != null)
		{
			_btnCloseHelp.Pressed += OnCloseHelpPressed;
		}
			
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	/// <summary>
	/// Mostra il popup dei comandi e trasferisce il focus sul bottone di chiusura per garantire l'accessibilità.
	/// </summary>
	private void OnHelpPressed()
	{
		if (_controlsPopup != null)
		{
			_controlsPopup.Visible = true;
			
			if (_btnCloseHelp != null)
			{
				_btnCloseHelp.GrabFocus();
			}
		}
	}

	/// <summary>
	/// Nasconde il popup dei comandi e ripristina il focus sul bottone di avvio per permettere all'utente di iniziare a giocare.
	/// </summary>
	private void OnCloseHelpPressed()
	{
		if (_controlsPopup != null)
		{
			_controlsPopup.Visible = false;
			
			if (_playBtn != null)
			{
				_playBtn.GrabFocus();
			}
		}
	}

	/// <summary>
	/// Cambia la scena corrente a quella principale del gioco, avviando di fatto la partita.
	/// </summary>
	private void OnPlayPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
	}

	/// <summary>
	/// Termina l'esecuzione del gioco chiudendo l'applicazione.
	/// </summary>
	private void OnQuitPressed()
	{
		GetTree().Quit();
	}
}
