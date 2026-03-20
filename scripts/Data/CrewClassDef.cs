using Godot;

namespace TidesOfTime.Data;

[GlobalClass]
public partial class CrewClassDef : Resource
{
	[Export] public string CrewClassId { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public Godot.Collections.Array<AbilityDef> StartingAbilities { get; set; } = new();
}
