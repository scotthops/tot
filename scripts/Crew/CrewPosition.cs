namespace TidesOfTime.Crew;

public sealed record CrewPosition(
	ShipSide CurrentShipSide,
	int TileX,
	int TileY);
