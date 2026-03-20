using TidesOfTime.Ships;

namespace TidesOfTime.Battle;

public sealed record BattleSelection(
	string ShipSource,
	ShipState Ship,
	ShipRoomState Room);
