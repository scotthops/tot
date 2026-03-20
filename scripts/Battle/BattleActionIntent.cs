namespace TidesOfTime.Battle;

public enum BattleActionKind
{
	TargetSystem,
	BoardRoom,
	RepairOrAssign,
	InspectSystem
}

public sealed record BattleAvailableAction(
	BattleActionKind Kind,
	string DisplayLabel);

public sealed record BattleActionIntent(
	BattleActionKind Kind,
	string ShipSource,
	string ShipName,
	string RoomId,
	string RoomDisplayName,
	string SystemType)
{
	public string ToStatusText()
	{
		return $"{ToDisplayLabel(Kind)}: {RoomDisplayName} ({SystemType}) on {ShipSource} ({ShipName})";
	}

	public static string ToDisplayLabel(BattleActionKind kind)
	{
		return kind switch
		{
			BattleActionKind.TargetSystem => "Target System",
			BattleActionKind.BoardRoom => "Board Room",
			BattleActionKind.RepairOrAssign => "Repair / Assign",
			BattleActionKind.InspectSystem => "Inspect System",
			_ => kind.ToString()
		};
	}
}
