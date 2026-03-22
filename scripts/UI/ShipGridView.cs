using Godot;
using System;
using System.Collections.Generic;
using TidesOfTime.Crew;
using TidesOfTime.Data;
using TidesOfTime.Ships;

namespace TidesOfTime.UI;

public partial class ShipGridView : PanelContainer
{
	[Export] public PackedScene? TileViewScene { get; set; }

	public event Action<ShipState, int, int>? TilePressed;
	public event Action<ShipState, CrewState>? CrewSelected;

	private Label _shipNameLabel = null!;
	private ProgressBar _hullBar = null!;
	private Control _grid = null!;
	private Control _crewOverlay = null!;
	private ShipState? _shipState;
	private string? _selectedCrewId;
	private int _boardRenderRevision;

	public override void _Ready()
	{
		_shipNameLabel = GetNode<Label>("MarginContainer/VBoxContainer/HeaderBar/ShipNameLabel");
		_hullBar = GetNode<ProgressBar>("MarginContainer/VBoxContainer/HullBar");
		_grid = GetNode<Control>("MarginContainer/VBoxContainer/GridStack/Grid");
		_crewOverlay = GetNode<Control>("MarginContainer/VBoxContainer/GridStack/CrewOverlay");

		if (TileViewScene == null)
		{
			GD.PushError("ShipGridView: TileViewScene is not assigned.");
		}
	}

	public void RenderFromLayout(ShipLayoutDef layout)
	{
		Render(ShipState.FromLayout(layout));
	}

