using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using TidesOfTime.Data;

namespace TidesOfTime.Battle;

public partial class RealBoatCombatGridView : Node3D
{
	[ExportGroup("Boat Visibility")]
	[Export] public NodePath BoatVisualRootPath { get; set; } = new("../BoatAnchor/PlayerBoatVisual");
	[Export] public bool HideTopDownObstructions { get; set; } = true;
	[Export] public string TopDownHiddenBoatNodeNames { get; set; } = "Mast,Sail";
	[Export] public string FadedBoatNodeNames { get; set; } = "Cabin";
	[Export] public float FadedBoatOpacity { get; set; } = 0.26f;
	[Export] public Color FadedBoatTint { get; set; } = new(0.62f, 0.39f, 0.18f);

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
	[Export] public float RoomBorderLineWidth { get; set; } = 0.026f;
	[Export] public float RoomBorderOpacity { get; set; } = 0.58f;
	[Export] public bool EmphasizeRoomBorders { get; set; } = true;
	[Export] public bool ShowRoomAccents { get; set; } = true;
	[Export] public float RoomAccentStrength { get; set; } = 0.42f;

	[ExportGroup("Hatch Access")]
	[Export] public bool ShowHatchAccessMarker { get; set; } = true;
	[Export] public string HatchRoomId { get; set; } = "doctor";
	[Export] public Vector2I HatchTileOverride { get; set; } = new(-1, -1);
	[Export] public float HatchMarkerSizeRatio { get; set; } = 0.62f;
	[Export] public float HatchMarkerLineWidth { get; set; } = 0.035f;
	[Export] public float HatchMarkerOpacity { get; set; } = 0.82f;

	[ExportGroup("Runtime Tuning")]
	[Export] public bool EnableRuntimeTuning { get; set; } = true;
	[Export] public NodePath TuningReadoutLabelPath { get; set; } = new("");
	[Export] public bool ShowTuningReadout { get; set; } = true;
	[Export] public float OriginNudgeStep { get; set; } = 0.02f;
	[Export] public float LargeNudgeMultiplier { get; set; } = 5.0f;
	[Export] public float TileSizeNudgeStep { get; set; } = 0.01f;
	[Export] public float HeightNudgeStep { get; set; } = 0.02f;

	private static readonly Color BaseTileColor = new(0.58f, 0.47f, 0.31f);
	private static readonly Color GridLineColor = new(0.11f, 0.075f, 0.045f);
	private static readonly Color HatchMarkerColor = new(0.07f, 0.045f, 0.025f);

	private readonly List<Node> _generatedNodes = new();
	private Label? _tuningReadoutLabel;

	public override void _Ready()
	{
		_tuningReadoutLabel = GetNodeOrNull<Label>(TuningReadoutLabelPath);
		SetProcessUnhandledInput(EnableRuntimeTuning);
		BuildView();
		UpdateTuningReadout();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!EnableRuntimeTuning || @event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
		{
			return;
		}

		var changed = false;
		var handled = true;
		var originStep = OriginNudgeStep * (keyEvent.ShiftPressed ? LargeNudgeMultiplier : 1.0f);
		var tileSizeStep = TileSizeNudgeStep * (keyEvent.ShiftPressed ? LargeNudgeMultiplier : 1.0f);
		var heightStep = HeightNudgeStep * (keyEvent.ShiftPressed ? LargeNudgeMultiplier : 1.0f);

		switch (keyEvent.Keycode)
		{
			case Key.Left:
				GridOrigin += new Vector2(-originStep, 0.0f);
				changed = true;
				break;
			case Key.Right:
				GridOrigin += new Vector2(originStep, 0.0f);
				changed = true;
				break;
			case Key.Up:
				GridOrigin += new Vector2(0.0f, -originStep);
				changed = true;
				break;
			case Key.Down:
				GridOrigin += new Vector2(0.0f, originStep);
				changed = true;
				break;
			case Key.Q:
				TileSize = Mathf.Max(0.05f, TileSize - tileSizeStep);
				changed = true;
				break;
			case Key.E:
				TileSize += tileSizeStep;
				changed = true;
				break;
			case Key.R:
				GridHeightOffset += heightStep;
				changed = true;
				break;
			case Key.F:
				GridHeightOffset = Mathf.Max(0.0f, GridHeightOffset - heightStep);
				changed = true;
				break;
			case Key.G:
				ShowGrid = !ShowGrid;
				changed = true;
				break;
			case Key.B:
				EmphasizeRoomBorders = !EmphasizeRoomBorders;
				changed = true;
				break;
			case Key.P:
				PrintCurrentTuningValues();
				UpdateTuningReadout();
				break;
			default:
				handled = false;
				break;
		}

		if (changed)
		{
			BuildView();
			UpdateTuningReadout();
		}

		if (handled)
		{
			GetViewport().SetInputAsHandled();
		}
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
		CreateHatchAccessMarker(gridRoot, tileRooms, columns, rows);
	}

