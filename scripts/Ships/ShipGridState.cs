using System.Collections.Generic;

namespace TidesOfTime.Ships;

public class ShipGridState
{
	public int Width { get; }
	public int Height { get; }

	public List<ShipTileState> Tiles { get; } = new();
	public List<ShipRoomState> Rooms { get; } = new();

	public ShipGridState(int width, int height)
	{
		Width = width;
		Height = height;
	}

	public ShipTileState? GetTile(int x, int y)
	{
		return Tiles.Find(t => t.X == x && t.Y == y);
	}
}
