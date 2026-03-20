using System.Collections.Generic;
using TidesOfTime.Crew;
using TidesOfTime.Data;
using TidesOfTime.Ships;

namespace TidesOfTime.Battle;

public class BattleState
{
	private static readonly BattleAvailableAction[] PlayerRoomActions =
	[
		new(BattleActionKind.RepairOrAssign, BattleActionIntent.ToDisplayLabel(BattleActionKind.RepairOrAssign)),
		new(BattleActionKind.InspectSystem, BattleActionIntent.ToDisplayLabel(BattleActionKind.InspectSystem))
	];

	private static readonly BattleAvailableAction[] EnemyRoomActions =
	[
		new(BattleActionKind.TargetSystem, BattleActionIntent.ToDisplayLabel(BattleActionKind.TargetSystem)),
		new(BattleActionKind.BoardRoom, BattleActionIntent.ToDisplayLabel(BattleActionKind.BoardRoom))
	];

	public ShipState PlayerShip { get; }
	public ShipState EnemyShip { get; }
	public BattleSelection? CurrentSelection { get; private set; }
	public BattleActionIntent? LastIssuedIntent { get; private set; }

	public BattleState(ShipState playerShip, ShipState enemyShip)
	{
		PlayerShip = playerShip;
		EnemyShip = enemyShip;
	}

	public static BattleState Create(ShipLayoutDef playerLayout, ShipLayoutDef enemyLayout)
	{
		var playerShip = ShipState.FromLayout(playerLayout);
		var enemyShip = ShipState.FromLayout(enemyLayout);

		SeedPrototypeCrew(playerShip, ShipSide.Player, CrewAllegiance.Player);
		SeedPrototypeCrew(enemyShip, ShipSide.Enemy, CrewAllegiance.Enemy);

		return new BattleState(playerShip, enemyShip);
	}

	public void SetSelection(string shipSource, ShipState ship, ShipRoomState? room)
	{
		if (ship == PlayerShip)
		{
			EnemyShip.ClearSelection();
		}
		else if (ship == EnemyShip)
		{
			PlayerShip.ClearSelection();
		}

		CurrentSelection = room == null
			? null
			: new BattleSelection(shipSource, ship, room);
		LastIssuedIntent = null;
	}

	public BattleActionIntent? CreateActionIntent(BattleActionKind kind)
	{
		if (CurrentSelection == null)
		{
			return null;
		}

		return new BattleActionIntent(
			kind,
			CurrentSelection.ShipSource,
			CurrentSelection.Ship.Name,
			CurrentSelection.Room.RoomId,
			CurrentSelection.Room.DisplayName,
			CurrentSelection.Room.SystemType);
	}

	public void SetLastIssuedIntent(BattleActionIntent? actionIntent)
	{
		LastIssuedIntent = actionIntent;
	}

	public IReadOnlyList<BattleAvailableAction> GetAvailableActions()
	{
		if (CurrentSelection == null)
		{
			return [];
		}

		return CurrentSelection.ShipSource == "Enemy"
			? EnemyRoomActions
			: PlayerRoomActions;
	}

	private static void SeedPrototypeCrew(ShipState ship, ShipSide currentShipSide, CrewAllegiance allegiance)
	{
		var spawnTiles = GetPrototypeCrewSpawnTiles(ship);
		var crewConfigs = new (string Id, string Name, string ShortLabel, string CrewClass)[]
		{
			("captain", "Captain Mara", "C", "Captain"),
			("gunner", "Gunner Flint", "G", "Gunner"),
			("surgeon", "Surgeon Vale", "S", "Surgeon")
		};

		for (var i = 0; i < spawnTiles.Count && i < crewConfigs.Length; i++)
		{
			var crewConfig = crewConfigs[i];
			var tile = spawnTiles[i];
			ship.Crew.Add(new CrewState(
				$"{ship.Name.ToLowerInvariant().Replace(" ", "-")}-{crewConfig.Id}",
				crewConfig.Name,
				crewConfig.ShortLabel,
				crewConfig.CrewClass,
				allegiance,
				new CrewPosition(currentShipSide, tile.X, tile.Y)));
		}
	}

	private static List<(int X, int Y)> GetPrototypeCrewSpawnTiles(ShipState ship)
	{
		var spawnTiles = new List<(int X, int Y)>();

		foreach (var room in ship.Grid.Rooms)
		{
			if (room.Tiles.Count == 0)
			{
				continue;
			}

			var tile = room.Tiles[0];
			spawnTiles.Add((tile.X, tile.Y));
			if (spawnTiles.Count >= 3)
			{
				return spawnTiles;
			}
		}

		foreach (var tile in ship.Grid.Tiles)
		{
			if (!tile.Walkable)
			{
				continue;
			}

			var tilePosition = (tile.X, tile.Y);
			if (spawnTiles.Contains(tilePosition))
			{
				continue;
			}

			spawnTiles.Add(tilePosition);
			if (spawnTiles.Count >= 3)
			{
				break;
			}
		}

		return spawnTiles;
	}
}
