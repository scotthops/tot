using Godot;

namespace TidesOfTime.Data;

[GlobalClass]
public partial class ThreadDef : Resource
{
	[Export] public string ThreadId { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
}
