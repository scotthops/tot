using Godot;
using System.Collections.Generic;
using TidesOfTime.Crew;
using TidesOfTime.Data;
using TidesOfTime.Ships;
using TidesOfTime.UI;

namespace TidesOfTime.Battle;

public partial class BattleScene : Control
{
	[Export] public ShipLayoutDef PlayerLayout { get; set; } = null!;
	[Export] public ShipLayoutDef EnemyLayout { get; set; } = null!;

	private BattleState _battleState = null!;
	private ShipGridView _playerShipView = null!;
	private ShipGridView _enemyShipView = null!;
	private Control _background = null!;
	private Control _selectionPanel = null!;
	private Label _selectionSourceLabel = null!;
	private Label _selectionRoomLabel = null!;
	private Label _selectionSystemLabel = null!;
	private Button _primaryActionButton = null!;
	private Button _secondaryActionButton = null!;
	private Label _actionStatusLabel = null!;

	public override void _Ready()
	{
		_playerShipView = GetNode<ShipGridView>("MarginContainer/VBoxContainer/HBoxContainer/PlayerShipGridView");
		_enemyShipView = GetNode<ShipGridView>("MarginContainer/VBoxContainer/HBoxContainer/EnemyShipGridView");
		_background = GetNode<Control>("Background");
		_selectionPanel = GetNode<Control>("MarginContainer/VBoxContainer/SelectionPanel");
		_selectionSourceLabel = GetNode<Label>("MarginContainer/VBoxContainer/SelectionPanel/MarginContainer/VBoxContainer/SelectionDetailsScroll/SelectionDetailsContent/SelectionSourceLabel");
		_selectionRoomLabel = GetNode<Label>("MarginContainer/VBoxContainer/SelectionPanel/MarginContainer/VBoxContainer/SelectionDetailsScroll/SelectionDetailsContent/SelectionRoomLabel");
		_selectionSystemLabel = GetNode<Label>("MarginContainer/VBoxContainer/SelectionPanel/MarginContainer/VBoxContainer/SelectionDetailsScroll/SelectionDetailsContent/SelectionSystemLabel");
		_primaryActionButton = GetNode<Button>("MarginContainer/VBoxContainer/SelectionPanel/MarginContainer/VBoxContainer/ActionButtonRow/PrimaryActionButton");
		_secondaryActionButton = GetNode<Button>("MarginContainer/VBoxContainer/SelectionPanel/MarginContainer/VBoxContainer/ActionButtonRow/SecondaryActionButton");
		_actionStatusLabel = GetNode<Label>("MarginContainer/VBoxContainer/SelectionPanel/MarginContainer/VBoxContainer/ActionStatusLabel");

		if (PlayerLayout == null || EnemyLayout == null)
		{
			GD.PushError("BattleScene: PlayerLayout and EnemyLayout must be assigned.");
			return;
		}

		_battleState = BattleState.Create(PlayerLayout, EnemyLayout);
		_playerShipView.Render(_battleState.PlayerShip);
		_enemyShipView.Render(_battleState.EnemyShip);

		_primaryActionButton.Pressed += OnPrimaryActionPressed;
		_secondaryActionButton.Pressed += OnSecondaryActionPressed;
		_background.GuiInput += OnBackgroundGuiInput;
		_selectionPanel.GuiInput += OnBackgroundGuiInput;
		_playerShipView.TilePressed += (ship, x, y) => OnTilePressed("Player", ship, x, y);
		_playerShipView.BackgroundPressed += OnBoardBackgroundPressed;
		_playerShipView.CrewSelected += (ship, crew) => OnCrewSelected("Player", ship, crew);
		_enemyShipView.TilePressed += (ship, x, y) => OnTilePressed("Enemy", ship, x, y);
		_enemyShipView.BackgroundPressed += OnBoardBackgroundPressed;
		_enemyShipView.CrewSelected += (ship, crew) => OnCrewSelected("Enemy", ship, crew);
		ShowSelectionState(_battleState.CurrentSelection);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			ClearCurrentSelection();
		}
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
		if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			return;
		}

		ClearCurrentSelection();
	}

	private void ShowSelectionState(BattleSelection? selection)
	{
		if (selection == null)
		{
			_selectionSourceLabel.Text = "Ship: None";
			_selectionRoomLabel.Text = "Room: None";
			_selectionSystemLabel.Text = "System: None";
			UpdateActionArea(selection);
			return;
		}

		_selectionSourceLabel.Text = $"Ship: {selection.ShipSource} ({selection.Ship.Name})";

		if (selection.Kind == BattleSelectionKind.Crew && selection.Crew != null)
		{
			_selectionRoomLabel.Text = $"Crew: {selection.Crew.DisplayName} [{selection.Crew.ShortLabel}]";
			_selectionSystemLabel.Text = BuildCrewSelectionSummary(
				selection.Ship,
				GetShipCrewAllegiance(selection.Ship),
				selection.Crew,
				selection.Room);
		}
		else
		{
			_selectionRoomLabel.Text = $"Room: {selection.Room?.DisplayName ?? "None"}";
			_selectionSystemLabel.Text = BuildRoomSelectionSummary(
				selection.Ship,
				GetShipCrewAllegiance(selection.Ship),
				selection.Room);
		}

		UpdateActionArea(selection);
	}

	private void UpdateActionArea(BattleSelection? selection)
	{
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
			? $"{selection.Room.DisplayName} is disabled."
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
		RunPrototypeAction(_primaryActionButton);
	}

	private void OnSecondaryActionPressed()
	{
		RunPrototypeAction(_secondaryActionButton);
	}

	private void RunPrototypeAction(Button actionButton)
	{
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

	private static void ConfigureActionButton(Button button, string text, BattleActionKind? actionKind)
	{
		button.Text = text;
		button.Disabled = actionKind == null;

		if (actionKind == null)
		{
			button.RemoveMeta("action_kind");
			return;
		}

		button.SetMeta("action_kind", (int)actionKind.Value);
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

	private static string BuildCrewSelectionSummary(
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
			$"Crew Here: {FormatCrewList(companions, emptyText: "Alone")}";
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
}
