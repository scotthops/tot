using Godot;
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
	private string? _currentSelectionSource;
	private ShipState? _currentSelectedShip;
	private ShipRoomState? _currentSelectedRoom;

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
		_enemyShipView.RoomSelected += (ship, room) => OnRoomSelected("Enemy", ship, room);
		ShowSelectionState("None", null, null);
	}

	private void OnRoomSelected(string shipSource, ShipState ship, ShipRoomState? room)
	{
		_currentSelectionSource = shipSource;
		_currentSelectedShip = ship;
		_currentSelectedRoom = room;
		ClearOtherShipSelection(ship);
		ShowSelectionState(shipSource, ship, room);
	}

	private void ClearOtherShipSelection(ShipState selectedShip)
	{
		if (selectedShip == _battleState.PlayerShip)
		{
			_battleState.EnemyShip.ClearSelection();
			_enemyShipView.Render(_battleState.EnemyShip);
			return;
		}

		if (selectedShip == _battleState.EnemyShip)
		{
			_battleState.PlayerShip.ClearSelection();
			_playerShipView.Render(_battleState.PlayerShip);
		}
	}

	private void ShowSelectionState(string shipSource, ShipState? ship, ShipRoomState? room)
	{
		_selectionSourceLabel.Text = $"Ship: {shipSource}{(ship != null ? $" ({ship.Name})" : "")}";
		_selectionRoomLabel.Text = $"Room: {room?.DisplayName ?? "None"}";
		_selectionSystemLabel.Text = $"System: {room?.SystemType ?? "None"}";
		UpdateActionArea(shipSource, ship, room);
	}

	private void UpdateActionArea(string shipSource, ShipState? ship, ShipRoomState? room)
	{
		if (ship == null || room == null)
		{
			_primaryActionButton.Text = "Action 1";
			_secondaryActionButton.Text = "Action 2";
			_primaryActionButton.Disabled = true;
			_secondaryActionButton.Disabled = true;
			_actionStatusLabel.Text = "Select a room to see actions.";
			return;
		}

		if (shipSource == "Enemy")
		{
			_primaryActionButton.Text = "Target System";
			_secondaryActionButton.Text = "Board Room";
		}
		else
		{
			_primaryActionButton.Text = "Repair / Assign";
			_secondaryActionButton.Text = "Inspect System";
		}

		_primaryActionButton.Disabled = false;
		_secondaryActionButton.Disabled = false;
		_actionStatusLabel.Text = $"Ready: {room.DisplayName} on {ship.Name}";
	}

	private void OnPrimaryActionPressed()
	{
		RunPrototypeAction(_primaryActionButton.Text);
	}

	private void OnSecondaryActionPressed()
	{
		RunPrototypeAction(_secondaryActionButton.Text);
	}

	private void RunPrototypeAction(string actionName)
	{
		if (_currentSelectedShip == null || _currentSelectedRoom == null || string.IsNullOrEmpty(_currentSelectionSource))
		{
			_actionStatusLabel.Text = "Select a room first.";
			return;
		}

		_actionStatusLabel.Text =
			$"{actionName}: {_currentSelectedRoom.DisplayName} on {_currentSelectionSource} ({_currentSelectedShip.Name})";
	}
}
