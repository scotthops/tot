using TidesOfTime.Data;

namespace TidesOfTime.Encounters;

public sealed class SailingEncounterData
{
	public SailingEncounterData(
		ShipArchetypeDef? playerShip,
		ShipArchetypeDef? enemyShip,
		string returnScenePath)
	{
		PlayerShip = playerShip;
		EnemyShip = enemyShip;
		ReturnScenePath = returnScenePath;
	}

	public ShipArchetypeDef? PlayerShip { get; }
	public ShipArchetypeDef? EnemyShip { get; }
	public string ReturnScenePath { get; }

	public string PlayerDisplayName => GetDisplayName(PlayerShip, "Player Ship");
	public string EnemyDisplayName => GetDisplayName(EnemyShip, "Enemy Ship");

	private static string GetDisplayName(ShipArchetypeDef? archetype, string fallback)
	{
		return string.IsNullOrWhiteSpace(archetype?.DisplayName)
			? fallback
			: archetype.DisplayName;
	}
}
