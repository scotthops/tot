using Godot;
using System;

namespace TidesOfTime.Sailing;

public partial class SailingChaseCamera : Camera3D
{
	[Export] public NodePath TargetPath { get; set; } = new("");
	[Export] public float FollowDistance { get; set; } = 11.0f;
	[Export] public float FollowHeight { get; set; } = 5.6f;
	[Export] public float LookHeight { get; set; } = 1.0f;
	[Export] public float LookAheadDistance { get; set; } = 5.2f;
	[Export] public float PositionResponsiveness { get; set; } = 5.8f;
	[Export] public float LookResponsiveness { get; set; } = 8.5f;

	private Node3D? _target;
	private Vector3 _smoothedLookTarget;

	public override void _Ready()
	{
		_target = ResolveTarget();

		if (_target == null)
		{
			GD.PushWarning("SailingChaseCamera: TargetPath is not assigned.");
			return;
		}

		_smoothedLookTarget = CalculateLookTarget();
		GlobalPosition = CalculateDesiredPosition();
		LookAt(_smoothedLookTarget, Vector3.Up);
	}

	public override void _Process(double delta)
	{
		if (_target == null)
		{
			return;
		}

		var deltaSeconds = (float)delta;
		var desiredPosition = CalculateDesiredPosition();
		var desiredLookTarget = CalculateLookTarget();

		GlobalPosition = GlobalPosition.Lerp(desiredPosition, GetSmoothingWeight(PositionResponsiveness, deltaSeconds));
		_smoothedLookTarget = _smoothedLookTarget.Lerp(desiredLookTarget, GetSmoothingWeight(LookResponsiveness, deltaSeconds));
		LookAt(_smoothedLookTarget, Vector3.Up);
	}

	private Node3D? ResolveTarget()
	{
		return string.IsNullOrWhiteSpace(TargetPath.ToString())
			? null
			: GetNodeOrNull<Node3D>(TargetPath);
	}

	private Vector3 CalculateDesiredPosition()
	{
		var targetPosition = _target!.GlobalPosition;
		var targetForward = GetTargetForward();

		return targetPosition - (targetForward * FollowDistance) + (Vector3.Up * FollowHeight);
	}

	private Vector3 CalculateLookTarget()
	{
		var lookTarget = _target!.GlobalPosition + (Vector3.Up * LookHeight);

		if (_target is PlayerBoatController boat && boat.Speed > 0.05f)
		{
			var velocity = boat.Velocity;
			velocity.Y = 0.0f;

			if (velocity.LengthSquared() > 0.001f)
			{
				var speedRatio = Mathf.Clamp(boat.Speed / Mathf.Max(0.01f, boat.MaxForwardSpeed), 0.0f, 1.0f);
				return lookTarget + (velocity.Normalized() * LookAheadDistance * speedRatio);
			}
		}

		return lookTarget + (GetTargetForward() * LookAheadDistance * 0.35f);
	}

	private Vector3 GetTargetForward()
	{
		var forward = -_target!.GlobalTransform.Basis.Z;
		forward.Y = 0.0f;

		return forward.LengthSquared() <= 0.0001f ? Vector3.Forward : forward.Normalized();
	}

	private static float GetSmoothingWeight(float responsiveness, float deltaSeconds)
	{
		return 1.0f - MathF.Exp(-Mathf.Max(0.0f, responsiveness) * deltaSeconds);
	}
}
