using Godot;

namespace TidesOfTime.Data;

[GlobalClass]
public partial class RoomDef : Resource
{
	[Export] public string RoomId { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public string SystemType { get; set; } = "";
	[Export] public Godot.Collections.Array<Vector2I> Tiles { get; set; } = new();
}
