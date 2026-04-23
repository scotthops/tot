using System;
using System.Collections.Generic;
using TidesOfTime.Crew;
using TidesOfTime.Data;
using TidesOfTime.Ships;

namespace TidesOfTime.Battle;

public class BattleState
{
	private const int StartingHull = 100;
	private const string OffensiveSystemType = "Cannons";
	private const string HelmSystemType = "HelmRigging";
	private const double CannonChargeDurationSeconds = 7.0;
	private const double TacticalPauseDurationSeconds = 3.0;
	private const int TargetSystemDamage = 40;
	private const int TargetHullDamage = 8;
	private const int RepairAmount = 35;
	private const int OpeningCrisisDamage = 35;
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
	public bool IsBattleOver { get; private set; }
	public string? BattleOverStatusText { get; private set; }
	public string? OpeningStatusText { get; private set; }

	private readonly CannonBatteryState _playerCannons = new();
	private readonly CannonBatteryState _enemyCannons = new();
	private BattleTimeControlMode _timeControlMode = BattleTimeControlMode.Normal;
	private double _pauseSecondsRemaining = TacticalPauseDurationSeconds;

	public BattleState(ShipState playerShip, ShipState enemyShip)
	{
		PlayerShip = playerShip;
		EnemyShip = enemyShip;
	}

	public static BattleState Create(ShipLayoutDef playerLayout, ShipLayoutDef enemyLayout)
	{
		var playerShip = ShipState.FromLayout(playerLayout, StartingHull);
		var enemyShip = ShipState.FromLayout(enemyLayout, StartingHull);

		SeedPrototypeCrew(playerShip, ShipSide.Player, CrewAllegiance.Player);
		SeedPrototypeCrew(enemyShip, ShipSide.Enemy, CrewAllegiance.Enemy);

		var battleState = new BattleState(playerShip, enemyShip);
		battleState.OpeningStatusText = ApplyOpeningCrisis(playerShip, enemyShip);
		return battleState;
	}

	public void SetSelection(string shipSource, ShipState ship, ShipRoomState? room)
	{
		if (IsBattleOver)
		{
			return;
		}

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
		if (IsBattleOver)
		{
			return;
		}

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
		if (IsBattleOver)
		{
			return;
		}

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

		if (IsBattleOver)
		{
			return new BattleActionResolution(false, BattleOverStatusText ?? "Battle is over.");
		}

		return kind switch
		{
			BattleActionKind.TargetSystem => ExecuteTargetSystemAction(),
			BattleActionKind.RepairOrAssign => ExecuteRepairOrAssignAction(),
			BattleActionKind.InspectSystem => ExecuteInspectSystemAction(),
			BattleActionKind.BoardRoom => new BattleActionResolution(false, "Boarding is not part of this slice yet."),
			_ => new BattleActionResolution(false, "That action is unavailable.")
		};
	}

	public BattleActionResolution? Update(double deltaSeconds)
	{
		if (IsBattleOver || deltaSeconds <= 0.0)
		{
			return null;
		}

		var statusLines = new List<string>();
		var simulationDeltaSeconds = AdvanceTimeControl(deltaSeconds, statusLines);
		if (simulationDeltaSeconds <= 0.0)
		{
			return statusLines.Count == 0
				? null
				: new BattleActionResolution(true, string.Join("\n", statusLines));
		}

		AdvancePlayerCannons(simulationDeltaSeconds, statusLines);
		if (IsBattleOver)
		{
			return statusLines.Count == 0
				? null
				: new BattleActionResolution(true, string.Join("\n", statusLines));
		}

		AdvanceEnemyCannons(simulationDeltaSeconds, statusLines);
		return statusLines.Count == 0
			? null
			: new BattleActionResolution(true, string.Join("\n", statusLines));
	}

	public BattleActionResolution ToggleTacticalPause()
	{
		if (IsBattleOver)
		{
			return new BattleActionResolution(false, BattleOverStatusText ?? "Battle is over.");
		}

		if (_timeControlMode == BattleTimeControlMode.Paused)
		{
			_timeControlMode = BattleTimeControlMode.Normal;
			return new BattleActionResolution(
				true,
				$"Tactical pause released with {_pauseSecondsRemaining:0.0}s remaining.");
		}

		if (_pauseSecondsRemaining <= 0.0)
		{
			return new BattleActionResolution(false, "Tactical pause is spent for this battle.");
		}

		_timeControlMode = BattleTimeControlMode.Paused;
		return new BattleActionResolution(
			true,
			$"Tactical pause engaged for up to {_pauseSecondsRemaining:0.0}s.");
	}

