using Godot;
using System;
using System.Collections.Generic;

// Si occupa della generazione procedurale della mappa (griglia 3x3), dell'istanziamento 
// delle stanze (Start, Combat, Boss) e del posizionamento/tracking dei nemici, // fungendo da gestore di stato globale per la progressione dei livelli (Single Responsibility).
public partial class RoomManager : Node
{
	public enum RoomType
	{
		Start, Combat, Boss
	}

	public enum EntrySide
	{
		None, Top, Bottom, Left, Right
	}

	private RoomType[,] map = new RoomType[3, 3];
	private bool[,] visited = new bool[3, 3];
	private bool[,] cleared = new bool[3, 3];

	private int currentX = 1;
	private int currentY = 1;

	private PackedScene startRoom;
	private PackedScene combatRoom;
	private PackedScene bossRoom;
	
	private PackedScene _coalScene;
	private PackedScene _ghostScene;
	private PackedScene _wardenScene;

	private Node2D currentRoomInstance;
	private ulong _roomChangeCooldownUntilMs = 0;
	private bool _roomChangeQueued = false;
	
	private int _pendingX = 1;
	private int _pendingY = 1;
	private EntrySide _pendingEntrySide = EntrySide.None;

	private Texture2D _topRoomTex;
	private Texture2D _bottomRoomTex;
	private Texture2D _leftRoomTex;
	private Texture2D _rightRoomTex;
	
	private Texture2D _topLeftRoomTex;
	private Texture2D _topRightRoomTex;
	private Texture2D _bottomLeftRoomTex;
	private Texture2D _bottomRightRoomTex;

	private Texture2D _topLeftBossRoomTex;
	private Texture2D _topRightBossRoomTex;
	private Texture2D _bottomLeftBossRoomTex;
	private Texture2D _bottomRightBossRoomTex;

	private List<Node2D> _activeEnemies = new();
	private int _aliveEnemyCount = 0;

	private RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		startRoom = GD.Load<PackedScene>("res://Scenes/Rooms/StartRoom.tscn");
		combatRoom = GD.Load<PackedScene>("res://Scenes/Rooms/CombatRoom.tscn");
		bossRoom = GD.Load<PackedScene>("res://Scenes/Rooms/BossRoom.tscn");
		
		_coalScene = GD.Load<PackedScene>("res://Scenes/Enemy/Coal.tscn");
		_ghostScene = GD.Load<PackedScene>("res://Scenes/Enemy/Ghost.tscn");
		_wardenScene = GD.Load<PackedScene>("res://Scenes/Enemy/Warden.tscn");

		_topRoomTex = GD.Load<Texture2D>("res://Assets/ui/TopRoom.png");
		_bottomRoomTex = GD.Load<Texture2D>("res://Assets/ui/BottomRoom.png");
		_leftRoomTex = GD.Load<Texture2D>("res://Assets/ui/LeftRoom.png");
		_rightRoomTex = GD.Load<Texture2D>("res://Assets/ui/RightRoom.png");
		
		_topLeftRoomTex = GD.Load<Texture2D>("res://Assets/ui/TopLeftRoom.png");
		_topRightRoomTex = GD.Load<Texture2D>("res://Assets/ui/TopRightRoom.png");
		_bottomLeftRoomTex = GD.Load<Texture2D>("res://Assets/ui/BottomLeftRoom.png");
		_bottomRightRoomTex = GD.Load<Texture2D>("res://Assets/ui/BottomRightRoom.png");

		_topLeftBossRoomTex = GD.Load<Texture2D>("res://Assets/ui/TopLeftBossRoom.png");
		_topRightBossRoomTex = GD.Load<Texture2D>("res://Assets/ui/TopRightBossRoom.png");
		_bottomLeftBossRoomTex = GD.Load<Texture2D>("res://Assets/ui/BottomLeftBossRoom.png");
		_bottomRightBossRoomTex = GD.Load<Texture2D>("res://Assets/ui/BottomRightBossRoom.png");

		_rng.Randomize();
		GenerateMap();
		
