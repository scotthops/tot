using Godot;
using System;

namespace TidesOfTime.Prototypes;

public partial class StationMarker3D : Area3D
{
	[Export] public string StationName { get; set; } = "Station";
	[Export] public Color MarkerColor { get; set; } = new(0.55f, 0.46f, 0.26f);

	public event Action<StationMarker3D>? Clicked;

	public Vector3 AssignmentOffset { get; set; } = new(0.72f, 0.0f, 0.0f);
	public Vector3 AssignmentSlotGlobalPosition => ToGlobal(AssignmentOffset);

	private MeshInstance3D? _baseMesh;
	private MeshInstance3D? _flagMesh;
	private MeshInstance3D? _highlightMesh;
	private Label3D? _stationLabel;
	private Label3D? _assignmentLabel;
	private bool _isHighlighted;
	private string? _assignedCrewName;

	public override void _Ready()
	{
		InputRayPickable = true;
		BuildVisuals();
		RefreshVisuals();
	}

	public override void _InputEvent(
		Camera3D camera,
		InputEvent @event,
		Vector3 position,
		Vector3 normal,
		int shapeIdx)
	{
		if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			return;
		}

		Clicked?.Invoke(this);
		GetViewport().SetInputAsHandled();
	}

	public void SetHighlighted(bool isHighlighted)
	{
		_isHighlighted = isHighlighted;
		RefreshVisuals();
	}

	public void SetAssignedCrew(string? crewName)
	{
		_assignedCrewName = crewName;
		RefreshVisuals();
	}

	private void BuildVisuals()
	{
		ClearChildren(this);

		_highlightMesh = CreateCylinder(
			"HighlightRing",
			0.42f,
			0.035f,
			new Vector3(0.0f, 0.02f, 0.0f),
			new Color(1.0f, 0.88f, 0.35f),
			10);
		AddChild(_highlightMesh);

		_baseMesh = CreateBox(
			"Base",
			new Vector3(0.62f, 0.1f, 0.62f),
			new Vector3(0.0f, 0.08f, 0.0f),
			MarkerColor);
		AddChild(_baseMesh);

		var post = CreateBox(
			"Post",
			new Vector3(0.12f, 0.38f, 0.12f),
			new Vector3(0.0f, 0.32f, 0.0f),
			new Color(0.22f, 0.13f, 0.06f));
		AddChild(post);

		_flagMesh = CreateBox(
			"MarkerFlag",
			new Vector3(0.58f, 0.28f, 0.08f),
			new Vector3(0.28f, 0.58f, 0.0f),
			MarkerColor);
		AddChild(_flagMesh);

		_stationLabel = CreateLabel("StationLabel", StationName, new Vector3(0.0f, 0.91f, 0.0f));
		AddChild(_stationLabel);

		_assignmentLabel = CreateLabel("AssignmentLabel", string.Empty, new Vector3(0.0f, 0.74f, 0.0f));
		AddChild(_assignmentLabel);

		var collisionShape = new CollisionShape3D
		{
			Name = "ClickShape",
			Position = new Vector3(0.0f, 0.38f, 0.0f),
			Shape = new BoxShape3D { Size = new Vector3(0.88f, 0.9f, 0.88f) }
		};
		AddChild(collisionShape);
	}

	private void RefreshVisuals()
	{
		var activeColor = _isHighlighted
			? MarkerColor.Lerp(new Color(1.0f, 0.92f, 0.45f), 0.55f)
			: MarkerColor;

		if (_baseMesh != null)
		{
			_baseMesh.MaterialOverride = CreateMaterial(activeColor);
		}

		if (_flagMesh != null)
		{
			_flagMesh.MaterialOverride = CreateMaterial(activeColor);
		}

		if (_highlightMesh != null)
		{
			_highlightMesh.Visible = _isHighlighted;
		}

		if (_stationLabel != null)
		{
			_stationLabel.Text = StationName;
		}

		if (_assignmentLabel != null)
		{
			_assignmentLabel.Text = string.IsNullOrWhiteSpace(_assignedCrewName)
				? string.Empty
				: _assignedCrewName;
		}
	}

	private static MeshInstance3D CreateBox(string nodeName, Vector3 size, Vector3 position, Color color)
	{
		return new MeshInstance3D
		{
			Name = nodeName,
			Position = position,
			Mesh = new BoxMesh { Size = size },
			MaterialOverride = CreateMaterial(color)
		};
	}

	private static MeshInstance3D CreateCylinder(
		string nodeName,
		float radius,
		float height,
		Vector3 position,
		Color color,
		int radialSegments)
	{
		return new MeshInstance3D
		{
			Name = nodeName,
			Position = position,
			Mesh = new CylinderMesh
			{
				TopRadius = radius,
				BottomRadius = radius,
				Height = height,
				RadialSegments = radialSegments
			},
			MaterialOverride = CreateMaterial(color)
		};
	}

	private static Label3D CreateLabel(string nodeName, string text, Vector3 position)
	{
		return new Label3D
		{
			Name = nodeName,
			Text = text,
			Position = position,
			FontSize = 28,
			PixelSize = 0.012f,
			Modulate = new Color(0.94f, 0.9f, 0.72f)
		};
	}

	private static StandardMaterial3D CreateMaterial(Color color)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = 0.76f
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
}
