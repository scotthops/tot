using System.Collections.Generic;
using Godot;

namespace TidesOfTime.Ships;

public class ShipModuleBayState
{
	public string BayId { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public string DefaultRole { get; set; } = "";
	public List<string> AllowedRoles { get; } = new();
	public List<Vector2I> Tiles { get; } = new();
}