	public void Render(ShipState shipState, string? selectedCrewId = null)
	{
		_shipState = shipState;
		_selectedCrewId = selectedCrewId;
		_shipNameLabel.Text = shipState.Name;
		_hullBar.Value = shipState.Hull;

		var renderRevision = ++_boardRenderRevision;
		Callable.From(() => RebuildBoardDeferred(renderRevision)).CallDeferred();
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

	private void ClearBoard()
	{
		foreach (Node child in _grid.GetChildren())
		{
			child.QueueFree();
		}

		foreach (Node child in _crewOverlay.GetChildren())
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

		TilePressed?.Invoke(_shipState, x, y);
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

	private void RebuildBoardDeferred(int renderRevision)
	{
		if (_shipState == null || renderRevision != _boardRenderRevision)
		{
			return;
		}

		if (TileViewScene == null)
		{
			GD.PushError("ShipGridView: TileViewScene is not assigned.");
			return;
		}

		ClearBoard();
		var roomById = BuildRoomIndex(_shipState.Grid);
		var layout = CalculateBoardLayout(_shipState.Grid.Width, _shipState.Grid.Height);

		foreach (var tile in _shipState.Grid.Tiles)
		{
			var tileNode = TileViewScene.Instantiate<Button>();
			tileNode.Text = "";
			tileNode.Position = GetTilePosition(layout, tile.X, tile.Y);
			tileNode.Size = new Vector2(layout.TileSize, layout.TileSize);
			tileNode.CustomMinimumSize = tileNode.Size;
			tileNode.FocusMode = Control.FocusModeEnum.None;
			tileNode.Pressed += () => OnTilePressed(tile.X, tile.Y);

			if (tile.Walkable)
			{
				var room = roomById.GetValueOrDefault(tile.RoomId);
				var color = GetRoomColor(room);
				var isSelected = room?.RoomId == _shipState.SelectedRoomId;
				ApplyTileStyle(tileNode, color, isSelected);
			}
			else
			{
				ApplyTileStyle(tileNode, new Color(0.15f, 0.15f, 0.15f), false);
				tileNode.Disabled = true;
			}

			_grid.AddChild(tileNode);
		}

		foreach (var crew in _shipState.GetCrewOnBoard())
		{
			var marker = CreateCrewMarker(crew, crew.Id == _selectedCrewId);
			if (marker is Button markerButton)
			{
				markerButton.Pressed += () => OnCrewPressed(crew);
			}

			var tilePosition = GetTilePosition(layout, crew.Position.TileX, crew.Position.TileY);
			var tileSize = new Vector2(layout.TileSize, layout.TileSize);
			marker.Position = tilePosition + (tileSize - marker.Size) * 0.5f;
			_crewOverlay.AddChild(marker);
		}
	}

	private BoardLayout CalculateBoardLayout(int gridWidth, int gridHeight)
	{
		var tileSize = Mathf.Floor(Mathf.Min(
			_grid.Size.X / Mathf.Max(gridWidth, 1),
			_grid.Size.Y / Mathf.Max(gridHeight, 1)));

		tileSize = Mathf.Max(tileSize, 24.0f);

		var boardSize = new Vector2(tileSize * gridWidth, tileSize * gridHeight);
		var origin = (_grid.Size - boardSize) * 0.5f;

		return new BoardLayout(origin, tileSize);
	}

	private static Vector2 GetTilePosition(BoardLayout layout, int tileX, int tileY)
	{
		return layout.Origin + new Vector2(tileX * layout.TileSize, tileY * layout.TileSize);
	}

	private void OnCrewPressed(CrewState crew)
	{
		if (_shipState == null)
		{
			return;
		}

		CrewSelected?.Invoke(_shipState, crew);
	}

	private static Control CreateCrewMarker(CrewState crew, bool isSelected)
	{
		var marker = new Button
		{
			CustomMinimumSize = new Vector2(24, 24),
			Size = new Vector2(24, 24),
			MouseFilter = Control.MouseFilterEnum.Stop,
			FocusMode = Control.FocusModeEnum.None,
			Text = crew.ShortLabel
		};

		var style = new StyleBoxFlat
		{
			BgColor = GetCrewMarkerFillColor(crew),
			BorderColor = GetCrewMarkerBorderColor(crew, isSelected),
			BorderWidthLeft = isSelected ? 4 : 2,
			BorderWidthTop = isSelected ? 4 : 2,
			BorderWidthRight = isSelected ? 4 : 2,
			BorderWidthBottom = isSelected ? 4 : 2,
			CornerRadiusTopLeft = 12,
			CornerRadiusTopRight = 12,
			CornerRadiusBottomRight = 12,
			CornerRadiusBottomLeft = 12
		};
		marker.AddThemeStyleboxOverride("normal", style);
		marker.AddThemeStyleboxOverride("hover", style);
		marker.AddThemeStyleboxOverride("pressed", style);
		marker.AddThemeColorOverride("font_color", GetCrewMarkerTextColor(crew));
		marker.AddThemeColorOverride("font_outline_color", new Color(0.04f, 0.04f, 0.08f, 0.95f));
		marker.AddThemeConstantOverride("outline_size", 2);
		marker.AddThemeFontSizeOverride("font_size", 14);

		return marker;
	}

	private static Color GetCrewMarkerFillColor(CrewState crew)
	{
		return crew.Allegiance switch
		{
			CrewAllegiance.Player => new Color(0.17f, 0.35f, 0.62f, 0.95f),
			CrewAllegiance.Enemy => new Color(0.62f, 0.22f, 0.18f, 0.95f),
			_ => new Color(0.35f, 0.35f, 0.35f, 0.95f)
		};
	}

	private static Color GetCrewMarkerBorderColor(CrewState crew, bool isSelected)
	{
		if (isSelected)
		{
			return new Color(1.0f, 0.98f, 0.86f);
		}

		return crew.Allegiance switch
		{
			CrewAllegiance.Player => new Color(0.78f, 0.9f, 1.0f),
			CrewAllegiance.Enemy => new Color(1.0f, 0.82f, 0.78f),
			_ => new Color(0.8f, 0.8f, 0.8f)
		};
	}

	private static Color GetCrewMarkerTextColor(CrewState crew)
	{
		return crew.Allegiance switch
		{
			CrewAllegiance.Player => new Color(0.95f, 0.98f, 1.0f),
			CrewAllegiance.Enemy => new Color(1.0f, 0.95f, 0.93f),
			_ => Colors.White
		};
	}

	private readonly record struct BoardLayout(Vector2 Origin, float TileSize);
}
