using Godot;

namespace TidesOfTime.Prototypes;

[Tool]
public partial class WickedTideBrigSchematicPrototype : Control
{
	private static readonly Color OceanDeep = new(0.035f, 0.145f, 0.18f);
	private static readonly Color OceanLine = new(0.35f, 0.78f, 0.82f, 0.16f);
	private static readonly Color HullFill = new(0.18f, 0.105f, 0.055f);
	private static readonly Color HullBorder = new(0.72f, 0.52f, 0.28f);
	private static readonly Color DeckFill = new(0.64f, 0.42f, 0.2f);
	private static readonly Color DeckLine = new(0.28f, 0.16f, 0.08f, 0.38f);
	private static readonly Color RoomFill = new(0.82f, 0.68f, 0.43f, 0.92f);
	private static readonly Color RoomBorder = new(0.29f, 0.17f, 0.08f, 0.78f);
	private static readonly Color Ink = new(0.13f, 0.08f, 0.045f);
	private static readonly Color MutedInk = new(0.24f, 0.17f, 0.11f, 0.82f);
	private static readonly Color DoorColor = new(0.95f, 0.77f, 0.36f);
	private static readonly Color RouteColor = new(0.48f, 0.9f, 0.95f);
	private static readonly Color RouteShadow = new(0.02f, 0.08f, 0.09f, 0.62f);
	private static readonly Color MastFill = new(0.09f, 0.055f, 0.035f, 0.92f);
	private static readonly Color Parchment = new(0.88f, 0.76f, 0.54f, 0.9f);

	private static readonly Color HelmAccent = new(0.4f, 0.63f, 0.86f);
	private static readonly Color ThreadAccent = new(0.62f, 0.42f, 0.78f);
	private static readonly Color LookoutAccent = new(0.86f, 0.72f, 0.28f);
	private static readonly Color DoctorAccent = new(0.44f, 0.78f, 0.54f);
	private static readonly Color CannonAccent = new(0.82f, 0.34f, 0.25f);
	private static readonly Color CargoAccent = new(0.58f, 0.48f, 0.34f);

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
		var rooms = BuildRooms(deckRect);
		var mastRect = CalculateMastRect(deckRect);
		var doctorRect = CalculateDoctorInsetRect(deckRect);
		var hatchCenter = deckRect.Position + new Vector2(deckRect.Size.X * 0.66f, deckRect.Size.Y * 0.61f);

