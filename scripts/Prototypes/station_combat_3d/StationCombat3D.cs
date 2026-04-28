using Godot;
using System;
using System.Collections.Generic;

namespace TidesOfTime.Prototypes;

public partial class StationCombat3D : Node3D
{
	[Export] public NodePath ShipRootPath { get; set; } = new("PrototypeRoot/ShipBlockout");
	[Export] public NodePath StationRootPath { get; set; } = new("PrototypeRoot/Stations");
	[Export] public NodePath CrewRootPath { get; set; } = new("PrototypeRoot/Crew");
	[Export] public NodePath HudPanelPath { get; set; } = new("HUD/PanelContainer");
	[Export] public NodePath SelectedCrewLabelPath { get; set; } = new("HUD/PanelContainer/MarginContainer/VBoxContainer/SelectedCrewValue");
	[Export] public NodePath ClickedStationLabelPath { get; set; } = new("HUD/PanelContainer/MarginContainer/VBoxContainer/ClickedStationValue");
	[Export] public NodePath StatusLabelPath { get; set; } = new("HUD/PanelContainer/MarginContainer/VBoxContainer/StatusValue");
	[Export] public NodePath AssignmentsLabelPath { get; set; } = new("HUD/PanelContainer/MarginContainer/VBoxContainer/AssignmentsValue");

	private static readonly Color HullColor = new(0.28f, 0.13f, 0.06f);
	private static readonly Color DeckColor = new(0.57f, 0.34f, 0.15f);
	private static readonly Color RailColor = new(0.18f, 0.09f, 0.035f);
	private static readonly Color SailColor = new(0.82f, 0.76f, 0.58f);
	private static readonly Color HatchColor = new(0.08f, 0.055f, 0.03f);

	private static readonly StationDefinition[] StationDefinitions =
	{
		new(
			"Helm",
			new Vector3(0.0f, 1.02f, 2.18f),
			new Vector3(0.68f, 0.0f, 0.0f),
			new Color(0.34f, 0.52f, 0.78f)),
		new(
			"Cannons",
			new Vector3(-1.08f, 1.02f, -0.18f),
			new Vector3(0.82f, 0.0f, 0.0f),
			new Color(0.74f, 0.28f, 0.2f)),
		new(
			"Crow's Nest",
			new Vector3(0.0f, 2.96f, -0.66f),
			new Vector3(0.6f, 0.0f, 0.0f),
			new Color(0.86f, 0.72f, 0.28f)),
		new(
			"Bilge",
			new Vector3(0.74f, 1.02f, 0.82f),
			new Vector3(-0.76f, 0.0f, 0.0f),
			new Color(0.28f, 0.58f, 0.44f))
	};

	private static readonly CrewDefinition[] CrewDefinitions =
	{
		new(
			"Captain",
			"Command",
			new Vector3(-0.58f, 1.04f, 1.54f),
			new Color(0.26f, 0.46f, 0.82f)),
		new(
			"Gunner",
			"Gunnery",
			new Vector3(-0.58f, 1.04f, -0.88f),
			new Color(0.72f, 0.32f, 0.24f)),
		new(
			"Deckhand",
			"Repair",
			new Vector3(0.58f, 1.04f, 0.12f),
			new Color(0.26f, 0.62f, 0.42f))
	};

	private readonly List<StationMarker3D> _stations = new();
	private readonly List<CrewToken3D> _crew = new();
	private readonly Dictionary<string, string> _stationByCrewName = new();
	private readonly Dictionary<string, string> _crewByStationName = new();

	private Node3D? _shipRoot;
	private Node3D? _stationRoot;
	private Node3D? _crewRoot;
	private PanelContainer? _hudPanel;
	private Label? _selectedCrewLabel;
	private Label? _clickedStationLabel;
	private Label? _statusLabel;
	private Label? _assignmentsLabel;
	private CrewToken3D? _selectedCrew;
	private StationMarker3D? _clickedStation;
	private string _statusText = "Awaiting assignment.";

	public override void _Ready()
	{
		_shipRoot = GetNodeOrNull<Node3D>(ShipRootPath);
		_stationRoot = GetNodeOrNull<Node3D>(StationRootPath);
		_crewRoot = GetNodeOrNull<Node3D>(CrewRootPath);
		_hudPanel = GetNodeOrNull<PanelContainer>(HudPanelPath);
		_selectedCrewLabel = GetNodeOrNull<Label>(SelectedCrewLabelPath);
		_clickedStationLabel = GetNodeOrNull<Label>(ClickedStationLabelPath);
		_statusLabel = GetNodeOrNull<Label>(StatusLabelPath);
		_assignmentsLabel = GetNodeOrNull<Label>(AssignmentsLabelPath);

		if (_shipRoot == null || _stationRoot == null || _crewRoot == null)
		{
			GD.PushError("StationCombat3D: prototype roots are missing from the scene.");
			return;
		}

		StyleHud();
		BuildShipBlockout();
		BuildStations();
		BuildCrew();
		UpdateHud();
	}

