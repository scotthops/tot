using System.Collections.Generic;
using TidesOfTime.Crew;
using TidesOfTime.Data;

namespace TidesOfTime.Ships;

public class ShipState
{
	public string Name { get; }
	public int Hull { get; set; }
	public ShipGridState Grid { get; }
	public List<CrewState> Crew { get; } = new();
	public string? SelectedRoomId { get; private set; }

	public ShipState(string name, int hull, ShipGridState grid)
	{
		Name = name;
		Hull = hull;
		Grid = grid;
	}

	public static ShipState FromLayout(ShipLayoutDef layout, int hull = 100)
	{
		return new ShipState(layout.ShipName, hull, ShipStateFactory.CreateGridState(layout));
	}

	public ShipRoomState? GetSelectedRoom()
	{
		return GetRoomById(SelectedRoomId);
	}

	public ShipRoomState? GetRoomAt(int x, int y)
	{
		var tile = Grid.GetTile(x, y);
		if (tile == null || string.IsNullOrEmpty(tile.RoomId))
		{
			return null;
		}

		return GetRoomById(tile.RoomId);
	}

	public void SelectRoomAt(int x, int y)
	{
		var room = GetRoomAt(x, y);
		SelectedRoomId = room?.RoomId;
	}

	public void ClearSelection()
	{
		SelectedRoomId = null;
	}

	public IReadOnlyList<CrewState> GetCrewOnBoard()
	{
		return Crew;
	}

	public CrewState? GetCrewAtTile(int x, int y)
	{
		return Crew.Find(crew => crew.Position.TileX == x && crew.Position.TileY == y);
	}

	public bool IsTileOccupied(int x, int y)
	{
		return GetCrewAtTile(x, y) != null;
	}

	private ShipRoomState? GetRoomById(string? roomId)
	{
		if (string.IsNullOrEmpty(roomId))
		{
			return null;
		}

		return Grid.Rooms.Find(room => room.RoomId == roomId);
	}
}
