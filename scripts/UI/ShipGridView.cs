using Godot;
using System.Collections.Generic;
using TidesOfTime.Data;
using TidesOfTime.Ships;

namespace TidesOfTime.UI;

public partial class ShipGridView : PanelContainer
{
	[Export] public PackedScene? TileViewScene { get; set; }

	private Label _shipNameLabel = null!;
	private ProgressBar _hullBar = null!;
	private GridContainer _grid = null!;

	public override void _Ready()
	{
		_shipNameLabel = GetNode<Label>("MarginContainer/VBoxContainer/HeaderBar/ShipNameLabel");
		_hullBar = GetNode<ProgressBar>("MarginContainer/VBoxContainer/HullBar");
		_grid = GetNode<GridContainer>("MarginContainer/VBoxContainer/GridStack/Grid");

		if (TileViewScene == null)
		{
			GD.PushError("ShipGridView: TileViewScene is not assigned.");
		}
	}

	public void RenderFromLayout(ShipLayoutDef layout)
	{
		Render(ShipState.FromLayout(layout));
	}

	public void Render(ShipState shipState)
	{
		_shipNameLabel.Text = shipState.Name;
		_hullBar.Value = shipState.Hull;

		ClearGrid();
		_grid.Columns = shipState.Grid.Width;
		var roomById = BuildRoomIndex(shipState.Grid);

		foreach (var tile in shipState.Grid.Tiles)
		{
			if (TileViewScene == null)
			{
				GD.PushError("ShipGridView: TileViewScene is not assigned.");
				return;
			}

			var tileNode = TileViewScene.Instantiate<Button>();

			tileNode.Text = "";
			tileNode.CustomMinimumSize = new Vector2(36, 36);

			if (tile.Walkable)
			{
				var color = GetRoomColor(tile.RoomId, roomById);
				tileNode.Modulate = color;
			}
			else
			{
				tileNode.Modulate = new Color(0.15f, 0.15f, 0.15f);
				tileNode.Disabled = true;
			}

			_grid.AddChild(tileNode);
		}
	}

	private static Dictionary<string, ShipRoomState> BuildRoomIndex(ShipGridState gridState)
	{
		var roomById = new Dictionary<string, ShipRoomState>();
		foreach (var room in gridState.Rooms)
		{
			roomById[room.RoomId] = room;
		}

		return roomById;
	}

	private void ClearGrid()
	{
		foreach (Node child in _grid.GetChildren())
		{
			child.QueueFree();
		}
	}

	private Color GetRoomColor(string roomId, IReadOnlyDictionary<string, ShipRoomState> roomById)
	{
		if (!roomById.TryGetValue(roomId, out var room))
		{
			return new Color(0.6f, 0.6f, 0.6f);
		}

		return room.SystemType switch
		{
			"HelmRigging" => new Color(0.45f, 0.65f, 0.95f),
			"Cannons" => new Color(0.95f, 0.45f, 0.45f),
			"ThreadChamber" => new Color(0.55f, 0.35f, 0.75f),
			"CrowsNest" => new Color(0.9f, 0.8f, 0.35f),
			"DoctorsQuarters" => new Color(0.35f, 0.8f, 0.55f),
			_ => new Color(0.6f, 0.6f, 0.6f)
		};
	}
}
