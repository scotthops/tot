using Godot;
using System;
using System.Collections.Generic;
using TidesOfTime.Battle;
using TidesOfTime.Crew;
using TidesOfTime.Data;
using TidesOfTime.Ships;

namespace TidesOfTime.UI;

public partial class SchematicShipGridView : Control
{
	[Export] public Color HullTint { get; set; } = new(0.24f, 0.48f, 0.78f);
	[Export] public bool BowFacesRight { get; set; } = true;

	public event Action<ShipState, int, int>? TilePressed;
	public event Action<ShipState>? BackgroundPressed;
	public event Action<ShipState, CrewState>? CrewSelected;

	private static readonly Color Parchment = new(0.89f, 0.76f, 0.53f, 0.94f);
	private static readonly Color ParchmentDark = new(0.5f, 0.33f, 0.16f, 0.76f);
	private static readonly Color Ink = new(0.13f, 0.08f, 0.045f);
	private static readonly Color MutedInk = new(0.24f, 0.17f, 0.11f, 0.82f);
	private static readonly Color HullFill = new(0.18f, 0.105f, 0.055f);
	private static readonly Color DeckFill = new(0.68f, 0.43f, 0.19f);
	private static readonly Color OpenTileFill = new(0.72f, 0.47f, 0.24f, 0.82f);
	private static readonly Color OutsideTileFill = new(0.17f, 0.095f, 0.045f, 0.36f);
	private static readonly Color ObstacleTileFill = new(0.11f, 0.07f, 0.045f, 0.92f);
	private static readonly Color TileLine = new(0.12f, 0.075f, 0.04f, 0.64f);
	private static readonly Color SelectedBorder = new(1.0f, 0.92f, 0.58f);
	private static readonly Color ChargeBack = new(0.08f, 0.05f, 0.025f, 0.82f);
	private static readonly Color ChargeFill = new(0.48f, 0.88f, 0.52f, 0.95f);
	private static readonly Color ChargeInactiveFill = new(0.42f, 0.42f, 0.38f, 0.7f);

