using Godot;
using System;

namespace TidesOfTime.Prototypes;

public partial class CrewToken3D : Area3D
{
	[Export] public string CrewName { get; set; } = "Crew";
	[Export] public string CrewRole { get; set; } = "Crew";
	[Export] public string ShortLabel { get; set; } = "C";
	[Export] public Color CrewColor { get; set; } = new(0.22f, 0.44f, 0.76f);
	[Export] public string ModelPath { get; set; } = string.Empty;
	[Export] public float ModelScale { get; set; } = 0.69f;
	[Export] public Vector3 ModelRotationDegrees { get; set; } = new(0.0f, 180.0f, 0.0f);

	public event Action<CrewToken3D>? Clicked;

	public Vector3 HomePosition { get; set; }

	private MeshInstance3D? _selectionRing;
	private MeshInstance3D? _bodyMesh;
	private Label3D? _nameLabel;
	private bool _isSelected;

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

	public void SetSelected(bool isSelected)
	{
		_isSelected = isSelected;
		RefreshVisuals();
	}

	public void SetAssignedStation(string? stationName)
	{
		// Assignments live in the HUD. The token owns only its crew name label in 3D.
	}

	private void BuildVisuals()
	{
		ClearChildren(this);

		_selectionRing = CreateCylinder(
			"SelectionRing",
			0.34f,
			0.035f,
			new Vector3(0.0f, 0.02f, 0.0f),
			new Color(1.0f, 0.88f, 0.34f),
			12);
		AddChild(_selectionRing);

		if (!TryCreateModelVisual())
		{
			_bodyMesh = CreateCylinder(
				"Body",
				0.22f,
				0.46f,
				new Vector3(0.0f, 0.28f, 0.0f),
				CrewColor,
				10);
			AddChild(_bodyMesh);

			var head = CreateCylinder(
				"Head",
				0.16f,
				0.14f,
				new Vector3(0.0f, 0.6f, 0.0f),
				new Color(0.86f, 0.68f, 0.48f),
				10);
			AddChild(head);
		}

		_nameLabel = CreateLabel("NameLabel", CrewName, new Vector3(0.0f, 1.02f, 0.0f));
		AddChild(_nameLabel);

		var collisionShape = new CollisionShape3D
		{
			Name = "ClickShape",
			Position = new Vector3(0.0f, 0.38f, 0.0f),
			Shape = new SphereShape3D { Radius = 0.42f }
		};
		AddChild(collisionShape);
	}

	private void RefreshVisuals()
	{
		if (_selectionRing != null)
		{
			_selectionRing.Visible = _isSelected;
		}

		if (_bodyMesh != null)
		{
			var bodyColor = _isSelected
				? CrewColor.Lerp(new Color(1.0f, 0.9f, 0.36f), 0.32f)
				: CrewColor;
			_bodyMesh.MaterialOverride = CreateMaterial(bodyColor);
		}

		if (_nameLabel != null)
		{
			_nameLabel.Text = ShortLabel;
		}
	}

	private bool TryCreateModelVisual()
	{
		if (string.IsNullOrWhiteSpace(ModelPath))
		{
			return false;
		}

		var scene = ResourceLoader.Load<PackedScene>(ModelPath);
		if (scene?.Instantiate() is not Node3D model)
		{
			return false;
		}

		model.Name = "Model";
		model.Position = new Vector3(0.0f, 0.03f, 0.0f);
		model.RotationDegrees = ModelRotationDegrees;
		model.Scale = Vector3.One * ModelScale;
		AddChild(model);
		return true;
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
			FontSize = 26,
			PixelSize = 0.011f,
			Modulate = new Color(0.95f, 0.96f, 0.86f),
			OutlineSize = 7,
			OutlineModulate = new Color(0.025f, 0.03f, 0.035f),
			NoDepthTest = true,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
		};
	}

	private static StandardMaterial3D CreateMaterial(Color color)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = 0.72f
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