	private void BuildShipBlockout()
	{
		if (_shipRoot == null)
		{
			return;
		}

		ClearChildren(_shipRoot);

		CreateBox(_shipRoot, "Hull", new Vector3(3.35f, 0.72f, 6.2f), new Vector3(0.0f, 0.36f, 0.0f), HullColor);
		CreateBox(_shipRoot, "Deck", new Vector3(2.72f, 0.16f, 5.28f), new Vector3(0.0f, 0.82f, 0.12f), DeckColor);
		CreateBox(_shipRoot, "PortRail", new Vector3(0.16f, 0.36f, 5.35f), new Vector3(-1.44f, 1.05f, 0.12f), RailColor);
		CreateBox(_shipRoot, "StarboardRail", new Vector3(0.16f, 0.36f, 5.35f), new Vector3(1.44f, 1.05f, 0.12f), RailColor);
		CreateBox(_shipRoot, "SternRail", new Vector3(2.86f, 0.36f, 0.16f), new Vector3(0.0f, 1.05f, 2.84f), RailColor);
		CreateBeamBetween(_shipRoot, "PortBowRail", new Vector2(-1.44f, -2.5f), new Vector2(0.0f, -3.24f), 1.05f);
		CreateBeamBetween(_shipRoot, "StarboardBowRail", new Vector2(1.44f, -2.5f), new Vector2(0.0f, -3.24f), 1.05f);

		for (var plank = -2; plank <= 2; plank++)
		{
			CreateBox(
				_shipRoot,
				$"DeckPlankLine_{plank}",
				new Vector3(0.025f, 0.022f, 4.92f),
				new Vector3(plank * 0.42f, 0.925f, 0.18f),
				new Color(0.22f, 0.12f, 0.055f));
		}

		CreateBox(_shipRoot, "SternCabin", new Vector3(1.18f, 0.54f, 0.86f), new Vector3(0.0f, 1.18f, 1.48f), new Color(0.5f, 0.28f, 0.12f));
		CreateBox(_shipRoot, "HelmWheelBlockout", new Vector3(0.58f, 0.42f, 0.12f), new Vector3(0.0f, 1.22f, 2.08f), new Color(0.12f, 0.07f, 0.035f));
		CreateBox(_shipRoot, "BilgeHatch", new Vector3(0.68f, 0.05f, 0.62f), new Vector3(0.42f, 0.94f, 0.72f), HatchColor);
		CreateBox(_shipRoot, "HatchSlatA", new Vector3(0.52f, 0.035f, 0.045f), new Vector3(0.42f, 0.985f, 0.58f), RailColor);
		CreateBox(_shipRoot, "HatchSlatB", new Vector3(0.52f, 0.035f, 0.045f), new Vector3(0.42f, 0.985f, 0.72f), RailColor);
		CreateBox(_shipRoot, "HatchSlatC", new Vector3(0.52f, 0.035f, 0.045f), new Vector3(0.42f, 0.985f, 0.86f), RailColor);

		CreateCylinder(_shipRoot, "Mast", 0.12f, 2.85f, new Vector3(0.0f, 2.17f, -0.66f), RailColor, 8);
		CreateBox(_shipRoot, "FurledSail", new Vector3(1.72f, 0.34f, 0.08f), new Vector3(0.0f, 2.1f, -0.66f), SailColor);
		CreateBox(_shipRoot, "CrowNestPlatform", new Vector3(0.9f, 0.12f, 0.9f), new Vector3(0.0f, 2.78f, -0.66f), DeckColor);
		CreateBox(_shipRoot, "CrowNestRailPort", new Vector3(0.08f, 0.22f, 0.9f), new Vector3(-0.45f, 2.93f, -0.66f), RailColor);
		CreateBox(_shipRoot, "CrowNestRailStarboard", new Vector3(0.08f, 0.22f, 0.9f), new Vector3(0.45f, 2.93f, -0.66f), RailColor);

		CreateCannon(_shipRoot, "PortCannonForward", new Vector3(-1.54f, 1.05f, -0.72f), pointsPort: true);
		CreateCannon(_shipRoot, "PortCannonAft", new Vector3(-1.54f, 1.05f, 0.28f), pointsPort: true);
		CreateCannon(_shipRoot, "StarboardCannonForward", new Vector3(1.54f, 1.05f, -0.72f), pointsPort: false);
		CreateCannon(_shipRoot, "StarboardCannonAft", new Vector3(1.54f, 1.05f, 0.28f), pointsPort: false);
	}

