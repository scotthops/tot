using Godot;
using System;

namespace TidesOfTime.Sailing;

public partial class SailingWakeView : Node3D
{
	[Export] public NodePath BoatPath { get; set; } = new("../PlayerBoat");
	[Export] public NodePath PortWakePath { get; set; } = new("PortWake");
	[Export] public NodePath StarboardWakePath { get; set; } = new("StarboardWake");

	[ExportGroup("Shape")]
	[Export] public float WaterHeight { get; set; } = 0.025f;
	[Export] public float DistanceBehindBoat { get; set; } = 1.25f;
	[Export] public float WakeBackOffset { get; set; } = 1.15f;
	[Export] public float WakeSeparation { get; set; } = 0.78f;
	[Export] public float WakeWidth { get; set; } = 0.55f;
	[Export] public float WakeLength { get; set; } = 4.8f;
	[Export] public float WakeAngleDegrees { get; set; } = 8.0f;

	[ExportGroup("Response")]
	[Export] public float MinVisibleSpeed { get; set; } = 1.2f;
	[Export] public float FullWakeSpeed { get; set; } = 11.0f;
	[Export] public float Responsiveness { get; set; } = 6.0f;

	private PlayerBoatController? _boat;
	private MeshInstance3D? _portWake;
	private MeshInstance3D? _starboardWake;
	private float _intensity;

	public override void _Ready()
	{
		ProcessPriority = -10;
		_boat = GetNodeOrNull<PlayerBoatController>(BoatPath);
		_portWake = GetNodeOrNull<MeshInstance3D>(PortWakePath);
		_starboardWake = GetNodeOrNull<MeshInstance3D>(StarboardWakePath);

		SetWakeVisible(false);
	}

	public override void _Process(double delta)
	{
		if (_boat == null || _portWake == null || _starboardWake == null)
		{
			return;
		}

		var deltaSeconds = (float)delta;
		var targetIntensity = Mathf.Clamp(
			(_boat.Speed - MinVisibleSpeed) / Mathf.Max(0.01f, FullWakeSpeed - MinVisibleSpeed),
			0.0f,
			1.0f);
		_intensity = Mathf.Lerp(_intensity, targetIntensity, GetSmoothingWeight(Responsiveness, deltaSeconds));

		UpdateWakeTransform();
		UpdateWakeMesh(_portWake, -1.0f);
		UpdateWakeMesh(_starboardWake, 1.0f);
		SetWakeVisible(_intensity > 0.03f);
	}

	private void UpdateWakeTransform()
	{
		var forward = -_boat!.GlobalTransform.Basis.Z;
		forward.Y = 0.0f;
		forward = forward.LengthSquared() <= 0.0001f ? Vector3.Forward : forward.Normalized();

		var position = _boat.GlobalPosition - (forward * DistanceBehindBoat);
		position.Y = WaterHeight;

		GlobalPosition = position;
		GlobalRotation = new Vector3(0.0f, _boat.GlobalRotation.Y, 0.0f);
	}

	private void UpdateWakeMesh(MeshInstance3D wakeMesh, float side)
	{
		var width = WakeWidth * (0.55f + (_intensity * 0.7f));
		var length = WakeLength * (0.3f + (_intensity * 0.9f));
		var angle = DegreesToRadians(WakeAngleDegrees) * side;

		wakeMesh.Position = new Vector3(side * WakeSeparation * (0.8f + _intensity), 0.0f, WakeBackOffset + (length * 0.25f));
		wakeMesh.Rotation = new Vector3(0.0f, angle, 0.0f);
		wakeMesh.Scale = new Vector3(width, 1.0f, length);
	}

	private void SetWakeVisible(bool visible)
	{
		if (_portWake != null)
		{
			_portWake.Visible = visible;
		}

		if (_starboardWake != null)
		{
			_starboardWake.Visible = visible;
		}
	}

	private static float DegreesToRadians(float degrees)
	{
		return degrees * (MathF.PI / 180.0f);
	}

	private static float GetSmoothingWeight(float responsiveness, float deltaSeconds)
	{
		return 1.0f - MathF.Exp(-Mathf.Max(0.0f, responsiveness) * deltaSeconds);
	}
}
