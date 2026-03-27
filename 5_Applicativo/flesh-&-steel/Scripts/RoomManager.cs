using Godot;
using System;
using System.Collections.Generic;

public partial class RoomManager : Node
{
	public enum RoomType
	{
		Start,
		Combat,
		Boss
	}

	public enum EntrySide
	{
		None,
		Top,
		Bottom,
		Left,
		Right
	}

	private RoomType[,] map = new RoomType[3,3];
	private bool[,] visited = new bool[3,3];
	private bool[,] cleared = new bool[3,3];

	private int currentX = 1;
	private int currentY = 1;

	private PackedScene startRoom;
	private PackedScene combatRoom;
	private PackedScene bossRoom;
	private PackedScene _enemyScene;

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

	private Texture2D _topRoomLockedTex;
	private Texture2D _bottomRoomLockedTex;
	private Texture2D _leftRoomLockedTex;
	private Texture2D _rightRoomLockedTex;
	private Texture2D _topLeftRoomLockedTex;
	private Texture2D _topRightRoomLockedTex;
	private Texture2D _bottomLeftRoomLockedTex;
	private Texture2D _bottomRightRoomLockedTex;

	private List<Enemy> _activeEnemies = new();
	private int _aliveEnemyCount = 0;

	private RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		startRoom = GD.Load<PackedScene>("res://Scenes/Rooms/StartRoom.tscn");
		combatRoom = GD.Load<PackedScene>("res://Scenes/Rooms/CombatRoom.tscn");
		bossRoom = GD.Load<PackedScene>("res://Scenes/Rooms/BossRoom.tscn");
		_enemyScene = GD.Load<PackedScene>("res://Scenes/Enemy/Enemy.tscn");

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

		_topRoomLockedTex = GD.Load<Texture2D>("res://Assets/ui/TopRoomLocked.png");
		_bottomRoomLockedTex = GD.Load<Texture2D>("res://Assets/ui/BottomRoomLocked.png");
		_leftRoomLockedTex = GD.Load<Texture2D>("res://Assets/ui/LeftRoomLocked.png");
		_rightRoomLockedTex = GD.Load<Texture2D>("res://Assets/ui/RightRoomLocked.png");
		_topLeftRoomLockedTex = GD.Load<Texture2D>("res://Assets/ui/TopLeftRoomLocked.png");
		_topRightRoomLockedTex = GD.Load<Texture2D>("res://Assets/ui/TopRightRoomLocked.png");
		_bottomLeftRoomLockedTex = GD.Load<Texture2D>("res://Assets/ui/BottomLeftRoomLocked.png");
		_bottomRightRoomLockedTex = GD.Load<Texture2D>("res://Assets/ui/BottomRightRoomLocked.png");