	private void ClearGeneratedNodes()
	{
		foreach (var node in _generatedNodes)
		{
			if (IsInstanceValid(node))
			{
				node.GetParent()?.RemoveChild(node);
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
		if (hiddenNames.Count > 0)
		{
			SetNamedDescendantsVisible(boatRoot, hiddenNames, !HideTopDownObstructions);
		}

		var fadedNames = ParseNodeNameSet(FadedBoatNodeNames);
		if (fadedNames.Count > 0)
		{
			SetNamedDescendantsFaded(boatRoot, fadedNames, FadedBoatTint, FadedBoatOpacity);
		}
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

				var center = MapTileToGridLocal(tile, columns, rows);
				AddGridEdge(
					gridRoot,
					tileRooms,
					createdEdges,
					tile,
					room,
					Vector2I.Up,
					$"H:{x}:{y}",
					center + new Vector3(0.0f, 0.0f, -TileSize * 0.5f),
					isHorizontal: true);
				AddGridEdge(
					gridRoot,
					tileRooms,
					createdEdges,
					tile,
					room,
					Vector2I.Down,
					$"H:{x}:{y + 1}",
					center + new Vector3(0.0f, 0.0f, TileSize * 0.5f),
					isHorizontal: true);
				AddGridEdge(
					gridRoot,
					tileRooms,
					createdEdges,
					tile,
					room,
					Vector2I.Left,
					$"V:{x}:{y}",
					center + new Vector3(-TileSize * 0.5f, 0.0f, 0.0f),
					isHorizontal: false);
				AddGridEdge(
					gridRoot,
					tileRooms,
					createdEdges,
					tile,
					room,
					Vector2I.Right,
					$"V:{x + 1}:{y}",
					center + new Vector3(TileSize * 0.5f, 0.0f, 0.0f),
					isHorizontal: false);
			}
		}
	}

	private void AddGridEdge(
		Node3D gridRoot,
		Dictionary<Vector2I, RoomDef> tileRooms,
		HashSet<string> createdEdges,
		Vector2I tile,
		RoomDef? room,
		Vector2I neighborOffset,
		string edgeKey,
		Vector3 position,
		bool isHorizontal)
	{
		if (!createdEdges.Add(edgeKey))
		{
			return;
		}

		var neighborTile = tile + neighborOffset;
		var sameRoom = room != null &&
			tileRooms.TryGetValue(neighborTile, out var neighborRoom) &&
			neighborRoom.RoomId == room.RoomId;
		var isRoomBorder = EmphasizeRoomBorders && (room == null || !sameRoom);
		var lineWidth = Mathf.Max(0.004f, isRoomBorder ? RoomBorderLineWidth : GridLineWidth);
		var lineHeight = Mathf.Max(0.004f, GridLineHeight);
		var lineY = (Mathf.Max(0.004f, TilePanelThickness) * 0.5f) + (lineHeight * 0.5f) + 0.004f;
		var lineOpacity = isRoomBorder ? RoomBorderOpacity : GridLineOpacity;
		var lineColor = new Color(
			GridLineColor.R,
			GridLineColor.G,
			GridLineColor.B,
			Mathf.Clamp(lineOpacity, 0.0f, 1.0f));
		var size = isHorizontal
			? new Vector3(TileSize + lineWidth, lineHeight, lineWidth)
			: new Vector3(lineWidth, lineHeight, TileSize + lineWidth);

		CreateBox(
			gridRoot,
			isRoomBorder ? $"RoomBorder_{edgeKey}" : $"GridLine_{edgeKey}",
			size,
			position + new Vector3(0.0f, lineY, 0.0f),
			lineColor);
	}

