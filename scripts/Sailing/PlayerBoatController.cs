using Godot;
using System;

namespace TidesOfTime.Sailing;

public partial class PlayerBoatController : Node3D
{
	private const float Tau = MathF.PI * 2.0f;

	[ExportGroup("Speed")]
	[Export] public float MaxForwardSpeed { get; set; } = 14.0f;
	[Export] public float MaxReverseSpeed { get; set; } = 3.2f;
	[Export] public float ForwardAcceleration { get; set; } = 16.0f;
	[Export] public float ReverseAcceleration { get; set; } = 8.0f;

	[ExportGroup("Handling")]
	[Export] public float TurnDegreesPerSecond { get; set; } = 82.0f;
	[Export] public float TurnInputResponsiveness { get; set; } = 7.0f;
	[Export] public float MinimumTurnInfluence { get; set; } = 0.18f;
	[Export] public float ActiveDrag { get; set; } = 0.2f;
	[Export] public float CoastDrag { get; set; } = 1.45f;
	[Export] public float LateralDrag { get; set; } = 4.6f;
	[Export] public float HardBrakeDrag { get; set; } = 10.0f;

	[ExportGroup("Feel")]
	[Export] public float LeanDegrees { get; set; } = 10.0f;
	[Export] public float LeanResponsiveness { get; set; } = 7.0f;
	[Export] public float PitchDegrees { get; set; } = 3.0f;
	[Export] public float PitchResponsiveness { get; set; } = 4.5f;

	[ExportGroup("Fake Waves")]
	[Export] public NodePath VisualRootPath { get; set; } = new("Visuals");
	[Export] public float WaveHeight { get; set; } = 0.18f;
	[Export] public float WaveLength { get; set; } = 14.0f;
	[Export] public float WaveFrequency { get; set; } = 0.16f;
	[Export] public float SecondaryWaveHeight { get; set; } = 0.06f;
	[Export] public float SecondaryWaveLength { get; set; } = 7.5f;
	[Export] public float SecondaryWaveFrequency { get; set; } = 0.27f;
	[Export] public float WaveSampleDistance { get; set; } = 2.0f;
	[Export] public float WaveSmoothing { get; set; } = 7.5f;
	[Export] public float WavePitchDegrees { get; set; } = 4.5f;
	[Export] public float WaveRollDegrees { get; set; } = 5.5f;

	public Vector3 Velocity => _velocity;
	public float Speed => new Vector2(_velocity.X, _velocity.Z).Length();

	private Node3D? _visualRoot;
	private Vector3 _visualRestPosition;
	private Vector3 _velocity = Vector3.Zero;
	private Vector3 _startPosition;
	private float _startYaw;
	private float _waterHeight;
	private float _yaw;
	private float _pitch;
	private float _roll;
	private float _smoothedTurnInput;
	private float _waveTime;
	private float _waveBob;
	private float _wavePitch;
	private float _waveRoll;

	public override void _Ready()
	{
		ProcessPriority = -20;
		_visualRoot = GetNodeOrNull<Node3D>(VisualRootPath);

		if (_visualRoot == null)
		{
			GD.PushWarning("PlayerBoatController: VisualRootPath is not assigned.");
		}
		else
		{
			_visualRestPosition = _visualRoot.Position;
		}

		_startPosition = GlobalPosition;
		_startYaw = Rotation.Y;
		_waterHeight = GlobalPosition.Y;
		_yaw = _startYaw;
	}

	public override void _Process(double delta)
	{
		var deltaSeconds = (float)delta;
		var throttleInput = GetThrottleInput();
		var turnInput = GetTurnInput();
		var hardBrakeHeld = Input.IsKeyPressed(Key.Space);

		var forward = GetFlatForward();

		if (Mathf.Abs(throttleInput) > 0.01f)
		{
			var acceleration = throttleInput > 0.0f ? ForwardAcceleration : ReverseAcceleration;
			_velocity += forward * throttleInput * acceleration * deltaSeconds;
		}

		var forwardSpeed = ApplyPlanarDrag(forward, throttleInput, hardBrakeHeld, deltaSeconds);
		ApplyTurning(turnInput, throttleInput, forwardSpeed, deltaSeconds);

		var nextPosition = GlobalPosition + (_velocity * deltaSeconds);
		nextPosition.Y = _waterHeight;
		GlobalPosition = nextPosition;

		_waveTime += deltaSeconds;
		UpdateWavePresentation(deltaSeconds);
	}

	public void ResetToStart()
	{
		_velocity = Vector3.Zero;
		_yaw = _startYaw;
		_pitch = 0.0f;
		_roll = 0.0f;
		_smoothedTurnInput = 0.0f;
		_waveTime = 0.0f;
		_waveBob = 0.0f;
		_wavePitch = 0.0f;
		_waveRoll = 0.0f;
		GlobalPosition = _startPosition;
		Rotation = new Vector3(0.0f, _yaw, 0.0f);

		if (_visualRoot != null)
		{
			_visualRoot.Position = _visualRestPosition;
			_visualRoot.Rotation = Vector3.Zero;
		}
	}

