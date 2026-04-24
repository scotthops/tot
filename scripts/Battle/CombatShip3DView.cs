using Godot;
using System;
using System.Collections.Generic;
using TidesOfTime.Data;

namespace TidesOfTime.Battle;

public partial class CombatShip3DView : Node3D
{
	[Export] public ShipLayoutDef? Layout { get; set; }
	[Export] public float TileSize { get; set; } = 1.0f;
	[Export] public float TileGap { get; set; } = 0.08f;
	[Export] public float TileThickness { get; set; } = 0.08f;
	[Export] public float AccentStripWidth { get; set; } = 0.12f;
	[Export] public float AccentStripThickness { get; set; } = 0.025f;
	[Export] public float PartitionThickness { get; set; } = 0.08f;
	[Export] public float PartitionHeight { get; set; } = 0.18f;
	[Export] public float HullPadding { get; set; } = 0.5f;
	[Export] public float HullRailThickness { get; set; } = 0.16f;
	[Export] public float HullRailHeight { get; set; } = 0.22f;

	private static readonly Color FloorColor = new(0.44f, 0.27f, 0.13f);
	private static readonly Color FloorSeamColor = new(0.18f, 0.11f, 0.06f);
	private static readonly Color PartitionColor = new(0.26f, 0.15f, 0.07f);
	private static readonly Color HullColor = new(0.30f, 0.14f, 0.06f);
	private static readonly Color CutawayDeckColor = new(0.12f, 0.075f, 0.04f);

	private readonly List<Node> _generatedNodes = new();

	public override void _Ready()
	{
		BuildView();
	}

	public void BuildView()
	{
		ClearGeneratedNodes();

		if (Layout == null)
		{
			GD.PushError("CombatShip3DView: Layout is not assigned.");
			return;
		}

		var tileRooms = BuildTileRoomIndex(Layout);
		if (tileRooms.Count == 0)
		{
			GD.PushWarning($"CombatShip3DView: Layout '{Layout.ShipName}' has no playable tiles.");
			return;
		}

		var bounds = CalculateBounds(tileRooms.Keys);
		CreateHullCutaway(bounds);
		CreateRoomTiles(tileRooms);
		CreateRoomPartitions(tileRooms);
	}

	private void ClearGeneratedNodes()
	{
		foreach (var node in _generatedNodes)
		{
			node.QueueFree();
		}

		_generatedNodes.Clear();
	}

	private Dictionary<Vector2I, RoomDef> BuildTileRoomIndex(ShipLayoutDef layout)
	{
		var tileRooms = new Dictionary<Vector2I, RoomDef>();
		foreach (var room in layout.Rooms)
		{
			foreach (var tile in room.Tiles)
			{
				if (tile.X < 0 || tile.X >= layout.Width || tile.Y < 0 || tile.Y >= layout.Height)
				{
					GD.PushWarning($"CombatShip3DView: '{room.DisplayName}' has out-of-bounds tile {tile}.");
					continue;
				}

				if (tileRooms.ContainsKey(tile))
				{
					GD.PushWarning($"CombatShip3DView: tile {tile} is assigned to multiple rooms.");
				}

				tileRooms[tile] = room;
			}
		}

		return tileRooms;
	}

	private void CreateRoomTiles(Dictionary<Vector2I, RoomDef> tileRooms)
	{
		var panelSize = Mathf.Max(0.05f, TileSize - TileGap);
		foreach (var (tile, room) in tileRooms)
		{
			var tilePosition = MapTileToWorld(tile);
			CreateBox(
				$"Floor_{tile.X}_{tile.Y}_{room.RoomId}",
				new Vector3(panelSize, TileThickness, panelSize),
				tilePosition,
				Vector3.Zero,
				FloorColor);

			CreatePlankSeam(tile, tilePosition, panelSize);
			CreateSystemAccent(tile, room, tilePosition, panelSize);
		}
	}

	private void CreatePlankSeam(Vector2I tile, Vector3 tilePosition, float panelSize)
	{
		var seamY = (TileThickness * 0.5f) + 0.009f;
		CreateBox(
			$"PlankSeam_{tile.X}_{tile.Y}",
			new Vector3(0.025f, 0.012f, panelSize * 0.78f),
			tilePosition + new Vector3(0.0f, seamY, 0.0f),
			Vector3.Zero,
			FloorSeamColor);
	}

