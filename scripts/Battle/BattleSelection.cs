using TidesOfTime.Crew;
using TidesOfTime.Ships;

namespace TidesOfTime.Battle;

public enum BattleSelectionKind
{
	Room,
	Crew
}

public sealed record BattleSelection(
	BattleSelectionKind Kind,
	string ShipSource,
	ShipState Ship,
	ShipRoomState? Room,
	CrewState? Crew)
{
	public static BattleSelection ForRoom(string shipSource, ShipState ship, ShipRoomState room)
	{
		return new BattleSelection(BattleSelectionKind.Room, shipSource, ship, room, null);
	}

	public static BattleSelection ForCrew(string shipSource, ShipState ship, CrewState crew, ShipRoomState? room)
	{
		return new BattleSelection(BattleSelectionKind.Crew, shipSource, ship, room, crew);
	}
}
