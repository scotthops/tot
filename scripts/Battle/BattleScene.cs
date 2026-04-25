using Godot;
using System.Collections.Generic;
using TidesOfTime.Crew;
using TidesOfTime.Data;
using TidesOfTime.Encounters;
using TidesOfTime.Ships;
using TidesOfTime.UI;

namespace TidesOfTime.Battle;

public partial class BattleScene : Control
{
	private const string SpecialActionMetaKey = "special_action";
	private const string RematchActionId = "rematch";
	private const string ReturnToSailingActionId = "return_to_sailing";

	[Export] public ShipLayoutDef PlayerLayout { get; set; } = null!;
	[Export] public ShipLayoutDef EnemyLayout { get; set; } = null!;

	private BattleState _battleState = null!;
	private SailingEncounterData? _activeEncounterData;
	private ShipGridView _playerShipView = null!;
	private ShipGridView _enemyShipView = null!;
	private Control _background = null!;
	private Control _selectionPanel = null!;
	private Label _selectionTitleLabel = null!;
	private Label _selectionSourceLabel = null!;
	private Label _selectionRoomLabel = null!;
	private Label _selectionSystemLabel = null!;
	private Button _primaryActionButton = null!;
	private Button _secondaryActionButton = null!;
	private Label _actionStatusLabel = null!;
	private Control _pauseOverlay = null!;
	private Button _resumeButton = null!;
	private Button _resumeSailingButton = null!;
	private Button _restartButton = null!;
	private Button _quitGameButton = null!;
	private bool _isPauseMenuOpen;

