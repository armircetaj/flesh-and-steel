using Godot;
using System;

public partial class MiniMap : Control
{
	[Export] public NodePath RoomManagerPath;

	private RoomManager roomManager;

	private Texture2D currentRoomTex;
	private Texture2D currentBossRoomTex;
	private Texture2D visitedRoomTex;
	private Texture2D unvisitedRoomTex;
	private Texture2D unvisitedBossRoomTex;
	private Texture2D backgroundTex;

	private const int GridSize = 3;
	private const int CellSize = 6;
	private const int CellPadding = 0;

	public override void _Ready()
	{
		backgroundTex = GD.Load<Texture2D>("res://Assets/ui/MiniUI.png");
		currentRoomTex = GD.Load<Texture2D>("res://Assets/ui/CurrentRoom.png");
		currentBossRoomTex = GD.Load<Texture2D>("res://Assets/ui/CurrentBossRoom.png");
		visitedRoomTex = GD.Load<Texture2D>("res://Assets/ui/VisitedRoom.png");
		unvisitedRoomTex = GD.Load<Texture2D>("res://Assets/ui/UnvisitedRoom.png");
		unvisitedBossRoomTex = GD.Load<Texture2D>("res://Assets/ui/UnvisitedBossRoom.png");

		if (RoomManagerPath != null && !string.IsNullOrEmpty(RoomManagerPath.ToString()))
		{
			roomManager = GetNode<RoomManager>(RoomManagerPath);
		}
		else
		{
			roomManager = GetNode<RoomManager>("../../RoomManager");
		}
	}

	public override void _Process(double delta)
	{
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (roomManager == null)
		{
			return;
		}

		float gridWidth = GridSize * CellSize + (GridSize - 1) * CellPadding;
		float gridHeight = GridSize * CellSize + (GridSize - 1) * CellPadding;
		Vector2 gridSize = new Vector2(gridWidth, gridHeight);

		Vector2 bgSize = gridSize + new Vector2(10, 10);

		Vector2 bgOffset = Vector2.Zero;
		if (backgroundTex != null)
		{
			Rect2 bgRect = new Rect2(bgOffset, bgSize);
			DrawTextureRect(backgroundTex, bgRect, false);
		}

		Vector2 baseOffset = (bgSize - gridSize) / 2.0f;

		var map = roomManager.GetMap();
		var visited = roomManager.GetVisited();
		Vector2I current = roomManager.GetCurrentRoomCoords();

		for (int x = 0; x < GridSize; x++)
		{
			for (int y = 0; y < GridSize; y++)
			{
				Texture2D icon = GetIconForCell(map[x, y], visited[x, y], x, y, current);

				if (icon == null)
				{
					continue;
				}

				Vector2 pos = baseOffset + new Vector2(
					x * (CellSize + CellPadding),
					y * (CellSize + CellPadding)
				);

				Rect2 rect = new Rect2(pos, new Vector2(CellSize, CellSize));
				DrawTextureRect(icon, rect, false);
			}
		}
	}

	private Texture2D GetIconForCell(RoomManager.RoomType roomType, bool isVisited, int x, int y, Vector2I current)
	{
		bool isCurrent = (current.X == x && current.Y == y);

		if (roomType == RoomManager.RoomType.Boss)
		{
			if (isCurrent)
			{
				return currentBossRoomTex;
			}

			return isVisited ? visitedRoomTex : unvisitedBossRoomTex;
		}

		if (isCurrent)
		{
			return currentRoomTex;
		}

		if (isVisited)
		{
			return visitedRoomTex;
		}

		return unvisitedRoomTex;
	}
}
