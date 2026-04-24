using Godot;
using System;
using System.Collections.Generic;
using TidesOfTime.Data;

namespace TidesOfTime.Battle;

public partial class RealBoatCombatGridView : Node3D
{
	[ExportGroup("Boat Visibility")]
	[Export] public NodePath BoatVisualRootPath { get; set; } = new("../BoatAnchor/PlayerBoatVisual");
	[Export] public bool HideTopDownObstructions { get; set; } = true;
	[Export] public string TopDownHiddenBoatNodeNames { get; set; } = "Mast,Sail";

	[ExportGroup("Grid Source")]
	[Export] public ShipLayoutDef? Layout { get; set; }
	[Export] public bool UseLayoutDimensions { get; set; } = true;
	[Export] public bool UseLayoutTilesOnly { get; set; } = true;
	[Export] public int Columns { get; set; } = 8;
	[Export] public int Rows { get; set; } = 6;

	[ExportGroup("Grid Placement")]
	[Export] public Vector2 GridOrigin { get; set; } = Vector2.Zero;
	[Export] public float GridHeightOffset { get; set; } = 0.86f;
	[Export] public Vector3 GridRotationDegrees { get; set; } = new(0.0f, 90.0f, 0.0f);
	[Export] public float TileSize { get; set; } = 0.38f;

	[ExportGroup("Grid Style")]
	[Export] public bool ShowGrid { get; set; } = true;
	[Export] public float TileGap { get; set; } = 0.035f;
	[Export] public float TilePanelThickness { get; set; } = 0.018f;
	[Export] public float GridLineWidth { get; set; } = 0.018f;
	[Export] public float GridLineHeight { get; set; } = 0.018f;
	[Export] public float TileOpacity { get; set; } = 0.28f;
	[Export] public float GridLineOpacity { get; set; } = 0.62f;
	[Export] public bool ShowRoomAccents { get; set; } = true;
	[Export] public float RoomAccentStrength { get; set; } = 0.42f;

	private static readonly Color BaseTileColor = new(0.58f, 0.47f, 0.31f);
	private static readonly Color GridLineColor = new(0.11f, 0.075f, 0.045f);

	private readonly List<Node> _generatedNodes = new();

	public override void _Ready()
	{
		BuildView();
	}

	public void BuildView()
	{
		ClearGeneratedNodes();
		ApplyBoatVisibility();

		if (!ShowGrid)
		{
			return;
		}

		var tileRooms = BuildTileRoomIndex();
		var columns = GetColumnCount();
		var rows = GetRowCount();

		if (columns <= 0 || rows <= 0)
		{
			GD.PushWarning("RealBoatCombatGridView: grid dimensions must be greater than zero.");
			return;
		}

		var gridRoot = new Node3D
		{
			Name = "TunableCombatGrid",
			Position = new Vector3(GridOrigin.X, GridHeightOffset, GridOrigin.Y),
			RotationDegrees = GridRotationDegrees
		};
		AddChild(gridRoot);
		_generatedNodes.Add(gridRoot);

		CreateTiles(gridRoot, tileRooms, columns, rows);
		CreateGridLines(gridRoot, tileRooms, columns, rows);
	}

	private void ClearGeneratedNodes()
	{
		foreach (var node in _generatedNodes)
		{
			if (IsInstanceValid(node))
			{
				node.QueueFree();
			}
		}

		_generatedNodes.Clear();
	}

	private void ApplyBoatVisibility()
	{
		var pathText = BoatVisualRootPath.ToString();
		if (string.IsNullOrWhiteSpace(pathText))
		{
			return;
		}

		var boatRoot = GetNodeOrNull<Node3D>(BoatVisualRootPath);
		if (boatRoot == null)
		{
			GD.PushWarning($"RealBoatCombatGridView: boat visual root '{pathText}' was not found.");
			return;
		}

		var hiddenNames = ParseNodeNameSet(TopDownHiddenBoatNodeNames);
		if (hiddenNames.Count == 0)
		{
			return;
		}

		SetNamedDescendantsVisible(boatRoot, hiddenNames, !HideTopDownObstructions);
	}

	private Dictionary<Vector2I, RoomDef> BuildTileRoomIndex()
	{
		var tileRooms = new Dictionary<Vector2I, RoomDef>();
		if (Layout == null)
		{
			return tileRooms;
		}

		foreach (var room in Layout.Rooms)
		{
			if (room == null)
			{
				continue;
			}

			foreach (var tile in room.Tiles)
			{
				if (tile.X < 0 || tile.X >= Layout.Width || tile.Y < 0 || tile.Y >= Layout.Height)
				{
					GD.PushWarning($"RealBoatCombatGridView: '{room.DisplayName}' has out-of-bounds tile {tile}.");
					continue;
				}

				if (tileRooms.ContainsKey(tile))
				{
					GD.PushWarning($"RealBoatCombatGridView: tile {tile} is assigned to multiple rooms.");
				}

				tileRooms[tile] = room;
			}
		}

		return tileRooms;
	}

	private void CreateTiles(
		Node3D gridRoot,
		Dictionary<Vector2I, RoomDef> tileRooms,
		int columns,
		int rows)
	{
		var panelSize = Mathf.Max(0.02f, TileSize - TileGap);
		var panelThickness = Mathf.Max(0.004f, TilePanelThickness);

		for (var y = 0; y < rows; y++)
		{
			for (var x = 0; x < columns; x++)
			{
				var tile = new Vector2I(x, y);
				var hasRoom = tileRooms.TryGetValue(tile, out var room);
				if (UseLayoutTilesOnly && Layout != null && !hasRoom)
				{
					continue;
				}

				CreateBox(
					gridRoot,
					$"Tile_{x}_{y}",
					new Vector3(panelSize, panelThickness, panelSize),
					MapTileToGridLocal(tile, columns, rows),
					GetTileColor(room));
			}
		}
	}

