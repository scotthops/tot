using TidesOfTime.Data;
using TidesOfTime.Ships;

namespace TidesOfTime.Battle;

public class BattleState
{
	public ShipState PlayerShip { get; }
	public ShipState EnemyShip { get; }

	public BattleState(ShipState playerShip, ShipState enemyShip)
	{
		PlayerShip = playerShip;
		EnemyShip = enemyShip;
	}

	public static BattleState Create(ShipLayoutDef playerLayout, ShipLayoutDef enemyLayout)
	{
		return new BattleState(
			ShipState.FromLayout(playerLayout),
			ShipState.FromLayout(enemyLayout));
	}
}