		_rng.Randomize();
		GenerateMap();
		LoadRoom(currentX, currentY);
	}

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
			new Vector2I(0, 0),
			new Vector2I(2, 0),
			new Vector2I(0, 2),
			new Vector2I(2, 2)
		};

		int index = (int)_rng.RandiRange(0, corners.Length - 1);
		Vector2I bossPos = corners[index];
		map[bossPos.X, bossPos.Y] = RoomType.Boss;	
	}

	private void LoadRoom(int x, int y)
	{
		LoadRoom(x, y, EntrySide.None);
	}

	private void LoadRoom(int x, int y, EntrySide entrySideInTarget)
	{
		foreach (var enemy in _activeEnemies)
		{
			if (IsInstanceValid(enemy))
				enemy.Died -= OnEnemyDied;
		}
		_activeEnemies.Clear();
		_aliveEnemyCount = 0;

		if(currentRoomInstance != null)
		{
			currentRoomInstance.QueueFree();
		}

		PackedScene roomToLoad = map[x, y] switch
		{
			RoomType.Start => startRoom,
			RoomType.Boss => bossRoom,
			_ => combatRoom
		};

		currentRoomInstance = roomToLoad.Instantiate<Node2D>();

		Node2D container = GetNode<Node2D>("../CurrentRoom");
		container.AddChild(currentRoomInstance);

		currentX = x;
		currentY = y;
		visited[x, y] = true;

		SpawnEnemiesForRoom();

		bool roomLocked = _aliveEnemyCount > 0;
		UpdateRoomBackgroundSprite(x, y, map[x, y], roomLocked);
		UpdateDoorsForCurrentRoom();
		SpawnPlayer(entrySideInTarget);
	}

	private void SpawnEnemiesForRoom()
	{
		if (currentRoomInstance == null)
			return;

		if (map[currentX, currentY] != RoomType.Combat)
			return;

		if (cleared[currentX, currentY])
			return;

		var spawnsNode = currentRoomInstance.GetNodeOrNull<Node2D>("EnemySpawns");
		if (spawnsNode == null)
			return;

		var spawnPoints = new List<Vector2>();
		foreach (var child in spawnsNode.GetChildren())
		{
			if (child is Marker2D marker)
				spawnPoints.Add(marker.GlobalPosition);
		}

		if (spawnPoints.Count == 0)
			return;

		int count = (int)_rng.RandiRange(2, Mathf.Min(3, spawnPoints.Count));

		ShuffleList(spawnPoints);

		for (int i = 0; i < count; i++)
		{
			var enemy = _enemyScene.Instantiate<Enemy>();
			currentRoomInstance.AddChild(enemy);
			enemy.GlobalPosition = spawnPoints[i];
			enemy.Died += OnEnemyDied;
			_activeEnemies.Add(enemy);
		}

		_aliveEnemyCount = count;
	}

	private void OnEnemyDied()
	{
		_aliveEnemyCount--;

		if (_aliveEnemyCount <= 0)
		{
			_aliveEnemyCount = 0;
			cleared[currentX, currentY] = true;

			UpdateDoorsForCurrentRoom();
			UpdateRoomBackgroundSprite(currentX, currentY, map[currentX, currentY], false);
		}
	}

	private void ShuffleList<T>(List<T> list)
	{
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = (int)_rng.RandiRange(0, i);
			(list[i], list[j]) = (list[j], list[i]);
		}
	}

	private void UpdateRoomBackgroundSprite(int x, int y, RoomType roomType, bool locked)
	{
		if (currentRoomInstance == null)
			return;

		var bgSprite = currentRoomInstance.GetNodeOrNull<Sprite2D>("Sprite2D");
		if (bgSprite == null)
			return;

		Texture2D tex;
		if (locked)
			tex = GetLockedRoomBackground(x, y);
		else
			tex = GetRoomBackgroundForCell(roomType, x, y);

		if (tex != null)
			bgSprite.Texture = tex;
	}

	private Texture2D GetRoomBackgroundForCell(RoomType roomType, int x, int y)
	{
		if (roomType == RoomType.Start)
			return null;

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

	private Texture2D GetLockedRoomBackground(int x, int y)
	{
		if (x == 1 && y == 0) return _topRoomLockedTex;
		if (x == 1 && y == 2) return _bottomRoomLockedTex;
		if (x == 0 && y == 1) return _leftRoomLockedTex;
		if (x == 2 && y == 1) return _rightRoomLockedTex;

		if (x == 0 && y == 0) return _topLeftRoomLockedTex;
		if (x == 2 && y == 0) return _topRightRoomLockedTex;
		if (x == 0 && y == 2) return _bottomLeftRoomLockedTex;
		if (x == 2 && y == 2) return _bottomRightRoomLockedTex;

		return null;
	}

	private void UpdateDoorsForCurrentRoom()
	{
		if (currentRoomInstance == null)
			return;

		var doors = currentRoomInstance.GetNodeOrNull<Node2D>("Doors");
		if (doors == null)
			return;

		if (_aliveEnemyCount > 0)
		{
			SetDoorEnabled(doors, "DoorTop", false);
			SetDoorEnabled(doors, "DoorBottom", false);
			SetDoorEnabled(doors, "DoorLeft", false);
			SetDoorEnabled(doors, "DoorRight", false);
			return;
		}

		bool canGoUp = currentY > 0;
		bool canGoDown = currentY < 2;
		bool canGoLeft = currentX > 0;
		bool canGoRight = currentX < 2;

		if (canGoUp && map[currentX, currentY - 1] == RoomType.Boss && !IsFloorCleared())
			canGoUp = false;
		if (canGoDown && map[currentX, currentY + 1] == RoomType.Boss && !IsFloorCleared())
			canGoDown = false;
		if (canGoLeft && map[currentX - 1, currentY] == RoomType.Boss && !IsFloorCleared())
			canGoLeft = false;
		if (canGoRight && map[currentX + 1, currentY] == RoomType.Boss && !IsFloorCleared())
			canGoRight = false;

		SetDoorEnabled(doors, "DoorTop", canGoUp);
		SetDoorEnabled(doors, "DoorBottom", canGoDown);
		SetDoorEnabled(doors, "DoorLeft", canGoLeft);
		SetDoorEnabled(doors, "DoorRight", canGoRight);
	}

	private void SetDoorEnabled(Node2D doorsRoot, string doorName, bool enabled)
	{
		var door = doorsRoot.GetNodeOrNull<Area2D>(doorName);
		if (door == null)
			return;

		door.SetDeferred("monitoring", enabled);

		var shape = door.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (shape != null)
			shape.SetDeferred("disabled", !enabled);
	}

	private bool IsFloorCleared()
	{
		for (int x = 0; x < 3; x++)
		{
			for (int y = 0; y < 3; y++)
			{
				if (map[x, y] == RoomType.Combat && !cleared[x, y])
					return false;
			}
		}
		return true;
	}

	private void SpawnPlayer(EntrySide entrySideInTarget)
	{
		Node2D player = GetNode<Node2D>("../Player");

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
			return;

		string doorName = entrySideInTarget switch
		{
			EntrySide.Top => "DoorTop",
			EntrySide.Bottom => "DoorBottom",
			EntrySide.Left => "DoorLeft",
			EntrySide.Right => "DoorRight",
			_ => ""
		};

		if (string.IsNullOrEmpty(doorName))
			return;

		var doorArea = doorsRoot.GetNodeOrNull<Area2D>(doorName);
		if (doorArea == null)
			return;

		var shape = doorArea.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (shape == null)
			return;

		Vector2 inset = GetDoorInset(entrySideInTarget);
		player.GlobalPosition = shape.GlobalPosition + inset;
	}

	private Vector2 GetDoorInset(EntrySide side)
	{
		const float inset = 35f;
		return side switch
		{
			EntrySide.Top => new Vector2(0, inset),
			EntrySide.Bottom => new Vector2(0, -inset),
			EntrySide.Left => new Vector2(inset, 0),
			EntrySide.Right => new Vector2(-inset, 0),
			_ => Vector2.Zero
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
			return;

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
		LoadRoom(_pendingX, _pendingY, _pendingEntrySide);
	}

	public void GoUp()
	{
		GoUp(EntrySide.None);
	}

	public void GoUp(EntrySide entrySideInTarget)
	{
		if (!CanChangeRoom())
			return;

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
			return;

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
			return;

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
			return;

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
}