	private void CreateGridLines(
		Node3D gridRoot,
		Dictionary<Vector2I, RoomDef> tileRooms,
		int columns,
		int rows)
	{
		var createdEdges = new HashSet<string>();
		var lineWidth = Mathf.Max(0.004f, GridLineWidth);
		var lineHeight = Mathf.Max(0.004f, GridLineHeight);
		var lineY = (Mathf.Max(0.004f, TilePanelThickness) * 0.5f) + (lineHeight * 0.5f) + 0.004f;
		var lineColor = new Color(GridLineColor.R, GridLineColor.G, GridLineColor.B, Mathf.Clamp(GridLineOpacity, 0.0f, 1.0f));

		for (var y = 0; y < rows; y++)
		{
			for (var x = 0; x < columns; x++)
			{
				var tile = new Vector2I(x, y);
				if (UseLayoutTilesOnly && Layout != null && !tileRooms.ContainsKey(tile))
				{
					continue;
				}

				var center = MapTileToGridLocal(tile, columns, rows);
				AddHorizontalEdge(gridRoot, createdEdges, $"H:{x}:{y}", center + new Vector3(0.0f, lineY, -TileSize * 0.5f), lineWidth, lineHeight, lineColor);
				AddHorizontalEdge(gridRoot, createdEdges, $"H:{x}:{y + 1}", center + new Vector3(0.0f, lineY, TileSize * 0.5f), lineWidth, lineHeight, lineColor);
				AddVerticalEdge(gridRoot, createdEdges, $"V:{x}:{y}", center + new Vector3(-TileSize * 0.5f, lineY, 0.0f), lineWidth, lineHeight, lineColor);
				AddVerticalEdge(gridRoot, createdEdges, $"V:{x + 1}:{y}", center + new Vector3(TileSize * 0.5f, lineY, 0.0f), lineWidth, lineHeight, lineColor);
			}
		}
	}

	private void AddHorizontalEdge(
		Node3D gridRoot,
		HashSet<string> createdEdges,
		string edgeKey,
		Vector3 position,
		float lineWidth,
		float lineHeight,
		Color color)
	{
		if (!createdEdges.Add(edgeKey))
		{
			return;
		}

		CreateBox(
			gridRoot,
			$"GridLine_{edgeKey}",
			new Vector3(TileSize + lineWidth, lineHeight, lineWidth),
			position,
			color);
	}

	private void AddVerticalEdge(
		Node3D gridRoot,
		HashSet<string> createdEdges,
		string edgeKey,
		Vector3 position,
		float lineWidth,
		float lineHeight,
		Color color)
	{
		if (!createdEdges.Add(edgeKey))
		{
			return;
		}

		CreateBox(
			gridRoot,
			$"GridLine_{edgeKey}",
			new Vector3(lineWidth, lineHeight, TileSize + lineWidth),
			position,
			color);
	}

	private void CreateBox(Node3D parent, string nodeName, Vector3 size, Vector3 position, Color color)
	{
		var node = new MeshInstance3D
		{
			Name = nodeName,
			Mesh = new BoxMesh { Size = size },
			Position = position,
			MaterialOverride = CreateTransparentMaterial(color)
		};

		parent.AddChild(node);
	}

	private Vector3 MapTileToGridLocal(Vector2I tile, int columns, int rows)
	{
		var originX = -((columns - 1) * TileSize * 0.5f);
		var originZ = -((rows - 1) * TileSize * 0.5f);

		return new Vector3(
			originX + (tile.X * TileSize),
			0.0f,
			originZ + (tile.Y * TileSize));
	}

	private int GetColumnCount()
	{
		if (UseLayoutDimensions && Layout != null)
		{
			return Mathf.Max(1, Layout.Width);
		}

		return Mathf.Max(1, Columns);
	}

	private int GetRowCount()
	{
		if (UseLayoutDimensions && Layout != null)
		{
			return Mathf.Max(1, Layout.Height);
		}

		return Mathf.Max(1, Rows);
	}

	private Color GetTileColor(RoomDef? room)
	{
		var alpha = Mathf.Clamp(TileOpacity, 0.0f, 1.0f);
		var color = BaseTileColor;

		if (ShowRoomAccents && room != null)
		{
			color = color.Lerp(GetSystemAccentColor(room), Mathf.Clamp(RoomAccentStrength, 0.0f, 1.0f));
		}

		return new Color(color.R, color.G, color.B, alpha);
	}

	private static HashSet<string> ParseNodeNameSet(string names)
	{
		var parsedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var rawName in names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			parsedNames.Add(rawName);
		}

		return parsedNames;
	}

	private static void SetNamedDescendantsVisible(Node node, HashSet<string> names, bool visible)
	{
		if (node is Node3D node3D && names.Contains(node.Name))
		{
			node3D.Visible = visible;
		}

		foreach (var child in node.GetChildren())
		{
			SetNamedDescendantsVisible(child, names, visible);
		}
	}

	private static StandardMaterial3D CreateTransparentMaterial(Color color)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			Roughness = 0.82f
		};
	}

	private static Color GetSystemAccentColor(RoomDef room)
	{
		return room.SystemType switch
		{
			"HelmRigging" => new Color(0.34f, 0.48f, 0.68f),
			"Cannons" => new Color(0.58f, 0.26f, 0.22f),
			"ThreadChamber" => new Color(0.42f, 0.30f, 0.56f),
			"CrowsNest" => new Color(0.62f, 0.52f, 0.22f),
			"DoctorsQuarters" => new Color(0.25f, 0.50f, 0.34f),
			_ => new Color(0.48f, 0.48f, 0.44f)
		};
	}
}
