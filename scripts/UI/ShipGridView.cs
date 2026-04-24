using Godot;
using System;
using System.Collections.Generic;
using TidesOfTime.Battle;
using TidesOfTime.Crew;
using TidesOfTime.Data;
using TidesOfTime.Ships;

namespace TidesOfTime.UI;

public partial class ShipGridView : PanelContainer
{
	[Export] public PackedScene? TileViewScene { get; set; }
	[Export] public bool UsePlayerCannonBarPalette { get; set; }

	[Export(PropertyHint.Range, "0,1,0.01")] public float TileFillAlpha { get; set; } = 1.0f;
	[Export] public bool ShowInteriorCutawayBacking { get; set; }
	[Export(PropertyHint.Range, "0,0.5,0.01")] public float InteriorCutawayPaddingTiles { get; set; } = 0.16f;

	public event Action<ShipState, int, int>? TilePressed;
	public event Action<ShipState>? BackgroundPressed;
	public event Action<ShipState, CrewState>? CrewSelected;

	private Label _shipNameLabel = null!;
	private ProgressBar _hullBar = null!;
	private Control _gridStack = null!;
	private ShipHullBackdrop _hullBackdrop = null!;
	private ShipInteriorCutawayBackdrop? _interiorCutawayBackdrop;
	private Control _grid = null!;
	private Control _roomOverlay = null!;
	private Control _crewOverlay = null!;
	private ProgressBar _cannonChargeBar = null!;
	private ShipState? _shipState;
	private string? _selectedCrewId;
	private CannonChargeBarState _cannonChargeBarState = new(null, 0.0, false, false);
	private int _boardRenderRevision;

	public override void _Ready()
	{
		_shipNameLabel = GetNode<Label>("MarginContainer/VBoxContainer/HeaderBar/ShipNameLabel");
		_hullBar = GetNode<ProgressBar>("MarginContainer/VBoxContainer/HullBar");
		_gridStack = GetNode<Control>("MarginContainer/VBoxContainer/GridStack");
		_grid = GetNode<Control>("MarginContainer/VBoxContainer/GridStack/Grid");
		_roomOverlay = GetNode<Control>("MarginContainer/VBoxContainer/GridStack/RoomOverlay");
		_crewOverlay = GetNode<Control>("MarginContainer/VBoxContainer/GridStack/CrewOverlay");
		_hullBackdrop = CreateHullBackdrop();
		_gridStack.AddChild(_hullBackdrop);
		_gridStack.MoveChild(_hullBackdrop, 0);
		CreateInteriorCutawayBackingIfNeeded();

		if (TileViewScene == null)
		{
			GD.PushError("ShipGridView: TileViewScene is not assigned.");
		}

		_cannonChargeBar = CreateRoomChargeBar();
		_roomOverlay.AddChild(_cannonChargeBar);
	}

	public void RenderFromLayout(ShipLayoutDef layout)
	{
		Render(ShipState.FromLayout(layout));
	}

	public void Render(ShipState shipState, string? selectedCrewId = null)
	{
		_shipState = shipState;
		_selectedCrewId = selectedCrewId;
		_shipNameLabel.Text = shipState.Name;
		_hullBar.Value = shipState.Hull;
		_hullBackdrop.SetBoardSize(shipState.Grid.Width, shipState.Grid.Height);
		UpdateInteriorCutawayBacking(shipState.Grid);
		RefreshCannonChargeBar();

		var renderRevision = ++_boardRenderRevision;
		Callable.From(() => RebuildBoardDeferred(renderRevision)).CallDeferred();
	}

	public void SetCannonChargeBar(CannonChargeBarState chargeBarState)
	{
		_cannonChargeBarState = chargeBarState;
		RefreshCannonChargeBar();
	}

	public void SetShipVisualStyle(Color hullTint, bool bowFacesRight)
	{
		_hullBackdrop.SetVisualStyle(hullTint, bowFacesRight);
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (_shipState == null || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			return;
		}

		BackgroundPressed?.Invoke(_shipState);
	}

	private static Dictionary<string, ShipRoomState> BuildRoomIndex(ShipGridState gridState)
	{
		var roomById = new Dictionary<string, ShipRoomState>();
		foreach (var room in gridState.Rooms)
		{
			roomById[room.RoomId] = room;
		}

		return roomById;
	}

