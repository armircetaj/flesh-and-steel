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
		if (!OS.HasFeature("demo") && !OS.IsDebugBuild())
		{
			QueueFree();
			return;
		}

		_menuPanel = GetNode<Control>("MenuPanel");
		_menuPanel.Visible = false;

		_player = GetNodeOrNull<Player>("/root/Main/Player");
		_roomManager = GetNodeOrNull<RoomManager>("/root/Main/RoomManager");

		if (_player == null || _roomManager == null)
		{
			GD.PrintErr("ModMenu: Could not find Player or RoomManager!");
		}

		var vbox = GetNode<VBoxContainer>("MenuPanel/VBoxContainer");
		var btnKillEnemies = vbox.GetNode<Button>("BtnKillEnemies");
		var btnClearRooms = vbox.GetNode<Button>("BtnClearRooms");
		var btnKillBoss = vbox.GetNode<Button>("BtnKillBoss");
		var btnFullHeal = vbox.GetNode<Button>("BtnFullHeal");
		_invincibilityButton = vbox.GetNode<CheckButton>("CheckInvincibility");

		btnKillEnemies.Pressed += OnKillEnemiesPressed;
		btnClearRooms.Pressed += OnClearRoomsPressed;
		btnKillBoss.Pressed += OnKillBossPressed;
		btnFullHeal.Pressed += OnFullHealPressed;
		_invincibilityButton.Toggled += OnInvincibilityToggled;

		var btnSpawnCoal = new Button();
		btnSpawnCoal.Text = "Spawn Coal";
		btnSpawnCoal.Pressed += OnSpawnCoalPressed;
		vbox.AddChild(btnSpawnCoal);

		var btnSpawnGhost = new Button();
		btnSpawnGhost.Text = "Spawn Ghost";
		btnSpawnGhost.Pressed += OnSpawnGhostPressed;
		vbox.AddChild(btnSpawnGhost);
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

	/// <summary>
	/// Intercetta gli input da tastiera non gestiti per rilevare l'inserimento del codice cheat.
	/// Utilizza un buffer per memorizzare gli ultimi tasti premuti.
	/// </summary>
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

	/// <summary>
	/// Alterna la visibilità del pannello dei cheat e aggiorna lo stato dei bottoni in base ai cheat attivi.
	/// </summary>
	private void ToggleMenu()
	{
		_menuPanel.Visible = !_menuPanel.Visible;
		
		if (_menuPanel.Visible && _player != null)
		{
			_invincibilityButton.ButtonPressed = _player.IsInvincibleCheat;
		}
	}

	/// <summary>
	/// Richiede al RoomManager di eliminare tutti i nemici presenti nella stanza corrente.
	/// </summary>
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

	private void OnSpawnCoalPressed()
	{
		_roomManager?.SpawnEnemyAtCenter("coal");
	}

	private void OnSpawnGhostPressed()
	{
		_roomManager?.SpawnEnemyAtCenter("ghost");
	}
}
