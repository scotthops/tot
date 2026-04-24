using Godot;

namespace TidesOfTime.UI;

public partial class ShipInteriorCutawayBackdrop : Control
{
	private int _gridWidth = 1;
	private int _gridHeight = 1;
	private int _minTileX;
	private int _minTileY;
	private int _maxTileX;
	private int _maxTileY;
	private bool _hasInterior;

	public Color FillColor { get; set; } = new(0.05f, 0.07f, 0.08f, 0.42f);
	public Color BorderColor { get; set; } = new(0.95f, 0.82f, 0.56f, 0.58f);
	public Color InnerLineColor { get; set; } = new(0.98f, 0.94f, 0.82f, 0.18f);
	public float PaddingTiles { get; set; } = 0.16f;

	public void SetInteriorBounds(
		int gridWidth,
		int gridHeight,
		int minTileX,
		int minTileY,
		int maxTileX,
		int maxTileY)
	{
		_gridWidth = Mathf.Max(1, gridWidth);
		_gridHeight = Mathf.Max(1, gridHeight);
		_minTileX = minTileX;
		_minTileY = minTileY;
		_maxTileX = maxTileX;
		_maxTileY = maxTileY;
		_hasInterior = true;
		QueueRedraw();
	}

	public void ClearInterior()
	{
		_hasInterior = false;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (!_hasInterior)
		{
			return;
		}

		var layout = CalculateBoardLayout();
		var topLeft = layout.Origin + new Vector2(_minTileX * layout.TileSize, _minTileY * layout.TileSize);
		var size = new Vector2(
			(_maxTileX - _minTileX + 1) * layout.TileSize,
			(_maxTileY - _minTileY + 1) * layout.TileSize);
		var rect = new Rect2(topLeft, size).Grow(layout.TileSize * PaddingTiles);

		DrawRect(rect, FillColor, true);
		DrawRect(rect, BorderColor, false, 2.0f);

		var insetRect = rect.Grow(-Mathf.Max(3.0f, layout.TileSize * 0.08f));
		DrawRect(insetRect, InnerLineColor, false, 1.0f);
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

	private readonly record struct BoardLayout(Vector2 Origin, float TileSize);
}
