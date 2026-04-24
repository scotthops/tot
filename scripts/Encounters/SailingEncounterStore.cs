namespace TidesOfTime.Encounters;

public static class SailingEncounterStore
{
	private static SailingEncounterData? _pendingEncounter;

	public static void SetPendingEncounter(SailingEncounterData encounter)
	{
		_pendingEncounter = encounter;
	}

	public static SailingEncounterData? ConsumePendingEncounter()
	{
		var encounter = _pendingEncounter;
		_pendingEncounter = null;
		return encounter;
	}
}
