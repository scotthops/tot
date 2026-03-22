using System;
using System.Collections.Generic;
using Godot;

namespace TidesOfTime.Ships;

public static class ShipLayoutTopologyValidator
{
	private static readonly Vector2I[] OrthogonalDirections =
	[
		Vector2I.Left,
		Vector2I.Right,
		Vector2I.Up,
		Vector2I.Down
	];

	public static void ValidateOrThrow(string layoutName, ShipGridState grid)
	{
		var traversableTiles = new HashSet<Vector2I>();

		foreach (var tile in grid.Tiles)
		{
			if (tile.Walkable)
			{
				traversableTiles.Add(new Vector2I(tile.X, tile.Y));
			}
		}

		if (traversableTiles.Count == 0)
		{
			throw new InvalidOperationException(
				$"Ship layout '{layoutName}' has no traversable tiles. Add at least one room tile to define the ship interior.");
		}

		var start = default(Vector2I);
		foreach (var tile in traversableTiles)
		{
			start = tile;
			break;
		}

		var reachableTiles = FindReachableTiles(start, traversableTiles);
		if (reachableTiles.Count == traversableTiles.Count)
		{
			return;
		}

		var unreachableTile = default(Vector2I);
		foreach (var tile in traversableTiles)
		{
			if (!reachableTiles.Contains(tile))
			{
				unreachableTile = tile;
				break;
			}
		}

		var unreachableTileState = grid.GetTile(unreachableTile.X, unreachableTile.Y);
		var unreachableRoom = unreachableTileState == null || string.IsNullOrEmpty(unreachableTileState.RoomId)
			? null
			: grid.Rooms.Find(room => room.RoomId == unreachableTileState.RoomId);
		var roomContext = unreachableRoom == null
			? ""
			: $" in room '{unreachableRoom.DisplayName}' ({unreachableRoom.RoomId})";

		throw new InvalidOperationException(
			$"Ship layout '{layoutName}' is disconnected. Expected 1 orthogonally connected traversable component, " +
			$"but only {reachableTiles.Count} of {traversableTiles.Count} traversable tiles are reachable from {start}. " +
			$"Example unreachable tile: {unreachableTile}{roomContext}.");
	}

	private static HashSet<Vector2I> FindReachableTiles(Vector2I start, HashSet<Vector2I> traversableTiles)
	{
		var visited = new HashSet<Vector2I> { start };
		var frontier = new Queue<Vector2I>();
		frontier.Enqueue(start);

		while (frontier.Count > 0)
		{
			var current = frontier.Dequeue();

			foreach (var direction in OrthogonalDirections)
			{
				var neighbor = current + direction;
				if (!traversableTiles.Contains(neighbor) || !visited.Add(neighbor))
				{
					continue;
				}

				frontier.Enqueue(neighbor);
			}
		}

		return visited;
	}
}