	private void CreateSystemAccent(Vector2I tile, RoomDef room, Vector3 tilePosition, float panelSize)
	{
		var accentY = (TileThickness * 0.5f) + (AccentStripThickness * 0.5f) + 0.014f;
		var accentZ = (panelSize * -0.5f) + Mathf.Max(0.08f, AccentStripWidth);
		CreateBox(
			$"Accent_{tile.X}_{tile.Y}_{room.RoomId}",
			new Vector3(panelSize * 0.68f, AccentStripThickness, AccentStripWidth),
			tilePosition + new Vector3(0.0f, accentY, accentZ),
			Vector3.Zero,
			GetSystemAccentColor(room));
	}

	private void CreateRoomPartitions(Dictionary<Vector2I, RoomDef> tileRooms)
	{
		var createdEdges = new HashSet<string>();
		foreach (var (tile, room) in tileRooms)
		{
			AddPartitionIfNeeded(
				tileRooms,
				createdEdges,
				tile,
				room,
				new Vector2I(0, -1),
				$"H:{tile.X}:{tile.Y}",
				isHorizontal: true);
			AddPartitionIfNeeded(
				tileRooms,
				createdEdges,
				tile,
				room,
				new Vector2I(0, 1),
				$"H:{tile.X}:{tile.Y + 1}",
				isHorizontal: true);
			AddPartitionIfNeeded(
				tileRooms,
				createdEdges,
				tile,
				room,
				new Vector2I(-1, 0),
				$"V:{tile.X}:{tile.Y}",
				isHorizontal: false);
			AddPartitionIfNeeded(
				tileRooms,
				createdEdges,
				tile,
				room,
				new Vector2I(1, 0),
				$"V:{tile.X + 1}:{tile.Y}",
				isHorizontal: false);
		}
	}

	private void AddPartitionIfNeeded(
		Dictionary<Vector2I, RoomDef> tileRooms,
		HashSet<string> createdEdges,
		Vector2I tile,
		RoomDef room,
		Vector2I neighborOffset,
		string edgeKey,
		bool isHorizontal)
	{
		var neighborTile = tile + neighborOffset;
		if (tileRooms.TryGetValue(neighborTile, out var neighborRoom) && neighborRoom.RoomId == room.RoomId)
		{
			return;
		}

		if (!createdEdges.Add(edgeKey))
		{
			return;
		}

		var tilePosition = MapTileToWorld(tile);
		var sideOffset = (TileSize * 0.5f) - (TileGap * 0.5f);
		var partitionY = (TileThickness * 0.5f) + (PartitionHeight * 0.5f);
		var size = isHorizontal
			? new Vector3(TileSize - (TileGap * 0.5f), PartitionHeight, PartitionThickness)
			: new Vector3(PartitionThickness, PartitionHeight, TileSize - (TileGap * 0.5f));
		var offset = isHorizontal
			? new Vector3(0.0f, partitionY, neighborOffset.Y < 0 ? -sideOffset : sideOffset)
			: new Vector3(neighborOffset.X < 0 ? -sideOffset : sideOffset, partitionY, 0.0f);

		CreateBox(
			$"Partition_{edgeKey}",
			size,
			tilePosition + offset,
			Vector3.Zero,
			PartitionColor);
	}

