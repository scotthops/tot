using Godot;
using TidesOfTime.Crew;
using TidesOfTime.Data;
using TidesOfTime.Ships;
using TidesOfTime.UI;

namespace TidesOfTime.Prototypes;

public partial class SchematicShipGridViewPrototype : Control
{
	[Export] public ShipLayoutDef Layout { get; set; } = null!;

	private SchematicShipGridView _shipView = null!;
	private ShipState? _shipState;
	private string? _selectedCrewId;

	public override void _Ready()
	{
		_shipView = GetNode<SchematicShipGridView>("MarginContainer/SchematicShipGridView");
		_shipView.TilePressed += OnTilePressed;
		_shipView.CrewSelected += OnCrewSelected;
		_shipView.BackgroundPressed += OnBackgroundPressed;
		BuildPrototypeShip();
	}

	private void BuildPrototypeShip()
	{
		if (Layout == null)
		{
			GD.PushError("SchematicShipGridViewPrototype: Layout must be assigned.");
			return;
		}

		_shipState = ShipState.FromLayout(Layout);
		SeedDisplayCrew(_shipState);
		SelectFirstRoom(_shipState);
		_shipView.Render(_shipState, _selectedCrewId);
	}

	private void OnTilePressed(ShipState ship, int tileX, int tileY)
	{
		_selectedCrewId = null;
		ship.SelectRoomAt(tileX, tileY);
		_shipView.Render(ship, _selectedCrewId);
	}

	private void OnCrewSelected(ShipState ship, CrewState crew)
	{
		_selectedCrewId = crew.Id;
		ship.ClearSelection();
		_shipView.Render(ship, _selectedCrewId);
	}

	private void OnBackgroundPressed(ShipState ship)
	{
		_selectedCrewId = null;
		ship.ClearSelection();
		_shipView.Render(ship, _selectedCrewId);
	}

	private static void SeedDisplayCrew(ShipState ship)
	{
		var crewConfigs = new (string Id, string Name, string ShortLabel, string CrewClass)[]
		{
			("captain", "Captain Mara", "C", "Captain"),
			("gunner", "Gunner Flint", "G", "Gunner"),
			("surgeon", "Surgeon Vale", "S", "Surgeon")
		};

		for (var i = 0; i < ship.Grid.Rooms.Count && i < crewConfigs.Length; i++)
		{
			var room = ship.Grid.Rooms[i];
			if (room.Tiles.Count == 0)
			{
				continue;
			}

			var tile = room.Tiles[0];
			var crewConfig = crewConfigs[i];
			ship.Crew.Add(new CrewState(
				$"schematic-{crewConfig.Id}",
				crewConfig.Name,
				crewConfig.ShortLabel,
				crewConfig.CrewClass,
				CrewAllegiance.Player,
				new CrewPosition(ShipSide.Player, tile.X, tile.Y)));
		}
	}

	private static void SelectFirstRoom(ShipState ship)
	{
		foreach (var room in ship.Grid.Rooms)
		{
			if (room.Tiles.Count == 0)
			{
				continue;
			}

			var tile = room.Tiles[0];
			ship.SelectRoomAt(tile.X, tile.Y);
			return;
		}
	}
}
