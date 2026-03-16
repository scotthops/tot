using Godot;
using TidesOfTime.Data;

namespace TidesOfTime.Ships;

public static class ShipStateFactory
{
	public static ShipGridState CreateGridState(ShipLayoutDef layout)
	{
		var gridState = new ShipGridState(layout.Width, layout.Height);

		for (int y = 0; y < layout.Height; y++)
		{
			for (int x = 0; x < layout.Width; x++)
			{
				gridState.Tiles.Add(new ShipTileState(x, y)
				{
					Walkable = false,
					RoomId = ""
				});
			}
		}

		foreach (var roomDef in layout.Rooms)
		{
			var roomState = new ShipRoomState
			{
				RoomId = roomDef.RoomId,
				DisplayName = roomDef.DisplayName,
				SystemType = roomDef.SystemType
			};

			foreach (var tilePos in roomDef.Tiles)
			{
				roomState.Tiles.Add(tilePos);

				var tile = gridState.GetTile(tilePos.X, tilePos.Y);
				if (tile != null)
				{
					tile.Walkable = true;
					tile.RoomId = roomDef.RoomId;
				}
			}

			gridState.Rooms.Add(roomState);
		}

		return gridState;
	}
}
