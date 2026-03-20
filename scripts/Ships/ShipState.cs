using TidesOfTime.Data;

namespace TidesOfTime.Ships;

public class ShipState
{
	public string Name { get; }
	public int Hull { get; set; }
	public ShipGridState Grid { get; }

	public ShipState(string name, int hull, ShipGridState grid)
	{
		Name = name;
		Hull = hull;
		Grid = grid;
	}

	public static ShipState FromLayout(ShipLayoutDef layout, int hull = 100)
	{
		return new ShipState(layout.ShipName, hull, ShipStateFactory.CreateGridState(layout));
	}
}
