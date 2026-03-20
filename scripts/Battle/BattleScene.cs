using Godot;
using TidesOfTime.Data;
using TidesOfTime.UI;

namespace TidesOfTime.Battle;

public partial class BattleScene : Control
{
	[Export] public ShipLayoutDef PlayerLayout { get; set; } = null!;
	[Export] public ShipLayoutDef EnemyLayout { get; set; } = null!;

	private ShipGridView _playerShipView = null!;
	private ShipGridView _enemyShipView = null!;

	public override void _Ready()
	{
		_playerShipView = GetNode<ShipGridView>("MarginContainer/HBoxContainer/PlayerShipGridView");
		_enemyShipView = GetNode<ShipGridView>("MarginContainer/HBoxContainer/EnemyShipGridView");

		if (PlayerLayout == null || EnemyLayout == null)
		{
			GD.PushError("BattleScene: PlayerLayout and EnemyLayout must be assigned.");
			return;
		}

		var battleState = BattleState.Create(PlayerLayout, EnemyLayout);
		_playerShipView.Render(battleState.PlayerShip);
		_enemyShipView.Render(battleState.EnemyShip);
	}
}
