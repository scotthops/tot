using Godot;
using TidesOfTime.Data;
using TidesOfTime.Ships;
using TidesOfTime.UI;

namespace TidesOfTime.Battle;

public partial class BattleScene : Control
{
	[Export] public ShipLayoutDef PlayerLayout { get; set; } = null!;
	[Export] public ShipLayoutDef EnemyLayout { get; set; } = null!;

	private ShipGridView _playerShipView = null!;
	private ShipGridView _enemyShipView = null!;
	private Label _selectionSourceLabel = null!;
	private Label _selectionRoomLabel = null!;
	private Label _selectionSystemLabel = null!;

	public override void _Ready()
	{
		_playerShipView = GetNode<ShipGridView>("MarginContainer/HBoxContainer/PlayerShipGridView");
		_enemyShipView = GetNode<ShipGridView>("MarginContainer/HBoxContainer/EnemyShipGridView");
		_selectionSourceLabel = GetNode<Label>("BattleHud/SelectionPanel/MarginContainer/VBoxContainer/SelectionSourceLabel");
		_selectionRoomLabel = GetNode<Label>("BattleHud/SelectionPanel/MarginContainer/VBoxContainer/SelectionRoomLabel");
		_selectionSystemLabel = GetNode<Label>("BattleHud/SelectionPanel/MarginContainer/VBoxContainer/SelectionSystemLabel");

		if (PlayerLayout == null || EnemyLayout == null)
		{
			GD.PushError("BattleScene: PlayerLayout and EnemyLayout must be assigned.");
			return;
		}

		var battleState = BattleState.Create(PlayerLayout, EnemyLayout);
		_playerShipView.Render(battleState.PlayerShip);
		_enemyShipView.Render(battleState.EnemyShip);

		_playerShipView.RoomSelected += (ship, room) => OnRoomSelected("Player", ship, room);
		_enemyShipView.RoomSelected += (ship, room) => OnRoomSelected("Enemy", ship, room);
		ShowSelectionState("None", null, null);
	}

	private void OnRoomSelected(string shipSource, ShipState ship, ShipRoomState? room)
	{
		ShowSelectionState(shipSource, ship, room);
	}

	private void ShowSelectionState(string shipSource, ShipState? ship, ShipRoomState? room)
	{
		_selectionSourceLabel.Text = $"Ship: {shipSource}{(ship != null ? $" ({ship.Name})" : "")}";
		_selectionRoomLabel.Text = $"Room: {room?.DisplayName ?? "None"}";
		_selectionSystemLabel.Text = $"System: {room?.SystemType ?? "None"}";
	}
}
