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
	private ShipState? _shipState;

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
		_shipState = shipState;
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
			tileNode.FocusMode = Control.FocusModeEnum.None;
			tileNode.Pressed += () => OnTilePressed(tile.X, tile.Y);

			if (tile.Walkable)
			{
				var room = roomById.GetValueOrDefault(tile.RoomId);
				var color = GetRoomColor(room);
				var isSelected = room?.RoomId == shipState.SelectedRoomId;
				ApplyTileStyle(tileNode, color, isSelected);
			}
			else
			{
				ApplyTileStyle(tileNode, new Color(0.15f, 0.15f, 0.15f), false);
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

	private void OnTilePressed(int x, int y)
	{
		if (_shipState == null)
		{
			return;
		}

		_shipState.SelectRoomAt(x, y);
		Render(_shipState);
	}

	private static void ApplyTileStyle(Button tileNode, Color fillColor, bool isSelected)
	{
		var style = new StyleBoxFlat
		{
			BgColor = fillColor,
			BorderWidthLeft = isSelected ? 3 : 1,
			BorderWidthTop = isSelected ? 3 : 1,
			BorderWidthRight = isSelected ? 3 : 1,
			BorderWidthBottom = isSelected ? 3 : 1,
			BorderColor = isSelected
				? new Color(0.98f, 0.98f, 0.92f)
				: fillColor.Darkened(0.35f),
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2
		};

		tileNode.AddThemeStyleboxOverride("normal", style);
		tileNode.AddThemeStyleboxOverride("hover", style);
		tileNode.AddThemeStyleboxOverride("pressed", style);
		tileNode.AddThemeStyleboxOverride("disabled", style);
	}

	private Color GetRoomColor(ShipRoomState? room)
	{
		if (room == null)
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
