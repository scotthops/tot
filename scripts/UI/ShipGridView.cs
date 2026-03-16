using Godot;
using TidesOfTime.Data;
using TidesOfTime.Ships;

namespace TidesOfTime.UI;

public partial class ShipGridView : PanelContainer
{
	[Export] public PackedScene TileViewScene { get; set; }

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
		var gridState = ShipStateFactory.CreateGridState(layout);
		Render(layout.ShipName, 100, gridState);
	}

	public void Render(string shipName, int hull, ShipGridState gridState)
	{
		_shipNameLabel.Text = shipName;
		_hullBar.Value = hull;

		ClearGrid();
		_grid.Columns = gridState.Width;

		foreach (var tile in gridState.Tiles)
		{
			var tileNode = TileViewScene.Instantiate<Button>();

			tileNode.Text = "";
			tileNode.CustomMinimumSize = new Vector2(36, 36);

			if (tile.Walkable)
			{
				var color = GetRoomColor(tile.RoomId);
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

	private void ClearGrid()
	{
		foreach (Node child in _grid.GetChildren())
		{
			child.QueueFree();
		}
	}

	private Color GetRoomColor(string roomId)
	{
		return roomId switch
		{
			"helm" => new Color(0.45f, 0.65f, 0.95f),
			"cannons" => new Color(0.95f, 0.45f, 0.45f),
			"thread" => new Color(0.55f, 0.35f, 0.75f),
			"crowsnest" => new Color(0.9f, 0.8f, 0.35f),
			"doctor" => new Color(0.35f, 0.8f, 0.55f),
			_ => new Color(0.6f, 0.6f, 0.6f)
		};
	}
}
