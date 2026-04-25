using Godot;

namespace TidesOfTime.Data;

[GlobalClass]
public partial class ShipLayoutDef : Resource
{
	[Export] public string ShipName { get; set; } = "Ship";
	[Export] public int Width { get; set; } = 8;
	[Export] public int Height { get; set; } = 6;
	[Export] public Godot.Collections.Array<Vector2I> OpenDeckTiles { get; set; } = new();
	[Export] public Godot.Collections.Array<Vector2I> ObstacleTiles { get; set; } = new();
	[Export] public Godot.Collections.Array<RoomDef> Rooms { get; set; } = new();
	[Export] public Godot.Collections.Array<ShipModuleBayDef> ModuleBays { get; set; } = new();
}
