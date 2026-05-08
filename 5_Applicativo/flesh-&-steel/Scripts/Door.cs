using Godot;
using System;

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

	[Export]
	public Vector2 OverlayOffset = Vector2.Zero;

	private Sprite2D _lockOverlay;
	private AnimatedSprite2D _unlockAnim;
	private bool _isLocked = false;
	private bool _isBossDoor = false;
	private bool _pendingBossUnlocked = false;

	public event Action UnlockAnimationFinished;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	public void Lock(bool isBoss = false)
	{
		_isLocked = true;
		_isBossDoor = isBoss;

		SetDeferred("monitoring", false);
		var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (shape != null)
			shape.SetDeferred("disabled", true);

		EnsureOverlay();
		_lockOverlay.Texture = LoadLockedTexture(isBoss);
		_lockOverlay.Visible = true;

		if (_unlockAnim != null)
			_unlockAnim.Visible = false;
	}

	public void Unlock(bool playAnimation = false, bool showBossUnlocked = false)
	{
		if (!_isLocked && _lockOverlay != null && !_lockOverlay.Visible)
		{
			if (showBossUnlocked)
				ShowBossUnlocked();
			EnableDoor();
			return;
		}

		if (playAnimation && _isLocked)
		{
			_pendingBossUnlocked = showBossUnlocked;
			PlayUnlockAnimation();
		}
		else
		{
			if (_lockOverlay != null)
				_lockOverlay.Visible = false;
			if (_unlockAnim != null)
				_unlockAnim.Visible = false;

			if (showBossUnlocked)
				ShowBossUnlocked();

			EnableDoor();
		}

		_isLocked = false;
	}

	public void Hide()
	{
		SetDeferred("monitoring", false);
		var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (shape != null)
			shape.SetDeferred("disabled", true);

		if (_lockOverlay != null)
			_lockOverlay.Visible = false;
		if (_unlockAnim != null)
			_unlockAnim.Visible = false;
	}

	private void EnableDoor()
	{
		SetDeferred("monitoring", true);
		var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (shape != null)
			shape.SetDeferred("disabled", false);
	}

	private void EnsureOverlay()
	{
		if (_lockOverlay != null)
			return;

		_lockOverlay = new Sprite2D();
		_lockOverlay.TextureFilter = TextureFilterEnum.Nearest;
		_lockOverlay.ZIndex = 0;
		_lockOverlay.Visible = false;
		AddChild(_lockOverlay);

		var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (shape != null)
			_lockOverlay.Position = shape.Position + OverlayOffset;
	}

	private void ShowBossUnlocked()
	{
		EnsureOverlay();
		string dirName = Direction switch
		{
			DoorDirection.Up => "Top",
			DoorDirection.Down => "Bottom",
			DoorDirection.Left => "Left",
			DoorDirection.Right => "Right",
			_ => "Top"
		};
		var tex = GD.Load<Texture2D>($"res://Assets/ui/BossDoor{dirName}Unlocked.png");
		if (tex != null)
		{
			_lockOverlay.Texture = tex;
			_lockOverlay.Visible = true;
		}
	}

	private void PlayUnlockAnimation()
	{
		if (_lockOverlay != null)
			_lockOverlay.Visible = false;

		Texture2D sheet = LoadOpeningTexture(_isBossDoor);
		if (sheet == null)
		{
			if (_lockOverlay != null)
				_lockOverlay.Visible = false;
			EnableDoor();
			return;
		}

		if (_unlockAnim != null)
		{
			_unlockAnim.QueueFree();
			_unlockAnim = null;
		}

		_unlockAnim = new AnimatedSprite2D();
		_unlockAnim.TextureFilter = TextureFilterEnum.Nearest;
		_unlockAnim.ZIndex = 0;
		AddChild(_unlockAnim);

		var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (shape != null)
			_unlockAnim.Position = shape.Position + OverlayOffset;

		int frameSize = 64;
		int frameCount = (int)(sheet.GetWidth() / frameSize);

		var frames = new SpriteFrames();
		frames.AddAnimation("unlock");
		frames.SetAnimationLoop("unlock", false);
		frames.SetAnimationSpeed("unlock", 14.0f);

		frames.RemoveFrame("unlock", 0);

		for (int i = 0; i < frameCount; i++)
		{
			var atlas = new AtlasTexture();
			atlas.Atlas = sheet;
			atlas.Region = new Rect2(i * frameSize, 0, frameSize, frameSize);
			frames.AddFrame("unlock", atlas);
		}

		_unlockAnim.SpriteFrames = frames;
		_unlockAnim.Visible = true;
		_unlockAnim.Play("unlock");

		_unlockAnim.AnimationFinished += OnUnlockAnimFinished;
	}

	private void OnUnlockAnimFinished()
	{
		if (_unlockAnim != null)
		{
			_unlockAnim.Visible = false;
			_unlockAnim.QueueFree();
			_unlockAnim = null;
		}

		if (_pendingBossUnlocked)
		{
			ShowBossUnlocked();
			_pendingBossUnlocked = false;
		}

		EnableDoor();
		UnlockAnimationFinished?.Invoke();
	}

	private Texture2D LoadLockedTexture(bool isBoss)
	{
		string prefix = isBoss ? "BossDoor" : "Door";
		string dirName = Direction switch
		{
			DoorDirection.Up => "Top",
			DoorDirection.Down => "Bottom",
			DoorDirection.Left => "Left",
			DoorDirection.Right => "Right",
			_ => "Top"
		};
		return GD.Load<Texture2D>($"res://Assets/ui/{prefix}{dirName}Locked.png");
	}

	private Texture2D LoadOpeningTexture(bool isBoss)
	{
		string prefix = isBoss ? "BossDoor" : "Door";
		string dirName = Direction switch
		{
			DoorDirection.Up => "Top",
			DoorDirection.Down => "Bottom",
			DoorDirection.Left => "Left",
			DoorDirection.Right => "Right",
			_ => "Top"
		};
		return GD.Load<Texture2D>($"res://Assets/ui/{prefix}{dirName}LockedOpening.png");
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
