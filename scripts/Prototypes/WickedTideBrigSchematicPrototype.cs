using Godot;

namespace TidesOfTime.Prototypes;

[Tool]
public partial class WickedTideBrigSchematicPrototype : Control
{
	private static readonly Color OceanDeep = new(0.035f, 0.145f, 0.18f);
	private static readonly Color OceanMid = new(0.045f, 0.22f, 0.25f, 0.42f);
	private static readonly Color OceanLine = new(0.35f, 0.78f, 0.82f, 0.16f);
	private static readonly Color OceanFoam = new(0.72f, 0.95f, 0.9f, 0.2f);
	private static readonly Color HullFill = new(0.18f, 0.105f, 0.055f);
	private static readonly Color HullBorder = new(0.72f, 0.52f, 0.28f);
	private static readonly Color DeckFill = new(0.68f, 0.43f, 0.19f);
	private static readonly Color DeckHighlight = new(0.86f, 0.61f, 0.31f, 0.3f);
	private static readonly Color DeckLine = new(0.28f, 0.16f, 0.08f, 0.46f);
	private static readonly Color WallShadow = new(0.11f, 0.065f, 0.035f, 0.78f);
	private static readonly Color RoomFill = new(0.84f, 0.69f, 0.43f, 0.95f);
	private static readonly Color RoomBorder = new(0.22f, 0.12f, 0.055f, 0.92f);
	private static readonly Color RoomInnerLine = new(0.98f, 0.86f, 0.55f, 0.22f);
	private static readonly Color OpenTileFill = new(0.72f, 0.47f, 0.24f, 0.82f);
	private static readonly Color OutsideTileFill = new(0.17f, 0.095f, 0.045f, 0.32f);
	private static readonly Color BlockedTileFill = new(0.11f, 0.07f, 0.045f, 0.92f);
	private static readonly Color TileLine = new(0.12f, 0.075f, 0.04f, 0.62f);
	private static readonly Color Ink = new(0.13f, 0.08f, 0.045f);
	private static readonly Color MutedInk = new(0.24f, 0.17f, 0.11f, 0.82f);
	private static readonly Color DoorColor = new(0.95f, 0.77f, 0.36f);
	private static readonly Color RouteColor = new(0.53f, 0.94f, 0.9f);
	private static readonly Color RouteShadow = new(0.02f, 0.08f, 0.09f, 0.62f);
	private static readonly Color MastFill = new(0.09f, 0.055f, 0.035f, 0.92f);
	private static readonly Color Parchment = new(0.89f, 0.76f, 0.53f, 0.94f);
	private static readonly Color ParchmentDark = new(0.5f, 0.33f, 0.16f, 0.76f);

	private static readonly Color HelmAccent = new(0.4f, 0.63f, 0.86f);
	private static readonly Color ThreadAccent = new(0.62f, 0.42f, 0.78f);
	private static readonly Color LookoutAccent = new(0.86f, 0.72f, 0.28f);
	private static readonly Color DoctorAccent = new(0.44f, 0.78f, 0.54f);
	private static readonly Color CannonAccent = new(0.82f, 0.34f, 0.25f);
	private static readonly Color CargoAccent = new(0.58f, 0.48f, 0.34f);
	private const int DeckGridColumns = 14;
	private const int DeckGridRows = 8;