	public BattleTimeControlStatus GetTimeControlStatus()
	{
		if (IsBattleOver)
		{
			return new BattleTimeControlStatus("Battle Over", 0.0, "Time: Battle Over");
		}

		if (_timeControlMode == BattleTimeControlMode.Paused)
		{
			return new BattleTimeControlStatus(
				"Paused",
				_pauseSecondsRemaining,
				$"Time: Paused ({_pauseSecondsRemaining:0.0}s left)");
		}

		var detailText = _pauseSecondsRemaining > 0.0
			? $"Time: Normal ({_pauseSecondsRemaining:0.0}s pause ready)"
			: "Time: Normal (pause spent)";

		return new BattleTimeControlStatus("Normal", _pauseSecondsRemaining, detailText);
	}

	public PlayerCannonStatus GetPlayerCannonStatus()
	{
		var cannonsRoom = PlayerShip.GetRoomBySystemType(OffensiveSystemType);
		var targetRoom = FindRoomById(EnemyShip, _playerCannons.TargetRoomId);
		var targetLabel = targetRoom?.DisplayName ?? "None";

		if (IsBattleOver)
		{
			return new PlayerCannonStatus(
				targetLabel,
				"Battle Over",
				BattleOverStatusText ?? "Battle is over.");
		}

		if (cannonsRoom == null)
		{
			return new PlayerCannonStatus(
				targetLabel,
				"Unavailable",
				"No cannons system is present.");
		}

		if (targetRoom == null)
		{
			return new PlayerCannonStatus(
				"None",
				"No Target",
				$"{cannonsRoom.DisplayName} are awaiting a target.");
		}

		if (!PlayerShip.IsRoomOperational(cannonsRoom))
		{
			return new PlayerCannonStatus(
				targetLabel,
				"Offline",
				$"{cannonsRoom.DisplayName} are offline.");
		}

		if (!PlayerShip.IsRoomManned(cannonsRoom, CrewAllegiance.Player))
		{
			return new PlayerCannonStatus(
				targetLabel,
				"Unmanned",
				$"{cannonsRoom.DisplayName} need crew to charge.");
		}

		var chargeSeconds = Math.Min(_playerCannons.ChargeSeconds, CannonChargeDurationSeconds);
		var stateLabel = chargeSeconds >= CannonChargeDurationSeconds - 1.0
			? "Ready Soon"
			: "Charging";

		return new PlayerCannonStatus(
			targetLabel,
			stateLabel,
			$"Charge: {chargeSeconds:0.0} / {CannonChargeDurationSeconds:0.0}s");
	}