	private void ClearBoard()
	{
		foreach (Node child in _grid.GetChildren())
		{
			child.QueueFree();
		}

		foreach (Node child in _crewOverlay.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void OnTilePressed(int x, int y)
	{
		if (_shipState == null)
		{
			return;
		}

		TilePressed?.Invoke(_shipState, x, y);
	}

	private static void ApplyTileStyle(Button tileNode, Color fillColor, bool isSelected)
	{
		var style = new StyleBoxFlat
		{
			BgColor = fillColor,
			BorderWidthLeft = isSelected ? 3 : 1,
			BorderWidthTop = isSelected ? 3 : 1,
			BorderWidthRight = isSelected ? 3 : 1,
			BorderWidthBottom = isSelected ? 3 : 1,
			BorderColor = isSelected
				? new Color(0.98f, 0.98f, 0.92f)
				: fillColor.Darkened(0.35f),
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2
		};

		tileNode.AddThemeStyleboxOverride("normal", style);
		tileNode.AddThemeStyleboxOverride("hover", style);
		tileNode.AddThemeStyleboxOverride("pressed", style);
		tileNode.AddThemeStyleboxOverride("disabled", style);
	}

	private Color GetRoomColor(ShipRoomState? room)
	{
		if (room == null)
		{
			return new Color(0.6f, 0.6f, 0.6f);
		}

		if (room.Disabled)
		{
			return new Color(0.27f, 0.18f, 0.18f);
		}

		var baseColor = room.SystemType switch
		{
			"HelmRigging" => new Color(0.45f, 0.65f, 0.95f),
			"Cannons" => new Color(0.95f, 0.45f, 0.45f),
			"ThreadChamber" => new Color(0.55f, 0.35f, 0.75f),
			"CrowsNest" => new Color(0.9f, 0.8f, 0.35f),
			"DoctorsQuarters" => new Color(0.35f, 0.8f, 0.55f),
			_ => new Color(0.6f, 0.6f, 0.6f)
		};

		if (!room.IsDamaged)
		{
			return baseColor;
		}

		var damageRatio = 1.0f - (float)room.Integrity / ShipRoomState.MaxIntegrity;
		return baseColor.Darkened(0.2f + damageRatio * 0.35f);
	}

	private void RebuildBoardDeferred(int renderRevision)
	{
		if (_shipState == null || renderRevision != _boardRenderRevision)
		{
			return;
		}

		if (TileViewScene == null)
		{
			GD.PushError("ShipGridView: TileViewScene is not assigned.");
			return;
		}

		ClearBoard();
		var roomById = BuildRoomIndex(_shipState.Grid);
		var layout = CalculateBoardLayout(_shipState.Grid.Width, _shipState.Grid.Height);

		foreach (var tile in _shipState.Grid.Tiles)
		{
			if (!tile.Walkable)
			{
				continue;
			}

			var tileNode = TileViewScene.Instantiate<Button>();
			tileNode.Text = "";
			tileNode.Position = GetTilePosition(layout, tile.X, tile.Y);
			tileNode.Size = new Vector2(layout.TileSize, layout.TileSize);
			tileNode.CustomMinimumSize = tileNode.Size;
			tileNode.FocusMode = Control.FocusModeEnum.None;
			tileNode.ButtonDown += () => OnTilePressed(tile.X, tile.Y);

			var room = roomById.GetValueOrDefault(tile.RoomId);
			var color = GetRoomColor(room);
			var isSelected = room?.RoomId == _shipState.SelectedRoomId;
			ApplyTileStyle(tileNode, WithTileFillAlpha(color), isSelected);

			_grid.AddChild(tileNode);
		}

		foreach (var crew in _shipState.GetCrewOnBoard())
		{
			var marker = CreateCrewMarker(crew, crew.Id == _selectedCrewId);
			if (marker is Button markerButton)
			{
				markerButton.ButtonDown += () => OnCrewPressed(crew);
			}

			var tilePosition = GetTilePosition(layout, crew.Position.TileX, crew.Position.TileY);
			var tileSize = new Vector2(layout.TileSize, layout.TileSize);
			marker.Position = tilePosition + (tileSize - marker.Size) * 0.5f;
			_crewOverlay.AddChild(marker);
		}

		RefreshCannonChargeBar(layout);
	}

	private BoardLayout CalculateBoardLayout(int gridWidth, int gridHeight)
	{
		var tileSize = Mathf.Floor(Mathf.Min(
			_grid.Size.X / Mathf.Max(gridWidth, 1),
			_grid.Size.Y / Mathf.Max(gridHeight, 1)));

		tileSize = Mathf.Max(tileSize, 24.0f);

		var boardSize = new Vector2(tileSize * gridWidth, tileSize * gridHeight);
		var origin = (_grid.Size - boardSize) * 0.5f;

		return new BoardLayout(origin, tileSize);
	}

	private static Vector2 GetTilePosition(BoardLayout layout, int tileX, int tileY)
	{
		return layout.Origin + new Vector2(tileX * layout.TileSize, tileY * layout.TileSize);
	}

	private void RefreshCannonChargeBar()
	{
		if (_shipState == null)
		{
			return;
		}

		var layout = CalculateBoardLayout(_shipState.Grid.Width, _shipState.Grid.Height);
		RefreshCannonChargeBar(layout);
	}

	private void RefreshCannonChargeBar(BoardLayout layout)
	{
		if (_shipState == null)
		{
			return;
		}

		if (!_cannonChargeBarState.IsVisible || string.IsNullOrEmpty(_cannonChargeBarState.RoomId))
		{
			_cannonChargeBar.Visible = false;
			return;
		}

		var room = _shipState.Grid.Rooms.Find(candidate => candidate.RoomId == _cannonChargeBarState.RoomId);
		if (room == null || room.Tiles.Count == 0)
		{
			_cannonChargeBar.Visible = false;
			return;
		}

		var roomBounds = GetRoomBounds(layout, room);
		var barWidth = Mathf.Max(roomBounds.Size.X - 8.0f, 28.0f);
		var barSize = new Vector2(barWidth, 10.0f);
		var maxBoardX = layout.Origin.X + (_shipState.Grid.Width * layout.TileSize) - barSize.X;
		var barX = Mathf.Clamp(
			roomBounds.Position.X + (roomBounds.Size.X - barSize.X) * 0.5f,
			layout.Origin.X,
			maxBoardX);
		var barY = Mathf.Max(layout.Origin.Y - 2.0f, roomBounds.Position.Y - barSize.Y - 6.0f);

		_cannonChargeBar.Position = new Vector2(barX, barY);
		_cannonChargeBar.Size = barSize;
		_cannonChargeBar.CustomMinimumSize = barSize;
		_cannonChargeBar.Value = _cannonChargeBarState.ProgressRatio;
		ApplyRoomChargeBarStyle(_cannonChargeBar, _cannonChargeBarState.IsActive, UsePlayerCannonBarPalette);
		_cannonChargeBar.Visible = true;
	}

	private static Rect2 GetRoomBounds(BoardLayout layout, ShipRoomState room)
	{
		var minTileX = room.Tiles[0].X;
		var maxTileX = room.Tiles[0].X;
		var minTileY = room.Tiles[0].Y;
		var maxTileY = room.Tiles[0].Y;

		foreach (var tile in room.Tiles)
		{
			minTileX = Mathf.Min(minTileX, tile.X);
			maxTileX = Mathf.Max(maxTileX, tile.X);
			minTileY = Mathf.Min(minTileY, tile.Y);
			maxTileY = Mathf.Max(maxTileY, tile.Y);
		}

		var topLeft = GetTilePosition(layout, minTileX, minTileY);
		var size = new Vector2(
			(maxTileX - minTileX + 1) * layout.TileSize,
			(maxTileY - minTileY + 1) * layout.TileSize);

		return new Rect2(topLeft, size);
	}

	private void OnCrewPressed(CrewState crew)
	{
		if (_shipState == null)
		{
			return;
		}

		CrewSelected?.Invoke(_shipState, crew);
	}

	private static Control CreateCrewMarker(CrewState crew, bool isSelected)
	{
		var marker = new Button
		{
			CustomMinimumSize = new Vector2(24, 24),
			Size = new Vector2(24, 24),
			MouseFilter = Control.MouseFilterEnum.Stop,
			FocusMode = Control.FocusModeEnum.None,
			Text = crew.ShortLabel
		};

		var style = new StyleBoxFlat
		{
			BgColor = GetCrewMarkerFillColor(crew),
			BorderColor = GetCrewMarkerBorderColor(crew, isSelected),
			BorderWidthLeft = isSelected ? 4 : 2,
			BorderWidthTop = isSelected ? 4 : 2,
			BorderWidthRight = isSelected ? 4 : 2,
			BorderWidthBottom = isSelected ? 4 : 2,
			CornerRadiusTopLeft = 12,
			CornerRadiusTopRight = 12,
			CornerRadiusBottomRight = 12,
			CornerRadiusBottomLeft = 12
		};
		marker.AddThemeStyleboxOverride("normal", style);
		marker.AddThemeStyleboxOverride("hover", style);
		marker.AddThemeStyleboxOverride("pressed", style);
		marker.AddThemeColorOverride("font_color", GetCrewMarkerTextColor(crew));
		marker.AddThemeColorOverride("font_outline_color", new Color(0.04f, 0.04f, 0.08f, 0.95f));
		marker.AddThemeConstantOverride("outline_size", 2);
		marker.AddThemeFontSizeOverride("font_size", 14);

		return marker;
	}

	private static ProgressBar CreateRoomChargeBar()
	{
		return new ProgressBar
		{
			MinValue = 0.0,
			MaxValue = 1.0,
			ShowPercentage = false,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			FocusMode = Control.FocusModeEnum.None,
			Step = 0.0,
			Visible = false
		};
	}

	private static ShipHullBackdrop CreateHullBackdrop()
	{
		var backdrop = new ShipHullBackdrop
		{
			Name = "HullBackdrop",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical = Control.GrowDirection.Both
		};

		return backdrop;
	}

	private void CreateInteriorCutawayBackingIfNeeded()
	{
		if (!ShowInteriorCutawayBacking)
		{
			return;
		}

		_interiorCutawayBackdrop = new ShipInteriorCutawayBackdrop
		{
			Name = "InteriorCutawayBackdrop",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			PaddingTiles = InteriorCutawayPaddingTiles,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical = Control.GrowDirection.Both
		};

		_gridStack.AddChild(_interiorCutawayBackdrop);
		_gridStack.MoveChild(_interiorCutawayBackdrop, 1);
	}

	private void UpdateInteriorCutawayBacking(ShipGridState gridState)
	{
		if (_interiorCutawayBackdrop == null)
		{
			return;
		}

		var hasWalkableTile = false;
		var minTileX = gridState.Width - 1;
		var minTileY = gridState.Height - 1;
		var maxTileX = 0;
		var maxTileY = 0;

		foreach (var tile in gridState.Tiles)
		{
			if (!tile.Walkable)
			{
				continue;
			}

			hasWalkableTile = true;
			minTileX = Mathf.Min(minTileX, tile.X);
			minTileY = Mathf.Min(minTileY, tile.Y);
			maxTileX = Mathf.Max(maxTileX, tile.X);
			maxTileY = Mathf.Max(maxTileY, tile.Y);
		}

		if (!hasWalkableTile)
		{
			_interiorCutawayBackdrop.ClearInterior();
			return;
		}

		_interiorCutawayBackdrop.PaddingTiles = InteriorCutawayPaddingTiles;
		_interiorCutawayBackdrop.SetInteriorBounds(
			gridState.Width,
			gridState.Height,
			minTileX,
			minTileY,
			maxTileX,
			maxTileY);
	}

	private Color WithTileFillAlpha(Color color)
	{
		return new Color(
			color.R,
			color.G,
			color.B,
			Mathf.Clamp(TileFillAlpha, 0.0f, 1.0f));
	}

	private static void ApplyRoomChargeBarStyle(ProgressBar bar, bool isActive, bool usePlayerPalette)
	{
		var backgroundStyle = new StyleBoxFlat
		{
			BgColor = isActive
				? usePlayerPalette
					? new Color(0.08f, 0.14f, 0.1f, 0.92f)
					: new Color(0.15f, 0.1f, 0.1f, 0.92f)
				: new Color(0.18f, 0.18f, 0.18f, 0.72f),
			BorderColor = isActive
				? usePlayerPalette
					? new Color(0.2f, 0.5f, 0.28f, 0.95f)
					: new Color(0.55f, 0.25f, 0.2f, 0.95f)
				: new Color(0.34f, 0.34f, 0.34f, 0.85f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3
		};

		var fillStyle = new StyleBoxFlat
		{
			BgColor = isActive
				? usePlayerPalette
					? new Color(0.48f, 0.88f, 0.52f, 0.98f)
					: new Color(0.98f, 0.6f, 0.34f, 0.98f)
				: new Color(0.42f, 0.42f, 0.42f, 0.55f),
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2
		};

		bar.AddThemeStyleboxOverride("background", backgroundStyle);
		bar.AddThemeStyleboxOverride("fill", fillStyle);
	}

	private static Color GetCrewMarkerFillColor(CrewState crew)
	{
		return crew.Allegiance switch
		{
			CrewAllegiance.Player => new Color(0.17f, 0.35f, 0.62f, 0.95f),
			CrewAllegiance.Enemy => new Color(0.62f, 0.22f, 0.18f, 0.95f),
			_ => new Color(0.35f, 0.35f, 0.35f, 0.95f)
		};
	}

	private static Color GetCrewMarkerBorderColor(CrewState crew, bool isSelected)
	{
		if (isSelected)
		{
			return new Color(1.0f, 0.98f, 0.86f);
		}

		return crew.Allegiance switch
		{
			CrewAllegiance.Player => new Color(0.78f, 0.9f, 1.0f),
			CrewAllegiance.Enemy => new Color(1.0f, 0.82f, 0.78f),
			_ => new Color(0.8f, 0.8f, 0.8f)
		};
	}

	private static Color GetCrewMarkerTextColor(CrewState crew)
	{
		return crew.Allegiance switch
		{
			CrewAllegiance.Player => new Color(0.95f, 0.98f, 1.0f),
			CrewAllegiance.Enemy => new Color(1.0f, 0.95f, 0.93f),
			_ => Colors.White
		};
	}

	private readonly record struct BoardLayout(Vector2 Origin, float TileSize);
}
