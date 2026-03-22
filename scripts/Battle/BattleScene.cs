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
		_selectionSourceLabel = GetNode<Label>("MarginContainer/VBoxContainer/SelectionPanel/MarginContainer/VBoxContainer/SelectionSourceLabel");
		_selectionRoomLabel = GetNode<Label>("MarginContainer/VBoxContainer/SelectionPanel/MarginContainer/VBoxContainer/SelectionRoomLabel");
		_selectionSystemLabel = GetNode<Label>("MarginContainer/VBoxContainer/SelectionPanel/MarginContainer/VBoxContainer/SelectionSystemLabel");
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
		_playerShipView.RoomSelected += (ship, room) => OnRoomSelected("Player", ship, room);
		_playerShipView.CrewSelected += (ship, crew) => OnCrewSelected("Player", ship, crew);
		_enemyShipView.RoomSelected += (ship, room) => OnRoomSelected("Enemy", ship, room);
		_enemyShipView.CrewSelected += (ship, crew) => OnCrewSelected("Enemy", ship, crew);
		ShowSelectionState(_battleState.CurrentSelection);
	}

	private void OnRoomSelected(string shipSource, ShipState ship, ShipRoomState? room)
	{
		_battleState.SetSelection(shipSource, ship, room);
		RenderBattleViews();
		ShowSelectionState(_battleState.CurrentSelection);
	}

	private void OnCrewSelected(string shipSource, ShipState ship, CrewState crew)
	{
		_battleState.SetCrewSelection(shipSource, ship, crew);
		RenderBattleViews();
		ShowSelectionState(_battleState.CurrentSelection);
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
			_selectionSystemLabel.Text =
				$"Role: {selection.Crew.CrewClass} | Allegiance: {selection.Crew.Allegiance} | Room: {selection.Room?.DisplayName ?? "Deck"}";
		}
		else
		{
			_selectionRoomLabel.Text = $"Room: {selection.Room?.DisplayName ?? "None"}";
			_selectionSystemLabel.Text = $"System: {selection.Room?.SystemType ?? "None"}";
		}

		UpdateActionArea(selection);
	}

	private void UpdateActionArea(BattleSelection? selection)
	{
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
		_actionStatusLabel.Text = $"Ready: {selection.Room!.DisplayName} on {selection.Ship.Name}";
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
		var actionIntent = actionKind == null
			? null
			: _battleState.CreateActionIntent(actionKind.Value);

		if (actionIntent == null)
		{
			_actionStatusLabel.Text = "Select a room first.";
			return;
		}

		_battleState.SetLastIssuedIntent(actionIntent);
		_actionStatusLabel.Text = actionIntent.ToStatusText();
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
}