	public IReadOnlyList<BattleAvailableAction> GetAvailableActions()
	{
		if (IsBattleOver)
		{
			return [];
		}

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

		var actionIntent = CreateActionIntent(BattleActionKind.TargetSystem);
		if (actionIntent == null)
		{
			SetLastIssuedIntent(null);
			return new BattleActionResolution(false, "Select an enemy room to target.");
		}

		SetLastIssuedIntent(actionIntent);
		_playerCannons.TargetRoomId = targetRoom.RoomId;
		return new BattleActionResolution(true, BuildPlayerCannonTargetStatus(targetRoom));
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

	private double AdvanceTimeControl(double realDeltaSeconds, List<string> statusLines)
	{
		if (_timeControlMode != BattleTimeControlMode.Paused)
		{
			return realDeltaSeconds * GetSimulationTimeScale();
		}

		if (_pauseSecondsRemaining > realDeltaSeconds)
		{
			_pauseSecondsRemaining -= realDeltaSeconds;
			return 0.0;
		}

		var remainingRealDelta = Math.Max(0.0, realDeltaSeconds - _pauseSecondsRemaining);
		_pauseSecondsRemaining = 0.0;
		_timeControlMode = BattleTimeControlMode.Normal;
		statusLines.Add("Tactical pause expires. Battle resumes.");
		return remainingRealDelta * GetSimulationTimeScale();
	}

	private double GetSimulationTimeScale()
	{
		return _timeControlMode switch
		{
			BattleTimeControlMode.Normal => 1.0,
			BattleTimeControlMode.Paused => 0.0,
			_ => 1.0
		};
	}

	private void AdvancePlayerCannons(double deltaSeconds, List<string> statusLines)
	{
		var targetRoom = FindRoomById(EnemyShip, _playerCannons.TargetRoomId);
		if (targetRoom == null)
		{
			return;
		}

		if (!targetRoom.IsOperational)
		{
			_playerCannons.TargetRoomId = null;
			_playerCannons.ChargeSeconds = 0.0;
			statusLines.Add($"Cannons lose lock on {targetRoom.DisplayName} and stop charging.");
			return;
		}

		AdvanceCannons(
			sourceShip: PlayerShip,
			sourceAllegiance: CrewAllegiance.Player,
			targetShip: EnemyShip,
			targetRoom: targetRoom,
			batteryState: _playerCannons,
			deltaSeconds: deltaSeconds,
			statusLines: statusLines,
			isEnemySource: false);
	}

	private void AdvanceEnemyCannons(double deltaSeconds, List<string> statusLines)
	{
		var currentTarget = FindRoomById(PlayerShip, _enemyCannons.TargetRoomId);
		if (currentTarget == null || !currentTarget.IsOperational)
		{
			var replacementTarget = SelectEnemyRetaliationTargetRoom();
			if (replacementTarget?.RoomId != _enemyCannons.TargetRoomId)
			{
				_enemyCannons.ChargeSeconds = 0.0;
			}

			_enemyCannons.TargetRoomId = replacementTarget?.RoomId;
			currentTarget = replacementTarget;
		}

		if (currentTarget == null)
		{
			return;
		}

		AdvanceCannons(
			sourceShip: EnemyShip,
			sourceAllegiance: CrewAllegiance.Enemy,
			targetShip: PlayerShip,
			targetRoom: currentTarget,
			batteryState: _enemyCannons,
			deltaSeconds: deltaSeconds,
			statusLines: statusLines,
			isEnemySource: true);
	}

	private void AdvanceCannons(
		ShipState sourceShip,
		CrewAllegiance sourceAllegiance,
		ShipState targetShip,
		ShipRoomState targetRoom,
		CannonBatteryState batteryState,
		double deltaSeconds,
		List<string> statusLines,
		bool isEnemySource)
	{
		var cannonsRoom = sourceShip.GetRoomBySystemType(OffensiveSystemType);
		if (!sourceShip.IsRoomOperational(cannonsRoom) || !sourceShip.IsRoomManned(cannonsRoom, sourceAllegiance))
		{
			return;
		}

		batteryState.ChargeSeconds = Math.Min(CannonChargeDurationSeconds, batteryState.ChargeSeconds + deltaSeconds);
		if (batteryState.ChargeSeconds < CannonChargeDurationSeconds)
		{
			return;
		}

		batteryState.ChargeSeconds = 0.0;
		var shotResolution = ResolveCannonShot(targetShip, targetRoom, isEnemySource);
		statusLines.Add(shotResolution.StatusText);

		if (!targetRoom.IsOperational)
		{
			batteryState.TargetRoomId = null;
		}
	}

	private BattleActionResolution ResolveCannonShot(ShipState targetShip, ShipRoomState targetRoom, bool isEnemySource)
	{
		if (isEnemySource)
		{
			if (TryEvadeTargetSystemAttack(PlayerShip, CrewAllegiance.Player, targetRoom, out var dodgeStatus))
			{
				return new BattleActionResolution(
					true,
					$"Enemy Cannons fire on {targetRoom.DisplayName}. {dodgeStatus}");
			}

			return ResolveTargetSystemHit(
				defendingShip: PlayerShip,
				targetRoom: targetRoom,
				damageSummaryPrefix: $"Enemy Cannons hit your {targetRoom.DisplayName} for ",
				disabledSummary: $"{targetRoom.DisplayName} is now disabled and offline.");
		}

		if (TryEvadeTargetSystemAttack(EnemyShip, CrewAllegiance.Enemy, targetRoom, out var playerDodgeStatus))
		{
			return new BattleActionResolution(
				true,
				$"{PlayerShip.Name} Cannons fire on {targetRoom.DisplayName}. {playerDodgeStatus}");
		}

		return ResolveTargetSystemHit(
			defendingShip: targetShip,
			targetRoom: targetRoom,
			damageSummaryPrefix: $"{PlayerShip.Name} Cannons hit {targetRoom.DisplayName} for ",
			disabledSummary: $"{targetRoom.DisplayName} is now disabled and no longer counts as an operational system.");
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

	private static ShipRoomState? FindRoomById(ShipState ship, string? roomId)
	{
		if (string.IsNullOrEmpty(roomId))
		{
			return null;
		}

		return ship.Grid.Rooms.Find(room => room.RoomId == roomId);
	}

	private BattleActionResolution ResolveTargetSystemHit(
		ShipState defendingShip,
		ShipRoomState targetRoom,
		string damageSummaryPrefix,
		string disabledSummary)
	{
		var integrityBeforeHit = targetRoom.Integrity;
		targetRoom.ApplyDamage(TargetSystemDamage);
		var systemDamageApplied = integrityBeforeHit - targetRoom.Integrity;

		var roomStatusText = targetRoom.Disabled
			? $"{damageSummaryPrefix}{systemDamageApplied} system damage. {disabledSummary}"
			: $"{damageSummaryPrefix}{systemDamageApplied} system damage. Integrity is now {targetRoom.Integrity}/{ShipRoomState.MaxIntegrity}.";

		var hullDamageApplied = ApplyHullDamage(defendingShip, TargetHullDamage);
		var hullStatusText =
			$"{defendingShip.Name} hull takes {hullDamageApplied} damage and is now {defendingShip.Hull}/{StartingHull}.";

		if (TrySetBattleOver(defendingShip, out var battleOverStatusText))
		{
			return new BattleActionResolution(
				true,
				$"{roomStatusText} {hullStatusText} {battleOverStatusText}");
		}

		return new BattleActionResolution(true, $"{roomStatusText} {hullStatusText}");
	}

	private static int ApplyHullDamage(ShipState defendingShip, int amount)
	{
		var hullBeforeHit = defendingShip.Hull;
		defendingShip.Hull = Math.Max(0, defendingShip.Hull - amount);
		return hullBeforeHit - defendingShip.Hull;
	}

	private bool TrySetBattleOver(ShipState destroyedShip, out string battleOverStatusText)
	{
		battleOverStatusText = string.Empty;
		if (destroyedShip.Hull > 0)
		{
			return false;
		}

		IsBattleOver = true;
		BattleOverStatusText = destroyedShip == EnemyShip
			? $"Battle Over: Victory! {destroyedShip.Name} has been reduced to 0 hull."
			: $"Battle Over: Defeat! {destroyedShip.Name} has been reduced to 0 hull.";
		battleOverStatusText = BattleOverStatusText;
		return true;
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

	private static string? ApplyOpeningCrisis(ShipState playerShip, ShipState enemyShip)
	{
		var damagedPlayerRoom = playerShip.GetRoomBySystemType(OffensiveSystemType);
		if (damagedPlayerRoom == null)
		{
			return null;
		}

		damagedPlayerRoom.ApplyDamage(OpeningCrisisDamage);

		var enemyCannons = enemyShip.GetRoomBySystemType(OffensiveSystemType);
		var enemyPressureText = enemyCannons == null
			? "Enemy pressure is mounting."
			: $"Enemy {enemyCannons.DisplayName} are ready to fire.";

		return $"{playerShip.Name} starts with damaged {damagedPlayerRoom.DisplayName}. {enemyPressureText}";
	}

	private string BuildPlayerCannonTargetStatus(ShipRoomState targetRoom)
	{
		var cannonsRoom = PlayerShip.GetRoomBySystemType(OffensiveSystemType);
		if (cannonsRoom == null)
		{
			return $"Cannons targeting {targetRoom.DisplayName}. No cannons system is available.";
		}

		if (!PlayerShip.IsRoomOperational(cannonsRoom))
		{
			return $"Cannons targeting {targetRoom.DisplayName}. {cannonsRoom.DisplayName} is offline.";
		}

		if (!PlayerShip.IsRoomManned(cannonsRoom, CrewAllegiance.Player))
		{
			return $"Cannons targeting {targetRoom.DisplayName}. Awaiting crew at {cannonsRoom.DisplayName}.";
		}

		return
			$"Cannons targeting {targetRoom.DisplayName}. " +
			$"Charge {_playerCannons.ChargeSeconds:0.0}/{CannonChargeDurationSeconds:0.0}s.";
	}

	private sealed class CannonBatteryState
	{
		public string? TargetRoomId { get; set; }
		public double ChargeSeconds { get; set; }
	}
}

public sealed record PlayerCannonStatus(
	string TargetLabel,
	string StateLabel,
	string DetailText);

public sealed record BattleTimeControlStatus(
	string ModeLabel,
	double RemainingPauseSeconds,
	string DisplayText);

public enum BattleTimeControlMode
{
	Normal,
	Paused
}