	private void CreateHullCutaway(TileBounds bounds)
	{
		var min = MapTileToWorld(new Vector2I(bounds.MinX, bounds.MinY));
		var max = MapTileToWorld(new Vector2I(bounds.MaxX, bounds.MaxY));
		var center = (min + max) * 0.5f;
		var width = Mathf.Abs(max.X - min.X) + TileSize;
		var depth = Mathf.Abs(max.Z - min.Z) + TileSize;
		var paddedWidth = width + (HullPadding * 2.0f);
		var paddedDepth = depth + (HullPadding * 2.0f);
		var leftX = center.X - (paddedWidth * 0.5f);
		var rightX = center.X + (paddedWidth * 0.5f);
		var bowZ = center.Z - (paddedDepth * 0.5f);
		var sternZ = center.Z + (paddedDepth * 0.5f);
		var bowPointZ = bowZ - (TileSize * 0.95f);
		var railY = HullRailHeight * 0.5f;

		CreateBox(
			"CutawayDeck",
			new Vector3(paddedWidth, 0.05f, paddedDepth),
			new Vector3(center.X, -0.08f, center.Z),
			Vector3.Zero,
			CutawayDeckColor);

		CreateBox(
			"PortRail",
			new Vector3(HullRailThickness, HullRailHeight, paddedDepth),
			new Vector3(leftX, railY, center.Z),
			Vector3.Zero,
			HullColor);

		CreateBox(
			"StarboardRail",
			new Vector3(HullRailThickness, HullRailHeight, paddedDepth),
			new Vector3(rightX, railY, center.Z),
			Vector3.Zero,
			HullColor);

		CreateBox(
			"SternRail",
			new Vector3(paddedWidth, HullRailHeight, HullRailThickness),
			new Vector3(center.X, railY, sternZ),
			Vector3.Zero,
			HullColor);

		CreateBeamBetween(
			"PortBowRail",
			new Vector2(leftX, bowZ),
			new Vector2(center.X, bowPointZ),
			railY);

		CreateBeamBetween(
			"StarboardBowRail",
			new Vector2(rightX, bowZ),
			new Vector2(center.X, bowPointZ),
			railY);
	}

	private void CreateBox(string nodeName, Vector3 size, Vector3 position, Vector3 rotation, Color color)
	{
		var node = new MeshInstance3D
		{
			Name = nodeName,
			Mesh = new BoxMesh { Size = size },
			Position = position,
			Rotation = rotation,
			MaterialOverride = CreateMaterial(color)
		};

		AddGeneratedChild(node);
	}

	private void CreateBeamBetween(string nodeName, Vector2 start, Vector2 end, float y)
	{
		var direction = end - start;
		var length = direction.Length();
		var angle = MathF.Atan2(direction.X, direction.Y);
		var midpoint = (start + end) * 0.5f;

		CreateBox(
			nodeName,
			new Vector3(HullRailThickness, HullRailHeight, length),
			new Vector3(midpoint.X, y, midpoint.Y),
			new Vector3(0.0f, angle, 0.0f),
			HullColor);
	}

	private void AddGeneratedChild(Node node)
	{
		AddChild(node);
		_generatedNodes.Add(node);
	}

	private Vector3 MapTileToWorld(Vector2I tile)
	{
		if (Layout == null)
		{
			return Vector3.Zero;
		}

		var spacing = TileSize;
		var originX = -((Layout.Width - 1) * spacing * 0.5f);
		var originZ = -((Layout.Height - 1) * spacing * 0.5f);
		return new Vector3(
			originX + (tile.X * spacing),
			0.0f,
			originZ + (tile.Y * spacing));
	}

	private static TileBounds CalculateBounds(IEnumerable<Vector2I> tiles)
	{
		var minX = int.MaxValue;
		var minY = int.MaxValue;
		var maxX = int.MinValue;
		var maxY = int.MinValue;

		foreach (var tile in tiles)
		{
			minX = Math.Min(minX, tile.X);
			minY = Math.Min(minY, tile.Y);
			maxX = Math.Max(maxX, tile.X);
			maxY = Math.Max(maxY, tile.Y);
		}

		return new TileBounds(minX, minY, maxX, maxY);
	}

	private static StandardMaterial3D CreateMaterial(Color color)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = 0.78f
		};
	}

	private static Color GetSystemAccentColor(RoomDef room)
	{
		return room.SystemType switch
		{
			"HelmRigging" => new Color(0.38f, 0.56f, 0.82f),
			"Cannons" => new Color(0.68f, 0.26f, 0.22f),
			"ThreadChamber" => new Color(0.45f, 0.3f, 0.62f),
			"CrowsNest" => new Color(0.72f, 0.62f, 0.24f),
			"DoctorsQuarters" => new Color(0.28f, 0.58f, 0.38f),
			_ => new Color(0.48f, 0.48f, 0.46f)
		};
	}

	private readonly record struct TileBounds(int MinX, int MinY, int MaxX, int MaxY);
}