		DrawOcean(canvas);
		DrawTitle(font, canvas);
		DrawShipHull(shipRect, deckRect);
		DrawRooms(font, rooms);
		DrawDoors(deckRect);
		DrawMast(font, mastRect);
		DrawHatchAndDoctorInset(font, hatchCenter, doctorRect);
		DrawRouteCue(rooms, mastRect);
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
		var y = topMargin + 42.0f;
		return new Rect2(new Vector2(x, y), new Vector2(shipWidth, shipHeight));
	}

	private static Rect2 CalculateDeckRect(Rect2 shipRect)
	{
		return new Rect2(
			shipRect.Position + new Vector2(shipRect.Size.X * 0.13f, shipRect.Size.Y * 0.16f),
			new Vector2(shipRect.Size.X * 0.74f, shipRect.Size.Y * 0.68f));
	}

	private static Rect2 CalculateMastRect(Rect2 deckRect)
	{
		var size = new Vector2(deckRect.Size.X * 0.07f, deckRect.Size.Y * 0.3f);
		return new Rect2(Center(deckRect) - (size * 0.5f), size);
	}

	private static Rect2 CalculateDoctorInsetRect(Rect2 deckRect)
	{
		var size = new Vector2(deckRect.Size.X * 0.28f, 104.0f);
		var position = new Vector2(
			deckRect.Position.X + deckRect.Size.X * 0.57f,
			deckRect.End.Y + 38.0f);
		return new Rect2(position, size);
	}

	private static RoomBox[] BuildRooms(Rect2 deckRect)
	{
		return new[]
		{
			Room(deckRect, "Helm", "Rigging", 0.05f, 0.35f, 0.17f, 0.28f, HelmAccent),
			Room(deckRect, "Port Module", "Cannon", 0.25f, 0.06f, 0.2f, 0.2f, CannonAccent),
			Room(deckRect, "Port Module", "Cannon", 0.55f, 0.06f, 0.2f, 0.2f, CannonAccent),
			Room(deckRect, "Starboard Module", "Cannon", 0.25f, 0.74f, 0.2f, 0.2f, CannonAccent),
			Room(deckRect, "Starboard Module", "Cannon", 0.55f, 0.74f, 0.2f, 0.2f, CannonAccent),
			Room(deckRect, "Cargo Stores", "Support", 0.25f, 0.36f, 0.14f, 0.27f, CargoAccent),
			Room(deckRect, "Thread", "Chamber", 0.42f, 0.35f, 0.16f, 0.3f, ThreadAccent),
			Room(deckRect, "Cargo Bay", "Supplies", 0.61f, 0.36f, 0.14f, 0.27f, CargoAccent),
			Room(deckRect, "Crow's Nest", "Lookout", 0.79f, 0.35f, 0.16f, 0.28f, LookoutAccent)
		};
	}

	private static RoomBox Room(
		Rect2 deckRect,
		string name,
		string detail,
		float x,
		float y,
		float width,
		float height,
		Color accent)
	{
		var rect = new Rect2(
			deckRect.Position + new Vector2(deckRect.Size.X * x, deckRect.Size.Y * y),
			new Vector2(deckRect.Size.X * width, deckRect.Size.Y * height));
		return new RoomBox(name, detail, rect, accent);
	}

	private void DrawOcean(Rect2 canvas)
	{
		DrawRect(canvas, OceanDeep, true);

		for (var i = 0; i < 14; i++)
		{
			var y = 58.0f + (i * 62.0f);
			var start = new Vector2(24.0f + ((i % 3) * 34.0f), y);
			var points = new Vector2[8];

			for (var pointIndex = 0; pointIndex < points.Length; pointIndex++)
			{
				var x = start.X + (pointIndex * 92.0f);
				var waveY = start.Y + ((pointIndex % 2 == 0) ? 0.0f : 10.0f);
				points[pointIndex] = new Vector2(x, waveY);
			}

			DrawPolyline(points, OceanLine, 2.0f, true);
		}
	}

	private void DrawTitle(Font font, Rect2 canvas)
	{
		DrawString(
			font,
			new Vector2(60.0f, 58.0f),
			"Wicked Tide Brig",
			HorizontalAlignment.Left,
			canvas.Size.X - 120.0f,
			34,
			new Color(0.92f, 0.82f, 0.6f));
		DrawString(
			font,
			new Vector2(62.0f, 86.0f),
			"medium ship tactical deck plan",
			HorizontalAlignment.Left,
			canvas.Size.X - 120.0f,
			16,
			new Color(0.72f, 0.89f, 0.89f, 0.82f));
	}

	private void DrawShipHull(Rect2 shipRect, Rect2 deckRect)
	{
		var top = shipRect.Position.Y;
		var bottom = shipRect.End.Y;
		var middleY = Center(shipRect).Y;
		var left = shipRect.Position.X;
		var right = shipRect.End.X;
		var width = shipRect.Size.X;
		var height = shipRect.Size.Y;

		var shadow = new[]
		{
			new Vector2(left + width * 0.08f, top + height * 0.12f) + new Vector2(8.0f, 12.0f),
			new Vector2(right - width * 0.13f, top + height * 0.12f) + new Vector2(8.0f, 12.0f),
			new Vector2(right, middleY) + new Vector2(8.0f, 12.0f),
			new Vector2(right - width * 0.13f, bottom - height * 0.12f) + new Vector2(8.0f, 12.0f),
			new Vector2(left + width * 0.08f, bottom - height * 0.12f) + new Vector2(8.0f, 12.0f),
			new Vector2(left, middleY) + new Vector2(8.0f, 12.0f)
		};

		var hull = new[]
		{
			new Vector2(left + width * 0.08f, top + height * 0.12f),
			new Vector2(right - width * 0.13f, top + height * 0.12f),
			new Vector2(right, middleY),
			new Vector2(right - width * 0.13f, bottom - height * 0.12f),
			new Vector2(left + width * 0.08f, bottom - height * 0.12f),
			new Vector2(left, middleY)
		};

		DrawColoredPolygon(shadow, new Color(0.01f, 0.035f, 0.04f, 0.46f));
		DrawColoredPolygon(hull, HullFill);
		DrawPolyline(ClosePolygon(hull), HullBorder, 3.0f, true);

		DrawRect(deckRect.Grow(12.0f), HullFill.Lightened(0.12f), true);
		DrawRect(deckRect, DeckFill, true);
		DrawRect(deckRect, HullBorder.Darkened(0.18f), false, 2.0f);

		for (var i = 1; i < 10; i++)
		{
			var x = deckRect.Position.X + (deckRect.Size.X * i / 10.0f);
			DrawLine(
				new Vector2(x, deckRect.Position.Y + 8.0f),
				new Vector2(x, deckRect.End.Y - 8.0f),
				DeckLine,
				1.0f,
				true);
		}
	}

	private void DrawRooms(Font font, RoomBox[] rooms)
	{
		foreach (var room in rooms)
		{
			DrawRect(room.Rect, RoomFill, true);
			DrawRect(room.Rect, RoomBorder, false, 2.0f);
			DrawRect(
				new Rect2(room.Rect.Position, new Vector2(room.Rect.Size.X, 7.0f)),
				new Color(room.Accent.R, room.Accent.G, room.Accent.B, 0.72f),
				true);

			DrawString(
				font,
				room.Rect.Position + new Vector2(6.0f, room.Rect.Size.Y * 0.46f),
				room.Name,
				HorizontalAlignment.Center,
				room.Rect.Size.X - 12.0f,
				14,
				Ink);
			DrawString(
				font,
				room.Rect.Position + new Vector2(6.0f, room.Rect.Size.Y * 0.68f),
				room.Detail,
				HorizontalAlignment.Center,
				room.Rect.Size.X - 12.0f,
				12,
				MutedInk);
		}
	}

	private void DrawDoors(Rect2 deckRect)
	{
		DrawDoor(deckRect, 0.235f, 0.5f, true);
		DrawDoor(deckRect, 0.405f, 0.5f, true);
		DrawDoor(deckRect, 0.595f, 0.5f, true);
		DrawDoor(deckRect, 0.765f, 0.5f, true);
		DrawDoor(deckRect, 0.34f, 0.31f, false);
		DrawDoor(deckRect, 0.34f, 0.69f, false);
		DrawDoor(deckRect, 0.66f, 0.31f, false);
		DrawDoor(deckRect, 0.66f, 0.69f, false);
	}

	private void DrawDoor(Rect2 deckRect, float xRatio, float yRatio, bool vertical)
	{
		var center = deckRect.Position + new Vector2(deckRect.Size.X * xRatio, deckRect.Size.Y * yRatio);
		var halfLength = 13.0f;
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
		DrawRect(mastRect.Grow(8.0f), new Color(0.05f, 0.03f, 0.02f, 0.45f), true);
		DrawCircle(center, Mathf.Min(mastRect.Size.X, mastRect.Size.Y) * 0.52f, MastFill);
		DrawLine(
			new Vector2(center.X, mastRect.Position.Y + 8.0f),
			new Vector2(center.X, mastRect.End.Y - 8.0f),
			HullBorder.Lightened(0.12f),
			5.0f,
			true);
		DrawString(
			font,
			new Vector2(mastRect.Position.X - 36.0f, mastRect.End.Y + 20.0f),
			"Mast",
			HorizontalAlignment.Center,
			mastRect.Size.X + 72.0f,
			12,
			new Color(0.96f, 0.82f, 0.54f));
	}

	private void DrawHatchAndDoctorInset(Font font, Vector2 hatchCenter, Rect2 doctorRect)
	{
		DrawDashedLine(
			hatchCenter + new Vector2(0.0f, 12.0f),
			new Vector2(Center(doctorRect).X, doctorRect.Position.Y),
			new Color(0.95f, 0.86f, 0.56f, 0.7f),
			2.0f,
			9.0f,
			6.0f);

		DrawCircle(hatchCenter, 15.0f, new Color(0.08f, 0.05f, 0.025f, 0.84f));
		DrawCircle(hatchCenter, 10.0f, new Color(0.94f, 0.74f, 0.34f, 0.94f));
		DrawString(
			font,
			hatchCenter + new Vector2(-18.0f, 36.0f),
			"Hatch",
			HorizontalAlignment.Center,
			36.0f,
			12,
			new Color(0.96f, 0.86f, 0.6f));

		DrawRect(doctorRect.Grow(8.0f), new Color(0.05f, 0.03f, 0.02f, 0.35f), true);
		DrawRect(doctorRect, Parchment, true);
		DrawRect(doctorRect, RoomBorder, false, 2.0f);
		DrawRect(new Rect2(doctorRect.Position, new Vector2(doctorRect.Size.X, 7.0f)), DoctorAccent, true);
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

	private void DrawRouteCue(RoomBox[] rooms, Rect2 mastRect)
	{
		var helm = rooms[0].Rect;
		var target = rooms[2].Rect;
		var start = Center(helm) + new Vector2(22.0f, 0.0f);
		var end = Center(target) + new Vector2(0.0f, 22.0f);
		var points = new[]
		{
			start,
			new Vector2(mastRect.Position.X - 62.0f, start.Y),
			new Vector2(mastRect.Position.X - 62.0f, mastRect.Position.Y - 28.0f),
			new Vector2(mastRect.End.X + 58.0f, mastRect.Position.Y - 28.0f),
			new Vector2(mastRect.End.X + 58.0f, end.Y),
			end
		};

		DrawPolyline(points, RouteShadow, 8.0f, true);
		DrawPolyline(points, RouteColor, 3.0f, true);

		foreach (var point in points)
		{
			DrawCircle(point, 4.0f, RouteColor);
		}

		DrawArrowHead(points[^2], points[^1], RouteColor);
	}

	private void DrawCrewTokens(Font font, RoomBox[] rooms)
	{
		DrawCrewToken(font, Center(rooms[0].Rect) + new Vector2(-24.0f, 8.0f), "C", new Color(0.22f, 0.42f, 0.78f));
		DrawCrewToken(font, Center(rooms[3].Rect) + new Vector2(28.0f, -8.0f), "M", new Color(0.24f, 0.56f, 0.42f));
		DrawCrewToken(font, Center(rooms[5].Rect) + new Vector2(-8.0f, -4.0f), "G", new Color(0.78f, 0.48f, 0.2f));
	}

	private void DrawCrewToken(Font font, Vector2 center, string label, Color fill)
	{
		DrawCircle(center + new Vector2(3.0f, 4.0f), 14.0f, new Color(0.03f, 0.02f, 0.015f, 0.46f));
		DrawCircle(center, 14.0f, fill);
		DrawCircle(center, 14.0f, new Color(0.96f, 0.88f, 0.66f), false, 2.0f, true);
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
		DrawRect(rect, Parchment, true);
		DrawRect(rect, RoomBorder, false, 2.0f);
		DrawString(font, rect.Position + new Vector2(16.0f, 36.0f), "Schematic Notes", HorizontalAlignment.Left, rect.Size.X - 32.0f, 20, Ink);

		var y = rect.Position.Y + 78.0f;
		DrawLegendItem(font, rect.Position.X + 18.0f, ref y, HelmAccent, "Helm / Rigging");
		DrawLegendItem(font, rect.Position.X + 18.0f, ref y, CannonAccent, "Broadside modules");
		DrawLegendItem(font, rect.Position.X + 18.0f, ref y, ThreadAccent, "Thread Chamber");
		DrawLegendItem(font, rect.Position.X + 18.0f, ref y, CargoAccent, "Cargo / support");
		DrawLegendItem(font, rect.Position.X + 18.0f, ref y, DoctorAccent, "Below-deck doctor");

		y += 16.0f;
		DrawLine(new Vector2(rect.Position.X + 22.0f, y), new Vector2(rect.Position.X + 78.0f, y), RouteColor, 3.0f, true);
		DrawString(font, new Vector2(rect.Position.X + 92.0f, y + 6.0f), "sample route", HorizontalAlignment.Left, rect.Size.X - 108.0f, 14, MutedInk);

		y += 44.0f;
		DrawCircle(new Vector2(rect.Position.X + 50.0f, y - 4.0f), 10.0f, new Color(0.22f, 0.42f, 0.78f));
		DrawString(font, new Vector2(rect.Position.X + 92.0f, y + 2.0f), "crew token", HorizontalAlignment.Left, rect.Size.X - 108.0f, 14, MutedInk);
	}

	private void DrawLegendItem(Font font, float x, ref float y, Color color, string text)
	{
		DrawRect(new Rect2(x, y - 13.0f, 20.0f, 10.0f), color, true);
		DrawString(font, new Vector2(x + 34.0f, y), text, HorizontalAlignment.Left, 200.0f, 14, MutedInk);
		y += 30.0f;
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

	private static Vector2 Center(Rect2 rect)
	{
		return rect.Position + (rect.Size * 0.5f);
	}

	private readonly record struct RoomBox(string Name, string Detail, Rect2 Rect, Color Accent);
}
