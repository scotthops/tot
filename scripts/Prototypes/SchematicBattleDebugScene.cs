using Godot;
using System.Collections.Generic;
using TidesOfTime.Battle;
using TidesOfTime.Crew;
using TidesOfTime.Data;
using TidesOfTime.Ships;
using TidesOfTime.UI;

namespace TidesOfTime.Prototypes;

public partial class SchematicBattleDebugScene : Control
{
	[Export] public ShipLayoutDef PlayerLayout { get; set; } = null!;
	[Export] public ShipLayoutDef EnemyLayout { get; set; } = null!;

	private BattleState _battleState = null!;
	private SchematicShipGridView _playerShipView = null!;
	private SchematicShipGridView _enemyShipView = null!;
	private Control _background = null!;
	private Label _titleLabel = null!;
	private Label _selectionSourceLabel = null!;
	private Label _selectionRoomLabel = null!;
	private Label _selectionSystemLabel = null!;
	private Label _actionStatusLabel = null!;
	private Button _primaryActionButton = null!;
	private Button _secondaryActionButton = null!;
	private Button _resetButton = null!;
	private BattleActionKind? _primaryActionKind;
	private BattleActionKind? _secondaryActionKind;

	public override void _Ready()
	{
		_playerShipView = GetNode<SchematicShipGridView>("MarginContainer/VBoxContainer/ShipRow/PlayerShipView");
		_enemyShipView = GetNode<SchematicShipGridView>("MarginContainer/VBoxContainer/ShipRow/EnemyShipView");
		_background = GetNode<Control>("Background");
		_titleLabel = GetNode<Label>("MarginContainer/VBoxContainer/StatusPanel/MarginContainer/VBoxContainer/TitleLabel");
		_selectionSourceLabel = GetNode<Label>("MarginContainer/VBoxContainer/StatusPanel/MarginContainer/VBoxContainer/SelectionDetails/SelectionSourceLabel");
		_selectionRoomLabel = GetNode<Label>("MarginContainer/VBoxContainer/StatusPanel/MarginContainer/VBoxContainer/SelectionDetails/SelectionRoomLabel");
		_selectionSystemLabel = GetNode<Label>("MarginContainer/VBoxContainer/StatusPanel/MarginContainer/VBoxContainer/SelectionDetails/SelectionSystemLabel");
		_actionStatusLabel = GetNode<Label>("MarginContainer/VBoxContainer/StatusPanel/MarginContainer/VBoxContainer/ActionStatusLabel");
		_primaryActionButton = GetNode<Button>("MarginContainer/VBoxContainer/StatusPanel/MarginContainer/VBoxContainer/ButtonRow/PrimaryActionButton");
		_secondaryActionButton = GetNode<Button>("MarginContainer/VBoxContainer/StatusPanel/MarginContainer/VBoxContainer/ButtonRow/SecondaryActionButton");
		_resetButton = GetNode<Button>("MarginContainer/VBoxContainer/StatusPanel/MarginContainer/VBoxContainer/ButtonRow/ResetButton");

		if (PlayerLayout == null || EnemyLayout == null)
		{
			GD.PushError("SchematicBattleDebugScene: PlayerLayout and EnemyLayout must be assigned.");
			return;
		}

		_playerShipView.SetShipVisualStyle(new Color(0.24f, 0.48f, 0.78f), true);
		_enemyShipView.SetShipVisualStyle(new Color(0.72f, 0.24f, 0.18f), false);
		_playerShipView.TilePressed += (ship, x, y) => OnTilePressed("Player", ship, x, y);
		_playerShipView.BackgroundPressed += OnBoardBackgroundPressed;
		_playerShipView.CrewSelected += (ship, crew) => OnCrewSelected("Player", ship, crew);
		_enemyShipView.TilePressed += (ship, x, y) => OnTilePressed("Enemy", ship, x, y);
		_enemyShipView.BackgroundPressed += OnBoardBackgroundPressed;
		_enemyShipView.CrewSelected += (ship, crew) => OnCrewSelected("Enemy", ship, crew);
		_background.GuiInput += OnBackgroundGuiInput;
		_primaryActionButton.Pressed += () => RunAction(_primaryActionKind);
		_secondaryActionButton.Pressed += () => RunAction(_secondaryActionKind);
		_resetButton.Pressed += () => ResetBattleState();

		ResetBattleState();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_battleState == null)
		{
			return;
		}

		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space })
		{
			var result = _battleState.ToggleTacticalPause();
			ShowSelectionState(_battleState.CurrentSelection);
			_actionStatusLabel.Text = result.StatusText;
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Q })
		{
			var result = _battleState.ToggleSlowTime();
			ShowSelectionState(_battleState.CurrentSelection);
			_actionStatusLabel.Text = result.StatusText;
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.R })
		{
			ResetBattleState();
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta)
	{
		if (_battleState == null)
		{
			return;
		}

		var updateResult = _battleState.Update(delta);
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
		_battleState.HandleTilePressed(shipSource, ship, tileX, tileY);
		RenderBattleViews();
		ShowSelectionState(_battleState.CurrentSelection);
	}

	private void OnCrewSelected(string shipSource, ShipState ship, CrewState crew)
	{
		_battleState.SetCrewSelection(shipSource, ship, crew);
		RenderBattleViews();
		ShowSelectionState(_battleState.CurrentSelection);
	}

	private void OnBoardBackgroundPressed(ShipState _)
	{
		ClearCurrentSelection();
	}

	private void OnBackgroundGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			ClearCurrentSelection();
		}
	}

	private void RunAction(BattleActionKind? actionKind)
	{
		if (actionKind == null)
		{
			_actionStatusLabel.Text = "Select a room to see actions.";
			return;
		}

		var result = _battleState.ExecuteAction(actionKind.Value);
		RenderBattleViews();
		ShowSelectionState(_battleState.CurrentSelection);
		_actionStatusLabel.Text = result.StatusText;
	}

	private void ResetBattleState()
	{
		_battleState = BattleState.Create(PlayerLayout, EnemyLayout);
		RenderBattleViews();
		ShowSelectionState(_battleState.CurrentSelection);
		_actionStatusLabel.Text = string.IsNullOrEmpty(_battleState.OpeningStatusText)
			? "Schematic battle debug scene ready."
			: _battleState.OpeningStatusText;
	}

	private void RenderBattleViews()
	{
		var selectedCrewId = _battleState.CurrentSelection?.Kind == BattleSelectionKind.Crew
			? _battleState.CurrentSelection.Crew?.Id
			: null;
		var playerSelectedCrewId = _battleState.CurrentSelection?.Ship == _battleState.PlayerShip
			? selectedCrewId
			: null;
		var enemySelectedCrewId = _battleState.CurrentSelection?.Ship == _battleState.EnemyShip
			? selectedCrewId
			: null;

		_playerShipView.Render(_battleState.PlayerShip, playerSelectedCrewId);
		_enemyShipView.Render(_battleState.EnemyShip, enemySelectedCrewId);
		UpdateCannonChargeBars();
	}

	private void ShowSelectionState(BattleSelection? selection)
	{
		RefreshSelectionDetails(selection);
		UpdateActionButtons(selection);
		UpdateTimeControlStatusLabel();
		UpdateCannonChargeBars();
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
			var room = selection.Ship.GetRoomForCrew(selection.Crew);
			_selectionRoomLabel.Text = $"Crew: {selection.Crew.DisplayName} [{selection.Crew.ShortLabel}]";
			_selectionSystemLabel.Text =
				$"Role: {selection.Crew.CrewClass} | Allegiance: {selection.Crew.Allegiance}\n" +
				$"Room: {room?.DisplayName ?? "Deck"}\n" +
				$"{_battleState.GetCrewTaskStatusText(selection.Crew)}";
			return;
		}

		_selectionRoomLabel.Text = $"Room: {selection.Room?.DisplayName ?? "None"}";
		_selectionSystemLabel.Text = BuildRoomSelectionSummary(selection.Ship, selection.Room);
	}

	private void UpdateActionButtons(BattleSelection? selection)
	{
		if (_battleState.IsBattleOver)
		{
			ConfigureActionButton(_primaryActionButton, ref _primaryActionKind, "Battle Over", null);
			ConfigureActionButton(_secondaryActionButton, ref _secondaryActionKind, "Action 2", null);
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
				? "Crew selected. Click a walkable player tile to queue movement."
				: "Select a room to see actions.";
			return;
		}

		ConfigureActionButtons(_battleState.GetAvailableActions());
		_actionStatusLabel.Text = selection.Room?.Disabled == true
			? $"{selection.Room.DisplayName} is disabled."
			: $"Ready: {selection.Room?.DisplayName ?? "Room"} on {selection.Ship.Name}";
	}

	private void ConfigureActionButtons(IReadOnlyList<BattleAvailableAction> actions)
	{
		ConfigureActionButton(
			_primaryActionButton,
			ref _primaryActionKind,
			actions.Count > 0 ? actions[0].DisplayLabel : "Action 1",
			actions.Count > 0 ? actions[0].Kind : null);
		ConfigureActionButton(
			_secondaryActionButton,
			ref _secondaryActionKind,
			actions.Count > 1 ? actions[1].DisplayLabel : "Action 2",
			actions.Count > 1 ? actions[1].Kind : null);
	}

	private static void ConfigureActionButton(
		Button button,
		ref BattleActionKind? storedKind,
		string text,
		BattleActionKind? actionKind)
	{
		storedKind = actionKind;
		button.Text = text;
		button.Disabled = actionKind == null;
	}

	private void UpdateCannonChargeBars()
	{
		_playerShipView.SetCannonChargeBar(_battleState.GetPlayerCannonChargeBarState());
		_enemyShipView.SetCannonChargeBar(_battleState.GetEnemyCannonChargeBarState());
	}

	private void UpdateTimeControlStatusLabel()
	{
		_titleLabel.Text = $"Schematic Battle Debug | {_battleState.GetTimeControlStatus().DisplayText}";
	}

	private string BuildRoomSelectionSummary(ShipState ship, ShipRoomState? room)
	{
		if (room == null)
		{
			return "System: None";
		}

		var allegiance = ship == _battleState.PlayerShip
			? CrewAllegiance.Player
			: CrewAllegiance.Enemy;

		return
			$"System: {room.SystemType} ({GetManningText(ship, room, allegiance)})\n" +
			$"Integrity: {room.Integrity}/{ShipRoomState.MaxIntegrity} | Status: {GetRoomStatusText(room)}\n" +
			$"Occupants: {FormatCrewList(ship.GetCrewInRoom(room))}";
	}

	private static string GetManningText(ShipState ship, ShipRoomState room, CrewAllegiance allegiance)
	{
		if (!ship.IsRoomOperational(room))
		{
			return "Offline";
		}

		return ship.IsRoomManned(room, allegiance) ? "Manned" : "Unmanned";
	}

	private static string GetRoomStatusText(ShipRoomState room)
	{
		if (room.Disabled)
		{
			return "Disabled";
		}

		return room.IsDamaged ? "Damaged" : "Operational";
	}

	private static string FormatCrewList(IReadOnlyList<CrewState> crewMembers)
	{
		if (crewMembers.Count == 0)
		{
			return "None";
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
}
