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

	[ExportGroup("Boost")]
	[Export] public float BoostAccelerationMultiplier { get; set; } = 1.35f;
	[Export] public float BoostMaxForwardSpeedMultiplier { get; set; } = 1.22f;
	[Export] public float BoostDurationSeconds { get; set; } = 1.25f;
	[Export] public float BoostRechargeSeconds { get; set; } = 6.0f;
	[Export] public float BoostRechargeDelaySeconds { get; set; } = 0.75f;

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

	[Export] public bool InputEnabled { get; set; } = true;

	public Vector3 Velocity => _velocity;
	public float Speed => new Vector2(_velocity.X, _velocity.Z).Length();
	public float BoostChargeRatio => BoostDurationSeconds <= 0.0f
		? 0.0f
		: Mathf.Clamp(_boostChargeSeconds / BoostDurationSeconds, 0.0f, 1.0f);
	public bool IsBoosting => _isBoosting;
	public bool IsFallingOffEdge => _isFallingOffEdge;

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
	private float _boostChargeSeconds;
	private float _boostRechargeDelayTimer;
	private bool _isBoosting;
	private bool _boostBlockedUntilReleased;
	private float _fallVerticalSpeed;
	private float _fallPitchSpeed;
	private float _fallRollSpeed;
	private bool _isFallingOffEdge;

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
		ResetBoost();
	}

	public override void _Process(double delta)
	{
		var deltaSeconds = (float)delta;

		if (_isFallingOffEdge)
		{
			UpdateFallOffEdge(deltaSeconds);
			return;
		}

		var throttleInput = InputEnabled ? GetThrottleInput() : 0.0f;
		var turnInput = InputEnabled ? GetTurnInput() : 0.0f;
		var hardBrakeHeld = InputEnabled && Input.IsKeyPressed(Key.Shift);
		var boostRequested = InputEnabled && IsBoostInputHeld();

		var forward = GetFlatForward();
		var forwardSpeedBeforeAcceleration = _velocity.Dot(forward);
		var boostActive = UpdateBoost(
			boostRequested,
			throttleInput,
			forwardSpeedBeforeAcceleration,
			hardBrakeHeld,
			deltaSeconds);
		var maxForwardSpeed = GetMaxForwardSpeed(boostActive);
		var accelerationThrottleInput = boostActive && throttleInput <= 0.01f
			? 1.0f
			: throttleInput;

		if (Mathf.Abs(accelerationThrottleInput) > 0.01f)
		{
			var isForwardAcceleration = accelerationThrottleInput > 0.0f;
			var canAccelerate = !isForwardAcceleration ||
				boostActive ||
				forwardSpeedBeforeAcceleration < maxForwardSpeed;

			if (canAccelerate)
			{
				var acceleration = isForwardAcceleration ? ForwardAcceleration : ReverseAcceleration;
				if (boostActive && isForwardAcceleration)
				{
					acceleration *= Mathf.Max(1.0f, BoostAccelerationMultiplier);
				}

				_velocity += forward * accelerationThrottleInput * acceleration * deltaSeconds;
			}
		}

		var shouldCoastDownFromBoost = !boostActive && forwardSpeedBeforeAcceleration > maxForwardSpeed;
		var forwardSpeed = ApplyPlanarDrag(
			forward,
			accelerationThrottleInput,
			hardBrakeHeld,
			deltaSeconds,
			maxForwardSpeed,
			shouldCoastDownFromBoost);
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
		ResetBoost();
		_isFallingOffEdge = false;
		_fallVerticalSpeed = 0.0f;
		_fallPitchSpeed = 0.0f;
		_fallRollSpeed = 0.0f;
		GlobalPosition = _startPosition;
		Rotation = new Vector3(0.0f, _yaw, 0.0f);

		if (_visualRoot != null)
		{
			_visualRoot.Position = _visualRestPosition;
			_visualRoot.Rotation = Vector3.Zero;
		}
	}

	public void BeginFallOffEdge()
	{
		if (_isFallingOffEdge)
		{
			return;
		}

		_isFallingOffEdge = true;
		_isBoosting = false;
		_boostBlockedUntilReleased = true;
		_boostRechargeDelayTimer = Mathf.Max(0.0f, BoostRechargeDelaySeconds);
		_fallVerticalSpeed = 2.5f;
		_fallPitchSpeed = DegreesToRadians(74.0f);
		_fallRollSpeed = DegreesToRadians(GlobalPosition.X >= 0.0f ? -112.0f : 112.0f);
	}

	private void UpdateFallOffEdge(float deltaSeconds)
	{
		_velocity = _velocity.MoveToward(Vector3.Zero, CoastDrag * 0.45f * deltaSeconds);
		_fallVerticalSpeed += 11.0f * deltaSeconds;

		GlobalPosition += new Vector3(
			_velocity.X,
			-_fallVerticalSpeed,
			_velocity.Z) * deltaSeconds;
		Rotation += new Vector3(
			_fallPitchSpeed * deltaSeconds,
			0.0f,
			_fallRollSpeed * deltaSeconds);
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

	private float ApplyPlanarDrag(
		Vector3 forward,
		float throttleInput,
		bool hardBrakeHeld,
		float deltaSeconds,
		float maxForwardSpeed,
		bool shouldCoastDownFromBoost)
	{
		var forwardSpeed = _velocity.Dot(forward);
		var lateralVelocity = _velocity - (forward * forwardSpeed);
		var drag = Mathf.Abs(throttleInput) > 0.01f ? ActiveDrag : CoastDrag;
		var cappedMaxForwardSpeed = Mathf.Max(0.01f, maxForwardSpeed);

		forwardSpeed = Mathf.MoveToward(forwardSpeed, 0.0f, drag * deltaSeconds);
		forwardSpeed = Mathf.Max(forwardSpeed, -MaxReverseSpeed);
		if (forwardSpeed > cappedMaxForwardSpeed)
		{
			forwardSpeed = shouldCoastDownFromBoost
				? Mathf.MoveToward(forwardSpeed, cappedMaxForwardSpeed, Mathf.Max(drag, CoastDrag) * deltaSeconds)
				: cappedMaxForwardSpeed;
		}

		lateralVelocity = lateralVelocity.MoveToward(Vector3.Zero, LateralDrag * deltaSeconds);

		if (hardBrakeHeld)
		{
			forwardSpeed = Mathf.MoveToward(forwardSpeed, 0.0f, HardBrakeDrag * deltaSeconds);
			lateralVelocity = lateralVelocity.MoveToward(Vector3.Zero, HardBrakeDrag * deltaSeconds);
		}

		_velocity = (forward * forwardSpeed) + lateralVelocity;

		return forwardSpeed;
	}

	private bool UpdateBoost(
		bool boostRequested,
		float throttleInput,
		float forwardSpeed,
		bool hardBrakeHeld,
		float deltaSeconds)
	{
		var boostCapacity = Mathf.Max(0.0f, BoostDurationSeconds);
		if (boostCapacity <= 0.0f)
		{
			_isBoosting = false;
			_boostChargeSeconds = 0.0f;
			return false;
		}

		if (!boostRequested)
		{
			_boostBlockedUntilReleased = false;
		}

		var canApplyBoost = boostRequested &&
			!_boostBlockedUntilReleased &&
			_boostChargeSeconds > 0.0f &&
			!hardBrakeHeld &&
			throttleInput >= -0.01f &&
			forwardSpeed >= 0.0f &&
			(throttleInput > 0.01f || forwardSpeed > 0.25f);

		if (canApplyBoost)
		{
			_isBoosting = true;
			_boostChargeSeconds = Mathf.Max(0.0f, _boostChargeSeconds - deltaSeconds);
			_boostRechargeDelayTimer = Mathf.Max(0.0f, BoostRechargeDelaySeconds);

			if (_boostChargeSeconds <= 0.0f)
			{
				_boostBlockedUntilReleased = true;
			}

			return true;
		}

		_isBoosting = false;
		if (_boostRechargeDelayTimer > 0.0f)
		{
			_boostRechargeDelayTimer = Mathf.Max(0.0f, _boostRechargeDelayTimer - deltaSeconds);
			return false;
		}

		var rechargeSeconds = Mathf.Max(0.01f, BoostRechargeSeconds);
		_boostChargeSeconds = Mathf.Min(
			boostCapacity,
			_boostChargeSeconds + (boostCapacity / rechargeSeconds * deltaSeconds));
		return false;
	}

	private float GetMaxForwardSpeed(bool boostActive)
	{
		var multiplier = boostActive
			? Mathf.Max(1.0f, BoostMaxForwardSpeedMultiplier)
			: 1.0f;

		return MaxForwardSpeed * multiplier;
	}

	private void ResetBoost()
	{
		_boostChargeSeconds = Mathf.Max(0.0f, BoostDurationSeconds);
		_boostRechargeDelayTimer = 0.0f;
		_isBoosting = false;
		_boostBlockedUntilReleased = false;
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

	private static bool IsBoostInputHeld()
	{
		return Input.IsKeyPressed(Key.Space);
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