	private void BuildStations()
	{
		if (_stationRoot == null)
		{
			return;
		}

		ClearChildren(_stationRoot);
		_stations.Clear();
		_clickedStation = null;
		_crewByStationName.Clear();

		foreach (var definition in StationDefinitions)
		{
			var marker = new StationMarker3D
			{
				Name = $"{SanitizeNodeName(definition.Name)}Station",
				StationName = definition.Name,
				MarkerColor = definition.Color,
				Position = definition.Position,
				AssignmentOffset = definition.AssignmentOffset
			};

			_stationRoot.AddChild(marker);
			marker.Clicked += OnStationClicked;
			_stations.Add(marker);
		}
	}

	private void BuildCrew()
	{
		if (_crewRoot == null)
		{
			return;
		}

		ClearChildren(_crewRoot);
		_crew.Clear();
		_stationByCrewName.Clear();
		_selectedCrew = null;

		foreach (var definition in CrewDefinitions)
		{
			var crew = new CrewToken3D
			{
				Name = $"{SanitizeNodeName(definition.Name)}Token",
				CrewName = definition.Name,
				CrewRole = definition.Role,
				CrewColor = definition.Color,
				Position = definition.HomePosition,
				HomePosition = definition.HomePosition
			};

			_crewRoot.AddChild(crew);
			crew.Clicked += OnCrewClicked;
			_crew.Add(crew);
		}
	}

	private void OnCrewClicked(CrewToken3D crew)
	{
		_selectedCrew = crew;
		_statusText = $"{crew.CrewName} selected.";
		UpdateSelectionVisuals();
		UpdateHud();
	}

	private void OnStationClicked(StationMarker3D station)
	{
		_clickedStation = station;

		if (_selectedCrew == null)
		{
			_statusText = $"{station.StationName} clicked.";
			UpdateSelectionVisuals();
			UpdateHud();
			return;
		}

		AssignCrewToStation(_selectedCrew, station);
		_statusText = $"{_selectedCrew.CrewName} assigned to {station.StationName}.";
		UpdateSelectionVisuals();
		UpdateHud();
	}

	private void AssignCrewToStation(CrewToken3D crew, StationMarker3D station)
	{
		if (_stationByCrewName.TryGetValue(crew.CrewName, out var oldStationName))
		{
			_crewByStationName.Remove(oldStationName);
			FindStation(oldStationName)?.SetAssignedCrew(null);
		}

		if (_crewByStationName.TryGetValue(station.StationName, out var displacedCrewName) &&
			displacedCrewName != crew.CrewName)
		{
			_stationByCrewName.Remove(displacedCrewName);
			var displacedCrew = FindCrew(displacedCrewName);
			if (displacedCrew != null)
			{
				displacedCrew.SetAssignedStation(null);
				displacedCrew.Position = displacedCrew.HomePosition;
			}
		}

		_stationByCrewName[crew.CrewName] = station.StationName;
		_crewByStationName[station.StationName] = crew.CrewName;
		station.SetAssignedCrew(crew.CrewName);
		crew.SetAssignedStation(station.StationName);
		crew.GlobalPosition = station.AssignmentSlotGlobalPosition;
	}

	private void UpdateSelectionVisuals()
	{
		foreach (var crew in _crew)
		{
			crew.SetSelected(crew == _selectedCrew);
		}

		foreach (var station in _stations)
		{
			station.SetHighlighted(station == _clickedStation);
		}
	}

	private void UpdateHud()
	{
		if (_selectedCrewLabel != null)
		{
			_selectedCrewLabel.Text = $"Crew: {_selectedCrew?.CrewName ?? "None"}";
		}

		if (_clickedStationLabel != null)
		{
			_clickedStationLabel.Text = $"Station: {_clickedStation?.StationName ?? "None"}";
		}

		if (_statusLabel != null)
		{
			_statusLabel.Text = $"Status: {_statusText}";
		}

		if (_assignmentsLabel != null)
		{
			_assignmentsLabel.Text = BuildAssignmentText();
		}
	}

	private string BuildAssignmentText()
	{
		if (_crew.Count == 0)
		{
			return "Assignments: None";
		}

		var rows = new List<string> { "Assignments:" };
		foreach (var crew in _crew)
		{
			var assignment = _stationByCrewName.TryGetValue(crew.CrewName, out var stationName)
				? stationName
				: "Unassigned";
			rows.Add($"{crew.CrewName} -> {assignment}");
		}

		return string.Join('\n', rows);
	}

