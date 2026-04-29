using Godot;

namespace TidesOfTime.Environment;

[Tool]
public partial class OceanEnvironment : Node3D
{
	[Export] public NodePath OceanMeshPath { get; set; } = new("OceanPlane");
	[Export] public NodePath TargetPath { get; set; } = new("");
	[Export(PropertyHint.Range, "8,512,1")] public float WaterSize { get; set; } = 160.0f;
	[Export(PropertyHint.Range, "1,160,1")] public int MeshSubdivisions { get; set; } = 64;
	[Export(PropertyHint.Range, "0,0.6,0.005")] public float WaveHeight { get; set; } = 0.08f;
	[Export(PropertyHint.Range, "0,2,0.01")] public float WaveSpeed { get; set; } = 0.55f;
	[Export(PropertyHint.Range, "0.05,2,0.01")] public float WaveScale { get; set; } = 0.42f;
	[Export(PropertyHint.Range, "0,0.5,0.005")] public float ColorScrollSpeed { get; set; } = 0.08f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float ColorVariation { get; set; } = 0.2f;
	[Export(PropertyHint.Range, "0,0.35,0.005")] public float GlintStrength { get; set; } = 0.05f;
	[Export(PropertyHint.Range, "0,0.45,0.005")] public float FoamStrength { get; set; } = 0.12f;
	[Export] public Color DeepColor { get; set; } = new(0.018f, 0.16f, 0.24f);
	[Export] public Color SurfaceColor { get; set; } = new(0.055f, 0.34f, 0.42f);
	[Export] public Color FoamColor { get; set; } = new(0.62f, 0.92f, 0.86f);
	[Export] public bool FollowTarget { get; set; }
	[Export] public bool FollowTargetX { get; set; } = true;
	[Export] public bool FollowTargetZ { get; set; } = true;

	private MeshInstance3D? _oceanMesh;
	private Node3D? _target;

	public override void _Ready()
	{
		ResolveNodes();
		ApplySettings();
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
		{
			ApplySettings();
		}

		UpdateFollowTarget();
	}

	private void ResolveNodes()
	{
		_oceanMesh = GetNodeOrNull<MeshInstance3D>(OceanMeshPath);
		_target = ResolveTarget();
	}

	private Node3D? ResolveTarget()
	{
		return string.IsNullOrWhiteSpace(TargetPath.ToString())
			? null
			: GetNodeOrNull<Node3D>(TargetPath);
	}

	private void ApplySettings()
	{
		_oceanMesh ??= GetNodeOrNull<MeshInstance3D>(OceanMeshPath);
		if (_oceanMesh == null)
		{
			return;
		}

		if (_oceanMesh.Mesh is PlaneMesh plane)
		{
			plane.Size = Vector2.One * Mathf.Max(8.0f, WaterSize);
			plane.SubdivideWidth = Mathf.Max(1, MeshSubdivisions);
			plane.SubdivideDepth = Mathf.Max(1, MeshSubdivisions);
		}

		if (_oceanMesh.MaterialOverride is not ShaderMaterial material)
		{
			return;
		}

		material.SetShaderParameter("deep_color", DeepColor);
		material.SetShaderParameter("surface_color", SurfaceColor);
		material.SetShaderParameter("foam_color", FoamColor);
		material.SetShaderParameter("wave_height", Mathf.Max(0.0f, WaveHeight));
		material.SetShaderParameter("wave_speed", Mathf.Max(0.0f, WaveSpeed));
		material.SetShaderParameter("wave_scale", Mathf.Max(0.01f, WaveScale));
		material.SetShaderParameter("color_scroll_speed", Mathf.Max(0.0f, ColorScrollSpeed));
		material.SetShaderParameter("color_variation", Mathf.Max(0.0f, ColorVariation));
		material.SetShaderParameter("glint_strength", Mathf.Max(0.0f, GlintStrength));
		material.SetShaderParameter("foam_strength", Mathf.Max(0.0f, FoamStrength));
	}

	private void UpdateFollowTarget()
	{
		if (!FollowTarget)
		{
			return;
		}

		_target ??= ResolveTarget();
		if (_target == null)
		{
			return;
		}

		var position = GlobalPosition;
		var targetPosition = _target.GlobalPosition;

		if (FollowTargetX)
		{
			position.X = targetPosition.X;
		}

		if (FollowTargetZ)
		{
			position.Z = targetPosition.Z;
		}

		GlobalPosition = position;
	}
}