	public override void _Ready()
	{
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (Size.X < 240.0f || Size.Y < 180.0f)
		{
			return;
		}

		var font = GetThemeFont("font", "Label");
		var canvas = new Rect2(Vector2.Zero, Size);
		var shipRect = CalculateShipRect(canvas);
		var deckRect = CalculateDeckRect(shipRect);
		var deckGrid = CreateDeckGrid(deckRect);
		var rooms = BuildRooms(deckGrid);
		var mastRect = CalculateMastRect(deckGrid);
		var doctorRect = CalculateDoctorInsetRect(deckGrid);
		var hatchCenter = CalculateHatchCenter(deckGrid);

		DrawOcean(canvas);
		DrawTitlePanel(font, canvas);
		DrawShipHull(shipRect, deckGrid);
		DrawTileMap(deckGrid, rooms);
		DrawDoors(deckGrid);
		DrawRouteCue(deckGrid);
		DrawMast(font, mastRect);
		DrawHatchAndDoctorInset(font, hatchCenter, doctorRect);
		DrawRoomSymbols(font, rooms);
		DrawCrewTokens(font, rooms);
		DrawLegend(font, canvas);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			QueueRedraw();
		}
	}

	private static Rect2 CalculateShipRect(Rect2 canvas)
	{
		var sideMargin = 60.0f;
		var topMargin = 124.0f;
		var legendSpace = canvas.Size.X >= 1120.0f ? 340.0f : 0.0f;
		var availableWidth = Mathf.Max(520.0f, canvas.Size.X - (sideMargin * 2.0f) - legendSpace);
		var availableHeight = Mathf.Max(360.0f, canvas.Size.Y - topMargin - 86.0f);
		var shipWidth = Mathf.Min(1060.0f, availableWidth);
		var shipHeight = shipWidth / 2.35f;

		if (shipHeight > availableHeight * 0.66f)
		{
			shipHeight = availableHeight * 0.66f;
			shipWidth = shipHeight * 2.35f;
		}

		var x = sideMargin + (availableWidth - shipWidth) * 0.5f;
		var y = topMargin + 34.0f;
		return new Rect2(new Vector2(x, y), new Vector2(shipWidth, shipHeight));
	}

	private static Rect2 CalculateDeckRect(Rect2 shipRect)
	{
		return new Rect2(
			shipRect.Position + new Vector2(shipRect.Size.X * 0.13f, shipRect.Size.Y * 0.16f),
			new Vector2(shipRect.Size.X * 0.74f, shipRect.Size.Y * 0.68f));
	}

	private static DeckGrid CreateDeckGrid(Rect2 deckRect)
	{
		return new DeckGrid(deckRect, DeckGridColumns, DeckGridRows);
	}

	private static Rect2 CalculateMastRect(DeckGrid deckGrid)
	{
		return deckGrid.RectFor(7, 2, 1, 3).Grow(-4.0f);
	}

	private static Vector2 CalculateHatchCenter(DeckGrid deckGrid)
	{
		return deckGrid.CellCenter(9, 5);
	}

	private static Rect2 CalculateDoctorInsetRect(DeckGrid deckGrid)
	{
		var size = new Vector2(deckGrid.Bounds.Size.X * 0.3f, 108.0f);
		var hatchCenter = CalculateHatchCenter(deckGrid);
		var position = new Vector2(
			hatchCenter.X - (size.X * 0.5f),
			deckGrid.Bounds.End.Y + 28.0f);
		return new Rect2(position, size);
	}

	private static RoomBox[] BuildRooms(DeckGrid deckGrid)
	{
		return new[]
		{
			Room(deckGrid, "Helm", "Rigging", 0, 3, 3, 2, HelmAccent, RoomIconKind.Helm),
			Room(deckGrid, "Port Bay", "Cannon", 4, 0, 3, 2, CannonAccent, RoomIconKind.Cannon),
			Room(deckGrid, "Port Bay", "Cannon", 9, 0, 3, 2, CannonAccent, RoomIconKind.Cannon),
			Room(deckGrid, "Stbd Bay", "Cannon", 4, 5, 3, 3, CannonAccent, RoomIconKind.Cannon),
			Room(deckGrid, "Stbd Bay", "Cannon", 10, 5, 3, 3, CannonAccent, RoomIconKind.Cannon),
			Room(deckGrid, "Cargo Stores", "Support", 4, 3, 2, 3, CargoAccent, RoomIconKind.Cargo),
			Room(deckGrid, "Crow's Nest", "Lookout", 6, 2, 3, 2, LookoutAccent, RoomIconKind.Lookout),
			Room(deckGrid, "Cargo Bay", "Supplies", 9, 3, 2, 2, CargoAccent, RoomIconKind.Cargo),
			Room(deckGrid, "Thread", "Chamber", 12, 3, 2, 2, ThreadAccent, RoomIconKind.Thread)
		};
	}

	private static RoomBox Room(
		DeckGrid deckGrid,
		string name,
		string detail,
		int x,
		int y,
		int width,
		int height,
		Color accent,
		RoomIconKind icon)
	{
		var rect = deckGrid.RectFor(x, y, width, height);
		return new RoomBox(name, detail, rect, accent, icon, x, y, width, height);
	}

	private void DrawOcean(Rect2 canvas)
	{
		DrawRect(canvas, OceanDeep, true);

		for (var band = 0; band < 5; band++)
		{
			var bandRect = new Rect2(
				0.0f,
				canvas.Size.Y * (0.12f + (band * 0.18f)),
				canvas.Size.X,
				canvas.Size.Y * 0.08f);
			DrawRect(bandRect, OceanMid, true);
		}

		for (var i = 0; i < 18; i++)
		{
			var y = 42.0f + (i * 50.0f);
			var start = new Vector2(20.0f + ((i % 4) * 28.0f), y);
			var points = new Vector2[10];

			for (var pointIndex = 0; pointIndex < points.Length; pointIndex++)
			{
				var x = start.X + (pointIndex * 82.0f);
				var waveY = start.Y + ((pointIndex % 2 == 0) ? 0.0f : 8.0f);
				points[pointIndex] = new Vector2(x, waveY);
			}

			DrawPolyline(points, OceanLine, 2.0f, true);

			if (i % 3 == 1)
			{
				DrawPolyline(
					new[] { points[1] + new Vector2(16.0f, 11.0f), points[2] + new Vector2(16.0f, 11.0f), points[3] + new Vector2(16.0f, 11.0f) },
					OceanFoam,
					1.0f,
					true);
			}
		}
	}

	private void DrawTitlePanel(Font font, Rect2 canvas)
	{
		var panelWidth = Mathf.Min(820.0f, Mathf.Max(220.0f, canvas.Size.X - 96.0f));
		var panel = new Rect2(48.0f, 26.0f, panelWidth, 88.0f);
		DrawParchmentPanel(panel);

		DrawString(
			font,
			panel.Position + new Vector2(18.0f, 35.0f),
			"Wicked Tide Brig",
			HorizontalAlignment.Left,
			panel.Size.X - 36.0f,
			30,
			Ink);
		DrawString(
			font,
			panel.Position + new Vector2(20.0f, 64.0f),
			"medium ship tactical deck plan",
			HorizontalAlignment.Left,
			panel.Size.X - 40.0f,
			16,
			MutedInk);

		if (panel.Size.X < 590.0f)
		{
			return;
		}

		var stats = new Rect2(panel.End.X - 262.0f, panel.Position.Y + 15.0f, 236.0f, 58.0f);
		DrawRect(stats, new Color(0.28f, 0.16f, 0.075f, 0.16f), true);
		DrawRect(stats, new Color(0.33f, 0.2f, 0.09f, 0.5f), false, 1.0f);
		DrawStatLine(font, stats.Position + new Vector2(12.0f, 22.0f), "Crew", "6/6");
		DrawStatLine(font, stats.Position + new Vector2(124.0f, 22.0f), "Hull", "100");
		DrawStatLine(font, stats.Position + new Vector2(12.0f, 47.0f), "Supplies", "14");
		DrawStatLine(font, stats.Position + new Vector2(124.0f, 47.0f), "Gold", "215");
	}

	private void DrawStatLine(Font font, Vector2 position, string label, string value)
	{
		DrawString(font, position, label, HorizontalAlignment.Left, 70.0f, 12, MutedInk);
		DrawString(font, position + new Vector2(58.0f, 0.0f), value, HorizontalAlignment.Left, 44.0f, 13, Ink);
	}

	private void DrawShipHull(Rect2 shipRect, DeckGrid deckGrid)
	{
		var deckRect = deckGrid.Bounds;
		var top = shipRect.Position.Y;
		var bottom = shipRect.End.Y;
		var middleY = Center(shipRect).Y;
		var left = shipRect.Position.X;
		var right = shipRect.End.X;
		var width = shipRect.Size.X;
		var height = shipRect.Size.Y;

		var hull = new[]
		{
			new Vector2(left + width * 0.05f, middleY),
			new Vector2(left + width * 0.11f, top + height * 0.2f),
			new Vector2(left + width * 0.23f, top + height * 0.09f),
			new Vector2(right - width * 0.18f, top + height * 0.08f),
			new Vector2(right - width * 0.06f, top + height * 0.2f),
			new Vector2(right, middleY),
			new Vector2(right - width * 0.06f, bottom - height * 0.2f),
			new Vector2(right - width * 0.18f, bottom - height * 0.08f),
			new Vector2(left + width * 0.23f, bottom - height * 0.09f),
			new Vector2(left + width * 0.11f, bottom - height * 0.2f)
		};

		var shadow = OffsetPoints(hull, new Vector2(8.0f, 12.0f));
		var innerHull = new[]
		{
			new Vector2(left + width * 0.12f, middleY),
			new Vector2(left + width * 0.18f, top + height * 0.27f),
			new Vector2(left + width * 0.29f, top + height * 0.17f),
			new Vector2(right - width * 0.22f, top + height * 0.17f),
			new Vector2(right - width * 0.11f, middleY),
			new Vector2(right - width * 0.22f, bottom - height * 0.17f),
			new Vector2(left + width * 0.29f, bottom - height * 0.17f),
			new Vector2(left + width * 0.18f, bottom - height * 0.27f)
		};
		var deckFrame = new[]
		{
			new Vector2(deckRect.Position.X - 18.0f, deckRect.Position.Y - 16.0f),
			new Vector2(deckRect.End.X + 18.0f, deckRect.Position.Y - 12.0f),
			new Vector2(deckRect.End.X + 28.0f, Center(deckRect).Y),
			new Vector2(deckRect.End.X + 18.0f, deckRect.End.Y + 12.0f),
			new Vector2(deckRect.Position.X - 18.0f, deckRect.End.Y + 16.0f),
			new Vector2(deckRect.Position.X - 30.0f, Center(deckRect).Y)
		};

		DrawColoredPolygon(shadow, new Color(0.01f, 0.035f, 0.04f, 0.46f));
		DrawColoredPolygon(hull, HullFill);
		DrawPolyline(ClosePolygon(hull), HullBorder, 3.0f, true);
		DrawPolyline(ClosePolygon(innerHull), new Color(0.92f, 0.68f, 0.34f, 0.34f), 1.0f, true);

		DrawColoredPolygon(deckFrame, HullFill.Lightened(0.14f));
		DrawPolyline(ClosePolygon(deckFrame), new Color(0.08f, 0.045f, 0.025f, 0.75f), 4.0f, true);
		DrawRect(deckRect, DeckFill, true);
		DrawRect(deckRect, HullBorder.Darkened(0.25f), false, 3.0f);

		for (var i = 0; i <= deckGrid.Columns; i++)
		{
			var x = deckGrid.Point(i, 0).X;
			DrawLine(
				new Vector2(x, deckRect.Position.Y + 10.0f),
				new Vector2(x, deckRect.End.Y - 10.0f),
				i % 2 == 0 ? DeckLine : new Color(0.28f, 0.16f, 0.08f, 0.2f),
				1.0f,
				true);
		}

		for (var i = 1; i < deckGrid.Rows; i++)
		{
			var y = deckGrid.Point(0, i).Y;
			DrawLine(
				new Vector2(deckRect.Position.X + 12.0f, y),
				new Vector2(deckRect.End.X - 12.0f, y),
				new Color(0.98f, 0.72f, 0.34f, i % 2 == 0 ? 0.13f : 0.08f),
				1.0f,
				true);
		}

		DrawLine(
			deckRect.Position + new Vector2(18.0f, 18.0f),
			deckRect.Position + new Vector2(deckRect.Size.X - 18.0f, 18.0f),
			DeckHighlight,
			2.0f,
			true);

		DrawHullRibs(shipRect, deckRect);
	}

	private void DrawTileMap(DeckGrid deckGrid, RoomBox[] rooms)
	{
		for (var row = 0; row < deckGrid.Rows; row++)
		{
			for (var column = 0; column < deckGrid.Columns; column++)
			{
				var room = FindRoomForTile(rooms, column, row);
				DrawDeckTile(deckGrid, column, row, room);
			}
		}

		foreach (var room in rooms)
		{
			DrawRoomRegion(room);
		}
	}

	private void DrawDeckTile(DeckGrid deckGrid, int column, int row, RoomBox? room)
	{
		var tileRect = deckGrid.RectFor(column, row, 1, 1).Grow(-2.5f);
		var isPlayable = IsPlayableTile(column, row);
		var isBlocked = IsBlockedTile(column, row);
		var isHatch = column == 9 && row == 5;
		var isRoute = IsRouteTile(column, row);
		var fill = GetTileFill(room, isPlayable, isBlocked, isHatch, isRoute);

		DrawRect(tileRect, fill, true);
		DrawRect(tileRect, TileLine, false, 1.0f);

		if (!isPlayable)
		{
			DrawLine(tileRect.Position + new Vector2(6.0f, tileRect.Size.Y - 6.0f), tileRect.End - new Vector2(6.0f, tileRect.Size.Y - 6.0f), new Color(0.05f, 0.035f, 0.025f, 0.35f), 1.0f, true);
			return;
		}

		if (isBlocked)
		{
			DrawRect(tileRect.Grow(-5.0f), new Color(0.72f, 0.52f, 0.28f, 0.26f), false, 2.0f);
			DrawLine(tileRect.Position + new Vector2(8.0f, 8.0f), tileRect.End - new Vector2(8.0f, 8.0f), new Color(0.92f, 0.74f, 0.42f, 0.34f), 2.0f, true);
			DrawLine(new Vector2(tileRect.End.X - 8.0f, tileRect.Position.Y + 8.0f), new Vector2(tileRect.Position.X + 8.0f, tileRect.End.Y - 8.0f), new Color(0.92f, 0.74f, 0.42f, 0.34f), 2.0f, true);
			return;
		}

		if (room != null)
		{
			var accentRect = new Rect2(tileRect.Position + new Vector2(4.0f, 4.0f), new Vector2(tileRect.Size.X - 8.0f, 5.0f));
			DrawRect(accentRect, new Color(room.Value.Accent.R, room.Value.Accent.G, room.Value.Accent.B, 0.58f), true);
		}
	}

	private Color GetTileFill(RoomBox? room, bool isPlayable, bool isBlocked, bool isHatch, bool isRoute)
	{
		if (!isPlayable)
		{
			return OutsideTileFill;
		}

		if (isBlocked)
		{
			return BlockedTileFill;
		}

		if (isHatch)
		{
			return new Color(0.82f, 0.58f, 0.24f, 0.95f);
		}

		if (room == null)
		{
			return isRoute
				? new Color(0.66f, 0.61f, 0.42f, 0.9f)
				: OpenTileFill;
		}

		var accent = room.Value.Accent;
		var fill = new Color(
			Mathf.Lerp(RoomFill.R, accent.R, 0.22f),
			Mathf.Lerp(RoomFill.G, accent.G, 0.22f),
			Mathf.Lerp(RoomFill.B, accent.B, 0.22f),
			0.9f);
		return isRoute ? fill.Lightened(0.12f) : fill;
	}

	private void DrawRoomRegion(RoomBox room)
	{
		var outlineRect = room.Rect.Grow(-2.5f);
		DrawRect(outlineRect, new Color(room.Accent.R, room.Accent.G, room.Accent.B, 0.74f), false, 2.0f);
		DrawRect(outlineRect.Grow(-4.0f), RoomInnerLine, false, 1.0f);
	}

	private void DrawRoomSymbols(Font font, RoomBox[] rooms)
	{
		foreach (var room in rooms)
		{
			DrawRoomSymbol(font, room);
		}
	}

	private void DrawRoomSymbol(Font font, RoomBox room)
	{
		var center = GetRoomSymbolCenter(room);
		var radius = Mathf.Clamp(Mathf.Min(room.Rect.Size.X, room.Rect.Size.Y) * 0.2f, 12.0f, 19.0f);
		DrawSystemIcon(room.Icon, center, radius, room.Accent, true);

		var caption = GetRoomCaption(room);
		var captionWidth = Mathf.Min(room.Rect.Size.X - 8.0f, 64.0f);
		var captionOffset = room.Icon == RoomIconKind.Lookout
			? new Vector2(-captionWidth * 0.5f, -radius - 8.0f)
			: new Vector2(-captionWidth * 0.5f, radius + 18.0f);

		DrawString(
			font,
			center + captionOffset,
			caption,
			HorizontalAlignment.Center,
			captionWidth,
			10,
			new Color(0.98f, 0.88f, 0.64f, 0.82f));
	}

	private static RoomBox? FindRoomForTile(RoomBox[] rooms, int column, int row)
	{
		foreach (var room in rooms)
		{
			if (column >= room.X
				&& column < room.X + room.Width
				&& row >= room.Y
				&& row < room.Y + room.Height)
			{
				return room;
			}
		}

		return null;
	}

	private static bool IsPlayableTile(int column, int row)
	{
		return row switch
		{
			0 => column is >= 3 and <= 11,
			1 => column is >= 3 and <= 12,
			2 => column is >= 2 and <= 13,
			3 or 4 => column is >= 0 and <= 13,
			5 => column is >= 1 and <= 13,
			6 => column is >= 3 and <= 12,
			7 => column is >= 4 and <= 12,
			_ => false
		};
	}

	private static bool IsBlockedTile(int column, int row)
	{
		return (column == 7 && row is >= 2 and <= 4)
			|| (row is >= 6 and <= 7 && column is >= 5 and <= 6)
			|| (row is >= 6 and <= 7 && column is >= 11 and <= 12);
	}

	private static bool IsRouteTile(int column, int row)
	{
		return (row == 4 && column is >= 2 and <= 5)
			|| (column == 5 && row == 5)
			|| (row == 5 && column is >= 5 and <= 9)
			|| (row == 4 && column is >= 9 and <= 12);
	}

	private void DrawDoors(DeckGrid deckGrid)
	{
		DrawDoor(deckGrid, 3, 4, true);
		DrawDoor(deckGrid, 6, 4, true);
		DrawDoor(deckGrid, 9, 4, true);
		DrawDoor(deckGrid, 12, 4, true);
		DrawDoor(deckGrid, 5.5f, 2, false);
		DrawDoor(deckGrid, 10.5f, 2, false);
		DrawDoor(deckGrid, 5.5f, 5, false);
		DrawDoor(deckGrid, 11.5f, 5, false);
		DrawDoor(deckGrid, 8.5f, 5, false);
	}

	private void DrawDoor(DeckGrid deckGrid, float column, float row, bool vertical)
	{
		var center = deckGrid.Point(column, row);
		var halfLength = vertical ? deckGrid.CellHeight * 0.36f : deckGrid.CellWidth * 0.24f;
		var from = vertical
			? center + new Vector2(0.0f, -halfLength)
			: center + new Vector2(-halfLength, 0.0f);
		var to = vertical
			? center + new Vector2(0.0f, halfLength)
			: center + new Vector2(halfLength, 0.0f);

		DrawLine(from, to, new Color(0.08f, 0.05f, 0.025f, 0.75f), 7.0f, true);
		DrawLine(from, to, DoorColor, 3.0f, true);
	}

	private void DrawMast(Font font, Rect2 mastRect)
	{
		var center = Center(mastRect);
		DrawRect(mastRect.Grow(11.0f), new Color(0.05f, 0.03f, 0.02f, 0.55f), true);
		DrawRect(mastRect.Grow(5.0f), new Color(0.5f, 0.31f, 0.12f, 0.42f), false, 2.0f);
		DrawCircle(center, Mathf.Min(mastRect.Size.X, mastRect.Size.Y) * 0.6f, MastFill);
		DrawCircle(center, Mathf.Min(mastRect.Size.X, mastRect.Size.Y) * 0.6f, HullBorder, false, 2.0f, true);
		DrawLine(
			new Vector2(center.X, mastRect.Position.Y + 8.0f),
			new Vector2(center.X, mastRect.End.Y - 8.0f),
			HullBorder.Lightened(0.12f),
			5.0f,
			true);
		DrawLine(
			new Vector2(mastRect.Position.X + 8.0f, center.Y),
			new Vector2(mastRect.End.X - 8.0f, center.Y),
			new Color(0.88f, 0.64f, 0.32f, 0.72f),
			4.0f,
			true);
		DrawLine(
			mastRect.Position + new Vector2(8.0f, 8.0f),
			mastRect.End - new Vector2(8.0f, 8.0f),
			new Color(0.88f, 0.64f, 0.32f, 0.44f),
			2.0f,
			true);
		DrawLine(
			new Vector2(mastRect.End.X - 8.0f, mastRect.Position.Y + 8.0f),
			new Vector2(mastRect.Position.X + 8.0f, mastRect.End.Y - 8.0f),
			new Color(0.88f, 0.64f, 0.32f, 0.44f),
			2.0f,
			true);
		DrawString(
			font,
			new Vector2(mastRect.End.X + 10.0f, center.Y + 5.0f),
			"Blocked Mast",
			HorizontalAlignment.Left,
			96.0f,
			12,
			new Color(0.96f, 0.82f, 0.54f));
	}

	private void DrawHatchAndDoctorInset(Font font, Vector2 hatchCenter, Rect2 doctorRect)
	{
		var hatchBottom = hatchCenter + new Vector2(0.0f, 18.0f);
		var doctorTop = new Vector2(Center(doctorRect).X, doctorRect.Position.Y);
		var connectorBend = new Vector2(hatchBottom.X, doctorRect.Position.Y - 16.0f);
		var connectorColor = new Color(0.95f, 0.86f, 0.56f, 0.72f);
		DrawDashedLine(hatchBottom, connectorBend, connectorColor, 3.0f, 10.0f, 6.0f);
		DrawDashedLine(connectorBend, new Vector2(doctorTop.X, connectorBend.Y), connectorColor, 3.0f, 10.0f, 6.0f);
		DrawDashedLine(new Vector2(doctorTop.X, connectorBend.Y), doctorTop, connectorColor, 3.0f, 10.0f, 6.0f);

		DrawCircle(hatchCenter + new Vector2(3.0f, 4.0f), 17.0f, new Color(0.04f, 0.025f, 0.015f, 0.52f));
		DrawCircle(hatchCenter, 17.0f, new Color(0.08f, 0.05f, 0.025f, 0.9f));
		DrawCircle(hatchCenter, 12.0f, new Color(0.94f, 0.74f, 0.34f, 0.94f));
		for (var i = -1; i <= 1; i++)
		{
			var y = hatchCenter.Y + (i * 5.0f);
			DrawLine(
				new Vector2(hatchCenter.X - 7.0f, y),
				new Vector2(hatchCenter.X + 7.0f, y + 4.0f),
				new Color(0.12f, 0.07f, 0.03f, 0.85f),
				2.0f,
				true);
		}
		DrawString(
			font,
			hatchCenter + new Vector2(22.0f, 7.0f),
			"Hatch Down",
			HorizontalAlignment.Left,
			86.0f,
			12,
			new Color(0.96f, 0.86f, 0.6f));

		DrawParchmentPanel(doctorRect);
		DrawRect(new Rect2(doctorRect.Position + new Vector2(8.0f, 8.0f), new Vector2(doctorRect.Size.X - 16.0f, 7.0f)), DoctorAccent, true);
		DrawString(
			font,
			doctorRect.Position + new Vector2(8.0f, 42.0f),
			"Doctor's Quarters",
			HorizontalAlignment.Center,
			doctorRect.Size.X - 16.0f,
			15,
			Ink);
		DrawString(
			font,
			doctorRect.Position + new Vector2(8.0f, 66.0f),
			"below deck inset",
			HorizontalAlignment.Center,
			doctorRect.Size.X - 16.0f,
			12,
			MutedInk);
	}

	private void DrawRouteCue(DeckGrid deckGrid)
	{
		var start = deckGrid.CellCenter(2, 4);
		var end = deckGrid.CellCenter(12, 4);
		var points = new[]
		{
			start,
			deckGrid.CellCenter(5, 4),
			deckGrid.CellCenter(5, 5),
			deckGrid.CellCenter(9, 5),
			deckGrid.CellCenter(9, 4),
			end
		};

		DrawPolyline(points, new Color(RouteShadow.R, RouteShadow.G, RouteShadow.B, 0.32f), 6.0f, true);
		DrawPolyline(points, new Color(0.98f, 0.86f, 0.52f, 0.2f), 4.0f, true);
		DrawPolyline(points, new Color(RouteColor.R, RouteColor.G, RouteColor.B, 0.62f), 2.0f, true);

		foreach (var point in points)
		{
			DrawCircle(point, 4.0f, new Color(RouteShadow.R, RouteShadow.G, RouteShadow.B, 0.7f));
			DrawCircle(point, 2.5f, new Color(RouteColor.R, RouteColor.G, RouteColor.B, 0.82f));
		}

		DrawArrowHead(points[2], points[3], new Color(0.98f, 0.86f, 0.52f, 0.86f));
		DrawArrowHead(points[^2], points[^1], RouteColor);
	}

	private static Vector2 GetRoomSymbolCenter(RoomBox room)
	{
		var center = Center(room.Rect);

		return room.Icon switch
		{
			RoomIconKind.Cannon when room.Height >= 3 => center + new Vector2(0.0f, room.Rect.Size.Y * -0.08f),
			RoomIconKind.Cargo when room.Width <= 2 => center + new Vector2(0.0f, room.Rect.Size.Y * -0.08f),
			RoomIconKind.Lookout => center + new Vector2(0.0f, room.Rect.Size.Y * 0.08f),
			_ => center
		};
	}

	private static string GetRoomCaption(RoomBox room)
	{
		return room.Icon switch
		{
			RoomIconKind.Helm => "Helm",
			RoomIconKind.Cannon => "Bay",
			RoomIconKind.Cargo => "Cargo",
			RoomIconKind.Lookout => "Nest",
			RoomIconKind.Thread => "Thread",
			_ => room.Name
		};
	}

	private void DrawSystemIcon(RoomIconKind icon, Vector2 center, float radius, Color accent, bool drawBacking)
	{
		if (drawBacking)
		{
			DrawIconBacking(center, radius, accent);
		}

		var ink = new Color(0.98f, 0.9f, 0.66f, 0.96f);
		var shadow = new Color(0.05f, 0.03f, 0.02f, 0.78f);

		switch (icon)
		{
			case RoomIconKind.Helm:
				DrawHelmIcon(center, radius, ink, shadow);
				break;
			case RoomIconKind.Cannon:
				DrawCannonIcon(center, radius, ink, shadow);
				break;
			case RoomIconKind.Cargo:
				DrawCargoIcon(center, radius, ink, shadow);
				break;
			case RoomIconKind.Lookout:
				DrawLookoutIcon(center, radius, ink, shadow);
				break;
			case RoomIconKind.Thread:
				DrawThreadIcon(center, radius, ink, shadow);
				break;
			case RoomIconKind.Doctor:
				DrawDoctorIcon(center, radius, ink, shadow);
				break;
		}
	}

	private void DrawIconBacking(Vector2 center, float radius, Color accent)
	{
		DrawCircle(center + new Vector2(2.0f, 3.0f), radius + 6.0f, new Color(0.03f, 0.02f, 0.012f, 0.35f));
		DrawCircle(center, radius + 6.0f, new Color(0.08f, 0.05f, 0.025f, 0.42f));
		DrawCircle(center, radius + 6.0f, new Color(accent.R, accent.G, accent.B, 0.42f), false, 2.0f, true);
	}

	private void DrawHelmIcon(Vector2 center, float radius, Color ink, Color shadow)
	{
		DrawCircle(center, radius * 0.78f, shadow, false, 4.0f, true);
		DrawCircle(center, radius * 0.78f, ink, false, 2.0f, true);
		DrawCircle(center, radius * 0.28f, ink, false, 2.0f, true);

		for (var i = 0; i < 8; i++)
		{
			var angle = (Mathf.Pi * 2.0f * i) / 8.0f;
			var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			DrawLine(center + (direction * radius * 0.24f), center + (direction * radius * 0.98f), ink, 2.0f, true);
			DrawCircle(center + (direction * radius * 1.04f), 2.2f, ink);
		}
	}

	private void DrawCannonIcon(Vector2 center, float radius, Color ink, Color shadow)
	{
		var barrelStart = center + new Vector2(-radius * 0.62f, radius * 0.12f);
		var barrelEnd = center + new Vector2(radius * 0.58f, -radius * 0.22f);
		DrawLine(barrelStart + new Vector2(2.0f, 3.0f), barrelEnd + new Vector2(2.0f, 3.0f), shadow, 8.0f, true);
		DrawLine(barrelStart, barrelEnd, ink, 6.0f, true);
		DrawCircle(center + new Vector2(-radius * 0.32f, radius * 0.52f), radius * 0.2f, ink);
		DrawCircle(center + new Vector2(radius * 0.48f, radius * 0.44f), radius * 0.16f, ink);
		DrawCircle(center + new Vector2(radius * 0.85f, -radius * 0.42f), radius * 0.14f, ink);
	}

	private void DrawCargoIcon(Vector2 center, float radius, Color ink, Color shadow)
	{
		var box = new Rect2(center - new Vector2(radius * 0.62f, radius * 0.54f), new Vector2(radius * 1.24f, radius * 1.08f));
		DrawRect(new Rect2(box.Position + new Vector2(2.0f, 3.0f), box.Size), shadow, true);
		DrawRect(box, new Color(0.28f, 0.17f, 0.08f, 0.62f), true);
		DrawRect(box, ink, false, 2.0f);
		DrawLine(box.Position, box.End, ink, 1.6f, true);
		DrawLine(new Vector2(box.End.X, box.Position.Y), new Vector2(box.Position.X, box.End.Y), ink, 1.6f, true);
		DrawLine(new Vector2(box.Position.X, center.Y), new Vector2(box.End.X, center.Y), ink, 1.4f, true);
	}

	private void DrawLookoutIcon(Vector2 center, float radius, Color ink, Color shadow)
	{
		DrawLine(center + new Vector2(2.0f, -radius * 0.94f), center + new Vector2(2.0f, radius * 0.96f), shadow, 5.0f, true);
		DrawLine(center + new Vector2(0.0f, -radius * 0.94f), center + new Vector2(0.0f, radius * 0.96f), ink, 3.0f, true);
		DrawLine(center + new Vector2(-radius * 0.74f, -radius * 0.18f), center + new Vector2(radius * 0.74f, -radius * 0.18f), ink, 3.0f, true);

		var eye = new[]
		{
			center + new Vector2(-radius * 0.76f, radius * 0.26f),
			center + new Vector2(-radius * 0.34f, radius * 0.0f),
			center + new Vector2(0.0f, -radius * 0.08f),
			center + new Vector2(radius * 0.34f, radius * 0.0f),
			center + new Vector2(radius * 0.76f, radius * 0.26f),
			center + new Vector2(radius * 0.34f, radius * 0.5f),
			center + new Vector2(0.0f, radius * 0.6f),
			center + new Vector2(-radius * 0.34f, radius * 0.5f),
			center + new Vector2(-radius * 0.76f, radius * 0.26f)
		};
		DrawPolyline(eye, ink, 2.0f, true);
		DrawCircle(center + new Vector2(0.0f, radius * 0.26f), radius * 0.16f, ink);
	}

	private void DrawThreadIcon(Vector2 center, float radius, Color ink, Color shadow)
	{
		var points = BuildSpiralPoints(center, radius * 0.74f, 22);
		var shadowPoints = OffsetPoints(points, new Vector2(2.0f, 3.0f));
		DrawPolyline(shadowPoints, shadow, 4.0f, true);
		DrawPolyline(points, ink, 2.4f, true);
		DrawCircle(center, radius * 0.13f, ink);
		DrawCircle(points[^1], radius * 0.11f, ink);
	}

	private void DrawDoctorIcon(Vector2 center, float radius, Color ink, Color shadow)
	{
		DrawCircle(center + new Vector2(2.0f, 3.0f), radius * 0.82f, shadow);
		DrawCircle(center, radius * 0.82f, new Color(0.12f, 0.08f, 0.04f, 0.5f));
		DrawLine(center + new Vector2(-radius * 0.48f, 0.0f), center + new Vector2(radius * 0.48f, 0.0f), ink, 3.0f, true);
		DrawLine(center + new Vector2(0.0f, -radius * 0.48f), center + new Vector2(0.0f, radius * 0.48f), ink, 3.0f, true);
	}

	private void DrawCrewTokens(Font font, RoomBox[] rooms)
	{
		DrawCrewToken(font, rooms[0].Rect.Position + new Vector2(22.0f, 24.0f), "C", new Color(0.22f, 0.42f, 0.78f));
		DrawCrewToken(font, rooms[6].Rect.Position + new Vector2(22.0f, rooms[6].Rect.Size.Y - 20.0f), "M", new Color(0.24f, 0.56f, 0.42f));
		DrawCrewToken(font, rooms[5].Rect.End - new Vector2(22.0f, 22.0f), "G", new Color(0.78f, 0.48f, 0.2f));
	}

	private void DrawCrewToken(Font font, Vector2 center, string label, Color fill)
	{
		DrawCircle(center + new Vector2(3.0f, 4.0f), 15.0f, new Color(0.03f, 0.02f, 0.015f, 0.5f));
		DrawCircle(center, 15.0f, new Color(0.08f, 0.045f, 0.025f, 0.95f));
		DrawCircle(center, 12.0f, fill);
		DrawCircle(center + new Vector2(-4.0f, -4.0f), 3.5f, new Color(1.0f, 1.0f, 1.0f, 0.22f));
		DrawCircle(center, 15.0f, new Color(0.96f, 0.88f, 0.66f), false, 2.0f, true);
		DrawString(
			font,
			center + new Vector2(-9.0f, 6.0f),
			label,
			HorizontalAlignment.Center,
			18.0f,
			14,
			Colors.White);
	}

	private void DrawLegend(Font font, Rect2 canvas)
	{
		if (canvas.Size.X < 1120.0f)
		{
			return;
		}

		var rect = new Rect2(canvas.End.X - 326.0f, 120.0f, 266.0f, 356.0f);
		DrawParchmentPanel(rect);
		DrawRect(new Rect2(rect.Position + new Vector2(10.0f, 10.0f), new Vector2(rect.Size.X - 20.0f, 30.0f)), new Color(0.31f, 0.17f, 0.08f, 0.16f), true);
		DrawString(font, rect.Position + new Vector2(18.0f, 33.0f), "Schematic Notes", HorizontalAlignment.Left, rect.Size.X - 36.0f, 20, Ink);
		DrawString(font, rect.Position + new Vector2(18.0f, 55.0f), "captain's quick read", HorizontalAlignment.Left, rect.Size.X - 36.0f, 12, MutedInk);

		var y = rect.Position.Y + 92.0f;
		DrawLegendItem(font, rect.Position.X + 18.0f, ref y, RoomIconKind.Helm, HelmAccent, "Helm / Rigging");
		DrawLegendItem(font, rect.Position.X + 18.0f, ref y, RoomIconKind.Cannon, CannonAccent, "Broadside modules");
		DrawLegendItem(font, rect.Position.X + 18.0f, ref y, RoomIconKind.Lookout, LookoutAccent, "Crow's Nest / lookout");
		DrawLegendItem(font, rect.Position.X + 18.0f, ref y, RoomIconKind.Thread, ThreadAccent, "Thread Chamber");
		DrawLegendItem(font, rect.Position.X + 18.0f, ref y, RoomIconKind.Cargo, CargoAccent, "Cargo / support");
		DrawLegendItem(font, rect.Position.X + 18.0f, ref y, RoomIconKind.Doctor, DoctorAccent, "Below-deck doctor");

		y += 10.0f;
		DrawLine(new Vector2(rect.Position.X + 22.0f, y), new Vector2(rect.Position.X + 78.0f, y), RouteColor, 3.0f, true);
		DrawString(font, new Vector2(rect.Position.X + 92.0f, y + 6.0f), "sample route", HorizontalAlignment.Left, rect.Size.X - 108.0f, 14, MutedInk);

		y += 44.0f;
		DrawCircle(new Vector2(rect.Position.X + 50.0f, y - 4.0f), 10.0f, new Color(0.22f, 0.42f, 0.78f));
		DrawString(font, new Vector2(rect.Position.X + 92.0f, y + 2.0f), "crew token", HorizontalAlignment.Left, rect.Size.X - 108.0f, 14, MutedInk);
	}

	private void DrawLegendItem(Font font, float x, ref float y, RoomIconKind icon, Color color, string text)
	{
		DrawSystemIcon(icon, new Vector2(x + 13.0f, y - 9.0f), 8.5f, color, true);
		DrawString(font, new Vector2(x + 38.0f, y), text, HorizontalAlignment.Left, 200.0f, 14, MutedInk);
		y += 30.0f;
	}

	private void DrawParchmentPanel(Rect2 rect)
	{
		DrawRect(new Rect2(rect.Position + new Vector2(5.0f, 7.0f), rect.Size), new Color(0.03f, 0.02f, 0.012f, 0.36f), true);
		DrawRect(rect, Parchment, true);
		DrawRect(rect, ParchmentDark, false, 2.0f);
		DrawRect(rect.Grow(-6.0f), new Color(0.98f, 0.88f, 0.62f, 0.28f), false, 1.0f);
		DrawPanelCornerPins(rect);
	}

	private void DrawPanelCornerPins(Rect2 rect)
	{
		var pins = new[]
		{
			rect.Position + new Vector2(10.0f, 10.0f),
			new Vector2(rect.End.X - 10.0f, rect.Position.Y + 10.0f),
			rect.End - new Vector2(10.0f, 10.0f),
			new Vector2(rect.Position.X + 10.0f, rect.End.Y - 10.0f)
		};

		foreach (var pin in pins)
		{
			DrawCircle(pin, 3.5f, ParchmentDark);
			DrawCircle(pin + new Vector2(-1.0f, -1.0f), 1.2f, new Color(1.0f, 0.92f, 0.68f, 0.52f));
		}
	}

	private void DrawHullRibs(Rect2 shipRect, Rect2 deckRect)
	{
		var ribColor = new Color(0.86f, 0.62f, 0.32f, 0.24f);
		for (var i = 0; i < 7; i++)
		{
			var ratio = 0.16f + (i * 0.11f);
			var topOuter = new Vector2(shipRect.Position.X + (shipRect.Size.X * ratio), shipRect.Position.Y + shipRect.Size.Y * 0.17f);
			var topInner = new Vector2(deckRect.Position.X + (deckRect.Size.X * Mathf.Clamp((ratio - 0.12f) / 0.76f, 0.0f, 1.0f)), deckRect.Position.Y);
			var bottomOuter = new Vector2(topOuter.X, shipRect.End.Y - shipRect.Size.Y * 0.17f);
			var bottomInner = new Vector2(topInner.X, deckRect.End.Y);

			DrawLine(topOuter, topInner, ribColor, 1.0f, true);
			DrawLine(bottomOuter, bottomInner, ribColor, 1.0f, true);
		}
	}

	private void DrawArrowHead(Vector2 from, Vector2 to, Color color)
	{
		var direction = (to - from).Normalized();
		var side = new Vector2(-direction.Y, direction.X);
		var points = new[]
		{
			to,
			to - (direction * 18.0f) + (side * 8.0f),
			to - (direction * 18.0f) - (side * 8.0f)
		};

		DrawColoredPolygon(points, color);
	}

	private void DrawDashedLine(
		Vector2 from,
		Vector2 to,
		Color color,
		float width,
		float dashLength,
		float gapLength)
	{
		var delta = to - from;
		var length = delta.Length();
		if (length <= 0.01f)
		{
			return;
		}

		var direction = delta / length;
		var distance = 0.0f;

		while (distance < length)
		{
			var next = Mathf.Min(distance + dashLength, length);
			DrawLine(from + (direction * distance), from + (direction * next), color, width, true);
			distance += dashLength + gapLength;
		}
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

	private static Vector2[] BuildSpiralPoints(Vector2 center, float radius, int count)
	{
		var points = new Vector2[count];
		for (var i = 0; i < count; i++)
		{
			var t = count <= 1 ? 1.0f : i / (count - 1.0f);
			var angle = -Mathf.Pi * 0.35f + (Mathf.Pi * 3.3f * t);
			var distance = radius * t;
			points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
		}

		return points;
	}

	private static Vector2 Center(Rect2 rect)
	{
		return rect.Position + (rect.Size * 0.5f);
	}

	private readonly record struct DeckGrid(Rect2 Bounds, int Columns, int Rows)
	{
		public float CellWidth => Bounds.Size.X / Columns;
		public float CellHeight => Bounds.Size.Y / Rows;

		public Vector2 Point(float column, float row)
		{
			return Bounds.Position + new Vector2(column * CellWidth, row * CellHeight);
		}

		public Vector2 CellCenter(int column, int row)
		{
			return Point(column + 0.5f, row + 0.5f);
		}

		public Rect2 RectFor(int column, int row, int width, int height)
		{
			return new Rect2(
				Point(column, row),
				new Vector2(width * CellWidth, height * CellHeight));
		}
	}

	private enum RoomIconKind
	{
		Helm,
		Cannon,
		Cargo,
		Lookout,
		Thread,
		Doctor
	}

	private readonly record struct RoomBox(string Name, string Detail, Rect2 Rect, Color Accent, RoomIconKind Icon, int X, int Y, int Width, int Height);
}
