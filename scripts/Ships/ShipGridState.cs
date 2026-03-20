using System.Collections.Generic;

namespace TidesOfTime.Ships;

public class ShipGridState
{
	public int Width { get; }
	public int Height { get; }

	public List<ShipTileState> Tiles { get; } = new();
	public List<ShipRoomState> Rooms { get; } = new();

	private readonly ShipTileState?[,] _tilesByPosition;

	public ShipGridState(int width, int height)
	{
		Width = width;
		Height = height;
		_tilesByPosition = new ShipTileState[width, height];
	}

	public void AddTile(ShipTileState tile)
	{
		Tiles.Add(tile);
		_tilesByPosition[tile.X, tile.Y] = tile;
	}

	public ShipTileState? GetTile(int x, int y)
	{
		if (x < 0 || x >= Width || y < 0 || y >= Height)
		{
			return null;
		}

		return _tilesByPosition[x, y];
	}
}