	private StationMarker3D? FindStation(string stationName)
	{
		foreach (var station in _stations)
		{
			if (station.StationName == stationName)
			{
				return station;
			}
		}

		return null;
	}

	private CrewToken3D? FindCrew(string crewName)
	{
		foreach (var crew in _crew)
		{
			if (crew.CrewName == crewName)
			{
				return crew;
			}
		}

		return null;
	}

	private void StyleHud()
	{
		if (_hudPanel != null)
		{
			var panelStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.035f, 0.05f, 0.055f, 0.88f),
				BorderColor = new Color(0.78f, 0.67f, 0.42f, 0.75f),
				BorderWidthLeft = 1,
				BorderWidthTop = 1,
				BorderWidthRight = 1,
				BorderWidthBottom = 1,
				CornerRadiusTopLeft = 4,
				CornerRadiusTopRight = 4,
				CornerRadiusBottomRight = 4,
				CornerRadiusBottomLeft = 4
			};
			_hudPanel.AddThemeStyleboxOverride("panel", panelStyle);
		}

		StyleHudLabel(_selectedCrewLabel);
		StyleHudLabel(_clickedStationLabel);
		StyleHudLabel(_statusLabel);
		StyleHudLabel(_assignmentsLabel);
	}

	private static void StyleHudLabel(Label? label)
	{
		if (label == null)
		{
			return;
		}

		label.AddThemeColorOverride("font_color", new Color(0.9f, 0.88f, 0.78f));
		label.AddThemeFontSizeOverride("font_size", 14);
	}

	private static void CreateCannon(Node3D parent, string nodeName, Vector3 position, bool pointsPort)
	{
		var rotation = new Vector3(0.0f, 0.0f, Mathf.Pi * 0.5f);
		var barrelOffset = pointsPort ? -0.16f : 0.16f;

		CreateCylinder(
			parent,
			nodeName,
			0.12f,
			0.62f,
			position + new Vector3(barrelOffset, 0.0f, 0.0f),
			new Color(0.075f, 0.075f, 0.075f),
			10,
			rotation);
	}

	private static MeshInstance3D CreateBox(
		Node3D parent,
		string nodeName,
		Vector3 size,
		Vector3 position,
		Color color,
		Vector3? rotation = null)
	{
		var node = new MeshInstance3D
		{
			Name = nodeName,
			Position = position,
			Rotation = rotation ?? Vector3.Zero,
			Mesh = new BoxMesh { Size = size },
			MaterialOverride = CreateMaterial(color)
		};

		parent.AddChild(node);
		return node;
	}

	private static MeshInstance3D CreateCylinder(
		Node3D parent,
		string nodeName,
		float radius,
		float height,
		Vector3 position,
		Color color,
		int radialSegments,
		Vector3? rotation = null)
	{
		var node = new MeshInstance3D
		{
			Name = nodeName,
			Position = position,
			Rotation = rotation ?? Vector3.Zero,
			Mesh = new CylinderMesh
			{
				TopRadius = radius,
				BottomRadius = radius,
				Height = height,
				RadialSegments = radialSegments
			},
			MaterialOverride = CreateMaterial(color)
		};

		parent.AddChild(node);
		return node;
	}

	private static void CreateBeamBetween(Node3D parent, string nodeName, Vector2 start, Vector2 end, float y)
	{
		var direction = end - start;
		var length = direction.Length();
		var angle = MathF.Atan2(direction.X, direction.Y);
		var midpoint = (start + end) * 0.5f;

		CreateBox(
			parent,
			nodeName,
			new Vector3(0.16f, 0.36f, length),
			new Vector3(midpoint.X, y, midpoint.Y),
			RailColor,
			new Vector3(0.0f, angle, 0.0f));
	}

	private static StandardMaterial3D CreateMaterial(Color color)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = 0.78f
		};
	}

	private static void ClearChildren(Node parent)
	{
		foreach (var child in parent.GetChildren())
		{
			parent.RemoveChild(child);
			child.QueueFree();
		}
	}

	private static string SanitizeNodeName(string value)
	{
		return value.Replace(" ", string.Empty).Replace("'", string.Empty);
	}

	private readonly record struct StationDefinition(
		string Name,
		Vector3 Position,
		Vector3 AssignmentOffset,
		Color Color);

	private readonly record struct CrewDefinition(
		string Name,
		string Role,
		Vector3 HomePosition,
		Color Color);
}
