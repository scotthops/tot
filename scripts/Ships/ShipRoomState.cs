using System.Collections.Generic;
using Godot;

namespace TidesOfTime.Ships;

public class ShipRoomState
{
	public string RoomId { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public string SystemType { get; set; } = "";
	public List<Vector2I> Tiles { get; set; } = new();
	public int Integrity { get; set; } = 100;
	public bool Disabled { get; set; } = false;
}
