using Godot;
using TidesOfTime.Data;
using TidesOfTime.UI;

namespace TidesOfTime.Battle;

public partial class BattleScene : Control
{
	private ShipGridView _playerShipView = null!;
	private ShipGridView _enemyShipView = null!;

	public override void _Ready()
	{
		_playerShipView = GetNode<ShipGridView>("MarginContainer/HBoxContainer/PlayerShipGridView");
		_enemyShipView = GetNode<ShipGridView>("MarginContainer/HBoxContainer/EnemyShipGridView");

		var playerLayout = CreatePlayerTestLayout();
		var enemyLayout = CreateEnemyTestLayout();

		_playerShipView.RenderFromLayout(playerLayout);
		_enemyShipView.RenderFromLayout(enemyLayout);
	}

	private ShipLayoutDef CreatePlayerTestLayout()
	{
		var layout = new ShipLayoutDef
		{
			ShipName = "The Tidebreaker",
			Width = 8,
			Height = 6
		};

		layout.Rooms.Add(MakeRoom("helm", "Helm / Rigging", "HelmRigging",
			new Vector2I(1, 1), new Vector2I(2, 1),
			new Vector2I(1, 2), new Vector2I(2, 2)));

		layout.Rooms.Add(MakeRoom("cannons", "Cannons", "Cannons",
			new Vector2I(4, 1), new Vector2I(5, 1),
			new Vector2I(4, 2), new Vector2I(5, 2)));

		layout.Rooms.Add(MakeRoom("thread", "Thread Chamber", "ThreadChamber",
			new Vector2I(3, 3), new Vector2I(4, 3),
			new Vector2I(3, 4), new Vector2I(4, 4)));

		layout.Rooms.Add(MakeRoom("crowsnest", "Crow's Nest", "CrowsNest",
			new Vector2I(1, 4), new Vector2I(2, 4)));

		layout.Rooms.Add(MakeRoom("doctor", "Doctor's Quarters", "DoctorsQuarters",
			new Vector2I(5, 4), new Vector2I(6, 4)));

		return layout;
	}

	private ShipLayoutDef CreateEnemyTestLayout()
	{
		var layout = new ShipLayoutDef
		{
			ShipName = "The Black Wake",
			Width = 8,
			Height = 6
		};

		layout.Rooms.Add(MakeRoom("helm", "Helm / Rigging", "HelmRigging",
			new Vector2I(5, 1), new Vector2I(6, 1),
			new Vector2I(5, 2), new Vector2I(6, 2)));

		layout.Rooms.Add(MakeRoom("cannons", "Cannons", "Cannons",
			new Vector2I(2, 1), new Vector2I(3, 1),
			new Vector2I(2, 2), new Vector2I(3, 2)));

		layout.Rooms.Add(MakeRoom("thread", "Thread Chamber", "ThreadChamber",
			new Vector2I(3, 3), new Vector2I(4, 3),
			new Vector2I(3, 4), new Vector2I(4, 4)));

		layout.Rooms.Add(MakeRoom("crowsnest", "Crow's Nest", "CrowsNest",
			new Vector2I(1, 3), new Vector2I(1, 4)));

		layout.Rooms.Add(MakeRoom("doctor", "Doctor's Quarters", "DoctorsQuarters",
			new Vector2I(6, 3), new Vector2I(6, 4)));

		return layout;
	}

	private RoomDef MakeRoom(string roomId, string displayName, string systemType, params Vector2I[] tiles)
	{
		var room = new RoomDef
		{
			RoomId = roomId,
			DisplayName = displayName,
			SystemType = systemType
		};

		foreach (var tile in tiles)
		{
			room.Tiles.Add(tile);
		}

		return room;
	}
}
