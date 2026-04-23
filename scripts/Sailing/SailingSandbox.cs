using Godot;
using System.Collections.Generic;

namespace TidesOfTime.Sailing;

public partial class SailingSandbox : Node3D
{
	[Export] public NodePath PlayerBoatPath { get; set; } = new("PlayerBoat");
	[Export] public NodePath HudLabelPath { get; set; } = new("HUD/PanelContainer/MarginContainer/InfoLabel");
	[Export] public NodePath CheckpointsRootPath { get; set; } = new("Course/Checkpoints");
	[Export] public float CheckpointRadius { get; set; } = 4.25f;
	[Export] public float FeedbackSeconds { get; set; } = 1.8f;

	private PlayerBoatController? _playerBoat;
	private Label? _hudLabel;
	private readonly List<Node3D> _checkpoints = new();
	private int _nextCheckpointIndex;
	private int _lapCount;
	private float _feedbackTimer;
	private string _feedbackText = "Find checkpoint 1.";

	public override void _Ready()
	{
		_playerBoat = GetNodeOrNull<PlayerBoatController>(PlayerBoatPath);
		_hudLabel = GetNodeOrNull<Label>(HudLabelPath);
		LoadCheckpoints();
		UpdateHud();
	}

	public override void _Process(double delta)
	{
		UpdateCourse((float)delta);
		UpdateHud();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
		{
			return;
		}

		if (keyEvent.Keycode == Key.R || keyEvent.PhysicalKeycode == Key.R)
		{
			_playerBoat?.ResetToStart();
			ResetCourse("Boat and course reset.");
		}

		if (keyEvent.Keycode == Key.C || keyEvent.PhysicalKeycode == Key.C)
		{
			ResetCourse("Course reset.");
		}
	}

	private void LoadCheckpoints()
	{
		_checkpoints.Clear();

		var checkpointRoot = GetNodeOrNull<Node3D>(CheckpointsRootPath);

		if (checkpointRoot == null)
		{
			GD.PushWarning("SailingSandbox: no checkpoint root assigned.");
			return;
		}

		foreach (var child in checkpointRoot.GetChildren())
		{
			if (child is Node3D checkpoint)
			{
				_checkpoints.Add(checkpoint);
			}
		}
	}

	private void UpdateCourse(float deltaSeconds)
	{
		if (_feedbackTimer > 0.0f)
		{
			_feedbackTimer = Mathf.Max(0.0f, _feedbackTimer - deltaSeconds);
		}

		if (_playerBoat == null || _checkpoints.Count == 0)
		{
			return;
		}

		var checkpoint = _checkpoints[_nextCheckpointIndex];

		if (GetFlatDistance(_playerBoat.GlobalPosition, checkpoint.GlobalPosition) > CheckpointRadius)
		{
			return;
		}

		AdvanceCheckpoint();
	}

	private void AdvanceCheckpoint()
	{
		var clearedCheckpointNumber = _nextCheckpointIndex + 1;
		_nextCheckpointIndex++;

		if (_nextCheckpointIndex >= _checkpoints.Count)
		{
			_lapCount++;
			_nextCheckpointIndex = 0;
			ShowFeedback($"Lap {_lapCount} complete. Start another run!");
			return;
		}

		ShowFeedback($"Checkpoint {clearedCheckpointNumber} cleared.");
	}

	private void ResetCourse(string message)
	{
		_nextCheckpointIndex = 0;
		_lapCount = 0;
		ShowFeedback(message);
	}

	private void ShowFeedback(string message)
	{
		_feedbackText = message;
		_feedbackTimer = FeedbackSeconds;
	}

	private void UpdateHud()
	{
		if (_hudLabel == null)
		{
			return;
		}

		var speedText = _playerBoat == null
			? "Speed: no player boat assigned"
			: $"Speed: {_playerBoat.Speed:0.0}";
		var courseText = GetCourseHudText();

		_hudLabel.Text = "Sailing Sandbox\n"
			+ "Goal: weave through the buoy course\n"
			+ "W / Up: accelerate\n"
			+ "S / Down: brake or reverse\n"
			+ "A/D or Left/Right: turn\n"
			+ "Space: hard brake\n"
			+ "R: reset boat + course\n"
			+ "C: reset course only\n"
			+ speedText + "\n"
			+ courseText;
	}

	private string GetCourseHudText()
	{
		if (_checkpoints.Count == 0)
		{
			return "Course: no checkpoints found";
		}

		var nextCheckpointNumber = _nextCheckpointIndex + 1;
		var nextCheckpointName = _checkpoints[_nextCheckpointIndex].Name;
		var feedback = _feedbackTimer > 0.0f ? $"\n{_feedbackText}" : string.Empty;

		return $"Lap: {_lapCount} | Next: {nextCheckpointNumber}/{_checkpoints.Count} {nextCheckpointName}{feedback}";
	}

	private static float GetFlatDistance(Vector3 first, Vector3 second)
	{
		first.Y = 0.0f;
		second.Y = 0.0f;

		return first.DistanceTo(second);
	}
}
