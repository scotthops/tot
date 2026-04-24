using Godot;
using System.Collections.Generic;
using TidesOfTime.Data;
using TidesOfTime.Encounters;

namespace TidesOfTime.Sailing;

public partial class SailingSandbox : Node3D
{
	[Export] public NodePath PlayerBoatPath { get; set; } = new("PlayerBoat");
	[Export] public NodePath HudLabelPath { get; set; } = new("HUD/PanelContainer/MarginContainer/InfoLabel");
	[Export] public NodePath CheckpointsRootPath { get; set; } = new("Course/Checkpoints");
	[Export] public NodePath EncounterContactPath { get; set; } = new("EnemyContact");
	[Export] public NodePath TownDockPath { get; set; } = new("TownDock");
	[Export] public NodePath TownPromptPanelPath { get; set; } = new("HUD/TownPromptPanel");
	[Export] public NodePath TownPromptLabelPath { get; set; } = new("HUD/TownPromptPanel/MarginContainer/PromptLabel");
	[Export] public NodePath TownPanelPath { get; set; } = new("HUD/TownPanel");
	[Export] public NodePath TownNameLabelPath { get; set; } = new("HUD/TownPanel/MarginContainer/VBoxContainer/TownNameLabel");
	[Export] public NodePath TownBodyLabelPath { get; set; } = new("HUD/TownPanel/MarginContainer/VBoxContainer/TownBodyLabel");
	[Export] public NodePath MerchantButtonPath { get; set; } = new("HUD/TownPanel/MarginContainer/VBoxContainer/ButtonRow/MerchantButton");
	[Export] public NodePath RepairButtonPath { get; set; } = new("HUD/TownPanel/MarginContainer/VBoxContainer/ButtonRow/RepairButton");
	[Export] public NodePath LeaveTownButtonPath { get; set; } = new("HUD/TownPanel/MarginContainer/VBoxContainer/ButtonRow/LeaveTownButton");
	[Export] public ShipArchetypeDef? PlayerShipArchetype { get; set; }
	[Export] public ShipArchetypeDef? EnemyShipArchetype { get; set; }
	[Export] public string BattleScenePath { get; set; } = "res://scenes/battle/battle_scene.tscn";
	[Export] public string ReturnScenePath { get; set; } = "res://scenes/sailing/sailing_sandbox.tscn";
	[Export] public string TownName { get; set; } = "Saltwind Harbor";
	[Export] public float CheckpointRadius { get; set; } = 4.25f;
	[Export] public float EncounterPromptRadius { get; set; } = 9.0f;
	[Export] public float TownPromptRadius { get; set; } = 7.5f;
	[Export] public float FeedbackSeconds { get; set; } = 1.8f;

	private PlayerBoatController? _playerBoat;
	private Label? _hudLabel;
	private Node3D? _encounterContact;
	private Node3D? _townDock;
	private Control? _townPromptPanel;
	private Label? _townPromptLabel;
	private Control? _townPanel;
	private Label? _townNameLabel;
	private Label? _townBodyLabel;
	private Button? _merchantButton;
	private Button? _repairButton;
	private Button? _leaveTownButton;
	private readonly List<Node3D> _checkpoints = new();
	private int _nextCheckpointIndex;
	private int _lapCount;
	private float _feedbackTimer;
	private string _feedbackText = "Find checkpoint 1.";
	private bool _encounterTriggered;
	private bool _isTownPanelOpen;
	private bool _isTownDockInRange;

	public override void _Ready()
	{
		_playerBoat = GetNodeOrNull<PlayerBoatController>(PlayerBoatPath);
		_hudLabel = GetNodeOrNull<Label>(HudLabelPath);
		_encounterContact = GetNodeOrNull<Node3D>(EncounterContactPath);
		_townDock = GetNodeOrNull<Node3D>(TownDockPath);
		_townPromptPanel = GetNodeOrNull<Control>(TownPromptPanelPath);
		_townPromptLabel = GetNodeOrNull<Label>(TownPromptLabelPath);
		_townPanel = GetNodeOrNull<Control>(TownPanelPath);
		_townNameLabel = GetNodeOrNull<Label>(TownNameLabelPath);
		_townBodyLabel = GetNodeOrNull<Label>(TownBodyLabelPath);
		_merchantButton = GetNodeOrNull<Button>(MerchantButtonPath);
		_repairButton = GetNodeOrNull<Button>(RepairButtonPath);
		_leaveTownButton = GetNodeOrNull<Button>(LeaveTownButtonPath);

		if (_merchantButton != null)
		{
			_merchantButton.Pressed += OnMerchantPressed;
		}

		if (_repairButton != null)
		{
			_repairButton.Pressed += OnRepairPressed;
		}

		if (_leaveTownButton != null)
		{
			_leaveTownButton.Pressed += LeaveTown;
		}

		SetTownPanelOpen(false);
		LoadCheckpoints();
		UpdateHud();
	}

