using System;
using System.Collections.Generic;
using Godot;
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
	private const double SlowTimeScale = 0.5;
	private const double CrewMoveStepDurationSeconds = 0.35;
	private const double CrewRepairTickDurationSeconds = 1.0;
	private const int TargetSystemDamage = 40;
	private const int TargetHullDamage = 8;
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
	public bool IsBattleOver { get; private set; }
	public string? BattleOverStatusText { get; private set; }
	public string? OpeningStatusText { get; private set; }

	private readonly CannonBatteryState _playerCannons = new();
	private readonly CannonBatteryState _enemyCannons = new();
	private readonly Dictionary<string, CrewTaskState> _playerCrewTasks = new();
	private BattleTimeControlMode _timeControlMode = BattleTimeControlMode.Normal;
	private BattleTimeControlMode _nonPausedTimeControlMode = BattleTimeControlMode.Normal;
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
		battleState.OpeningStatusText = BuildOpeningStatusText(enemyShip);
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

		if (TryHandleCrewSelectionFromTile(shipSource, ship, tileX, tileY))
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

		AdvanceFriendlyCrewTasks(simulationDeltaSeconds, statusLines);
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

	public string GetCrewTaskStatusText(CrewState crew)
	{
		if (crew.Allegiance != CrewAllegiance.Player || !_playerCrewTasks.TryGetValue(crew.Id, out var taskState))
		{
			return "Task: Idle";
		}

		if (taskState.Kind == CrewTaskKind.Moving)
		{
			var destinationRoom = PlayerShip.GetRoomAt(taskState.DestinationTileX, taskState.DestinationTileY);
			var destinationLabel = destinationRoom?.DisplayName ?? $"({taskState.DestinationTileX}, {taskState.DestinationTileY})";
			var nextStep = taskState.PendingPath.Count > 0
				? taskState.PendingPath.Peek()
				: new Vector2I(taskState.DestinationTileX, taskState.DestinationTileY);

			return
				$"Task: Moving to {destinationLabel}\n" +
				$"Step: ({nextStep.X}, {nextStep.Y}) {taskState.ProgressSeconds:0.0}/{CrewMoveStepDurationSeconds:0.0}s | Steps Left: {taskState.PendingPath.Count}";
		}

		var targetRoom = FindRoomById(PlayerShip, taskState.TargetRoomId);
		var roomLabel = targetRoom?.DisplayName ?? "Unknown Room";
		return
			$"Task: Repairing {roomLabel}\n" +
			$"Progress: {taskState.ProgressSeconds:0.0}/{CrewRepairTickDurationSeconds:0.0}s";
	}

	public BattleActionResolution ToggleTacticalPause()
	{
		if (IsBattleOver)
		{
			return new BattleActionResolution(false, BattleOverStatusText ?? "Battle is over.");
		}

		if (_timeControlMode == BattleTimeControlMode.Paused)
		{
			_timeControlMode = _nonPausedTimeControlMode;
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

	public BattleActionResolution ToggleSlowTime()
	{
		if (IsBattleOver)
		{
			return new BattleActionResolution(false, BattleOverStatusText ?? "Battle is over.");
		}

		_nonPausedTimeControlMode = _nonPausedTimeControlMode == BattleTimeControlMode.Slow
			? BattleTimeControlMode.Normal
			: BattleTimeControlMode.Slow;

		if (_timeControlMode != BattleTimeControlMode.Paused)
		{
			_timeControlMode = _nonPausedTimeControlMode;
		}

		return _nonPausedTimeControlMode == BattleTimeControlMode.Slow
			? new BattleActionResolution(true, $"Slow time engaged at {SlowTimeScale:0.0}x.")
			: new BattleActionResolution(true, "Time returned to normal speed.");
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

		if (_timeControlMode == BattleTimeControlMode.Slow)
		{
			return new BattleTimeControlStatus(
				"Slow",
				_pauseSecondsRemaining,
				$"Time: Slow ({SlowTimeScale:0.0}x)");
		}

		return new BattleTimeControlStatus("Normal", _pauseSecondsRemaining, "Time: Normal");
	}

	public CannonChargeBarState GetPlayerCannonChargeBarState()
	{
		var cannonsRoom = PlayerShip.GetRoomBySystemType(OffensiveSystemType);
		var targetRoom = FindRoomById(EnemyShip, _playerCannons.TargetRoomId);
		return BuildCannonChargeBarState(PlayerShip, CrewAllegiance.Player, cannonsRoom, targetRoom, _playerCannons);
	}

	public CannonChargeBarState GetEnemyCannonChargeBarState()
	{
		var cannonsRoom = EnemyShip.GetRoomBySystemType(OffensiveSystemType);
		var targetRoom = FindRoomById(PlayerShip, _enemyCannons.TargetRoomId);
		return BuildCannonChargeBarState(EnemyShip, CrewAllegiance.Enemy, cannonsRoom, targetRoom, _enemyCannons);
	}

	private CannonChargeBarState BuildCannonChargeBarState(
		ShipState ship,
		CrewAllegiance allegiance,
		ShipRoomState? cannonsRoom,
		ShipRoomState? targetRoom,
		CannonBatteryState batteryState)
	{
		if (IsBattleOver || cannonsRoom == null)
		{
			return new CannonChargeBarState(null, 0.0, false, false);
		}

		if (targetRoom == null)
		{
			return new CannonChargeBarState(cannonsRoom.RoomId, 0.0, false, false);
		}

		if (!ship.IsRoomOperational(cannonsRoom) || !ship.IsRoomManned(cannonsRoom, allegiance))
		{
			return new CannonChargeBarState(cannonsRoom.RoomId, 0.0, true, false);
		}

		var progressRatio = Math.Clamp(batteryState.ChargeSeconds / CannonChargeDurationSeconds, 0.0, 1.0);
		return new CannonChargeBarState(cannonsRoom.RoomId, progressRatio, true, true);
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

		var repairCrew = FindRepairCrew(room);
		if (repairCrew == null)
		{
			return new BattleActionResolution(
				false,
				$"{room.DisplayName} needs an idle crew member present before repairs can begin.");
		}

		if (_playerCrewTasks.TryGetValue(repairCrew.Id, out var existingTask) &&
			existingTask.Kind == CrewTaskKind.Repairing &&
			existingTask.TargetRoomId == room.RoomId)
		{
			return new BattleActionResolution(true, $"{repairCrew.DisplayName} is already repairing {room.DisplayName}.");
		}

		_playerCrewTasks[repairCrew.Id] = CrewTaskState.ForRepair(room.RoomId);
		return new BattleActionResolution(true, $"{repairCrew.DisplayName} begins repairing {room.DisplayName}.");
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
		_timeControlMode = _nonPausedTimeControlMode;
		var resumedModeText = _nonPausedTimeControlMode == BattleTimeControlMode.Slow
			? $"slow time ({SlowTimeScale:0.0}x)"
			: "normal speed";
		statusLines.Add($"Tactical pause expires. Battle resumes at {resumedModeText}.");
		return remainingRealDelta * GetSimulationTimeScale();
	}

	private double GetSimulationTimeScale()
	{
		return _timeControlMode switch
		{
			BattleTimeControlMode.Normal => 1.0,
			BattleTimeControlMode.Slow => SlowTimeScale,
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

	private void AdvanceFriendlyCrewTasks(double deltaSeconds, List<string> statusLines)
	{
		if (_playerCrewTasks.Count == 0)
		{
			return;
		}

		var completedCrewIds = new List<string>();
		foreach (var crew in PlayerShip.Crew)
		{
			if (crew.Allegiance != CrewAllegiance.Player || !_playerCrewTasks.TryGetValue(crew.Id, out var taskState))
			{
				continue;
			}

			AdvanceCrewTask(crew, taskState, deltaSeconds, statusLines, completedCrewIds);
		}

		foreach (var crewId in completedCrewIds)
		{
			_playerCrewTasks.Remove(crewId);
		}
	}

	private void AdvanceCrewTask(
		CrewState crew,
		CrewTaskState taskState,
		double deltaSeconds,
		List<string> statusLines,
		List<string> completedCrewIds)
	{
		var remainingDeltaSeconds = deltaSeconds;
		while (remainingDeltaSeconds > 0.0)
		{
			switch (taskState.Kind)
			{
				case CrewTaskKind.Moving:
					if (!AdvanceCrewMovement(crew, taskState, ref remainingDeltaSeconds, statusLines, completedCrewIds))
					{
						return;
					}
					break;

				case CrewTaskKind.Repairing:
					if (!AdvanceCrewRepair(crew, taskState, ref remainingDeltaSeconds, statusLines, completedCrewIds))
					{
						return;
					}
					break;

				default:
					completedCrewIds.Add(crew.Id);
					return;
			}
		}
	}

	private bool AdvanceCrewMovement(
		CrewState crew,
		CrewTaskState taskState,
		ref double remainingDeltaSeconds,
		List<string> statusLines,
		List<string> completedCrewIds)
	{
		if (taskState.PendingPath.Count == 0)
		{
			completedCrewIds.Add(crew.Id);
			return false;
		}

		var stepTimeRemaining = CrewMoveStepDurationSeconds - taskState.ProgressSeconds;
		var appliedDeltaSeconds = Math.Min(remainingDeltaSeconds, stepTimeRemaining);
		taskState.ProgressSeconds += appliedDeltaSeconds;
		remainingDeltaSeconds -= appliedDeltaSeconds;

		if (taskState.ProgressSeconds < CrewMoveStepDurationSeconds)
		{
			return false;
		}

		var nextStep = taskState.PendingPath.Dequeue();
		taskState.ProgressSeconds = 0.0;
		if (!PlayerShip.TryMoveCrewTo(crew, nextStep.X, nextStep.Y))
		{
			completedCrewIds.Add(crew.Id);
			statusLines.Add($"{crew.DisplayName} stops moving because the path is blocked.");
			return false;
		}

		var currentRoom = PlayerShip.GetRoomForCrew(crew);
		statusLines.Add($"{crew.DisplayName} moves to ({nextStep.X}, {nextStep.Y}).");
		if (taskState.PendingPath.Count > 0)
		{
			return true;
		}

		completedCrewIds.Add(crew.Id);
		if (currentRoom != null)
		{
			statusLines.Add($"{crew.DisplayName} arrives in {currentRoom.DisplayName}.");
		}

		return false;
	}

	private bool AdvanceCrewRepair(
		CrewState crew,
		CrewTaskState taskState,
		ref double remainingDeltaSeconds,
		List<string> statusLines,
		List<string> completedCrewIds)
	{
		var targetRoom = FindRoomById(PlayerShip, taskState.TargetRoomId);
		if (targetRoom == null || PlayerShip.GetRoomForCrew(crew)?.RoomId != targetRoom.RoomId)
		{
			completedCrewIds.Add(crew.Id);
			statusLines.Add($"{crew.DisplayName} stops repairing.");
			return false;
		}

		if (!targetRoom.IsDamaged)
		{
			completedCrewIds.Add(crew.Id);
			return false;
		}

		var repairTimeRemaining = CrewRepairTickDurationSeconds - taskState.ProgressSeconds;
		var appliedDeltaSeconds = Math.Min(remainingDeltaSeconds, repairTimeRemaining);
		taskState.ProgressSeconds += appliedDeltaSeconds;
		remainingDeltaSeconds -= appliedDeltaSeconds;

		if (taskState.ProgressSeconds < CrewRepairTickDurationSeconds)
		{
			return false;
		}

		taskState.ProgressSeconds = 0.0;
		statusLines.Add(ApplyTimedRepairTick(targetRoom, crew.DisplayName));
		if (!targetRoom.IsDamaged)
		{
			completedCrewIds.Add(crew.Id);
			return false;
		}

		return true;
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
			LastMovementFeedback = new BattleMovementFeedback(
				BattleMovementFeedbackKind.WrongShip,
				selectedCrew.DisplayName,
				tileX,
				tileY);
			return true;
		}

		var moveValidationResult = ShipReachability.EvaluateMove(ship, selectedCrew, tileX, tileY);
		if (moveValidationResult != ShipMoveValidationResult.Reachable)
		{
			LastMovementFeedback = new BattleMovementFeedback(
				MapMoveValidationFeedback(moveValidationResult),
				selectedCrew.DisplayName,
				tileX,
				tileY);
			return true;
		}

		if (!ShipReachability.TryBuildPath(ship, selectedCrew, tileX, tileY, out var path) || path.Count == 0)
		{
			LastMovementFeedback = new BattleMovementFeedback(
				BattleMovementFeedbackKind.InvalidDestination,
				selectedCrew.DisplayName,
				tileX,
				tileY);
			return true;
		}

		_playerCrewTasks[selectedCrew.Id] = CrewTaskState.ForMovement(path);
		SetCrewSelection(shipSource, ship, selectedCrew);
		LastMovementFeedback = new BattleMovementFeedback(
			BattleMovementFeedbackKind.Queued,
			selectedCrew.DisplayName,
			tileX,
			tileY);
		return true;
	}

	private bool TryHandleCrewSelectionFromTile(string shipSource, ShipState ship, int tileX, int tileY)
	{
		var clickedCrew = ship.GetCrewAtTile(tileX, tileY);
		if (clickedCrew == null)
		{
			return false;
		}

		if (CurrentSelection?.Kind == BattleSelectionKind.Crew && CurrentSelection.Crew?.Id == clickedCrew.Id)
		{
			SetCrewSelection(shipSource, ship, clickedCrew);
			return true;
		}

		SetCrewSelection(shipSource, ship, clickedCrew);
		return true;
	}

	private CrewState? FindRepairCrew(ShipRoomState room)
	{
		if (CurrentSelection?.Kind == BattleSelectionKind.Crew &&
			CurrentSelection.Crew?.Allegiance == CrewAllegiance.Player &&
			PlayerShip.GetRoomForCrew(CurrentSelection.Crew)?.RoomId == room.RoomId &&
			IsCrewAvailableForRepair(CurrentSelection.Crew))
		{
			return CurrentSelection.Crew;
		}

		foreach (var crew in PlayerShip.GetCrewInRoom(room, CrewAllegiance.Player))
		{
			if (IsCrewAvailableForRepair(crew))
			{
				return crew;
			}
		}

		return null;
	}

	private bool IsCrewAvailableForRepair(CrewState crew)
	{
		if (!_playerCrewTasks.TryGetValue(crew.Id, out var taskState))
		{
			return true;
		}

		return taskState.Kind == CrewTaskKind.Repairing &&
			taskState.TargetRoomId == PlayerShip.GetRoomForCrew(crew)?.RoomId;
	}

	private static BattleMovementFeedbackKind MapMoveValidationFeedback(ShipMoveValidationResult validationResult)
	{
		return validationResult switch
		{
			ShipMoveValidationResult.TileOccupied => BattleMovementFeedbackKind.TileOccupied,
			ShipMoveValidationResult.Unreachable => BattleMovementFeedbackKind.Unreachable,
			ShipMoveValidationResult.InvalidDestination => BattleMovementFeedbackKind.InvalidDestination,
			_ => BattleMovementFeedbackKind.NoMovableCrewSelected
		};
	}

	private static string ApplyTimedRepairTick(ShipRoomState room, string crewName)
	{
		var wasDisabled = room.Disabled;
		var integrityBeforeRepair = room.Integrity;
		room.ApplyRepair(RepairAmount);
		var amountRepaired = room.Integrity - integrityBeforeRepair;
		var repairSummary =
			$"{crewName} repairs {room.DisplayName} for {amountRepaired}. Integrity is now {room.Integrity}/{ShipRoomState.MaxIntegrity}.";

		if (wasDisabled && room.IsOperational)
		{
			return $"{repairSummary} {room.DisplayName} is back online.";
		}

		if (!room.IsDamaged)
		{
			return $"{repairSummary} {room.DisplayName} is fully restored.";
		}

		return repairSummary;
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

	private static string? BuildOpeningStatusText(ShipState enemyShip)
	{
		var enemyCannons = enemyShip.GetRoomBySystemType(OffensiveSystemType);
		return enemyCannons == null
			? "Enemy pressure is mounting."
			: $"Enemy {enemyCannons.DisplayName} are ready to fire.";
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

		return $"Cannons targeting {targetRoom.DisplayName}.";
	}

	private sealed class CannonBatteryState
	{
		public string? TargetRoomId { get; set; }
		public double ChargeSeconds { get; set; }
	}

	private sealed class CrewTaskState
	{
		public CrewTaskKind Kind { get; private set; }
		public Queue<Vector2I> PendingPath { get; } = new();
		public int DestinationTileX { get; private set; }
		public int DestinationTileY { get; private set; }
		public string? TargetRoomId { get; private set; }
		public double ProgressSeconds { get; set; }

		public static CrewTaskState ForMovement(IReadOnlyList<Vector2I> path)
		{
			var movementTask = new CrewTaskState
			{
				Kind = CrewTaskKind.Moving,
				DestinationTileX = path[^1].X,
				DestinationTileY = path[^1].Y
			};

			foreach (var step in path)
			{
				movementTask.PendingPath.Enqueue(step);
			}

			return movementTask;
		}

		public static CrewTaskState ForRepair(string roomId)
		{
			return new CrewTaskState
			{
				Kind = CrewTaskKind.Repairing,
				TargetRoomId = roomId
			};
		}
	}
}

public sealed record CannonChargeBarState(
	string? RoomId,
	double ProgressRatio,
	bool IsVisible,
	bool IsActive);

public sealed record BattleTimeControlStatus(
	string ModeLabel,
	double RemainingPauseSeconds,
	string DisplayText);

public enum BattleTimeControlMode
{
	Normal,
	Slow,
	Paused
}

public enum CrewTaskKind
{
	Moving,
	Repairing
}
