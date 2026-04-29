using Godot;
using System;

namespace TidesOfTime.Prototypes;

public partial class StationMarker3D : Area3D
{
	[Export] public string StationName { get; set; } = "Station";
	[Export] public Color MarkerColor { get; set; } = new(0.55f, 0.46f, 0.26f);
	[Export] public Vector3 ClickShapeSize { get; set; } = new(0.88f, 0.9f, 0.88f);
	[Export] public Vector3 ClickShapeOffset { get; set; } = new(0.0f, 0.38f, 0.0f);

	public event Action<StationMarker3D, MouseButton>? Clicked;

	public Vector3 AssignmentOffset { get; set; } = new(0.72f, 0.0f, 0.0f);
	public Vector3 AssignmentSlotGlobalPosition => ToGlobal(AssignmentOffset);

	private MeshInstance3D? _baseMesh;
	private MeshInstance3D? _highlightMesh;
	private Node3D? _targetReticle;
	private Label3D? _stationLabel;
	private bool _isHighlighted;
	private bool _isTargeted;
	private float _durabilityPercent = 100.0f;

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
		if (@event is not InputEventMouseButton { Pressed: true } mouseButton ||
			mouseButton.ButtonIndex is not MouseButton.Left and not MouseButton.Right)
		{
			return;
		}

		Clicked?.Invoke(this, mouseButton.ButtonIndex);
		GetViewport().SetInputAsHandled();
	}

	public void SetHighlighted(bool isHighlighted)
	{
		_isHighlighted = isHighlighted;
		RefreshVisuals();
	}

	public void SetTargeted(bool isTargeted)
	{
		_isTargeted = isTargeted;
		RefreshVisuals();
	}

	public void SetAssignedCrew(string? crewName)
	{
		// Assignments are shown in the HUD so station labels never move with crew tokens.
	}

	public void SetDurabilityPercent(float durabilityPercent)
	{
		_durabilityPercent = Mathf.Clamp(durabilityPercent, 0.0f, 100.0f);
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

		_targetReticle = CreateTargetReticle();
		AddChild(_targetReticle);

		_baseMesh = CreateBox(
			"Base",
			new Vector3(0.62f, 0.1f, 0.62f),
			new Vector3(0.0f, 0.08f, 0.0f),
			MarkerColor);
		AddChild(_baseMesh);

		_stationLabel = CreateLabel("StationLabel", StationName, GetStationLabelPosition());
		AddChild(_stationLabel);

		var collisionShape = new CollisionShape3D
		{
			Name = "ClickShape",
			Position = ClickShapeOffset,
			Shape = new BoxShape3D { Size = ClickShapeSize }
		};
		AddChild(collisionShape);
	}

	private void RefreshVisuals()
	{
		var damageRatio = 1.0f - (_durabilityPercent / 100.0f);
		var damagedColor = MarkerColor.Lerp(new Color(0.035f, 0.03f, 0.03f), damageRatio * 0.72f);
		var activeColor = _isHighlighted
			? damagedColor.Lerp(new Color(1.0f, 0.92f, 0.45f), 0.45f)
			: damagedColor;

		if (_baseMesh != null)
		{
			_baseMesh.MaterialOverride = CreateMaterial(activeColor);
		}

		if (_highlightMesh != null)
		{
			_highlightMesh.Visible = _isHighlighted;
		}

		if (_targetReticle != null)
		{
			_targetReticle.Visible = _isTargeted;
		}

		if (_stationLabel != null)
		{
			_stationLabel.Text = StationName;
			_stationLabel.Visible = _isHighlighted || _isTargeted;
		}
	}

	private static Node3D CreateTargetReticle()
	{
		var root = new Node3D
		{
			Name = "TargetReticle",
			Position = new Vector3(0.0f, 0.16f, 0.0f),
			Visible = false
		};
		var color = new Color(1.0f, 0.08f, 0.05f);
		var length = 0.58f;
		var thickness = 0.075f;
		var offset = 0.42f;

		root.AddChild(CreateBox("NorthReticle", new Vector3(length, thickness, thickness), new Vector3(0.0f, 0.0f, -offset), color));
		root.AddChild(CreateBox("SouthReticle", new Vector3(length, thickness, thickness), new Vector3(0.0f, 0.0f, offset), color));
		root.AddChild(CreateBox("WestReticle", new Vector3(thickness, thickness, length), new Vector3(-offset, 0.0f, 0.0f), color));
		root.AddChild(CreateBox("EastReticle", new Vector3(thickness, thickness, length), new Vector3(offset, 0.0f, 0.0f), color));
		root.AddChild(CreateBox("SlashReticleA", new Vector3(1.02f, thickness, thickness), Vector3.Zero, color, new Vector3(0.0f, Mathf.Pi * 0.25f, 0.0f)));
		root.AddChild(CreateBox("SlashReticleB", new Vector3(1.02f, thickness, thickness), Vector3.Zero, color, new Vector3(0.0f, -Mathf.Pi * 0.25f, 0.0f)));

		return root;
	}

	private static MeshInstance3D CreateBox(string nodeName, Vector3 size, Vector3 position, Color color, Vector3? rotation = null)
	{
		return new MeshInstance3D
		{
			Name = nodeName,
			Position = position,
			Rotation = rotation ?? Vector3.Zero,
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
			FontSize = 22,
			PixelSize = 0.01f,
			Modulate = new Color(0.96f, 0.9f, 0.64f),
			OutlineSize = 6,
			OutlineModulate = new Color(0.035f, 0.028f, 0.018f),
			NoDepthTest = true,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
		};
	}

	private Vector3 GetStationLabelPosition()
	{
		var labelSide = AssignmentOffset.X >= 0.0f ? -0.36f : 0.36f;
		return new Vector3(labelSide, 1.12f, -0.32f);
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
