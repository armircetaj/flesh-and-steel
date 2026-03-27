using Godot;

public partial class Door : Area2D
{
	public enum DoorDirection
	{
		Up,
		Down,
		Left,
		Right
	}

	[Export]
	public DoorDirection Direction;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body is not Player)
			return;

		var roomManager = GetNodeOrNull<RoomManager>("/root/Main/RoomManager");
		if (roomManager == null)
			return;

		switch (Direction)
		{
			case DoorDirection.Up:
				roomManager.GoUp(RoomManager.EntrySide.Bottom);
				break;
			case DoorDirection.Down:
				roomManager.GoDown(RoomManager.EntrySide.Top);
				break;
			case DoorDirection.Left:
				roomManager.GoLeft(RoomManager.EntrySide.Right);
				break;
			case DoorDirection.Right:
				roomManager.GoRight(RoomManager.EntrySide.Left);
				break;
		}
	}
}

