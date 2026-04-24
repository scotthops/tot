using Godot;

namespace TidesOfTime.UI;

public partial class ShipHullBackdrop : Control
{
	private int _gridWidth = 1;
	private int _gridHeight = 1;
	private Color _tint = new(0.32f, 0.42f, 0.55f, 0.72f);
	private bool _bowFacesRight = true;

	public void SetBoardSize(int gridWidth, int gridHeight)
	{
		_gridWidth = Mathf.Max(1, gridWidth);
		_gridHeight = Mathf.Max(1, gridHeight);
		QueueRedraw();
	}

	public void SetVisualStyle(Color tint, bool bowFacesRight)
	{
		_tint = new Color(tint.R, tint.G, tint.B, 0.72f);
		_bowFacesRight = bowFacesRight;
		QueueRedraw();
	}

	public override void _Draw()
	{
		var layout = CalculateBoardLayout();
		var boardRect = new Rect2(layout.Origin, new Vector2(_gridWidth * layout.TileSize, _gridHeight * layout.TileSize));
		var padding = Mathf.Max(12.0f, layout.TileSize * 0.34f);
		var bowLength = Mathf.Max(24.0f, layout.TileSize * 0.74f);
		var sternLength = Mathf.Max(14.0f, layout.TileSize * 0.38f);
		var top = boardRect.Position.Y - padding;
		var bottom = boardRect.End.Y + padding;
		var middleY = (top + bottom) * 0.5f;
		var left = boardRect.Position.X - sternLength;
		var right = boardRect.End.X + bowLength;
		var deckInset = Mathf.Max(8.0f, layout.TileSize * 0.18f);

		var hullPoints = _bowFacesRight
			? new[]
			{
				new Vector2(left + sternLength, top),
				new Vector2(right - bowLength, top),
				new Vector2(right, middleY),
				new Vector2(right - bowLength, bottom),
				new Vector2(left + sternLength, bottom),
				new Vector2(left, middleY)
			}
			: new[]
			{
				new Vector2(left + bowLength, top),
				new Vector2(right - sternLength, top),
				new Vector2(right, middleY),
				new Vector2(right - sternLength, bottom),
				new Vector2(left + bowLength, bottom),
				new Vector2(left, middleY)
			};

		var hullFill = _tint.Darkened(0.34f);
		var deckFill = _tint.Lightened(0.08f);
		var border = _tint.Lightened(0.44f);
		var seam = _tint.Darkened(0.1f);

		DrawColoredPolygon(hullPoints, hullFill);
		DrawPolyline(ClosePolygon(hullPoints), border, 2.0f, true);

		var deckRect = boardRect.Grow(deckInset);
		deckRect.Position = new Vector2(deckRect.Position.X, deckRect.Position.Y + (deckInset * 0.35f));
		deckRect.Size = new Vector2(deckRect.Size.X, deckRect.Size.Y - (deckInset * 0.7f));
		DrawRect(deckRect, deckFill.Darkened(0.12f), true);
		DrawRect(deckRect, seam, false, 1.0f);
		DrawLine(
			new Vector2(boardRect.Position.X, middleY),
			new Vector2(boardRect.End.X, middleY),
			seam.Lightened(0.2f),
			1.0f,
			true);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			QueueRedraw();
		}
	}

	private BoardLayout CalculateBoardLayout()
	{
		var tileSize = Mathf.Floor(Mathf.Min(
			Size.X / Mathf.Max(_gridWidth, 1),
			Size.Y / Mathf.Max(_gridHeight, 1)));

		tileSize = Mathf.Max(tileSize, 24.0f);

		var boardSize = new Vector2(tileSize * _gridWidth, tileSize * _gridHeight);
		var origin = (Size - boardSize) * 0.5f;

		return new BoardLayout(origin, tileSize);
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

	private readonly record struct BoardLayout(Vector2 Origin, float TileSize);
}
