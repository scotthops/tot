using Godot;

namespace TidesOfTime.Data;

[GlobalClass]
public partial class ShipModuleBayDef : Resource
{
	[Export] public string BayId { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public string DefaultRole { get; set; } = "";
	[Export] public Godot.Collections.Array<string> AllowedRoles { get; set; } = new();
	[Export] public Godot.Collections.Array<Vector2I> Tiles { get; set; } = new();
}
