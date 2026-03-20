using TidesOfTime.Data;
using TidesOfTime.Ships;

namespace TidesOfTime.Battle;

public class BattleState
{
	public ShipState PlayerShip { get; }
	public ShipState EnemyShip { get; }
	public BattleSelection? CurrentSelection { get; private set; }
	public BattleActionIntent? LastIssuedIntent { get; private set; }

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

	public void SetSelection(string shipSource, ShipState ship, ShipRoomState? room)
	{
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
			: new BattleSelection(shipSource, ship, room);
		LastIssuedIntent = null;
	}

	public BattleActionIntent? CreateActionIntent(BattleActionKind kind)
	{
		if (CurrentSelection == null)
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
	}
}
