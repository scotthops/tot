using Godot;

namespace TidesOfTime.Data;

[GlobalClass]
public partial class AbilityDef : Resource
{
	[Export] public string AbilityId { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
}
