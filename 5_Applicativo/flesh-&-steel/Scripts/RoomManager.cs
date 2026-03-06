using Godot;
using System;

public partial class RoomManager : Node
{
	public enum RoomType
	{
		Start,
		Combat,
		Boss
	}

	private RoomType[,] map = new RoomType[3,3];
	private bool[,] visited = new bool[3,3];

	private int currentX = 1;
	private int currentY = 1;

	private PackedScene startRoom;
	private PackedScene combatRoom;
	private PackedScene bossRoom;

	private Node2D currentRoomInstance;

	public override void _Ready()
	{
		startRoom = GD.Load<PackedScene>("res://Scenes/Rooms/StartRoom.tscn");
		combatRoom = GD.Load<PackedScene>("res://Scenes/Rooms/CombatRoom.tscn");
		bossRoom = GD.Load<PackedScene>("res://Scenes/Rooms/BossRoom.tscn");

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

		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();

		Vector2I[] corners = new Vector2I[]
		{
			new Vector2I(0, 0),
			new Vector2I(2, 0),
			new Vector2I(0, 2),
			new Vector2I(2, 2)
		};

		int index = (int)rng.RandiRange(0, corners.Length - 1);
		Vector2I bossPos = corners[index];
		map[bossPos.X, bossPos.Y] = RoomType.Boss;	
	}

	private void LoadRoom(int x, int y)
	{
		if(currentRoomInstance != null)
		{
			currentRoomInstance.QueueFree();
		}

		PackedScene roomToLoad;

		switch(map[x,y])
		{
			case RoomType.Start:
				roomToLoad = startRoom;
				break;

			case RoomType.Boss:
				roomToLoad = bossRoom;
				break;

			default:
				roomToLoad = combatRoom;
				break;
		}

		currentRoomInstance = roomToLoad.Instantiate<Node2D>();

		Node2D container = GetNode<Node2D>("../CurrentRoom");
		container.AddChild(currentRoomInstance);

		currentX = x;
		currentY = y;
		visited[x, y] = true;

		SpawnPlayer();
	}

	public RoomType[,] GetMap()
	{
		return map;
	}

	public bool[,] GetVisited()
	{
		return visited;
	}

	public Vector2I GetCurrentRoomCoords()
	{
		return new Vector2I(currentX, currentY);
	}

	private void SpawnPlayer()
	{
		Node2D player = GetNode<Node2D>("../Player");

		if(currentRoomInstance.HasNode("PlayerSpawn"))
		{
			Marker2D spawn = currentRoomInstance.GetNode<Marker2D>("PlayerSpawn");
			player.GlobalPosition = spawn.GlobalPosition;
		}
	}

	public void GoUp()
	{
		if(currentY > 0)
		{
			currentY--;
			LoadRoom(currentX, currentY);
		}
	}

	public void GoDown()
	{
		if(currentY < 2)
		{
			currentY++;
			LoadRoom(currentX, currentY);
		}
	}

	public void GoLeft()
	{
		if(currentX > 0)
		{
			currentX--;
			LoadRoom(currentX, currentY);
		}
	}

	public void GoRight()
	{
		if(currentX < 2)
		{
			currentX++;
			LoadRoom(currentX, currentY);
		}
	}
}
