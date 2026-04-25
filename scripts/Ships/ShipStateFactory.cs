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
				gridState.AddTile(new ShipTileState(x, y)
				{
					Walkable = false,
					RoomId = "",
					TileKind = ShipTileKind.Outside
				});
			}
		}

		foreach (var tilePos in layout.OpenDeckTiles)
		{
			var tile = gridState.GetTile(tilePos.X, tilePos.Y);
			if (tile == null)
			{
				GD.PushWarning($"Ship layout '{layout.ShipName}' has out-of-bounds open deck tile {tilePos}.");
				continue;
			}

			tile.Walkable = true;
			tile.RoomId = "";
			tile.TileKind = ShipTileKind.OpenDeck;
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
				if (tile == null)
				{
					GD.PushWarning($"Ship layout '{layout.ShipName}' has out-of-bounds tile {tilePos} in room '{roomDef.RoomId}'.");
					continue;
				}

				if (tile.Walkable && !string.IsNullOrEmpty(tile.RoomId))
				{
					GD.PushWarning($"Ship layout '{layout.ShipName}' assigns tile {tilePos} to multiple rooms.");
				}

				tile.Walkable = true;
				tile.RoomId = roomDef.RoomId;
				tile.TileKind = ShipTileKind.Room;
			}

			gridState.Rooms.Add(roomState);
		}

		foreach (var tilePos in layout.ObstacleTiles)
		{
			var tile = gridState.GetTile(tilePos.X, tilePos.Y);
			if (tile == null)
			{
				GD.PushWarning($"Ship layout '{layout.ShipName}' has out-of-bounds obstacle tile {tilePos}.");
				continue;
			}

			if (!string.IsNullOrEmpty(tile.RoomId))
			{
				GD.PushWarning($"Ship layout '{layout.ShipName}' marks room tile {tilePos} as an obstacle. The obstacle will make it non-walkable.");
			}

			tile.Walkable = false;
			tile.RoomId = "";
			tile.TileKind = ShipTileKind.Obstacle;
		}

		foreach (var moduleBayDef in layout.ModuleBays)
		{
			var moduleBayState = new ShipModuleBayState
			{
				BayId = moduleBayDef.BayId,
				DisplayName = moduleBayDef.DisplayName,
				DefaultRole = moduleBayDef.DefaultRole
			};

			foreach (var allowedRole in moduleBayDef.AllowedRoles)
			{
				moduleBayState.AllowedRoles.Add(allowedRole);
			}

			foreach (var tilePos in moduleBayDef.Tiles)
			{
				if (gridState.GetTile(tilePos.X, tilePos.Y) == null)
				{
					GD.PushWarning($"Ship layout '{layout.ShipName}' has out-of-bounds tile {tilePos} in module bay '{moduleBayDef.BayId}'.");
					continue;
				}

				moduleBayState.Tiles.Add(tilePos);
			}

			gridState.ModuleBays.Add(moduleBayState);
		}

		ShipLayoutTopologyValidator.ValidateOrThrow(layout.ShipName, gridState);

		return gridState;
	}
}