	private void CreateHatchAccessMarker(
		Node3D gridRoot,
		Dictionary<Vector2I, RoomDef> tileRooms,
		int columns,
		int rows)
	{
		if (!ShowHatchAccessMarker || !TryFindHatchTile(tileRooms, out var hatchTile))
		{
			return;
		}

		var center = MapTileToGridLocal(hatchTile, columns, rows);
		var lineHeight = Mathf.Max(0.004f, GridLineHeight);
		var lineWidth = Mathf.Max(0.006f, HatchMarkerLineWidth);
		var markerY = (Mathf.Max(0.004f, TilePanelThickness) * 0.5f) + (lineHeight * 0.5f) + 0.018f;
		var frameSize = TileSize * Mathf.Clamp(HatchMarkerSizeRatio, 0.2f, 0.9f);
		var halfFrame = frameSize * 0.5f;
		var markerColor = new Color(
			HatchMarkerColor.R,
			HatchMarkerColor.G,
			HatchMarkerColor.B,
			Mathf.Clamp(HatchMarkerOpacity, 0.0f, 1.0f));

		CreateBox(
			gridRoot,
			"HatchFrame_North",
			new Vector3(frameSize, lineHeight, lineWidth),
			center + new Vector3(0.0f, markerY, -halfFrame),
			markerColor);
		CreateBox(
			gridRoot,
			"HatchFrame_South",
			new Vector3(frameSize, lineHeight, lineWidth),
			center + new Vector3(0.0f, markerY, halfFrame),
			markerColor);
		CreateBox(
			gridRoot,
			"HatchFrame_West",
			new Vector3(lineWidth, lineHeight, frameSize),
			center + new Vector3(-halfFrame, markerY, 0.0f),
			markerColor);
		CreateBox(
			gridRoot,
			"HatchFrame_East",
			new Vector3(lineWidth, lineHeight, frameSize),
			center + new Vector3(halfFrame, markerY, 0.0f),
			markerColor);

		var slatWidth = frameSize * 0.58f;
		var slatGap = frameSize * 0.18f;
		for (var i = 0; i < 3; i++)
		{
			CreateBox(
				gridRoot,
				$"HatchStep_{i + 1}",
				new Vector3(slatWidth, lineHeight, lineWidth * 0.78f),
				center + new Vector3(0.0f, markerY + 0.004f, (i - 1) * slatGap),
				markerColor);
		}
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

		if (IsHatchRoom(room))
		{
			color = color.Darkened(0.34f);
			alpha = Mathf.Clamp(alpha + 0.1f, 0.0f, 1.0f);
		}

		return new Color(color.R, color.G, color.B, alpha);
	}

	private bool TryFindHatchTile(Dictionary<Vector2I, RoomDef> tileRooms, out Vector2I hatchTile)
	{
		hatchTile = Vector2I.Zero;

		if (tileRooms.TryGetValue(HatchTileOverride, out var overrideRoom) && IsHatchRoom(overrideRoom))
		{
			hatchTile = HatchTileOverride;
			return true;
		}

		var hatchTileCount = 0;
		var hatchTileSum = Vector2.Zero;
		foreach (var (tile, room) in tileRooms)
		{
			if (!IsHatchRoom(room))
			{
				continue;
			}

			hatchTileCount++;
			hatchTileSum += new Vector2(tile.X, tile.Y);
		}

		if (hatchTileCount == 0)
		{
			return false;
		}

		var hatchCenter = hatchTileSum / hatchTileCount;
		var bestDistanceSquared = float.MaxValue;
		foreach (var (tile, room) in tileRooms)
		{
			if (!IsHatchRoom(room))
			{
				continue;
			}

			var tileCenter = new Vector2(tile.X, tile.Y);
			var distanceSquared = tileCenter.DistanceSquaredTo(hatchCenter);
			if (distanceSquared >= bestDistanceSquared)
			{
				continue;
			}

			bestDistanceSquared = distanceSquared;
			hatchTile = tile;
		}

		return true;
	}