	private ShipState? _shipState;
	private string? _selectedCrewId;
	private CannonChargeBarState _cannonChargeBarState = new(null, 0.0, false, false);

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			QueueRedraw();
		}
	}

	public void RenderFromLayout(ShipLayoutDef layout)
	{
		Render(ShipState.FromLayout(layout));
	}

	public void Render(ShipState shipState, string? selectedCrewId = null)
	{
		_shipState = shipState;
		_selectedCrewId = selectedCrewId;
		QueueRedraw();
	}

	public void SetCannonChargeBar(CannonChargeBarState chargeBarState)
	{
		_cannonChargeBarState = chargeBarState;
		QueueRedraw();
	}

	public void SetShipVisualStyle(Color hullTint, bool bowFacesRight)
	{
		HullTint = hullTint;
		BowFacesRight = bowFacesRight;
		QueueRedraw();
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (_shipState == null || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouseButton)
		{
			return;
		}

		var layout = CalculateBoardLayout(_shipState.Grid.Width, _shipState.Grid.Height);
		if (TryFindCrewAtPosition(layout, mouseButton.Position, out var crew))
		{
			CrewSelected?.Invoke(_shipState, crew);
			AcceptEvent();
			return;
		}

		if (TryFindWalkableTileAtPosition(layout, mouseButton.Position, out var tileX, out var tileY))
		{
			TilePressed?.Invoke(_shipState, tileX, tileY);
			AcceptEvent();
			return;
		}

		BackgroundPressed?.Invoke(_shipState);
		AcceptEvent();
	}

	public override void _Draw()
	{
		if (_shipState == null || Size.X < 240.0f || Size.Y < 180.0f)
		{
			return;
		}

		var font = GetThemeFont("font", "Label");
		var layout = CalculateBoardLayout(_shipState.Grid.Width, _shipState.Grid.Height);
		var roomById = BuildRoomIndex(_shipState.Grid);

		DrawPanel(font);
		DrawHullBacking(layout);
		DrawTiles(layout, roomById);
		DrawRoomBorders(layout);
		DrawRoomLabels(font, layout);
		DrawCannonChargeBar(layout);
		DrawCrewTokens(font, layout);
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

	private BoardLayout CalculateBoardLayout(int gridWidth, int gridHeight)
	{
		const float margin = 22.0f;
		const float headerHeight = 78.0f;
		var availableSize = new Vector2(
			Mathf.Max(1.0f, Size.X - (margin * 2.0f)),
			Mathf.Max(1.0f, Size.Y - headerHeight - (margin * 2.0f)));
		var tileSize = Mathf.Floor(Mathf.Min(
			availableSize.X / Mathf.Max(gridWidth, 1),
			availableSize.Y / Mathf.Max(gridHeight, 1)));

		tileSize = Mathf.Max(tileSize, 20.0f);
		var boardSize = new Vector2(tileSize * gridWidth, tileSize * gridHeight);
		var origin = new Vector2(
			(Size.X - boardSize.X) * 0.5f,
			headerHeight + ((availableSize.Y - boardSize.Y) * 0.5f));

		return new BoardLayout(new Rect2(origin, boardSize), tileSize);
	}

	private void DrawPanel(Font font)
	{
		var panel = new Rect2(12.0f, 12.0f, Size.X - 24.0f, 58.0f);
		DrawRect(new Rect2(panel.Position + new Vector2(4.0f, 5.0f), panel.Size), new Color(0.03f, 0.02f, 0.012f, 0.28f), true);
		DrawRect(panel, Parchment, true);
		DrawRect(panel, ParchmentDark, false, 2.0f);
		DrawString(font, panel.Position + new Vector2(14.0f, 28.0f), _shipState!.Name, HorizontalAlignment.Left, panel.Size.X - 28.0f, 19, Ink);
		DrawString(font, panel.Position + new Vector2(16.0f, 49.0f), $"Hull {_shipState.Hull}", HorizontalAlignment.Left, 180.0f, 13, MutedInk);

		var hullBar = new Rect2(panel.End.X - 156.0f, panel.Position.Y + 22.0f, 132.0f, 14.0f);
		DrawRect(hullBar, new Color(0.16f, 0.09f, 0.045f, 0.42f), true);
		DrawRect(new Rect2(hullBar.Position, new Vector2(hullBar.Size.X * Mathf.Clamp(_shipState.Hull / 100.0f, 0.0f, 1.0f), hullBar.Size.Y)), HullTint.Lightened(0.28f), true);
		DrawRect(hullBar, ParchmentDark, false, 1.0f);
	}

	private void DrawHullBacking(BoardLayout layout)
	{
		var rect = layout.BoardRect.Grow(layout.TileSize * 0.36f);
		var centerY = rect.Position.Y + (rect.Size.Y * 0.5f);
		var bowX = BowFacesRight ? rect.End.X + (layout.TileSize * 0.38f) : rect.Position.X - (layout.TileSize * 0.38f);
		var sternX = BowFacesRight ? rect.Position.X - (layout.TileSize * 0.18f) : rect.End.X + (layout.TileSize * 0.18f);
		var topLeft = BowFacesRight ? rect.Position.X : rect.End.X;
		var topRight = BowFacesRight ? rect.End.X : rect.Position.X;
		var bottomLeft = topLeft;
		var bottomRight = topRight;

		var hull = new[]
		{
			new Vector2(sternX, centerY),
			new Vector2(topLeft + (BowFacesRight ? layout.TileSize * 0.48f : -layout.TileSize * 0.48f), rect.Position.Y),
			new Vector2(topRight, rect.Position.Y + (layout.TileSize * 0.18f)),
			new Vector2(bowX, centerY),
			new Vector2(bottomRight, rect.End.Y - (layout.TileSize * 0.18f)),
			new Vector2(bottomLeft + (BowFacesRight ? layout.TileSize * 0.48f : -layout.TileSize * 0.48f), rect.End.Y)
		};

		DrawColoredPolygon(OffsetPoints(hull, new Vector2(5.0f, 7.0f)), new Color(0.02f, 0.03f, 0.025f, 0.32f));
		DrawColoredPolygon(hull, HullFill.Lerp(HullTint, 0.18f));
		DrawPolyline(ClosePolygon(hull), new Color(0.72f, 0.52f, 0.28f, 0.84f), 2.0f, true);
		DrawRect(layout.BoardRect.Grow(6.0f), new Color(0.08f, 0.045f, 0.025f, 0.54f), true);
		DrawRect(layout.BoardRect, DeckFill, true);
	}

	private void DrawTiles(BoardLayout layout, Dictionary<string, ShipRoomState> roomById)
	{
		for (var y = 0; y < _shipState!.Grid.Height; y++)
		{
			for (var x = 0; x < _shipState.Grid.Width; x++)
			{
				var tile = _shipState.Grid.GetTile(x, y);
				if (tile == null)
				{
					continue;
				}

				var room = string.IsNullOrEmpty(tile.RoomId) ? null : roomById.GetValueOrDefault(tile.RoomId);
				DrawTile(layout, tile, room);
			}
		}
	}

	private void DrawTile(BoardLayout layout, ShipTileState tile, ShipRoomState? room)
	{
		var tileRect = GetTileRect(layout, tile.X, tile.Y).Grow(-2.0f);
		var fill = GetTileFill(tile, room);

		DrawRect(tileRect, fill, true);
		DrawRect(tileRect, TileLine, false, 1.0f);

		if (!tile.Walkable)
		{
			if (tile.TileKind == ShipTileKind.Obstacle)
			{
				DrawRect(tileRect.Grow(-5.0f), new Color(0.72f, 0.52f, 0.28f, 0.24f), false, 2.0f);
				DrawLine(tileRect.Position + new Vector2(8.0f, 8.0f), tileRect.End - new Vector2(8.0f, 8.0f), new Color(0.92f, 0.74f, 0.42f, 0.34f), 2.0f, true);
				DrawLine(new Vector2(tileRect.End.X - 8.0f, tileRect.Position.Y + 8.0f), new Vector2(tileRect.Position.X + 8.0f, tileRect.End.Y - 8.0f), new Color(0.92f, 0.74f, 0.42f, 0.34f), 2.0f, true);
				return;
			}

			DrawLine(tileRect.Position + new Vector2(6.0f, tileRect.Size.Y - 6.0f), tileRect.End - new Vector2(6.0f, tileRect.Size.Y - 6.0f), new Color(0.05f, 0.035f, 0.025f, 0.28f), 1.0f, true);
			return;
		}

		if (room == null)
		{
			return;
		}

		var accent = GetSystemAccentColor(room);
		var accentRect = new Rect2(tileRect.Position + new Vector2(4.0f, 4.0f), new Vector2(Mathf.Max(0.0f, tileRect.Size.X - 8.0f), 4.0f));
		DrawRect(accentRect, new Color(accent.R, accent.G, accent.B, 0.64f), true);

		if (room.RoomId == _shipState!.SelectedRoomId)
		{
			DrawRect(tileRect.Grow(-1.0f), SelectedBorder, false, 2.0f);
		}
	}

	private static Color GetTileFill(ShipTileState tile, ShipRoomState? room)
	{
		if (!tile.Walkable)
		{
			return tile.TileKind == ShipTileKind.Obstacle
				? ObstacleTileFill
				: OutsideTileFill;
		}

		if (room == null)
		{
			return OpenTileFill;
		}

		if (room.Disabled)
		{
			return new Color(0.22f, 0.13f, 0.09f, 0.94f);
		}

		var accent = GetSystemAccentColor(room);
		var fill = OpenTileFill.Lerp(accent, 0.26f);
		if (!room.IsDamaged)
		{
			return fill;
		}

		var damageRatio = 1.0f - (float)room.Integrity / ShipRoomState.MaxIntegrity;
		return fill.Darkened(0.14f + damageRatio * 0.32f);
	}

	private void DrawRoomBorders(BoardLayout layout)
	{
		foreach (var room in _shipState!.Grid.Rooms)
		{
			var color = GetSystemAccentColor(room).Lightened(0.28f);
			foreach (var tile in room.Tiles)
			{
				DrawRoomEdgeIfNeeded(layout, room, tile, Vector2I.Up, color);
				DrawRoomEdgeIfNeeded(layout, room, tile, Vector2I.Down, color);
				DrawRoomEdgeIfNeeded(layout, room, tile, Vector2I.Left, color);
				DrawRoomEdgeIfNeeded(layout, room, tile, Vector2I.Right, color);
			}
		}
	}

	private void DrawRoomEdgeIfNeeded(BoardLayout layout, ShipRoomState room, Vector2I tile, Vector2I neighborOffset, Color color)
	{
		var neighbor = _shipState!.Grid.GetTile(tile.X + neighborOffset.X, tile.Y + neighborOffset.Y);
		if (neighbor != null && neighbor.RoomId == room.RoomId)
		{
			return;
		}

		var rect = GetTileRect(layout, tile.X, tile.Y).Grow(-2.0f);
		var from = neighborOffset == Vector2I.Up
			? rect.Position
			: neighborOffset == Vector2I.Down
				? new Vector2(rect.Position.X, rect.End.Y)
				: neighborOffset == Vector2I.Left
					? rect.Position
					: new Vector2(rect.End.X, rect.Position.Y);
		var to = neighborOffset == Vector2I.Up
			? new Vector2(rect.End.X, rect.Position.Y)
			: neighborOffset == Vector2I.Down
				? rect.End
				: neighborOffset == Vector2I.Left
					? new Vector2(rect.Position.X, rect.End.Y)
					: rect.End;

		DrawLine(from, to, new Color(color.R, color.G, color.B, 0.78f), 2.0f, true);
	}

	private void DrawRoomLabels(Font font, BoardLayout layout)
	{
		foreach (var room in _shipState!.Grid.Rooms)
		{
			if (room.Tiles.Count == 0)
			{
				continue;
			}

			var bounds = GetRoomBounds(layout, room);
			var center = bounds.Position + (bounds.Size * 0.5f);
			var label = GetRoomLabel(room);
			DrawString(
				font,
				center + new Vector2(-bounds.Size.X * 0.5f, 4.0f),
				label,
				HorizontalAlignment.Center,
				bounds.Size.X,
				10,
				new Color(1.0f, 0.88f, 0.6f, 0.82f));
		}
	}

	private void DrawCannonChargeBar(BoardLayout layout)
	{
		if (!_cannonChargeBarState.IsVisible || string.IsNullOrEmpty(_cannonChargeBarState.RoomId))
		{
			return;
		}

		var room = _shipState!.Grid.Rooms.Find(candidate => candidate.RoomId == _cannonChargeBarState.RoomId);
		if (room == null || room.Tiles.Count == 0)
		{
			return;
		}

		var roomBounds = GetRoomBounds(layout, room);
		var barWidth = Mathf.Max(28.0f, roomBounds.Size.X - 8.0f);
		var barSize = new Vector2(barWidth, 8.0f);
		var barPosition = new Vector2(
			roomBounds.Position.X + ((roomBounds.Size.X - barWidth) * 0.5f),
			Mathf.Max(layout.BoardRect.Position.Y, roomBounds.Position.Y - 13.0f));
		var barRect = new Rect2(barPosition, barSize);
		var progressRect = new Rect2(barRect.Position, new Vector2(barRect.Size.X * Mathf.Clamp((float)_cannonChargeBarState.ProgressRatio, 0.0f, 1.0f), barRect.Size.Y));

		DrawRect(barRect, ChargeBack, true);
		DrawRect(progressRect, _cannonChargeBarState.IsActive ? ChargeFill : ChargeInactiveFill, true);
		DrawRect(barRect, new Color(0.96f, 0.86f, 0.56f, 0.72f), false, 1.0f);
	}

	private void DrawCrewTokens(Font font, BoardLayout layout)
	{
		foreach (var crew in _shipState!.GetCrewOnBoard())
		{
			var tile = _shipState.Grid.GetTile(crew.Position.TileX, crew.Position.TileY);
			if (tile == null)
			{
				continue;
			}

			var center = GetTileCenter(layout, crew.Position.TileX, crew.Position.TileY);
			DrawCrewToken(font, center, GetCrewTokenRadius(layout), crew, crew.Id == _selectedCrewId);
		}
	}

	private void DrawCrewToken(Font font, Vector2 center, float radius, CrewState crew, bool isSelected)
	{
		var fill = crew.Allegiance == CrewAllegiance.Player
			? new Color(0.2f, 0.43f, 0.76f)
			: new Color(0.72f, 0.26f, 0.22f);
		var border = isSelected
			? new Color(1.0f, 0.96f, 0.68f)
			: new Color(0.96f, 0.88f, 0.66f);

		DrawCircle(center + new Vector2(2.0f, 3.0f), radius + 2.0f, new Color(0.03f, 0.02f, 0.015f, 0.52f));
		DrawCircle(center, radius + 1.0f, new Color(0.08f, 0.045f, 0.025f, 0.95f));
		DrawCircle(center, radius, fill);
		DrawCircle(center, radius + 2.0f, border, false, isSelected ? 3.0f : 2.0f, true);
		DrawString(
			font,
			center + new Vector2(-radius, radius * 0.45f),
			crew.ShortLabel,
			HorizontalAlignment.Center,
			radius * 2.0f,
			12,
			Colors.White);
	}

	private bool TryFindCrewAtPosition(BoardLayout layout, Vector2 position, out CrewState crew)
	{
		crew = null!;
		var radius = GetCrewTokenRadius(layout) + 4.0f;
		foreach (var candidate in _shipState!.GetCrewOnBoard())
		{
			var center = GetTileCenter(layout, candidate.Position.TileX, candidate.Position.TileY);
			if (center.DistanceTo(position) > radius)
			{
				continue;
			}

			crew = candidate;
			return true;
		}

		return false;
	}

	private bool TryFindWalkableTileAtPosition(BoardLayout layout, Vector2 position, out int tileX, out int tileY)
	{
		tileX = -1;
		tileY = -1;
		if (!layout.BoardRect.HasPoint(position))
		{
			return false;
		}

		var local = position - layout.BoardRect.Position;
		tileX = Mathf.FloorToInt(local.X / layout.TileSize);
		tileY = Mathf.FloorToInt(local.Y / layout.TileSize);
		var tile = _shipState!.Grid.GetTile(tileX, tileY);
		return tile?.Walkable == true;
	}

	private static Rect2 GetTileRect(BoardLayout layout, int tileX, int tileY)
	{
		return new Rect2(
			layout.BoardRect.Position + new Vector2(tileX * layout.TileSize, tileY * layout.TileSize),
			new Vector2(layout.TileSize, layout.TileSize));
	}

	private static Vector2 GetTileCenter(BoardLayout layout, int tileX, int tileY)
	{
		return GetTileRect(layout, tileX, tileY).Position + new Vector2(layout.TileSize * 0.5f, layout.TileSize * 0.5f);
	}

	private static float GetCrewTokenRadius(BoardLayout layout)
	{
		return Mathf.Clamp(layout.TileSize * 0.27f, 9.0f, 15.0f);
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

		var topLeft = GetTileRect(layout, minTileX, minTileY).Position;
		var size = new Vector2(
			(maxTileX - minTileX + 1) * layout.TileSize,
			(maxTileY - minTileY + 1) * layout.TileSize);
		return new Rect2(topLeft, size);
	}

	private static string GetRoomLabel(ShipRoomState room)
	{
		return room.SystemType switch
		{
			"HelmRigging" => "Helm",
			"Cannons" => "Cannon",
			"ThreadChamber" => "Thread",
			"CrowsNest" => "Nest",
			"DoctorsQuarters" => "Doctor",
			_ => room.DisplayName
		};
	}

	private static Color GetSystemAccentColor(ShipRoomState room)
	{
		return room.SystemType switch
		{
			"HelmRigging" => new Color(0.4f, 0.63f, 0.86f),
			"Cannons" => new Color(0.82f, 0.34f, 0.25f),
			"ThreadChamber" => new Color(0.62f, 0.42f, 0.78f),
			"CrowsNest" => new Color(0.86f, 0.72f, 0.28f),
			"DoctorsQuarters" => new Color(0.44f, 0.78f, 0.54f),
			_ => new Color(0.58f, 0.48f, 0.34f)
		};
	}

	private static Vector2[] ClosePolygon(Vector2[] points)
	{
		var closed = new Vector2[points.Length + 1];
		for (var i = 0; i < points.Length; i++)
		{
			closed[i] = points[i];
		}

		closed[^1] = points[0];
		return closed;
	}

	private static Vector2[] OffsetPoints(Vector2[] points, Vector2 offset)
	{
		var offsetPoints = new Vector2[points.Length];
		for (var i = 0; i < points.Length; i++)
		{
			offsetPoints[i] = points[i] + offset;
		}

		return offsetPoints;
	}

	private readonly record struct BoardLayout(Rect2 BoardRect, float TileSize);
}
