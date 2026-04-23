using Godot;
using System;

namespace TidesOfTime.Sailing;

public partial class PlayerBoatController : Node3D
{
	[ExportGroup("Speed")]
	[Export] public float MaxForwardSpeed { get; set; } = 14.0f;
	[Export] public float MaxReverseSpeed { get; set; } = 4.0f;
	[Export] public float ForwardAcceleration { get; set; } = 18.0f;
	[Export] public float ReverseAcceleration { get; set; } = 10.0f;

	[ExportGroup("Handling")]
	[Export] public float TurnDegreesPerSecond { get; set; } = 74.0f;
	[Export] public float MinimumTurnInfluence { get; set; } = 0.24f;
	[Export] public float ActiveDrag { get; set; } = 0.35f;
	[Export] public float CoastDrag { get; set; } = 3.2f;
	[Export] public float LateralDrag { get; set; } = 7.5f;
	[Export] public float HardBrakeDrag { get; set; } = 12.0f;

	[ExportGroup("Feel")]
	[Export] public float LeanDegrees { get; set; } = 8.0f;
	[Export] public float LeanResponsiveness { get; set; } = 8.0f;
	[Export] public float BobHeight { get; set; } = 0.08f;
	[Export] public float BobFrequency { get; set; } = 1.7f;
	[Export] public float SpeedBobInfluence { get; set; } = 1.3f;

	public Vector3 Velocity => _velocity;
	public float Speed => new Vector2(_velocity.X, _velocity.Z).Length();

	private Vector3 _velocity = Vector3.Zero;
	private Vector3 _startPosition;
	private float _startYaw;
	private float _waterHeight;
	private float _yaw;
	private float _roll;
	private float _bobTime;

	public override void _Ready()
	{
		_startPosition = GlobalPosition;
		_startYaw = Rotation.Y;
		_waterHeight = GlobalPosition.Y;
		_yaw = _startYaw;
	}

	public override void _PhysicsProcess(double delta)
	{
		var deltaSeconds = (float)delta;
		var throttleInput = GetThrottleInput();
		var turnInput = GetTurnInput();
		var hardBrakeHeld = Input.IsKeyPressed(Key.Space);

		var forward = GetFlatForward();
		var forwardSpeed = _velocity.Dot(forward);
		var lateralVelocity = _velocity - (forward * forwardSpeed);

		if (Mathf.Abs(throttleInput) > 0.01f)
		{
			var acceleration = throttleInput > 0.0f ? ForwardAcceleration : ReverseAcceleration;
			forwardSpeed += throttleInput * acceleration * deltaSeconds;
		}

		var drag = Mathf.Abs(throttleInput) > 0.01f ? ActiveDrag : CoastDrag;
		forwardSpeed = Mathf.MoveToward(forwardSpeed, 0.0f, drag * deltaSeconds);
		forwardSpeed = Mathf.Clamp(forwardSpeed, -MaxReverseSpeed, MaxForwardSpeed);
		lateralVelocity = lateralVelocity.MoveToward(Vector3.Zero, LateralDrag * deltaSeconds);

		if (hardBrakeHeld)
		{
			forwardSpeed = Mathf.MoveToward(forwardSpeed, 0.0f, HardBrakeDrag * deltaSeconds);
			lateralVelocity = lateralVelocity.MoveToward(Vector3.Zero, HardBrakeDrag * deltaSeconds);
		}

		ApplyTurning(turnInput, throttleInput, forwardSpeed, deltaSeconds);

		var steeredForward = GetFlatForward();
		_velocity = (steeredForward * forwardSpeed) + lateralVelocity;

		var nextPosition = GlobalPosition + (_velocity * deltaSeconds);
		nextPosition.Y = _waterHeight + GetBobOffset(deltaSeconds);
		GlobalPosition = nextPosition;
	}

	public void ResetToStart()
	{
		_velocity = Vector3.Zero;
		_yaw = _startYaw;
		_roll = 0.0f;
		_bobTime = 0.0f;
		GlobalPosition = _startPosition;
		Rotation = new Vector3(0.0f, _yaw, 0.0f);
	}

	private void ApplyTurning(float turnInput, float throttleInput, float forwardSpeed, float deltaSeconds)
	{
		var speedRatio = Mathf.Clamp(Mathf.Abs(forwardSpeed) / Mathf.Max(0.01f, MaxForwardSpeed), 0.0f, 1.0f);
		var throttleTurnBoost = Mathf.Abs(throttleInput) > 0.01f ? 0.35f : 0.0f;
		var turnInfluence = Mathf.Clamp(Mathf.Max(speedRatio, throttleTurnBoost), MinimumTurnInfluence, 1.0f);
		var turnRadiansPerSecond = DegreesToRadians(TurnDegreesPerSecond);

		_yaw -= turnInput * turnRadiansPerSecond * turnInfluence * deltaSeconds;

		var desiredRoll = DegreesToRadians(-turnInput * LeanDegrees) * speedRatio;
		_roll = Mathf.Lerp(_roll, desiredRoll, GetSmoothingWeight(LeanResponsiveness, deltaSeconds));
		Rotation = new Vector3(0.0f, _yaw, _roll);
	}

	private float GetBobOffset(float deltaSeconds)
	{
		var speedRatio = Mathf.Clamp(Speed / Mathf.Max(0.01f, MaxForwardSpeed), 0.0f, 1.0f);
		_bobTime += deltaSeconds * (BobFrequency + (speedRatio * SpeedBobInfluence));

		return MathF.Sin(_bobTime) * BobHeight * (0.35f + (speedRatio * 0.65f));
	}

	private Vector3 GetFlatForward()
	{
		var forward = -GlobalTransform.Basis.Z;
		forward.Y = 0.0f;

		return forward.LengthSquared() <= 0.0001f ? Vector3.Forward : forward.Normalized();
	}

	private static float GetThrottleInput()
	{
		var input = 0.0f;

		if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
		{
			input += 1.0f;
		}

		if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
		{
			input -= 1.0f;
		}

		return Mathf.Clamp(input, -1.0f, 1.0f);
	}

	private static float GetTurnInput()
	{
		var input = 0.0f;

		if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
		{
			input -= 1.0f;
		}

		if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
		{
			input += 1.0f;
		}

		return Mathf.Clamp(input, -1.0f, 1.0f);
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
