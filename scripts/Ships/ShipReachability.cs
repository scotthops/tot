using System.Collections.Generic;
using Godot;
using TidesOfTime.Crew;

namespace TidesOfTime.Ships;

public static class ShipReachability
{
	private static readonly Vector2I[] OrthogonalDirections =
	[
		Vector2I.Left,
		Vector2I.Right,
		Vector2I.Up,
		Vector2I.Down
	];

	public static ShipMoveValidationResult EvaluateMove(ShipState ship, CrewState crew, int destinationX, int destinationY)
	{
		var destinationTile = ship.Grid.GetTile(destinationX, destinationY);
		if (destinationTile == null || !destinationTile.Walkable)
		{
			return ShipMoveValidationResult.InvalidDestination;
		}

		if (ship.IsTileOccupied(destinationX, destinationY))
		{
			return ShipMoveValidationResult.TileOccupied;
		}

		var start = new Vector2I(crew.Position.TileX, crew.Position.TileY);
		var destination = new Vector2I(destinationX, destinationY);
		var visited = new HashSet<Vector2I> { start };
		var frontier = new Queue<Vector2I>();
		frontier.Enqueue(start);

		while (frontier.Count > 0)
		{
			var current = frontier.Dequeue();
			if (current == destination)
			{
				return ShipMoveValidationResult.Reachable;
			}

			foreach (var direction in OrthogonalDirections)
			{
				var neighbor = current + direction;
				if (!visited.Add(neighbor) || !IsTraversableForCrew(ship, crew, neighbor))
				{
					continue;
				}

				frontier.Enqueue(neighbor);
			}
		}

		return ShipMoveValidationResult.Unreachable;
	}

	private static bool IsTraversableForCrew(ShipState ship, CrewState crew, Vector2I tilePosition)
	{
		if (tilePosition.X == crew.Position.TileX && tilePosition.Y == crew.Position.TileY)
		{
			return true;
		}

		var tile = ship.Grid.GetTile(tilePosition.X, tilePosition.Y);
		if (tile == null || !tile.Walkable)
		{
			return false;
		}

		return !ship.IsTileOccupied(tilePosition.X, tilePosition.Y);
	}
}
