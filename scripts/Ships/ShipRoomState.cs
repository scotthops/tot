using System.Collections.Generic;
using Godot;

namespace TidesOfTime.Ships;

public class ShipRoomState
{
	public const int MaxIntegrity = 100;

	public string RoomId { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public string SystemType { get; set; } = "";
	public List<Vector2I> Tiles { get; set; } = new();
	public int Integrity { get; private set; } = MaxIntegrity;
	public bool Disabled { get; private set; } = false;
	public bool IsDamaged => Integrity < MaxIntegrity;
	public bool IsOperational => !Disabled && Integrity > 0;

	public void ApplyDamage(int amount)
	{
		if (amount <= 0)
		{
			return;
		}

		Integrity = Mathf.Clamp(Integrity - amount, 0, MaxIntegrity);
		Disabled = Integrity <= 0;
	}
}
