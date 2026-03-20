namespace TidesOfTime.Crew;

public enum CrewAllegiance
{
	Player,
	Enemy
}

public enum ShipSide
{
	Player,
	Enemy
}

public sealed class CrewState
{
	public string Id { get; }
	public string DisplayName { get; }
	public string ShortLabel { get; }
	public string CrewClass { get; }
	public CrewAllegiance Allegiance { get; }
	public CrewPosition Position { get; private set; }

	public CrewState(
		string id,
		string displayName,
		string shortLabel,
		string crewClass,
		CrewAllegiance allegiance,
		CrewPosition position)
	{
		Id = id;
		DisplayName = displayName;
		ShortLabel = shortLabel;
		CrewClass = crewClass;
		Allegiance = allegiance;
		Position = position;
	}
}
