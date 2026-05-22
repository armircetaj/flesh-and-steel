using Godot;
using System.Collections.Generic;

public partial class ModMenu : CanvasLayer
{
	private Control _menuPanel;
	private Player _player;
	private RoomManager _roomManager;

	private readonly string _cheatCode = "cheat";
	private string _inputBuffer = "";
	private float _inputTimeout = 1.5f;
	private float _timeSinceLastInput = 0f;

	private CheckButton _invincibilityButton;

	public override void _Ready()
	{
		_menuPanel = GetNode<Control>("MenuPanel");
		_menuPanel.Visible = false;

		// Get nodes from the absolute root since ModMenu is deeply nested under UI CanvasLayer
		_player = GetNodeOrNull<Player>("/root/Main/Player");
		_roomManager = GetNodeOrNull<RoomManager>("/root/Main/RoomManager");

		if (_player == null || _roomManager == null)
		{
			GD.PrintErr("ModMenu: Could not find Player or RoomManager!");
		}

		var btnKillEnemies = GetNode<Button>("MenuPanel/VBoxContainer/BtnKillEnemies");
		var btnClearRooms = GetNode<Button>("MenuPanel/VBoxContainer/BtnClearRooms");
		var btnKillBoss = GetNode<Button>("MenuPanel/VBoxContainer/BtnKillBoss");
		var btnFullHeal = GetNode<Button>("MenuPanel/VBoxContainer/BtnFullHeal");
		_invincibilityButton = GetNode<CheckButton>("MenuPanel/VBoxContainer/CheckInvincibility");

		btnKillEnemies.Pressed += OnKillEnemiesPressed;
		btnClearRooms.Pressed += OnClearRoomsPressed;
		btnKillBoss.Pressed += OnKillBossPressed;
		btnFullHeal.Pressed += OnFullHealPressed;
		_invincibilityButton.Toggled += OnInvincibilityToggled;
	}

	public override void _Process(double delta)
	{
		if (_inputBuffer.Length > 0)
		{
			_timeSinceLastInput += (float)delta;
			if (_timeSinceLastInput > _inputTimeout)
			{
				_inputBuffer = "";
			}
		}
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			string keyString = OS.GetKeycodeString(keyEvent.Keycode).ToLower();
			if (keyString.Length == 1 && char.IsLetter(keyString[0]))
			{
				_inputBuffer += keyString;
				_timeSinceLastInput = 0f;

				if (_inputBuffer.EndsWith(_cheatCode))
				{
					ToggleMenu();
					_inputBuffer = "";
				}
				else if (_inputBuffer.Length > _cheatCode.Length * 2)
				{
					// Prevent buffer from growing indefinitely
					_inputBuffer = _inputBuffer.Substring(_inputBuffer.Length - _cheatCode.Length);
				}
			}
		}
	}

	private void ToggleMenu()
	{
		_menuPanel.Visible = !_menuPanel.Visible;
		
		if (_menuPanel.Visible && _player != null)
		{
			_invincibilityButton.ButtonPressed = _player.IsInvincibleCheat;
		}
	}

	private void OnKillEnemiesPressed()
	{
		_roomManager?.KillAllEnemiesInCurrentRoom();
	}

	private void OnClearRoomsPressed()
	{
		_roomManager?.ClearAllRoomsExceptBoss();
	}

	private void OnKillBossPressed()
	{
		_roomManager?.KillBoss();
	}

	private void OnFullHealPressed()
	{
		_player?.FullHeal();
	}

	private void OnInvincibilityToggled(bool toggledOn)
	{
		if (_player != null)
		{
			_player.IsInvincibleCheat = toggledOn;
		}
	}
}