	public override void _Ready()
	{
		_playerShipView = GetNode<ShipGridView>("MarginContainer/VBoxContainer/HBoxContainer/PlayerShipGridView");
		_enemyShipView = GetNode<ShipGridView>("MarginContainer/VBoxContainer/HBoxContainer/EnemyShipGridView");
		_background = GetNode<Control>("Background");
		_selectionPanel = GetNode<Control>("MarginContainer/VBoxContainer/SelectionPanel");
		_selectionTitleLabel = GetNode<Label>("MarginContainer/VBoxContainer/SelectionPanel/MarginContainer/VBoxContainer/SelectionTitleLabel");
		_selectionSourceLabel = GetNode<Label>("MarginContainer/VBoxContainer/SelectionPanel/MarginContainer/VBoxContainer/SelectionDetailsScroll/SelectionDetailsContent/SelectionSourceLabel");
		_selectionRoomLabel = GetNode<Label>("MarginContainer/VBoxContainer/SelectionPanel/MarginContainer/VBoxContainer/SelectionDetailsScroll/SelectionDetailsContent/SelectionRoomLabel");
		_selectionSystemLabel = GetNode<Label>("MarginContainer/VBoxContainer/SelectionPanel/MarginContainer/VBoxContainer/SelectionDetailsScroll/SelectionDetailsContent/SelectionSystemLabel");
		_primaryActionButton = GetNode<Button>("MarginContainer/VBoxContainer/SelectionPanel/MarginContainer/VBoxContainer/ActionButtonRow/PrimaryActionButton");
		_secondaryActionButton = GetNode<Button>("MarginContainer/VBoxContainer/SelectionPanel/MarginContainer/VBoxContainer/ActionButtonRow/SecondaryActionButton");
		_actionStatusLabel = GetNode<Label>("MarginContainer/VBoxContainer/SelectionPanel/MarginContainer/VBoxContainer/ActionStatusLabel");
		_pauseOverlay = GetNode<Control>("PauseOverlay");
		_resumeButton = GetNode<Button>("PauseOverlay/MarginContainer/VBoxContainer/ButtonStack/ResumeButton");
		_resumeSailingButton = GetNode<Button>("PauseOverlay/MarginContainer/VBoxContainer/ButtonStack/ResumeSailingButton");
		_restartButton = GetNode<Button>("PauseOverlay/MarginContainer/VBoxContainer/ButtonStack/RestartButton");
		_quitGameButton = GetNode<Button>("PauseOverlay/MarginContainer/VBoxContainer/ButtonStack/QuitGameButton");

		ApplyPendingEncounterIfAny();

		if (PlayerLayout == null || EnemyLayout == null)
		{
			GD.PushError("BattleScene: PlayerLayout and EnemyLayout must be assigned.");
			return;
		}

		_primaryActionButton.Pressed += OnPrimaryActionPressed;
		_secondaryActionButton.Pressed += OnSecondaryActionPressed;
		_resumeButton.Pressed += OnResumePressed;
		_resumeSailingButton.Pressed += OnResumeSailingPressed;
		_restartButton.Pressed += OnRestartPressed;
		_quitGameButton.Pressed += OnQuitGamePressed;
		_playerShipView.UsePlayerCannonBarPalette = true;
		_enemyShipView.UsePlayerCannonBarPalette = false;
		ApplyShipVisualStyles();
		_background.GuiInput += OnBackgroundGuiInput;
		_selectionPanel.GuiInput += OnBackgroundGuiInput;
		_playerShipView.TilePressed += (ship, x, y) => OnTilePressed("Player", ship, x, y);
		_playerShipView.BackgroundPressed += OnBoardBackgroundPressed;
		_playerShipView.CrewSelected += (ship, crew) => OnCrewSelected("Player", ship, crew);
		_enemyShipView.TilePressed += (ship, x, y) => OnTilePressed("Enemy", ship, x, y);
		_enemyShipView.BackgroundPressed += OnBoardBackgroundPressed;
		_enemyShipView.CrewSelected += (ship, crew) => OnCrewSelected("Enemy", ship, crew);
		SetPauseMenuOpen(false);
		ResetBattleState();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
		{
			SetPauseMenuOpen(!_isPauseMenuOpen);
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_isPauseMenuOpen)
		{
			return;
		}

		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space })
		{
			var pauseResult = _battleState.ToggleTacticalPause();
			UpdateTimeControlStatusLabel();
			_actionStatusLabel.Text = pauseResult.StatusText;
			return;
		}

		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Q })
		{
			var slowTimeResult = _battleState.ToggleSlowTime();
			UpdateTimeControlStatusLabel();
			_actionStatusLabel.Text = slowTimeResult.StatusText;
			return;
		}

		if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			ClearCurrentSelection();
		}
	}

	public override void _Process(double delta)
	{
		if (_battleState == null || _isPauseMenuOpen)
		{
			return;
		}

		var updateResult = _battleState.Update(delta);
		RefreshSelectionDetails(_battleState.CurrentSelection);
		UpdateCannonChargeBars();
		UpdateTimeControlStatusLabel();
		if (updateResult == null)
		{
			return;
		}

		RenderBattleViews();
		ShowSelectionState(_battleState.CurrentSelection);
		_actionStatusLabel.Text = updateResult.StatusText;
	}

	private void OnTilePressed(string shipSource, ShipState ship, int tileX, int tileY)
	{
		if (_isPauseMenuOpen)
		{
			return;
		}

		_battleState.HandleTilePressed(shipSource, ship, tileX, tileY);
		RenderBattleViews();
		ShowSelectionState(_battleState.CurrentSelection);
	}

	private void OnCrewSelected(string shipSource, ShipState ship, CrewState crew)
	{
		if (_isPauseMenuOpen)
		{
			return;
		}

		_battleState.SetCrewSelection(shipSource, ship, crew);
		RenderBattleViews();
		ShowSelectionState(_battleState.CurrentSelection);
	}

	private void OnBoardBackgroundPressed(ShipState _)
	{
		if (_isPauseMenuOpen)
		{
			return;
		}

		ClearCurrentSelection();
	}

	private void OnBackgroundGuiInput(InputEvent @event)
	{
		if (_isPauseMenuOpen)
		{
			return;
		}

		if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			return;
		}

		ClearCurrentSelection();
	}

	private void ShowSelectionState(BattleSelection? selection)
	{
		RefreshSelectionDetails(selection);
		UpdateActionArea(selection);
		UpdateCannonChargeBars();
		UpdateTimeControlStatusLabel();
	}

	private void RefreshSelectionDetails(BattleSelection? selection)
	{
		if (selection == null)
		{
			_selectionSourceLabel.Text = "Ship: None";
			_selectionRoomLabel.Text = "Room: None";
			_selectionSystemLabel.Text = "System: None";
			return;
		}

		_selectionSourceLabel.Text = $"Ship: {selection.ShipSource} ({selection.Ship.Name})";

		if (selection.Kind == BattleSelectionKind.Crew && selection.Crew != null)
		{
			var liveRoom = selection.Ship.GetRoomForCrew(selection.Crew);
			_selectionRoomLabel.Text = $"Crew: {selection.Crew.DisplayName} [{selection.Crew.ShortLabel}]";
			_selectionSystemLabel.Text = BuildCrewSelectionSummary(
				selection.Ship,
				GetShipCrewAllegiance(selection.Ship),
				selection.Crew,
				liveRoom);
		}
		else
		{
			_selectionRoomLabel.Text = $"Room: {selection.Room?.DisplayName ?? "None"}";
			_selectionSystemLabel.Text = BuildRoomSelectionSummary(
				selection.Ship,
				GetShipCrewAllegiance(selection.Ship),
				selection.Room);
		}

	}

	private void UpdateActionArea(BattleSelection? selection)
	{
		if (_battleState.IsBattleOver)
		{
			ConfigureRematchActionButtons();
			_actionStatusLabel.Text = _battleState.BattleOverStatusText ?? "Battle is over.";
			return;
		}

		if (_battleState.LastMovementFeedback != null)
		{
			ConfigureActionButtons([]);
			_actionStatusLabel.Text = _battleState.LastMovementFeedback.ToStatusText();
			return;
		}

		if (selection == null || selection.Kind != BattleSelectionKind.Room)
		{
			ConfigureActionButtons([]);
			_battleState.SetLastIssuedIntent(null);
			_actionStatusLabel.Text = selection?.Kind == BattleSelectionKind.Crew
				? "Crew selected. No direct crew actions yet."
				: "Select a room to see actions.";
			return;
		}

		ConfigureActionButtons(_battleState.GetAvailableActions());
		_actionStatusLabel.Text = selection.Room!.Disabled
			? selection.Ship == _battleState.PlayerShip
				? $"{selection.Room.DisplayName} is disabled. Crew inside can repair it."
				: $"{selection.Room.DisplayName} is disabled."
			: $"Ready: {selection.Room.DisplayName} on {selection.Ship.Name}";
	}

	private void RenderBattleViews()
	{
		var selectedCrewId = _battleState.CurrentSelection?.Kind == BattleSelectionKind.Crew
			? _battleState.CurrentSelection.Crew?.Id
			: null;

		var playerSelectedCrewId =
			_battleState.CurrentSelection?.Ship == _battleState.PlayerShip ? selectedCrewId : null;
		var enemySelectedCrewId =
			_battleState.CurrentSelection?.Ship == _battleState.EnemyShip ? selectedCrewId : null;

		_playerShipView.Render(_battleState.PlayerShip, playerSelectedCrewId);
		_enemyShipView.Render(_battleState.EnemyShip, enemySelectedCrewId);
	}

	private void OnPrimaryActionPressed()
	{
		if (_isPauseMenuOpen)
		{
			return;
		}

		RunPrototypeAction(_primaryActionButton);
	}

	private void OnSecondaryActionPressed()
	{
		if (_isPauseMenuOpen)
		{
			return;
		}

		RunPrototypeAction(_secondaryActionButton);
	}

	private void RunPrototypeAction(Button actionButton)
	{
		if (TryRunSpecialAction(actionButton))
		{
			return;
		}

		var actionKind = GetActionKind(actionButton);
		if (actionKind == null)
		{
			_actionStatusLabel.Text = "Select a room first.";
			return;
		}

		var actionResult = _battleState.ExecuteAction(actionKind.Value);
		RenderBattleViews();
		ShowSelectionState(_battleState.CurrentSelection);
		_actionStatusLabel.Text = actionResult.StatusText;
	}

	private void ResetBattleState(string? statusText = null)
	{
		_battleState = BattleState.Create(PlayerLayout, EnemyLayout);
		RenderBattleViews();
		ShowSelectionState(_battleState.CurrentSelection);

		var resolvedStatusText = string.IsNullOrEmpty(statusText)
			? BuildOpeningStatusText()
			: statusText;

		if (!string.IsNullOrEmpty(resolvedStatusText))
		{
			_actionStatusLabel.Text = resolvedStatusText;
		}

		UpdateCannonChargeBars();
		UpdateTimeControlStatusLabel();
	}

	private void OnResumePressed()
	{
		SetPauseMenuOpen(false);
	}

	private void OnResumeSailingPressed()
	{
		SetPauseMenuOpen(false);
		ReturnToSailing();
	}

	private void OnRestartPressed()
	{
		SetPauseMenuOpen(false);
		ResetBattleState();
	}

	private void OnQuitGamePressed()
	{
		SetPauseMenuOpen(false);
		GetTree().Quit();
	}

	private void UpdateCannonChargeBars()
	{
		_playerShipView.SetCannonChargeBar(_battleState.GetPlayerCannonChargeBarState());
		_enemyShipView.SetCannonChargeBar(_battleState.GetEnemyCannonChargeBarState());
	}

	private void UpdateTimeControlStatusLabel()
	{
		_selectionTitleLabel.Text = $"Selection | {_battleState.GetTimeControlStatus().DisplayText}";
	}

	private bool TryRunSpecialAction(Button actionButton)
	{
		if (!actionButton.HasMeta(SpecialActionMetaKey))
		{
			return false;
		}

		var specialAction = actionButton.GetMeta(SpecialActionMetaKey).AsString();
		if (specialAction == RematchActionId)
		{
			ResetBattleState();
			return true;
		}

		if (specialAction == ReturnToSailingActionId)
		{
			ReturnToSailing();
			return true;
		}

		return false;
	}

	private void ApplyPendingEncounterIfAny()
	{
		_activeEncounterData = SailingEncounterStore.ConsumePendingEncounter();
		if (_activeEncounterData == null)
		{
			return;
		}

		if (_activeEncounterData.PlayerShip?.CombatLayout != null)
		{
			PlayerLayout = _activeEncounterData.PlayerShip.CombatLayout;
		}

		if (_activeEncounterData.EnemyShip?.CombatLayout != null)
		{
			EnemyLayout = _activeEncounterData.EnemyShip.CombatLayout;
		}
	}

	private void ApplyShipVisualStyles()
	{
		_playerShipView.SetShipVisualStyle(
			GetArchetypeTint(_activeEncounterData?.PlayerShip, new Color(0.24f, 0.48f, 0.78f)),
			true);
		_enemyShipView.SetShipVisualStyle(
			GetArchetypeTint(_activeEncounterData?.EnemyShip, new Color(0.72f, 0.24f, 0.18f)),
			false);
	}

	private string? BuildOpeningStatusText()
	{
		var battleOpeningText = _battleState.OpeningStatusText;
		if (_activeEncounterData == null)
		{
			return battleOpeningText;
		}

		var encounterText = $"Encounter: {_activeEncounterData.PlayerDisplayName} vs {_activeEncounterData.EnemyDisplayName}.";
		return string.IsNullOrEmpty(battleOpeningText)
			? encounterText
			: $"{encounterText} {battleOpeningText}";
	}

	private void ReturnToSailing()
	{
		var returnScenePath = _activeEncounterData?.ReturnScenePath;
		if (string.IsNullOrWhiteSpace(returnScenePath))
		{
			_actionStatusLabel.Text = "No sailing scene return path is available.";
			return;
		}

		var sceneChangeError = GetTree().ChangeSceneToFile(returnScenePath);
		if (sceneChangeError != Error.Ok)
		{
			_actionStatusLabel.Text = $"Could not return to sailing: {sceneChangeError}.";
		}
	}

	private bool CanReturnToSailing()
	{
		return !string.IsNullOrWhiteSpace(_activeEncounterData?.ReturnScenePath);
	}

	private static Color GetArchetypeTint(ShipArchetypeDef? archetype, Color fallback)
	{
		return archetype == null
			? fallback
			: archetype.CombatTint;
	}

	private static void ConfigureActionButton(
		Button button,
		string text,
		BattleActionKind? actionKind,
		string? specialAction = null)
	{
		button.Text = text;
		button.Disabled = actionKind == null && string.IsNullOrEmpty(specialAction);
		button.RemoveMeta("action_kind");
		button.RemoveMeta(SpecialActionMetaKey);

		if (actionKind != null)
		{
			button.SetMeta("action_kind", (int)actionKind.Value);
			return;
		}

		if (!string.IsNullOrEmpty(specialAction))
		{
			button.SetMeta(SpecialActionMetaKey, specialAction);
		}
	}

	private void ConfigureActionButtons(IReadOnlyList<BattleAvailableAction> actions)
	{
		ConfigureActionButton(
			_primaryActionButton,
			actions.Count > 0 ? actions[0].DisplayLabel : "Action 1",
			actions.Count > 0 ? actions[0].Kind : null);

		ConfigureActionButton(
			_secondaryActionButton,
			actions.Count > 1 ? actions[1].DisplayLabel : "Action 2",
			actions.Count > 1 ? actions[1].Kind : null);
	}

	private void ConfigureRematchActionButtons()
	{
		ConfigureActionButton(_primaryActionButton, "Rematch", null, RematchActionId);
		var canReturnToSailing = !string.IsNullOrWhiteSpace(_activeEncounterData?.ReturnScenePath);
		ConfigureActionButton(
			_secondaryActionButton,
			canReturnToSailing ? "Return to Sailing" : "Action 2",
			null,
			canReturnToSailing ? ReturnToSailingActionId : null);
	}

	private static BattleActionKind? GetActionKind(Button button)
	{
		if (!button.HasMeta("action_kind"))
		{
			return null;
		}

		return (BattleActionKind)(int)button.GetMeta("action_kind");
	}

	private CrewAllegiance GetShipCrewAllegiance(ShipState ship)
	{
		return ship == _battleState.PlayerShip
			? CrewAllegiance.Player
			: CrewAllegiance.Enemy;
	}

	private static string BuildRoomSelectionSummary(ShipState ship, CrewAllegiance shipAllegiance, ShipRoomState? room)
	{
		if (room == null)
		{
			return "System: None";
		}

		var systemSummary = string.IsNullOrEmpty(room.SystemType)
			? "System: None"
			: $"System: {room.SystemType} ({GetManningText(ship, room, shipAllegiance)})";

		return
			$"{systemSummary}\n" +
			$"Integrity: {room.Integrity}/{ShipRoomState.MaxIntegrity} | Status: {GetRoomStatusText(room)}\n" +
			$"Occupants: {FormatCrewList(ship.GetCrewInRoom(room))}";
	}

	private string BuildCrewSelectionSummary(
		ShipState ship,
		CrewAllegiance shipAllegiance,
		CrewState crew,
		ShipRoomState? room)
	{
		var roomName = room?.DisplayName ?? "Deck";
		var companions = room == null
			? []
			: FilterCrew(ship.GetCrewInRoom(room), crew.Id);
		var manningSummary = room == null || string.IsNullOrEmpty(room.SystemType)
			? "System Manning: None"
			: $"System Manning: {GetManningText(ship, room, shipAllegiance)}";
		var roomStatusSummary = room == null
			? "Room Status: None"
			: $"Room Status: {GetRoomStatusText(room)} ({room.Integrity}/{ShipRoomState.MaxIntegrity})";

		return
			$"Role: {crew.CrewClass} | Allegiance: {crew.Allegiance}\n" +
			$"Room: {roomName}\n" +
			$"{manningSummary}\n" +
			$"{roomStatusSummary}\n" +
			$"Crew Here: {FormatCrewList(companions, emptyText: "Alone")}\n" +
			$"{_battleState.GetCrewTaskStatusText(crew)}";
	}

	private static string GetManningText(ShipState ship, ShipRoomState room, CrewAllegiance shipAllegiance)
	{
		if (!ship.IsRoomOperational(room))
		{
			return "Offline";
		}

		return ship.IsRoomManned(room, shipAllegiance) ? "Manned" : "Unmanned";
	}

	private static string GetRoomStatusText(ShipRoomState room)
	{
		if (room.Disabled)
		{
			return "Disabled";
		}

		return room.IsDamaged ? "Damaged" : "Operational";
	}

	private static IReadOnlyList<CrewState> FilterCrew(IReadOnlyList<CrewState> crewMembers, string excludedCrewId)
	{
		var filteredCrew = new List<CrewState>();
		foreach (var crew in crewMembers)
		{
			if (crew.Id != excludedCrewId)
			{
				filteredCrew.Add(crew);
			}
		}

		return filteredCrew;
	}

	private static string FormatCrewList(IReadOnlyList<CrewState> crewMembers, string emptyText = "None")
	{
		if (crewMembers.Count == 0)
		{
			return emptyText;
		}

		var labels = new List<string>();
		foreach (var crew in crewMembers)
		{
			labels.Add($"{crew.DisplayName} [{crew.ShortLabel}]");
		}

		return string.Join(", ", labels);
	}

	private void ClearCurrentSelection()
	{
		_battleState.ClearSelection();
		RenderBattleViews();
		ShowSelectionState(_battleState.CurrentSelection);
	}

	private void SetPauseMenuOpen(bool isOpen)
	{
		_isPauseMenuOpen = isOpen;
		_pauseOverlay.Visible = isOpen;
		_resumeSailingButton.Visible = CanReturnToSailing();
		if (isOpen)
		{
			_resumeButton.GrabFocus();
		}
	}
}
