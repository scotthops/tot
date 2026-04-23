namespace TidesOfTime.Battle;

public enum BattleMovementFeedbackKind
{
	Queued,
	Succeeded,
	TileOccupied,
	Unreachable,
	InvalidDestination,
	WrongShip,
	NoMovableCrewSelected
}

public sealed record BattleMovementFeedback(
	BattleMovementFeedbackKind Kind,
	string CrewName,
	int TileX,
	int TileY)
{
	public string ToStatusText()
	{
		return Kind switch
		{
			BattleMovementFeedbackKind.Queued => $"{CrewName} is moving to ({TileX}, {TileY}).",
			BattleMovementFeedbackKind.Succeeded => $"{CrewName} moved to ({TileX}, {TileY}).",
			BattleMovementFeedbackKind.TileOccupied => $"Can't move {CrewName}: tile ({TileX}, {TileY}) is occupied.",
			BattleMovementFeedbackKind.Unreachable => $"Can't move {CrewName}: tile ({TileX}, {TileY}) is unreachable.",
			BattleMovementFeedbackKind.InvalidDestination => $"Can't move {CrewName}: tile ({TileX}, {TileY}) is not walkable.",
			BattleMovementFeedbackKind.WrongShip => $"Can't move {CrewName}: choose a tile on the same ship.",
			BattleMovementFeedbackKind.NoMovableCrewSelected => "Select a player crew member to move.",
			_ => "Movement unavailable."
		};
	}
}
