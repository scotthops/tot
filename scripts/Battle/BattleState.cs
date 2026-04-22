using System;
using System.Collections.Generic;
using TidesOfTime.Crew;
using TidesOfTime.Data;
using TidesOfTime.Ships;

namespace TidesOfTime.Battle;

public class BattleState
{
	private const string OffensiveSystemType = "Cannons";
	private const string HelmSystemType = "HelmRigging";
	private const int TargetSystemDamage = 40;
	private const int RepairAmount = 35;
	private const double HelmDodgeChance = 0.2;

	private static readonly BattleAvailableAction[] PlayerRoomActions =
	[
		new(BattleActionKind.RepairOrAssign, BattleActionIntent.ToDisplayLabel(BattleActionKind.RepairOrAssign)),
		new(BattleActionKind.InspectSystem, BattleActionIntent.ToDisplayLabel(BattleActionKind.InspectSystem))
	];

	private static readonly BattleAvailableAction[] EnemyRoomActions =
	[
		new(BattleActionKind.TargetSystem, BattleActionIntent.ToDisplayLabel(BattleActionKind.TargetSystem))
	];

	public ShipState PlayerShip { get; }
	public ShipState EnemyShip { get; }
	public BattleSelection? CurrentSelection { get; private set; }
	public BattleActionIntent? LastIssuedIntent { get; private set; }
	public BattleMovementFeedback? LastMovementFeedback { get; private set; }

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
			: BattleSelection.ForRoom(shipSource, ship, room);
		LastIssuedIntent = null;
		LastMovementFeedback = null;
	}

	public void SetCrewSelection(string shipSource, ShipState ship, CrewState? crew)
	{
		PlayerShip.ClearSelection();
		EnemyShip.ClearSelection();

		if (crew == null)
		{
			CurrentSelection = null;
			LastIssuedIntent = null;
			return;
		}

		var room = ship.GetRoomAt(crew.Position.TileX, crew.Position.TileY);
		CurrentSelection = BattleSelection.ForCrew(shipSource, ship, crew, room);
		LastIssuedIntent = null;
		LastMovementFeedback = null;
	}

	public void HandleTilePressed(string shipSource, ShipState ship, int tileX, int tileY)
	{
		if (TryHandleCrewMovement(shipSource, ship, tileX, tileY))
		{
			return;
		}

		ship.SelectRoomAt(tileX, tileY);
		SetSelection(shipSource, ship, ship.GetSelectedRoom());
	}

	public void ClearSelection()
	{
		PlayerShip.ClearSelection();
		EnemyShip.ClearSelection();
		CurrentSelection = null;
		LastIssuedIntent = null;
		LastMovementFeedback = null;
	}

	public BattleActionIntent? CreateActionIntent(BattleActionKind kind)
	{
		if (CurrentSelection == null || CurrentSelection.Kind != BattleSelectionKind.Room || CurrentSelection.Room == null)
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
		LastMovementFeedback = null;
	}

	public BattleActionResolution ExecuteAction(BattleActionKind kind)
	{
		LastMovementFeedback = null;

		return kind switch
		{
			BattleActionKind.TargetSystem => ExecuteTargetSystemAction(),
			BattleActionKind.RepairOrAssign => ExecuteRepairOrAssignAction(),
			BattleActionKind.InspectSystem => ExecuteInspectSystemAction(),
			BattleActionKind.BoardRoom => new BattleActionResolution(false, "Boarding is not part of this slice yet."),
			_ => new BattleActionResolution(false, "That action is unavailable.")
		};
	}

	public IReadOnlyList<BattleAvailableAction> GetAvailableActions()
	{
		if (CurrentSelection == null || CurrentSelection.Kind != BattleSelectionKind.Room || CurrentSelection.Room == null)
		{
			return [];
		}

		if (CurrentSelection.Room.Disabled && CurrentSelection.ShipSource == "Enemy")
		{
			return [];
		}

		return CurrentSelection.ShipSource == "Enemy"
			? EnemyRoomActions
			: PlayerRoomActions;
	}

	private BattleActionResolution ExecuteTargetSystemAction()
	{
		if (CurrentSelection == null || CurrentSelection.Kind != BattleSelectionKind.Room || CurrentSelection.Room == null)
		{
			SetLastIssuedIntent(null);
			return new BattleActionResolution(false, "Select an enemy room to target.");
		}

		if (CurrentSelection.Ship != EnemyShip)
		{
			SetLastIssuedIntent(null);
			return new BattleActionResolution(false, "Target System requires selecting a room on the enemy ship.");
		}

		var targetRoom = CurrentSelection.Room;
		if (targetRoom.Disabled)
		{
			SetLastIssuedIntent(null);
			return new BattleActionResolution(false, $"{targetRoom.DisplayName} is already disabled.");
		}

		var offensiveRoom = PlayerShip.GetRoomBySystemType(OffensiveSystemType);
		if (offensiveRoom == null)
		{
			SetLastIssuedIntent(null);
			return new BattleActionResolution(false, "Your ship has no cannons room to fire from.");
		}

		if (!PlayerShip.IsRoomOperational(offensiveRoom))
		{
			SetLastIssuedIntent(null);
			return new BattleActionResolution(false, $"{offensiveRoom.DisplayName} is disabled and cannot fire.");
		}

		if (!PlayerShip.IsRoomManned(offensiveRoom, CrewAllegiance.Player))
		{
			SetLastIssuedIntent(null);
			return new BattleActionResolution(false, $"{offensiveRoom.DisplayName} must be manned before it can fire.");
		}

		var actionIntent = CreateActionIntent(BattleActionKind.TargetSystem);
		if (actionIntent == null)
		{
			SetLastIssuedIntent(null);
			return new BattleActionResolution(false, "Select an enemy room to target.");
		}

		SetLastIssuedIntent(actionIntent);

		if (TryEvadeTargetSystemAttack(EnemyShip, CrewAllegiance.Enemy, targetRoom, out var dodgeStatus))
		{
			var retaliationAfterDodge = CreateEnemyRetaliationResult();
			return BuildResolutionWithRetaliation(true, dodgeStatus, retaliationAfterDodge);
		}

		var integrityBeforeHit = targetRoom.Integrity;
		targetRoom.ApplyDamage(TargetSystemDamage);
		var damageApplied = integrityBeforeHit - targetRoom.Integrity;
		var damageSummary = $"{offensiveRoom.DisplayName} hit {targetRoom.DisplayName} for {damageApplied} system damage.";

		if (targetRoom.Disabled)
		{
			var retaliationResult = CreateEnemyRetaliationResult();
			return BuildResolutionWithRetaliation(
				true,
				$"{damageSummary} {targetRoom.DisplayName} is now disabled and no longer counts as an operational system.",
				retaliationResult);
		}

		var successfulAttackResult = CreateEnemyRetaliationResult();
		return BuildResolutionWithRetaliation(
			true,
			$"{damageSummary} Integrity is now {targetRoom.Integrity}/{ShipRoomState.MaxIntegrity}.",
			successfulAttackResult);
	}

	private BattleActionResolution ExecuteRepairOrAssignAction()
	{
		if (CurrentSelection == null || CurrentSelection.Kind != BattleSelectionKind.Room || CurrentSelection.Room == null)
		{
			SetLastIssuedIntent(null);
			return new BattleActionResolution(false, "Select one of your rooms first.");
		}

		var actionIntent = CreateActionIntent(BattleActionKind.RepairOrAssign);
		SetLastIssuedIntent(actionIntent);

		var room = CurrentSelection.Room;
		if (CurrentSelection.Ship != PlayerShip)
		{
			return new BattleActionResolution(false, "Repair / Assign only applies to your ship.");
		}

		if (PlayerShip.GetCrewInRoom(room, CrewAllegiance.Player).Count == 0)
		{
			return new BattleActionResolution(
				false,
				$"{room.DisplayName} needs a crew member present before repairs can begin.");
		}

		if (room.Integrity >= ShipRoomState.MaxIntegrity)
		{
			return new BattleActionResolution(true, $"{room.DisplayName} is fully repaired and operational.");
		}

		var wasDisabled = room.Disabled;
		var integrityBeforeRepair = room.Integrity;
		room.ApplyRepair(RepairAmount);
		var amountRepaired = room.Integrity - integrityBeforeRepair;
		var repairSummary = $"{room.DisplayName} repaired for {amountRepaired}. Integrity is now {room.Integrity}/{ShipRoomState.MaxIntegrity}.";

		if (wasDisabled && room.IsOperational)
		{
			return new BattleActionResolution(true, $"{repairSummary} {room.DisplayName} is back online.");
		}

		if (!room.IsDamaged)
		{
			return new BattleActionResolution(true, $"{repairSummary} {room.DisplayName} is fully restored.");
		}

		return new BattleActionResolution(true, repairSummary);
	}

	private BattleActionResolution ExecuteInspectSystemAction()
	{
		if (CurrentSelection == null || CurrentSelection.Kind != BattleSelectionKind.Room || CurrentSelection.Room == null)
		{
			SetLastIssuedIntent(null);
			return new BattleActionResolution(false, "Select a room to inspect.");
		}

		var actionIntent = CreateActionIntent(BattleActionKind.InspectSystem);
		SetLastIssuedIntent(actionIntent);

		var room = CurrentSelection.Room;
		var statusText = room.Disabled
			? "Disabled"
			: room.IsDamaged
				? "Damaged"
				: "Operational";

		return new BattleActionResolution(
			true,
			$"{room.DisplayName}: {statusText}, integrity {room.Integrity}/{ShipRoomState.MaxIntegrity}.");
	}

	private BattleActionResolution CreateEnemyRetaliationResult()
	{
		var offensiveRoom = EnemyShip.GetRoomBySystemType(OffensiveSystemType);
		if (offensiveRoom == null)
		{
			return new BattleActionResolution(false, "Enemy has no cannons room to retaliate from.");
		}

		if (!EnemyShip.IsRoomOperational(offensiveRoom))
		{
			return new BattleActionResolution(false, $"Enemy {offensiveRoom.DisplayName} is offline and cannot retaliate.");
		}

		if (!EnemyShip.IsRoomManned(offensiveRoom, CrewAllegiance.Enemy))
		{
			return new BattleActionResolution(false, $"Enemy {offensiveRoom.DisplayName} is unmanned and cannot retaliate.");
		}

		var targetRoom = SelectEnemyRetaliationTargetRoom();
		if (targetRoom == null)
		{
			return new BattleActionResolution(false, "Enemy finds no operational player system to target.");
		}

		if (TryEvadeTargetSystemAttack(PlayerShip, CrewAllegiance.Player, targetRoom, out var dodgeStatus))
		{
			return new BattleActionResolution(true, dodgeStatus);
		}

		var integrityBeforeHit = targetRoom.Integrity;
		targetRoom.ApplyDamage(TargetSystemDamage);
		var damageApplied = integrityBeforeHit - targetRoom.Integrity;
		var damageSummary = $"Enemy {offensiveRoom.DisplayName} hits your {targetRoom.DisplayName} for {damageApplied} system damage.";

		if (targetRoom.Disabled)
		{
			return new BattleActionResolution(
				true,
				$"{damageSummary} {targetRoom.DisplayName} is now disabled and offline.");
		}

		return new BattleActionResolution(
			true,
			$"{damageSummary} Integrity is now {targetRoom.Integrity}/{ShipRoomState.MaxIntegrity}.");
	}

	private bool TryEvadeTargetSystemAttack(
		ShipState defendingShip,
		CrewAllegiance defendingAllegiance,
		ShipRoomState targetRoom,
		out string statusText)
	{
		statusText = string.Empty;

		var helmRoom = defendingShip.GetRoomBySystemType(HelmSystemType);
		if (!defendingShip.IsRoomManned(helmRoom, defendingAllegiance))
		{
			return false;
		}

		if (Random.Shared.NextDouble() >= HelmDodgeChance)
		{
			return false;
		}

		statusText = $"{defendingShip.Name}'s {helmRoom!.DisplayName} evades the shot aimed at {targetRoom.DisplayName}.";
		return true;
	}

	private ShipRoomState? SelectEnemyRetaliationTargetRoom()
	{
		var playerCannons = PlayerShip.GetRoomBySystemType(OffensiveSystemType);
		if (PlayerShip.IsRoomOperational(playerCannons))
		{
			return playerCannons;
		}

		foreach (var room in PlayerShip.Grid.Rooms)
		{
			if (PlayerShip.IsRoomOperational(room))
			{
				return room;
			}
		}

		return null;
	}

	private static BattleActionResolution BuildResolutionWithRetaliation(
		bool playerActionSucceeded,
		string playerActionStatus,
		BattleActionResolution retaliationResult)
	{
		return new BattleActionResolution(
			playerActionSucceeded,
			$"{playerActionStatus}\n{retaliationResult.StatusText}");
	}

	private bool TryHandleCrewMovement(string shipSource, ShipState ship, int tileX, int tileY)
	{
		if (CurrentSelection?.Kind != BattleSelectionKind.Crew || CurrentSelection.Crew == null)
		{
			return false;
		}

		var selectedCrew = CurrentSelection.Crew;
		if (selectedCrew.Allegiance != CrewAllegiance.Player)
		{
			return false;
		}

		if (CurrentSelection.Ship != ship)
		{
			return false;
		}

		var moveValidationResult = ShipReachability.EvaluateMove(ship, selectedCrew, tileX, tileY);
		if (moveValidationResult != ShipMoveValidationResult.Reachable)
		{
			return false;
		}

		if (!ship.TryMoveCrewTo(selectedCrew, tileX, tileY))
		{
			return false;
		}

		SetCrewSelection(shipSource, ship, selectedCrew);
		LastMovementFeedback = new BattleMovementFeedback(
			BattleMovementFeedbackKind.Succeeded,
			selectedCrew.DisplayName,
			tileX,
			tileY);
		return true;
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