	public override void _Process(double delta)
	{
		UpdateTownInteraction();
		UpdateCourse((float)delta);
		UpdateHud();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
		{
			return;
		}

		if (_isTownPanelOpen)
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

		if (keyEvent.Keycode == Key.E || keyEvent.PhysicalKeycode == Key.E)
		{
			if (_isTownDockInRange)
			{
				OpenTownPanel();
				GetViewport().SetInputAsHandled();
				return;
			}

			TriggerEncounter();
			GetViewport().SetInputAsHandled();
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
			+ "E: engage contact\n"
			+ speedText + "\n"
			+ courseText + "\n"
			+ GetEncounterHudText();
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

	private string GetEncounterHudText()
	{
		if (_encounterTriggered)
		{
			return "Contact: loading combat...";
		}

		var enemyName = string.IsNullOrWhiteSpace(EnemyShipArchetype?.DisplayName)
			? "Enemy contact"
			: EnemyShipArchetype.DisplayName;

		if (_playerBoat == null || _encounterContact == null)
		{
			return $"Contact: {enemyName} ready";
		}

		var distance = GetFlatDistance(_playerBoat.GlobalPosition, _encounterContact.GlobalPosition);
		var rangeText = distance <= EncounterPromptRadius
			? "in boarding range"
			: $"{distance:0}m away";

		return $"Contact: {enemyName} {rangeText}";
	}

	private void UpdateTownInteraction()
	{
		_isTownDockInRange = IsPlayerNearTownDock();

		if (_townPromptPanel != null)
		{
			_townPromptPanel.Visible = _isTownDockInRange && !_isTownPanelOpen;
		}

		if (_townPromptLabel != null)
		{
			_townPromptLabel.Text = $"Press E to dock at {TownName}";
		}
	}

	private bool IsPlayerNearTownDock()
	{
		if (_playerBoat == null || _townDock == null)
		{
			return false;
		}

		return GetFlatDistance(_playerBoat.GlobalPosition, _townDock.GlobalPosition) <= TownPromptRadius;
	}

	private void OpenTownPanel()
	{
		SetTownPanelOpen(true);
		SetTownBodyText($"You arrive at {TownName}.");
	}

	private void SetTownPanelOpen(bool isOpen)
	{
		_isTownPanelOpen = isOpen;

		if (_playerBoat != null)
		{
			_playerBoat.InputEnabled = !isOpen;
		}

		if (_townPanel != null)
		{
			_townPanel.Visible = isOpen;
		}

		if (_townPromptPanel != null)
		{
			_townPromptPanel.Visible = _isTownDockInRange && !isOpen;
		}

		if (_townNameLabel != null)
		{
			_townNameLabel.Text = TownName;
		}

		if (isOpen)
		{
			_merchantButton?.GrabFocus();
		}
	}

	private void OnMerchantPressed()
	{
		SetTownBodyText("The merchant has crates of rope, powder, and dried fish. Trading is not implemented yet.");
	}

	private void OnRepairPressed()
	{
		SetTownBodyText("The dockworkers inspect your ship. Repairs are not implemented yet.");
	}

	private void LeaveTown()
	{
		SetTownPanelOpen(false);
	}

	private void SetTownBodyText(string text)
	{
		if (_townBodyLabel != null)
		{
			_townBodyLabel.Text = text;
		}
	}

	private void TriggerEncounter()
	{
		if (_encounterTriggered)
		{
			return;
		}

		if (string.IsNullOrWhiteSpace(BattleScenePath))
		{
			ShowFeedback("No combat scene path assigned.");
			return;
		}

		var encounter = new SailingEncounterData(PlayerShipArchetype, EnemyShipArchetype, ReturnScenePath);
		SailingEncounterStore.SetPendingEncounter(encounter);
		_encounterTriggered = true;
		ShowFeedback($"Engaging {encounter.EnemyDisplayName}.");

		var sceneChangeError = GetTree().ChangeSceneToFile(BattleScenePath);
		if (sceneChangeError == Error.Ok)
		{
			return;
		}

		SailingEncounterStore.ConsumePendingEncounter();
		_encounterTriggered = false;
		GD.PushError($"SailingSandbox: failed to load battle scene '{BattleScenePath}' ({sceneChangeError}).");
	}

	private static float GetFlatDistance(Vector3 first, Vector3 second)
	{
		first.Y = 0.0f;
		second.Y = 0.0f;

		return first.DistanceTo(second);
	}
}