	private void ApplyTurning(float turnInput, float throttleInput, float forwardSpeed, float deltaSeconds)
	{
		var speedRatio = Mathf.Clamp(Mathf.Abs(forwardSpeed) / Mathf.Max(0.01f, MaxForwardSpeed), 0.0f, 1.0f);
		var throttleTurnBoost = Mathf.Abs(throttleInput) > 0.01f ? 0.28f : 0.0f;
		var turnInfluence = Mathf.Clamp(Mathf.Max(speedRatio, throttleTurnBoost), MinimumTurnInfluence, 1.0f);
		var turnRadiansPerSecond = DegreesToRadians(TurnDegreesPerSecond);

		_smoothedTurnInput = Mathf.Lerp(
			_smoothedTurnInput,
			turnInput,
			GetSmoothingWeight(TurnInputResponsiveness, deltaSeconds));
		_yaw -= _smoothedTurnInput * turnRadiansPerSecond * turnInfluence * deltaSeconds;

		var desiredPitch = DegreesToRadians(-throttleInput * PitchDegrees) * (0.2f + (speedRatio * 0.8f));
		var desiredRoll = DegreesToRadians(-_smoothedTurnInput * LeanDegrees) * speedRatio;
		_pitch = Mathf.Lerp(_pitch, desiredPitch, GetSmoothingWeight(PitchResponsiveness, deltaSeconds));
		_roll = Mathf.Lerp(_roll, desiredRoll, GetSmoothingWeight(LeanResponsiveness, deltaSeconds));
		Rotation = new Vector3(0.0f, _yaw, 0.0f);
	}

	private float ApplyPlanarDrag(Vector3 forward, float throttleInput, bool hardBrakeHeld, float deltaSeconds)
	{
		var forwardSpeed = _velocity.Dot(forward);
		var lateralVelocity = _velocity - (forward * forwardSpeed);
		var drag = Mathf.Abs(throttleInput) > 0.01f ? ActiveDrag : CoastDrag;

		forwardSpeed = Mathf.MoveToward(forwardSpeed, 0.0f, drag * deltaSeconds);
		forwardSpeed = Mathf.Clamp(forwardSpeed, -MaxReverseSpeed, MaxForwardSpeed);
		lateralVelocity = lateralVelocity.MoveToward(Vector3.Zero, LateralDrag * deltaSeconds);

		if (hardBrakeHeld)
		{
			forwardSpeed = Mathf.MoveToward(forwardSpeed, 0.0f, HardBrakeDrag * deltaSeconds);
			lateralVelocity = lateralVelocity.MoveToward(Vector3.Zero, HardBrakeDrag * deltaSeconds);
		}

		_velocity = (forward * forwardSpeed) + lateralVelocity;

		return forwardSpeed;
	}

	private void UpdateWavePresentation(float deltaSeconds)
	{
		if (_visualRoot == null)
		{
			return;
		}

		var sampleDistance = Mathf.Max(0.01f, WaveSampleDistance);
		var forward = GetFlatForward();
		var right = GetFlatRight();
		var center = GlobalPosition;
		var centerHeight = GetWaveHeightAt(center);
		var frontHeight = GetWaveHeightAt(center + (forward * sampleDistance));
		var backHeight = GetWaveHeightAt(center - (forward * sampleDistance));
		var rightHeight = GetWaveHeightAt(center + (right * sampleDistance));
		var leftHeight = GetWaveHeightAt(center - (right * sampleDistance));
		var heightRange = sampleDistance * 2.0f;
		var targetWavePitch = Mathf.Clamp((backHeight - frontHeight) / heightRange, -1.0f, 1.0f) * DegreesToRadians(WavePitchDegrees);
		var targetWaveRoll = Mathf.Clamp((rightHeight - leftHeight) / heightRange, -1.0f, 1.0f) * DegreesToRadians(WaveRollDegrees);
		var smoothingWeight = GetSmoothingWeight(WaveSmoothing, deltaSeconds);

		_waveBob = Mathf.Lerp(_waveBob, centerHeight, smoothingWeight);
		_wavePitch = Mathf.Lerp(_wavePitch, targetWavePitch, smoothingWeight);
		_waveRoll = Mathf.Lerp(_waveRoll, targetWaveRoll, smoothingWeight);

		_visualRoot.Position = _visualRestPosition + (Vector3.Up * _waveBob);
		_visualRoot.Rotation = new Vector3(_pitch + _wavePitch, 0.0f, _roll + _waveRoll);
	}

	private float GetWaveHeightAt(Vector3 worldPosition)
	{
		var primaryPhase = (((worldPosition.X * 0.35f) + (worldPosition.Z * 0.94f)) / Mathf.Max(0.01f, WaveLength) * Tau)
			+ (_waveTime * WaveFrequency * Tau);
		var secondaryPhase = (((worldPosition.X * -0.8f) + (worldPosition.Z * 0.45f)) / Mathf.Max(0.01f, SecondaryWaveLength) * Tau)
			+ (_waveTime * SecondaryWaveFrequency * Tau)
			+ 1.4f;

		return (MathF.Sin(primaryPhase) * WaveHeight) + (MathF.Sin(secondaryPhase) * SecondaryWaveHeight);
	}

	private Vector3 GetFlatForward()
	{
		var forward = -GlobalTransform.Basis.Z;
		forward.Y = 0.0f;

		return forward.LengthSquared() <= 0.0001f ? Vector3.Forward : forward.Normalized();
	}

	private Vector3 GetFlatRight()
	{
		var right = GlobalTransform.Basis.X;
		right.Y = 0.0f;

		return right.LengthSquared() <= 0.0001f ? Vector3.Right : right.Normalized();
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