		LoadRoom(currentX, currentY);
	}

	/// <summary>
	/// Inizializza la griglia della mappa impostando tutte le celle come stanze di combattimento,
	/// piazza la stanza iniziale al centro (1,1) e sceglie casualmente uno dei quattro angoli per la stanza del Boss.
	/// </summary>
	private void GenerateMap()
	{
		for(int x = 0; x < 3; x++)
		{
			for(int y = 0; y < 3; y++)
			{
				map[x,y] = RoomType.Combat;
			}
		}

		map[1,1] = RoomType.Start;

		Vector2I[] corners = new Vector2I[]
		{
			new Vector2I(0, 0), new Vector2I(2, 0), new Vector2I(0, 2), new Vector2I(2, 2)
		};

		int index = (int)_rng.RandiRange(0, corners.Length - 1);
		
		Vector2I bossPos = corners[index];
		map[bossPos.X, bossPos.Y] = RoomType.Boss;	
	}

	private void LoadRoom(int x, int y)
	{
		LoadRoom(x, y, EntrySide.None);
	}

	/// <summary>
	/// Rimuove la stanza corrente (e i relativi nemici) e istanzia la nuova stanza.
	/// Si occupa inoltre di generare i nemici, aggiornare lo sfondo e determinare l'entry point del giocatore.
	/// </summary>
	private void LoadRoom(int x, int y, EntrySide entrySideInTarget)
	{
		foreach (var tracked in _activeEnemies)
		{
			if (!IsInstanceValid(tracked))
			{
				continue;
			}
			
			if (tracked is Enemy coal)
			{
				coal.Died -= OnEnemyDied;
			}
			else if (tracked is Ghost ghost)
			{
				ghost.Died -= OnEnemyDied;
			}
			else if (tracked is Warden warden)
			{
				warden.Died -= OnEnemyDied;
			}
		}
		
		_activeEnemies.Clear();
		_aliveEnemyCount = 0;

		if(currentRoomInstance != null)
		{
			currentRoomInstance.QueueFree();
		}

		PackedScene roomToLoad = map[x, y] switch
		{
			RoomType.Start => startRoom, RoomType.Boss => bossRoom, _ => combatRoom
		};

		currentRoomInstance = roomToLoad.Instantiate<Node2D>();

		Node2D container = GetNode<Node2D>("../CurrentRoom");
		
		container.AddChild(currentRoomInstance);

		currentX = x;
		currentY = y;
		visited[x, y] = true;

		SpawnEnemiesForRoom();

		if (map[x, y] == RoomType.Boss)
		{
			SpawnBoss();
		}

		UpdateRoomBackgroundSprite(x, y, map[x, y]);
		
		UpdateDoorsForCurrentRoom(false);
		
		SpawnPlayer(entrySideInTarget);
	}

	private void SpawnEnemiesForRoom()
	{
		if (currentRoomInstance == null)
		{
			return;
		}

		if (map[currentX, currentY] != RoomType.Combat)
		{
			return;
		}

		if (cleared[currentX, currentY])
		{
			return;
		}

		var spawnsNode = currentRoomInstance.GetNodeOrNull<Node2D>("EnemySpawns");
		
		if (spawnsNode == null)
		{
			return;
		}

		var spawnPoints = new List<Vector2>();
		
		foreach (var child in spawnsNode.GetChildren())
		{
			if (child is Marker2D marker)
			{
				spawnPoints.Add(marker.GlobalPosition);
			}
		}

		if (spawnPoints.Count == 0)
		{
			return;
		}

		int count = (int)_rng.RandiRange(2, Mathf.Min(3, spawnPoints.Count));

		ShuffleList(spawnPoints);

		for (int i = 0; i < count; i++)
		{
			bool spawnGhost = i > 0 && _rng.Randf() < 0.4f;

			if (spawnGhost)
			{
				var ghost = _ghostScene.Instantiate<Ghost>();
				
				currentRoomInstance.AddChild(ghost);
				
				ghost.GlobalPosition = spawnPoints[i];
				ghost.Died += OnEnemyDied;
				
				_activeEnemies.Add(ghost);
			}
			else
			{
				var coal = _coalScene.Instantiate<Enemy>();
				
				currentRoomInstance.AddChild(coal);
				
				coal.GlobalPosition = spawnPoints[i];
				coal.Died += OnEnemyDied;
				
				_activeEnemies.Add(coal);
			}
		}

		_aliveEnemyCount = count;
	}

	private void SpawnBoss()
	{
		if (currentRoomInstance == null || _wardenScene == null)
		{
			return;
		}

		if (cleared[currentX, currentY])
		{
			return;
		}

		var spawnMarker = currentRoomInstance.GetNodeOrNull<Marker2D>("BossSpawn");
		
		if (spawnMarker == null)
		{
			spawnMarker = currentRoomInstance.GetNodeOrNull<Marker2D>("PlayerSpawn");
		}
			
		Vector2 spawnPos = spawnMarker != null ? spawnMarker.GlobalPosition : Vector2.Zero;

		var warden = _wardenScene.Instantiate<Warden>();
		
		currentRoomInstance.AddChild(warden);
		
		warden.GlobalPosition = spawnPos;
		warden.Died += OnEnemyDied;
		warden.Died += OnBossDied;
		
		_activeEnemies.Add(warden);
		
		_aliveEnemyCount = 1;
	}

	private void OnBossDied()
	{
		var timer = GetTree().CreateTimer(2.5f);
		
		timer.Timeout += () => 
		{
			var endgame = GetNodeOrNull<EndgameOverlay>("/root/Main/EndgameOverlay");
			
			if (endgame != null)
			{
				endgame.TriggerVictory();
			}
		};
	}

	private void OnEnemyDied()
	{
		_aliveEnemyCount--;

		if (_aliveEnemyCount <= 0)
		{
			_aliveEnemyCount = 0;
			cleared[currentX, currentY] = true;

			UpdateDoorsForCurrentRoom(true);
		}
	}

	public void KillAllEnemiesInCurrentRoom()
	{
		var enemiesToKill = new List<Node2D>(_activeEnemies);
		
		foreach (var enemyNode in enemiesToKill)
		{
			if (!IsInstanceValid(enemyNode))
			{
				continue;
			}

			if (enemyNode is Enemy coal)
			{
				coal.TakeDamage(9999);
			}
			else if (enemyNode is Ghost ghost)
			{
				ghost.TakeDamage(9999);
			}
			else if (enemyNode is Warden warden)
			{
				warden.TakeDamage(9999, fromDash: false);
			}
		}
	}

	public void KillBoss()
	{
		var enemiesToKill = new List<Node2D>(_activeEnemies);
		
		foreach (var enemyNode in enemiesToKill)
		{
			if (IsInstanceValid(enemyNode) && enemyNode is Warden warden)
			{
				warden.TakeDamage(9999, fromDash: false);
			}
		}
	}

	public void ClearAllRoomsExceptBoss()
	{
		for (int x = 0; x < 3; x++)
		{
			for (int y = 0; y < 3; y++)
			{
				if (map[x, y] == RoomType.Combat)
				{
					cleared[x, y] = true;
				}
			}
		}

		if (map[currentX, currentY] == RoomType.Combat && _aliveEnemyCount > 0)
		{
			KillAllEnemiesInCurrentRoom();
		}
		
		UpdateDoorsForCurrentRoom(true);
	}

	private void ShuffleList<T>(List<T> list)
	{
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = (int)_rng.RandiRange(0, i);
			
			(list[i], list[j]) = (list[j], list[i]);
		}
	}

	private void UpdateRoomBackgroundSprite(int x, int y, RoomType roomType)
	{
		if (currentRoomInstance == null)
		{
			return;
		}

		var bgSprite = currentRoomInstance.GetNodeOrNull<Sprite2D>("Sprite2D");
		
		if (bgSprite == null)
		{
			return;
		}

		Texture2D tex = GetRoomBackgroundForCell(roomType, x, y);
		
		if (tex != null)
		{
			bgSprite.Texture = tex;
		}
	}

	private Texture2D GetRoomBackgroundForCell(RoomType roomType, int x, int y)
	{
		if (roomType == RoomType.Start)
		{
			return null;
		}

		if (roomType == RoomType.Boss)
		{
			if (x == 0 && y == 0) return _topLeftBossRoomTex;
			if (x == 2 && y == 0) return _topRightBossRoomTex;
			if (x == 0 && y == 2) return _bottomLeftBossRoomTex;
			if (x == 2 && y == 2) return _bottomRightBossRoomTex;

			return null;
		}

		if (x == 1 && y == 0) return _topRoomTex;
		if (x == 1 && y == 2) return _bottomRoomTex;
		if (x == 0 && y == 1) return _leftRoomTex;
		if (x == 2 && y == 1) return _rightRoomTex;

		if (x == 0 && y == 0) return _topLeftRoomTex;
		if (x == 2 && y == 0) return _topRightRoomTex;
		if (x == 0 && y == 2) return _bottomLeftRoomTex;
		if (x == 2 && y == 2) return _bottomRightRoomTex;

		return null;
	}

	private void UpdateDoorsForCurrentRoom(bool playUnlockAnimation = false)
	{
		if (currentRoomInstance == null)
		{
			return;
		}

		var doorsNode = currentRoomInstance.GetNodeOrNull<Node2D>("Doors");
		
		if (doorsNode == null)
		{
			return;
		}

		var doorInfos = new (string name, int dx, int dy)[]
		{
			("DoorTop", 0, -1), ("DoorBottom", 0, 1), ("DoorLeft", -1, 0), ("DoorRight", 1, 0)
		};

		foreach (var (name, dx, dy) in doorInfos)
		{
			var door = doorsNode.GetNodeOrNull<Door>(name);
			
			if (door == null)
			{
				continue;
			}

			int nx = currentX + dx;
			int ny = currentY + dy;

			bool hasNeighbor = nx >= 0 && nx < 3 && ny >= 0 && ny < 3;

			if (!hasNeighbor)
			{
				door.Hide();
				continue;
			}

			if (map[currentX, currentY] == RoomType.Boss)
			{
				door.Lock(false);
				
				door.Hide();
				
				continue;
			}

			bool neighborIsBoss = map[nx, ny] == RoomType.Boss;
			bool bossLocked = neighborIsBoss && !IsFloorCleared();

			if (bossLocked)
			{
				door.Lock(true);
			}
			else if (_aliveEnemyCount > 0)
			{
				door.Lock(false);
			}
			else
			{
				door.Unlock(playUnlockAnimation, neighborIsBoss);
			}
		}
	}

	private bool IsFloorCleared()
	{
		for (int x = 0; x < 3; x++)
		{
			for (int y = 0; y < 3; y++)
			{
				if (map[x, y] == RoomType.Combat && !cleared[x, y])
				{
					return false;
				}
			}
		}
		
		return true;
	}

	private void SpawnPlayer(EntrySide entrySideInTarget)
	{
		Node2D player = GetNode<Node2D>("../Player");
		
		if (player is CharacterBody2D cb)
		{
			cb.Velocity = Vector2.Zero;
		}

		if(entrySideInTarget == EntrySide.None)
		{
			if(currentRoomInstance.HasNode("PlayerSpawn"))
			{
				Marker2D spawn = currentRoomInstance.GetNode<Marker2D>("PlayerSpawn");
				
				player.GlobalPosition = spawn.GlobalPosition;
			}
			
			return;
		}

		var doorsRoot = currentRoomInstance.GetNodeOrNull<Node2D>("Doors");
		
		if (doorsRoot == null)
		{
			return;
		}

		string doorName = entrySideInTarget switch
		{
			EntrySide.Top => "DoorTop", EntrySide.Bottom => "DoorBottom", EntrySide.Left => "DoorLeft", EntrySide.Right => "DoorRight", _ => ""
		};

		if (string.IsNullOrEmpty(doorName))
		{
			return;
		}

		var doorArea = doorsRoot.GetNodeOrNull<Area2D>(doorName);
		
		if (doorArea == null)
		{
			return;
		}

		var shape = doorArea.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		
		if (shape == null)
		{
			return;
		}

		Vector2 inset = GetDoorInset(entrySideInTarget);
		
		player.GlobalPosition = shape.GlobalPosition + inset;
	}

	private Vector2 GetDoorInset(EntrySide side)
	{
		const float inset = 35f;
		
		return side switch
		{
			EntrySide.Top => new Vector2(0, inset), EntrySide.Bottom => new Vector2(0, -inset), EntrySide.Left => new Vector2(inset, 0), EntrySide.Right => new Vector2(-inset, 0), _ => Vector2.Zero
		};
	}

	private bool CanChangeRoom()
	{
		return Time.GetTicksMsec() >= _roomChangeCooldownUntilMs;
	}

	private void StartRoomChangeCooldown()
	{
		_roomChangeCooldownUntilMs = Time.GetTicksMsec() + (ulong)250;
	}

	private void RequestRoomChange(int x, int y, EntrySide entrySideInTarget)
	{
		if (_roomChangeQueued)
		{
			return;
		}

		_roomChangeQueued = true;
		_pendingX = x;
		_pendingY = y;
		_pendingEntrySide = entrySideInTarget;

		StartRoomChangeCooldown();
		
		CallDeferred(nameof(ApplyPendingRoomChange));
	}

	private void ApplyPendingRoomChange()
	{
		_roomChangeQueued = false;
		
		PerformRoomTransitionAsync(_pendingX, _pendingY, _pendingEntrySide);
	}

	/// <summary>
	/// Esegue la transizione visiva e logica tra le stanze in modo asincrono:
	/// mette in pausa il gioco, avvia un fade a nero, carica la nuova stanza e infine dissolve il fade.
	/// </summary>
	private async void PerformRoomTransitionAsync(int targetX, int targetY, EntrySide entrySide)
	{
		GetTree().Paused = true;

		var transitionLayer = new CanvasLayer() 
		{ 
			Layer = 110 
		};
		
		var colorRect = new ColorRect()
		{
			Color = new Color(0, 0, 0, 0), };
		
		colorRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		
		transitionLayer.AddChild(colorRect);
		
		GetTree().CurrentScene.AddChild(transitionLayer);

		var tweenIn = CreateTween();
		
		tweenIn.SetPauseMode(Tween.TweenPauseMode.Process);
		
		tweenIn.TweenProperty(colorRect, "color", new Color(0, 0, 0, 1), 0.3f);
		
		await ToSignal(tweenIn, Tween.SignalName.Finished);

		ClearLingeringProjectiles();
		LoadRoom(targetX, targetY, entrySide);

		var tweenOut = CreateTween();
		
		tweenOut.SetPauseMode(Tween.TweenPauseMode.Process);
		
		tweenOut.TweenProperty(colorRect, "color", new Color(0, 0, 0, 0), 0.3f);
		
		await ToSignal(tweenOut, Tween.SignalName.Finished);

		transitionLayer.QueueFree();
		GetTree().Paused = false;
		
		StartRoomChangeCooldown();
	}

	private void ClearLingeringProjectiles()
	{
		foreach (var child in GetTree().CurrentScene.GetChildren())
		{
			if (child is Projectile || child is GhostProjectile || child is Explosion)
			{
				child.QueueFree();
			}
		}
	}

	public void GoUp()
	{
		GoUp(EntrySide.None);
	}

	public void GoUp(EntrySide entrySideInTarget)
	{
		if (!CanChangeRoom())
		{
			return;
		}

		if(currentY > 0)
		{
			RequestRoomChange(currentX, currentY - 1, entrySideInTarget);
		}
	}

	public void GoDown()
	{
		GoDown(EntrySide.None);
	}

	public void GoDown(EntrySide entrySideInTarget)
	{
		if (!CanChangeRoom())
		{
			return;
		}

		if(currentY < 2)
		{
			RequestRoomChange(currentX, currentY + 1, entrySideInTarget);
		}
	}

	public void GoLeft()
	{
		GoLeft(EntrySide.None);
	}

	public void GoLeft(EntrySide entrySideInTarget)
	{
		if (!CanChangeRoom())
		{
			return;
		}

		if(currentX > 0)
		{
			RequestRoomChange(currentX - 1, currentY, entrySideInTarget);
		}
	}

	public void GoRight()
	{
		GoRight(EntrySide.None);
	}

	public void GoRight(EntrySide entrySideInTarget)
	{
		if (!CanChangeRoom())
		{
			return;
		}

		if(currentX < 2)
		{
			RequestRoomChange(currentX + 1, currentY, entrySideInTarget);
		}
	}

	public RoomType[,] GetMap()
	{
		return map;
	}

	public bool[,] GetVisited()
	{
		return visited;
	}

	public bool[,] GetCleared()
	{
		return cleared;
	}

	public Vector2I GetCurrentRoomCoords()
	{
		return new Vector2I(currentX, currentY);
	}

	public void SpawnEnemyAtCenter(string enemyType)
	{
		if (currentRoomInstance == null || map[currentX, currentY] != RoomType.Combat)
		{
			return; 
		}

		Node2D newEnemy = null;
		if (enemyType == "coal" && _coalScene != null)
		{
			var coal = _coalScene.Instantiate<Enemy>();
			coal.Died += OnEnemyDied;
			newEnemy = coal;
		}
		else if (enemyType == "ghost" && _ghostScene != null)
		{
			var ghost = _ghostScene.Instantiate<Ghost>();
			ghost.Died += OnEnemyDied;
			newEnemy = ghost;
		}

		if (newEnemy != null)
		{
			currentRoomInstance.AddChild(newEnemy);
			
			var spawnMarker = currentRoomInstance.GetNodeOrNull<Marker2D>("PlayerSpawn");
			if (spawnMarker != null)
			{
				newEnemy.GlobalPosition = spawnMarker.GlobalPosition;
			}
			else
			{
				newEnemy.GlobalPosition = currentRoomInstance.GlobalPosition;
			}
			
			_activeEnemies.Add(newEnemy);
			_aliveEnemyCount++;
			
			if (cleared[currentX, currentY])
			{
				cleared[currentX, currentY] = false;
				UpdateDoorsForCurrentRoom(false);
			}
		}
	}
}
