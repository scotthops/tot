using Godot;

namespace TidesOfTime.Data;

[GlobalClass]
public partial class ShipArchetypeDef : Resource
{
	[Export] public string ArchetypeId { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "Ship";
	[Export] public ShipLayoutDef? CombatLayout { get; set; }
	[Export] public Color CombatTint { get; set; } = new(0.55f, 0.66f, 0.76f);
}
