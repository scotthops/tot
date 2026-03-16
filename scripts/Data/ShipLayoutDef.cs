using Godot;

namespace TidesOfTime.Data;

[GlobalClass]
public partial class ShipLayoutDef : Resource
{
	[Export] public string ShipName { get; set; } = "Ship";
	[Export] public int Width { get; set; } = 8;
	[Export] public int Height { get; set; } = 6;
	[Export] public Godot.Collections.Array<RoomDef> Rooms { get; set; } = new();
}
