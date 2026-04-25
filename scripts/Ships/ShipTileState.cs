namespace TidesOfTime.Ships;

public class ShipTileState
{
	public int X { get; set; }
	public int Y { get; set; }
	public bool Walkable { get; set; } = true;
	public string RoomId { get; set; } = "";
	public ShipTileKind TileKind { get; set; } = ShipTileKind.OpenDeck;
	public string? OccupiedCrewId { get; set; }

	public ShipTileState(int x, int y)
	{
		X = x;
		Y = y;
	}
}

public enum ShipTileKind
{
	Outside,
	OpenDeck,
	Room,
	Obstacle
}