	private bool IsHatchRoom(RoomDef? room)
	{
		return room != null &&
			!string.IsNullOrWhiteSpace(HatchRoomId) &&
			string.Equals(room.RoomId, HatchRoomId, StringComparison.OrdinalIgnoreCase);
	}

	private void UpdateTuningReadout()
	{
		if (_tuningReadoutLabel == null)
		{
			return;
		}

		_tuningReadoutLabel.Visible = EnableRuntimeTuning && ShowTuningReadout;
		if (!_tuningReadoutLabel.Visible)
		{
			return;
		}

		_tuningReadoutLabel.Text =
			$"{BuildTuningSummaryLine()}\n" +
			"Arrow: origin  Shift+Arrow: large  Q/E: tile  R/F: height  G: grid  B: borders  P: print";
	}

	private void PrintCurrentTuningValues()
	{
		GD.Print(BuildPrintableTuningValues());
	}

	private string BuildTuningSummaryLine()
	{
		return
			$"Origin {FormatVector(GridOrigin)} | " +
			$"Tile {FormatFloat(TileSize)} | " +
			$"Height {FormatFloat(GridHeightOffset)} | " +
			$"Grid {(ShowGrid ? "on" : "off")} | " +
			$"Borders {(EmphasizeRoomBorders ? "on" : "off")}";
	}

	private string BuildPrintableTuningValues()
	{
		return
			"RealBoatCombatGridView tuning values:\n" +
			$"GridOrigin = Vector2({FormatFloat(GridOrigin.X)}, {FormatFloat(GridOrigin.Y)})\n" +
			$"GridHeightOffset = {FormatFloat(GridHeightOffset)}\n" +
			$"GridRotationDegrees = Vector3({FormatFloat(GridRotationDegrees.X)}, {FormatFloat(GridRotationDegrees.Y)}, {FormatFloat(GridRotationDegrees.Z)})\n" +
			$"TileSize = {FormatFloat(TileSize)}\n" +
			$"TileOpacity = {FormatFloat(TileOpacity)}\n" +
			$"GridLineOpacity = {FormatFloat(GridLineOpacity)}\n" +
			$"RoomBorderOpacity = {FormatFloat(RoomBorderOpacity)}\n" +
			$"RoomAccentStrength = {FormatFloat(RoomAccentStrength)}\n" +
			$"FadedBoatOpacity = {FormatFloat(FadedBoatOpacity)}\n" +
			$"ShowGrid = {ShowGrid}\n" +
			$"EmphasizeRoomBorders = {EmphasizeRoomBorders}";
	}

	private static string FormatVector(Vector2 value)
	{
		return $"({FormatFloat(value.X)}, {FormatFloat(value.Y)})";
	}

	private static string FormatFloat(float value)
	{
		return value.ToString("0.###", CultureInfo.InvariantCulture);
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

	private static void SetNamedDescendantsFaded(Node node, HashSet<string> names, Color tint, float opacity)
	{
		if (node is MeshInstance3D meshInstance && names.Contains(node.Name))
		{
			meshInstance.Visible = true;
			meshInstance.MaterialOverride = CreateTransparentMaterial(new Color(
				tint.R,
				tint.G,
				tint.B,
				Mathf.Clamp(opacity, 0.0f, 1.0f)));
		}

		foreach (var child in node.GetChildren())
		{
			SetNamedDescendantsFaded(child, names, tint, opacity);
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
