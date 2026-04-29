using Godot;
using System;
using System.Collections.Generic;
using TidesOfTime.Audio;
using TidesOfTime.Data;
using TidesOfTime.Encounters;

namespace TidesOfTime.Sailing;

public partial class SailingSandbox : Node3D
{
	private const float CourseMarkerHeight = 0.055f;
	private const float BuoyHeight = 0.45f;
	private const float CourseLaneHalfWidth = 7.2f;
	private const float GuideMarkerSpacing = 20.0f;
	private const float PrecisionLaneHalfWidth = 4.4f;
	private const string CourseRootName = "Course";
	private const string GeneratedPropsRootName = "GeneratedCourseProps";

	private static readonly CourseGateDefinition[] HandlingCourse =
	{
		// Start straight: gives the boat room to accelerate and settle near top speed.
		new("Start Gate", "Start straight", new Vector3(0.0f, CourseMarkerHeight, -12.0f), 11.5f, 5.4f, GateVisualKind.Start),
		new("Full Sail Straight", "Start straight", new Vector3(0.0f, CourseMarkerHeight, -56.0f), 12.5f, 5.8f, GateVisualKind.Straight),

		// Sweeping turn: wide left arc for coasting and drift without punishing recovery.
		new("Sweep Entry", "Sweeping left turn", new Vector3(22.0f, CourseMarkerHeight, -92.0f), 13.0f, 6.0f, GateVisualKind.Turn),
		new("Outer Sweep Apex", "Sweeping left turn", new Vector3(-34.0f, CourseMarkerHeight, -104.0f), 13.0f, 6.0f, GateVisualKind.Turn),

		// Tight turn / hairpin: asks for braking discipline, but leaves open water around the miss.
		new("Hairpin Setup", "Tight turn / hairpin", new Vector3(-78.0f, CourseMarkerHeight, -72.0f), 10.5f, 5.0f, GateVisualKind.Hairpin),
		new("Hairpin Pivot", "Tight turn / hairpin", new Vector3(-84.0f, CourseMarkerHeight, -32.0f), 8.5f, 4.4f, GateVisualKind.Hairpin),
		new("Hairpin Exit", "Tight turn / hairpin", new Vector3(-52.0f, CourseMarkerHeight, -4.0f), 10.5f, 5.0f, GateVisualKind.Hairpin),

		// S-curve / chicane: a gentle rhythm test after the hairpin recovery.
		new("Chicane Left", "S-curve / chicane", new Vector3(-20.0f, CourseMarkerHeight, -26.0f), 9.0f, 4.7f, GateVisualKind.Chicane),
		new("Chicane Right", "S-curve / chicane", new Vector3(12.0f, CourseMarkerHeight, -6.0f), 9.0f, 4.7f, GateVisualKind.Chicane),

		// Narrow gate: precision section with closer buoys and a slightly smaller trigger radius.
		new("Needle Gate", "Narrow gate", new Vector3(42.0f, CourseMarkerHeight, -24.0f), 6.0f, 3.4f, GateVisualKind.Precision),

		// Final return straight: a wide recovery bend that feeds back toward the start gate.
		new("Recovery Bend", "Final return straight", new Vector3(52.0f, CourseMarkerHeight, -58.0f), 12.0f, 5.6f, GateVisualKind.Finish),
		new("Home Stretch", "Final return straight", new Vector3(18.0f, CourseMarkerHeight, -44.0f), 12.0f, 5.6f, GateVisualKind.Finish),
	};

	[Export] public NodePath PlayerBoatPath { get; set; } = new("PlayerBoat");
	[Export] public NodePath HudLabelPath { get; set; } = new("HUD/PanelContainer/MarginContainer/VBoxContainer/InfoLabel");
	[Export] public NodePath BoostBarPath { get; set; } = new("HUD/PanelContainer/MarginContainer/VBoxContainer/BoostBar");
	[Export] public NodePath BoostLabelPath { get; set; } = new("HUD/PanelContainer/MarginContainer/VBoxContainer/BoostLabel");
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
	[Export] public NodePath PauseOverlayPath { get; set; } = new("HUD/PauseOverlay");
	[Export] public NodePath ResumeButtonPath { get; set; } = new("HUD/PauseOverlay/MarginContainer/VBoxContainer/ButtonStack/ResumeButton");
	[Export] public NodePath RestartButtonPath { get; set; } = new("HUD/PauseOverlay/MarginContainer/VBoxContainer/ButtonStack/RestartButton");
	[Export] public NodePath QuitGameButtonPath { get; set; } = new("HUD/PauseOverlay/MarginContainer/VBoxContainer/ButtonStack/QuitGameButton");
	[Export] public NodePath VolumeSliderPath { get; set; } = new("HUD/PauseOverlay/MarginContainer/VBoxContainer/ButtonStack/VolumeRow/VolumeSlider");
	[Export] public ShipArchetypeDef? PlayerShipArchetype { get; set; }
	[Export] public ShipArchetypeDef? EnemyShipArchetype { get; set; }
	[Export] public string BattleScenePath { get; set; } = "res://scenes/battle/battle_scene.tscn";
	[Export] public string ReturnScenePath { get; set; } = "res://scenes/sailing/sailing_sandbox.tscn";
	[Export] public string TownName { get; set; } = "Saltwind Harbor";
	[Export] public float CheckpointRadius { get; set; } = 4.25f;
	[Export] public float EncounterPromptRadius { get; set; } = 22.0f;
	[Export] public float TownPromptRadius { get; set; } = 7.5f;
	[Export] public float FeedbackSeconds { get; set; } = 1.8f;
	[Export] public float PlayableMinX { get; set; } = -132.0f;
	[Export] public float PlayableMaxX { get; set; } = 132.0f;
	[Export] public float PlayableMinZ { get; set; } = -132.0f;
	[Export] public float PlayableMaxZ { get; set; } = 132.0f;
	[Export] public float FallResetDelaySeconds { get; set; } = 1.6f;

	private PlayerBoatController? _playerBoat;
	private Label? _hudLabel;
	private ProgressBar? _boostBar;
	private Label? _boostLabel;
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
	private Control? _pauseOverlay;
	private Button? _resumeButton;
	private Button? _restartButton;
	private Button? _quitGameButton;
	private HSlider? _volumeSlider;
	private readonly List<Node3D> _checkpoints = new();
	private readonly List<GuideSegmentVisual> _guideSegments = new();
	private readonly Dictionary<Node3D, float> _checkpointRadii = new();
	private readonly Dictionary<Node3D, string> _checkpointSections = new();
	private int _nextCheckpointIndex;
	private int _lapCount;
	private float _feedbackTimer;
	private string _feedbackText = "Find checkpoint 1.";
	private float _hudStatusTimer;
	private string _hudStatusText = string.Empty;
	private float _fallResetTimer;
	private bool _encounterTriggered;
	private bool _isTownPanelOpen;
	private bool _isTownDockInRange;
	private bool _isBoatFallingOffEdge;
	private bool _isPauseMenuOpen;
	private StandardMaterial3D? _inactiveCheckpointMaterial;
	private StandardMaterial3D? _activeCheckpointMaterial;
	private StandardMaterial3D? _nextCheckpointMaterial;
	private StandardMaterial3D? _subtlePortGuideMaterial;
	private StandardMaterial3D? _subtleStarboardGuideMaterial;
	private StandardMaterial3D? _focusedPortGuideMaterial;
	private StandardMaterial3D? _focusedStarboardGuideMaterial;
	private StandardMaterial3D? _directionArrowMaterial;

	public override void _Ready()
	{
		GetNodeOrNull<MusicManager>("/root/MusicManager")?.PlaySailingCombatMusic();

		_playerBoat = GetNodeOrNull<PlayerBoatController>(PlayerBoatPath);
		_hudLabel = GetNodeOrNull<Label>(HudLabelPath);
		_boostBar = GetNodeOrNull<ProgressBar>(BoostBarPath);
		_boostLabel = GetNodeOrNull<Label>(BoostLabelPath);
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
		_pauseOverlay = GetNodeOrNull<Control>(PauseOverlayPath);
		_resumeButton = GetNodeOrNull<Button>(ResumeButtonPath);
		_restartButton = GetNodeOrNull<Button>(RestartButtonPath);
		_quitGameButton = GetNodeOrNull<Button>(QuitGameButtonPath);
		_volumeSlider = GetNodeOrNull<HSlider>(VolumeSliderPath);

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

		if (_resumeButton != null)
		{
			_resumeButton.Pressed += OnResumePressed;
		}

		if (_restartButton != null)
		{
			_restartButton.Pressed += OnRestartPressed;
		}

		if (_quitGameButton != null)
		{
			_quitGameButton.Pressed += OnQuitGamePressed;
		}

		if (_volumeSlider != null)
		{
			_volumeSlider.Value = GetMusicVolumePercent();
			_volumeSlider.ValueChanged += OnVolumeSliderValueChanged;
		}

		SetPauseMenuOpen(false);
		SetTownPanelOpen(false);
		BuildHandlingCourse();
		LoadCheckpoints();
		UpdateCheckpointVisuals();
		UpdateHud();
	}

	public override void _Process(double delta)
	{
		var deltaSeconds = (float)delta;

		UpdateHudStatus(deltaSeconds);
		UpdateTownInteraction();
		UpdateEdgeFall(deltaSeconds);
		UpdateCourse(deltaSeconds);
		UpdateHud();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
		{
			return;
		}

		if (keyEvent.Keycode == Key.Escape)
		{
			SetPauseMenuOpen(!_isPauseMenuOpen);
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_isPauseMenuOpen)
		{
			return;
		}

		if (_isTownPanelOpen)
		{
			return;
		}

		if (_isBoatFallingOffEdge)
		{
			return;
		}

		if (keyEvent.Keycode == Key.R || keyEvent.PhysicalKeycode == Key.R)
		{
			ResetBoatToCourseStart();
			ResetCourse("Boat and course reset.");
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

		if (_isBoatFallingOffEdge || _playerBoat == null || _checkpoints.Count == 0)
		{
			return;
		}

		var checkpoint = _checkpoints[_nextCheckpointIndex];
		var checkpointRadius = _checkpointRadii.TryGetValue(checkpoint, out var radius)
			? radius
			: CheckpointRadius;

		if (GetFlatDistance(_playerBoat.GlobalPosition, checkpoint.GlobalPosition) > checkpointRadius)
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
			UpdateCheckpointVisuals();
			return;
		}

		ShowFeedback($"Checkpoint {clearedCheckpointNumber} cleared.");
		UpdateCheckpointVisuals();
	}

	private void ResetCourse(string message)
	{
		_nextCheckpointIndex = 0;
		_lapCount = 0;
		ShowFeedback(message);
		UpdateCheckpointVisuals();
	}

	private void UpdateEdgeFall(float deltaSeconds)
	{
		if (_playerBoat == null)
		{
			return;
		}

		if (_isBoatFallingOffEdge)
		{
			_fallResetTimer -= deltaSeconds;
			if (_fallResetTimer > 0.0f)
			{
				return;
			}

			ResetBoatToCourseStart();
			ResetCourse("Back on the water.");
			return;
		}

		if (IsInsidePlayableBounds(_playerBoat.GlobalPosition))
		{
			return;
		}

		_isBoatFallingOffEdge = true;
		_fallResetTimer = Mathf.Max(0.1f, FallResetDelaySeconds);
		_playerBoat.InputEnabled = false;
		_playerBoat.BeginFallOffEdge();
		ShowHudStatus("Off the edge! Resetting...");
	}

	private void ResetBoatToCourseStart()
	{
		_isBoatFallingOffEdge = false;
		_fallResetTimer = 0.0f;

		if (_playerBoat == null)
		{
			return;
		}

		_playerBoat.ResetToStart();
		_playerBoat.InputEnabled = !_isTownPanelOpen && !_isPauseMenuOpen;
	}

	private bool IsInsidePlayableBounds(Vector3 position)
	{
		return position.X >= PlayableMinX &&
			position.X <= PlayableMaxX &&
			position.Z >= PlayableMinZ &&
			position.Z <= PlayableMaxZ;
	}

	private void ShowFeedback(string message)
	{
		_feedbackText = message;
		_feedbackTimer = FeedbackSeconds;
	}

	private void ShowHudStatus(string message)
	{
		_hudStatusText = message;
		_hudStatusTimer = FeedbackSeconds;
	}

	private void UpdateHudStatus(float deltaSeconds)
	{
		if (_hudStatusTimer <= 0.0f)
		{
			return;
		}

		_hudStatusTimer = Mathf.Max(0.0f, _hudStatusTimer - deltaSeconds);
		if (_hudStatusTimer <= 0.0f)
		{
			_hudStatusText = string.Empty;
		}
	}

	private void UpdateHud()
	{
		UpdateBoostHud();

		if (_hudLabel == null)
		{
			return;
		}

		var speedText = _playerBoat == null
			? "Speed: no player boat assigned"
			: $"Speed: {_playerBoat.Speed:0.0}";
		var courseText = GetCourseHudText();

		_hudLabel.Text = "Sailing Demo\n"
			+ courseText + "\n"
			+ speedText + "\n"
			+ GetEncounterHudText() + "\n"
			+ "W/S drive · A/D turn · Space boost · Shift brake\n"
			+ "E dock/engage · R reset"
			+ GetHudStatusText();
	}

	private void UpdateBoostHud()
	{
		var boostRatio = _playerBoat?.BoostChargeRatio ?? 0.0f;
		var isBoosting = _playerBoat?.IsBoosting == true;

		if (_boostBar != null)
		{
			_boostBar.Value = boostRatio * 100.0f;
		}

		if (_boostLabel != null)
		{
			_boostLabel.Text = isBoosting ? "Boost: active" : "Boost";
		}
	}

	private string GetCourseHudText()
	{
		if (_checkpoints.Count == 0)
		{
			return "Course: no checkpoints found";
		}

		var nextCheckpointNumber = _nextCheckpointIndex + 1;
		var nextCheckpointName = _checkpoints[_nextCheckpointIndex].Name;
		var nextSection = _checkpointSections.TryGetValue(_checkpoints[_nextCheckpointIndex], out var section)
			? $" | {section}"
			: string.Empty;

		return $"Next: {nextCheckpointNumber}/{_checkpoints.Count} {nextCheckpointName}{nextSection}\nLap: {_lapCount}";
	}

	private string GetHudStatusText()
	{
		return _hudStatusTimer > 0.0f && !string.IsNullOrWhiteSpace(_hudStatusText)
			? $"\n{_hudStatusText}"
			: string.Empty;
	}

	private void BuildHandlingCourse()
	{
		var checkpointRoot = GetNodeOrNull<Node3D>(CheckpointsRootPath);

		if (checkpointRoot == null)
		{
			return;
		}

		ClearChildren(checkpointRoot);
		_guideSegments.Clear();
		_checkpointRadii.Clear();
		_checkpointSections.Clear();

		var courseRoot = GetNodeOrNull<Node3D>(CourseRootName) ?? checkpointRoot.GetParentOrNull<Node3D>();
		if (courseRoot != null)
		{
			ClearLegacyCourseDecorations(courseRoot, checkpointRoot);
			CreateCourseProps(courseRoot);
		}

		for (var i = 0; i < HandlingCourse.Length; i++)
		{
			var gate = HandlingCourse[i];
			var checkpoint = CreateCheckpointNode(gate, i);
			checkpointRoot.AddChild(checkpoint);
			_checkpointRadii[checkpoint] = Mathf.Max(1.0f, gate.Radius);
			_checkpointSections[checkpoint] = gate.Section;
		}
	}

	private Node3D CreateCheckpointNode(CourseGateDefinition gate, int index)
	{
		var checkpoint = new Node3D
		{
			Name = gate.Name,
			Position = gate.Position
		};
		var lateral = GetGateLateral(index);
		var markerRadius = Mathf.Max(gate.Width * 0.44f, gate.Radius);

		var marker = new MeshInstance3D
		{
			Name = "Marker",
			Mesh = new CylinderMesh
			{
				TopRadius = markerRadius,
				BottomRadius = markerRadius,
				Height = 0.045f
			},
			MaterialOverride = _inactiveCheckpointMaterial ??= CreateTransparentMaterial(new Color(0.23f, 0.68f, 0.95f, 0.28f))
		};
		checkpoint.AddChild(marker);

		CreateBuoy(checkpoint, "Port Buoy", lateral * (-gate.Width * 0.5f), GetPortBuoyColor(gate.VisualKind));
		CreateBuoy(checkpoint, "Starboard Buoy", lateral * (gate.Width * 0.5f), GetStarboardBuoyColor(gate.VisualKind));

		if (gate.VisualKind is GateVisualKind.Start or GateVisualKind.Precision or GateVisualKind.Finish)
		{
			CreateFlagPost(checkpoint, "Port Flag", lateral * (-gate.Width * 0.5f), GetFlagColor(gate.VisualKind));
			CreateFlagPost(checkpoint, "Starboard Flag", lateral * (gate.Width * 0.5f), GetFlagColor(gate.VisualKind));
		}

		return checkpoint;
	}

	private void CreateBuoy(Node3D parent, string nodeName, Vector3 localOffset, Color color)
	{
		var buoy = new MeshInstance3D
		{
			Name = nodeName,
			Position = localOffset + new Vector3(0.0f, BuoyHeight, 0.0f),
			Mesh = new CylinderMesh
			{
				TopRadius = 0.34f,
				BottomRadius = 0.34f,
				Height = 0.9f
			},
			MaterialOverride = CreateMaterial(color, 0.5f)
		};

		parent.AddChild(buoy);
	}

	private void CreateFlagPost(Node3D parent, string nodeName, Vector3 localOffset, Color color)
	{
		var post = new MeshInstance3D
		{
			Name = nodeName,
			Position = localOffset + new Vector3(0.0f, 1.3f, 0.0f),
			Mesh = new BoxMesh { Size = new Vector3(0.18f, 1.8f, 0.18f) },
			MaterialOverride = CreateMaterial(new Color(0.22f, 0.12f, 0.05f), 0.64f)
		};
		parent.AddChild(post);

		var pennant = new MeshInstance3D
		{
			Name = $"{nodeName} Pennant",
			Position = localOffset + new Vector3(0.42f, 2.05f, 0.0f),
			Mesh = new BoxMesh { Size = new Vector3(0.78f, 0.36f, 0.08f) },
			MaterialOverride = CreateMaterial(color, 0.62f)
		};
		parent.AddChild(pennant);
	}

	private void CreateCourseProps(Node3D courseRoot)
	{
		var propsRoot = new Node3D { Name = GeneratedPropsRootName };
		courseRoot.AddChild(propsRoot);

		CreateGuideLane(propsRoot);
		CreateFloatingBarrel(propsRoot, "Hairpin Range Barrel", new Vector3(-94.0f, 0.42f, -50.0f), 0.4f);
		CreateFloatingBarrel(propsRoot, "Chicane Reference Barrel", new Vector3(-2.0f, 0.42f, -20.0f), -0.25f);
		CreateFloatingBarrel(propsRoot, "Needle Reference Barrel", new Vector3(33.0f, 0.42f, -34.0f), 0.9f);
	}

	private void CreateGuideLane(Node3D propsRoot)
	{
		var guideRoot = new Node3D { Name = "LaneGuideMarkers" };
		propsRoot.AddChild(guideRoot);

		for (var segmentIndex = 0; segmentIndex < HandlingCourse.Length; segmentIndex++)
		{
			var nextIndex = GetWrappedCourseIndex(segmentIndex + 1);
			var from = HandlingCourse[segmentIndex];
			var to = HandlingCourse[nextIndex];
			var direction = to.Position - from.Position;
			direction.Y = 0.0f;

			if (direction.LengthSquared() <= 0.0001f)
			{
				continue;
			}

			var segmentLength = direction.Length();
			direction = direction.Normalized();
			var starboard = new Vector3(-direction.Z, 0.0f, direction.X).Normalized();
			var laneHalfWidth = GetSegmentLaneHalfWidth(segmentIndex, nextIndex);
			var guideMarkerCount = Math.Max(1, (int)MathF.Floor(segmentLength / GuideMarkerSpacing));
			var segmentRoot = new Node3D { Name = $"Guide {segmentIndex + 1:00} {from.Name} to {to.Name}" };
			var segmentVisual = new GuideSegmentVisual(segmentIndex, segmentRoot);

			guideRoot.AddChild(segmentRoot);

			for (var markerIndex = 1; markerIndex <= guideMarkerCount; markerIndex++)
			{
				var t = markerIndex / (guideMarkerCount + 1.0f);
				var center = from.Position.Lerp(to.Position, t);
				center.Y = 0.0f;

				segmentVisual.PortMarkers.Add(CreateGuideBuoy(
					segmentRoot,
					$"Port Guide {markerIndex:00}",
					center - (starboard * laneHalfWidth),
					isPort: true));
				segmentVisual.StarboardMarkers.Add(CreateGuideBuoy(
					segmentRoot,
					$"Starboard Guide {markerIndex:00}",
					center + (starboard * laneHalfWidth),
					isPort: false));
			}

			segmentVisual.ArrowRoot = CreateDirectionArrow(
				segmentRoot,
				"Route Arrow",
				from.Position.Lerp(to.Position, 0.58f),
				direction);
			_guideSegments.Add(segmentVisual);
		}
	}

	private float GetSegmentLaneHalfWidth(int fromIndex, int toIndex)
	{
		if (HandlingCourse[fromIndex].VisualKind == GateVisualKind.Precision ||
			HandlingCourse[toIndex].VisualKind == GateVisualKind.Precision)
		{
			return PrecisionLaneHalfWidth;
		}

		var gateHalfWidth = Math.Min(HandlingCourse[fromIndex].Width, HandlingCourse[toIndex].Width) * 0.5f;
		return Mathf.Max(CourseLaneHalfWidth, gateHalfWidth + 1.2f);
	}

	private MeshInstance3D CreateGuideBuoy(Node3D parent, string nodeName, Vector3 position, bool isPort)
	{
		var buoy = new MeshInstance3D
		{
			Name = nodeName,
			Position = position + new Vector3(0.0f, 0.26f, 0.0f),
			Mesh = new CylinderMesh
			{
				TopRadius = 0.2f,
				BottomRadius = 0.28f,
				Height = 0.52f
			},
			MaterialOverride = isPort
				? GetSubtlePortGuideMaterial()
				: GetSubtleStarboardGuideMaterial()
		};

		parent.AddChild(buoy);
		return buoy;
	}

	private Node3D CreateDirectionArrow(Node3D parent, string nodeName, Vector3 position, Vector3 direction)
	{
		var arrowRoot = new Node3D
		{
			Name = nodeName,
			Position = new Vector3(position.X, 0.08f, position.Z),
			Rotation = new Vector3(0.0f, MathF.Atan2(-direction.X, -direction.Z), 0.0f),
			Visible = false
		};

		parent.AddChild(arrowRoot);
		AddArrowBox(arrowRoot, "Shaft", new Vector3(0.34f, 0.06f, 2.35f), new Vector3(0.0f, 0.0f, 0.18f), 0.0f);
		AddArrowBox(arrowRoot, "Port Head", new Vector3(0.24f, 0.06f, 1.05f), new Vector3(-0.34f, 0.0f, -0.95f), -0.55f);
		AddArrowBox(arrowRoot, "Starboard Head", new Vector3(0.24f, 0.06f, 1.05f), new Vector3(0.34f, 0.0f, -0.95f), 0.55f);

		return arrowRoot;
	}

	private void AddArrowBox(Node3D parent, string nodeName, Vector3 size, Vector3 position, float yaw)
	{
		var node = new MeshInstance3D
		{
			Name = nodeName,
			Position = position,
			Rotation = new Vector3(0.0f, yaw, 0.0f),
			Mesh = new BoxMesh { Size = size },
			MaterialOverride = GetDirectionArrowMaterial()
		};

		parent.AddChild(node);
	}

	private void CreateFloatingBarrel(Node3D parent, string nodeName, Vector3 position, float yaw)
	{
		var barrel = new MeshInstance3D
		{
			Name = nodeName,
			Position = position,
			Rotation = new Vector3(0.0f, yaw, Mathf.Pi * 0.5f),
			Mesh = new CylinderMesh
			{
				TopRadius = 0.42f,
				BottomRadius = 0.42f,
				Height = 0.8f
			},
			MaterialOverride = CreateMaterial(new Color(0.46f, 0.24f, 0.1f), 0.74f)
		};

		parent.AddChild(barrel);
	}

	private Vector3 GetGateLateral(int index)
	{
		var current = HandlingCourse[index].Position;
		var direction = Vector3.Zero;

		if (index == 0)
		{
			direction = HandlingCourse[1].Position - current;
		}
		else if (index == HandlingCourse.Length - 1)
		{
			direction = current - HandlingCourse[index - 1].Position;
		}
		else
		{
			direction = HandlingCourse[index + 1].Position - HandlingCourse[index - 1].Position;
		}

		direction.Y = 0.0f;
		if (direction.LengthSquared() <= 0.0001f)
		{
			return Vector3.Right;
		}

		direction = direction.Normalized();
		return new Vector3(-direction.Z, 0.0f, direction.X).Normalized();
	}

	private void ClearLegacyCourseDecorations(Node3D courseRoot, Node3D checkpointRoot)
	{
		foreach (var child in courseRoot.GetChildren())
		{
			if (child == checkpointRoot)
			{
				continue;
			}

			if (child is Node node)
			{
				courseRoot.RemoveChild(node);
				node.QueueFree();
			}
		}
	}

	private static void ClearChildren(Node node)
	{
		foreach (var child in node.GetChildren())
		{
			if (child is Node childNode)
			{
				node.RemoveChild(childNode);
				childNode.QueueFree();
			}
		}
	}

	private void UpdateCheckpointVisuals()
	{
		if (_inactiveCheckpointMaterial == null)
		{
			_inactiveCheckpointMaterial = CreateTransparentMaterial(new Color(0.18f, 0.42f, 0.58f, 0.16f));
		}

		if (_activeCheckpointMaterial == null)
		{
			_activeCheckpointMaterial = CreateTransparentMaterial(new Color(0.95f, 0.86f, 0.24f, 0.48f));
		}

		if (_nextCheckpointMaterial == null)
		{
			_nextCheckpointMaterial = CreateTransparentMaterial(new Color(0.34f, 0.82f, 1.0f, 0.32f));
		}

		for (var i = 0; i < _checkpoints.Count; i++)
		{
			var isActive = i == _nextCheckpointIndex;
			var isNext = i == GetWrappedCheckpointIndex(_nextCheckpointIndex + 1);
			var checkpoint = _checkpoints[i];
			checkpoint.Visible = isActive || isNext;
			checkpoint.Scale = isActive ? Vector3.One : new Vector3(0.78f, 1.0f, 0.78f);

			var marker = _checkpoints[i].GetNodeOrNull<MeshInstance3D>("Marker");
			if (marker == null)
			{
				continue;
			}

			marker.MaterialOverride = isActive
				? _activeCheckpointMaterial
				: isNext
					? _nextCheckpointMaterial
					: _inactiveCheckpointMaterial;
		}

		UpdateGuideVisuals();
	}

	private void UpdateGuideVisuals()
	{
		foreach (var segment in _guideSegments)
		{
			var isFocused = segment.SegmentIndex == GetCurrentGuideSegmentIndex();
			var isPreview = segment.SegmentIndex == GetPreviewGuideSegmentIndex();
			var isVisible = isFocused || isPreview;
			var portMaterial = isFocused ? GetFocusedPortGuideMaterial() : GetSubtlePortGuideMaterial();
			var starboardMaterial = isFocused ? GetFocusedStarboardGuideMaterial() : GetSubtleStarboardGuideMaterial();

			segment.Root.Visible = isVisible;
			if (!isVisible)
			{
				continue;
			}

			foreach (var marker in segment.PortMarkers)
			{
				marker.MaterialOverride = portMaterial;
				marker.Scale = isFocused ? Vector3.One : new Vector3(0.72f, 0.72f, 0.72f);
			}

			foreach (var marker in segment.StarboardMarkers)
			{
				marker.MaterialOverride = starboardMaterial;
				marker.Scale = isFocused ? Vector3.One : new Vector3(0.72f, 0.72f, 0.72f);
			}

			if (segment.ArrowRoot != null)
			{
				segment.ArrowRoot.Visible = isFocused;
			}
		}
	}

	private int GetCurrentGuideSegmentIndex()
	{
		if (_checkpoints.Count == 0)
		{
			return 0;
		}

		if (_lapCount == 0 && _nextCheckpointIndex == 0)
		{
			return 0;
		}

		return GetWrappedCheckpointIndex(_nextCheckpointIndex - 1);
	}

	private int GetPreviewGuideSegmentIndex()
	{
		if (_checkpoints.Count == 0)
		{
			return 0;
		}

		return GetWrappedCheckpointIndex(GetCurrentGuideSegmentIndex() + 1);
	}

	private int GetWrappedCheckpointIndex(int index)
	{
		if (_checkpoints.Count == 0)
		{
			return 0;
		}

		return ((index % _checkpoints.Count) + _checkpoints.Count) % _checkpoints.Count;
	}

	private static int GetWrappedCourseIndex(int index)
	{
		return ((index % HandlingCourse.Length) + HandlingCourse.Length) % HandlingCourse.Length;
	}

	private static Color GetPortBuoyColor(GateVisualKind kind)
	{
		return kind switch
		{
			GateVisualKind.Start => new Color(0.86f, 0.08f, 0.05f),
			GateVisualKind.Hairpin => new Color(0.95f, 0.34f, 0.14f),
			GateVisualKind.Precision => new Color(0.96f, 0.12f, 0.12f),
			GateVisualKind.Finish => new Color(0.42f, 0.52f, 0.92f),
			_ => new Color(0.95f, 0.76f, 0.18f)
		};
	}

	private static Color GetStarboardBuoyColor(GateVisualKind kind)
	{
		return kind switch
		{
			GateVisualKind.Start => new Color(0.08f, 0.68f, 0.38f),
			GateVisualKind.Hairpin => new Color(1.0f, 0.58f, 0.16f),
			GateVisualKind.Precision => new Color(0.12f, 0.82f, 0.32f),
			GateVisualKind.Finish => new Color(0.25f, 0.72f, 0.86f),
			_ => new Color(0.08f, 0.68f, 0.38f)
		};
	}

	private static Color GetFlagColor(GateVisualKind kind)
	{
		return kind switch
		{
			GateVisualKind.Start => new Color(0.92f, 0.84f, 0.36f),
			GateVisualKind.Precision => new Color(1.0f, 0.96f, 0.44f),
			GateVisualKind.Finish => new Color(0.36f, 0.9f, 0.95f),
			_ => new Color(0.92f, 0.84f, 0.36f)
		};
	}

	private static StandardMaterial3D CreateMaterial(Color color, float roughness)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = roughness
		};
	}

	private static StandardMaterial3D CreateTransparentMaterial(Color color)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			Roughness = 0.7f
		};
	}

	private StandardMaterial3D GetSubtlePortGuideMaterial()
	{
		return _subtlePortGuideMaterial ??= CreateTransparentMaterial(new Color(0.78f, 0.18f, 0.16f, 0.42f));
	}

	private StandardMaterial3D GetSubtleStarboardGuideMaterial()
	{
		return _subtleStarboardGuideMaterial ??= CreateTransparentMaterial(new Color(0.12f, 0.64f, 0.32f, 0.42f));
	}

	private StandardMaterial3D GetFocusedPortGuideMaterial()
	{
		return _focusedPortGuideMaterial ??= CreateTransparentMaterial(new Color(0.96f, 0.16f, 0.12f, 0.82f));
	}

	private StandardMaterial3D GetFocusedStarboardGuideMaterial()
	{
		return _focusedStarboardGuideMaterial ??= CreateTransparentMaterial(new Color(0.12f, 0.9f, 0.36f, 0.82f));
	}

	private StandardMaterial3D GetDirectionArrowMaterial()
	{
		return _directionArrowMaterial ??= CreateTransparentMaterial(new Color(1.0f, 0.84f, 0.24f, 0.74f));
	}

	private string GetEncounterHudText()
	{
		if (_encounterTriggered)
		{
			return "Contact: loading combat...";
		}

		var enemyName = GetEnemyDisplayName();
		if (!TryGetContactDistance(out var distance))
		{
			return $"Contact: {enemyName} ready";
		}

		return IsContactInEngageRange(distance)
			? $"Press E: Engage {enemyName}"
			: $"Contact: {enemyName} {distance:0}m";
	}

	private string GetEnemyDisplayName()
	{
		return string.IsNullOrWhiteSpace(EnemyShipArchetype?.DisplayName)
			? "Enemy contact"
			: EnemyShipArchetype.DisplayName;
	}

	private bool TryGetContactDistance(out float distance)
	{
		distance = 0.0f;
		if (_playerBoat == null || _encounterContact == null)
		{
			return false;
		}

		distance = GetFlatDistance(_playerBoat.GlobalPosition, _encounterContact.GlobalPosition);
		return true;
	}

	private bool IsContactInEngageRange()
	{
		return TryGetContactDistance(out var distance) && IsContactInEngageRange(distance);
	}

	private bool IsContactInEngageRange(float distance)
	{
		return distance <= EncounterPromptRadius;
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
			_playerBoat.InputEnabled = !isOpen && !_isPauseMenuOpen;
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

	private void OnResumePressed()
	{
		SetPauseMenuOpen(false);
	}

	private void OnRestartPressed()
	{
		SetPauseMenuOpen(false);
		ResetBoatToCourseStart();
		ResetCourse("Boat and course reset.");
	}

	private void OnQuitGamePressed()
	{
		SetPauseMenuOpen(false);
		GetTree().Quit();
	}

	private void OnVolumeSliderValueChanged(double value)
	{
		GetNodeOrNull<MusicManager>("/root/MusicManager")?.SetVolume((float)value / 100.0f);
	}

	private float GetMusicVolumePercent()
	{
		return (GetNodeOrNull<MusicManager>("/root/MusicManager")?.GetVolume() ?? 1.0f) * 100.0f;
	}

	private void SetPauseMenuOpen(bool isOpen)
	{
		_isPauseMenuOpen = isOpen;

		if (_pauseOverlay != null)
		{
			_pauseOverlay.Visible = isOpen;
		}

		if (_playerBoat != null)
		{
			_playerBoat.InputEnabled = !isOpen && !_isTownPanelOpen && !_isBoatFallingOffEdge;
		}

		if (isOpen)
		{
			_resumeButton?.GrabFocus();
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
			ShowHudStatus("No combat scene path assigned.");
			return;
		}

		if (!IsContactInEngageRange())
		{
			ShowHudStatus($"Sail closer to {GetEnemyDisplayName()}.");
			return;
		}

		var encounter = new SailingEncounterData(PlayerShipArchetype, EnemyShipArchetype, ReturnScenePath);
		SailingEncounterStore.SetPendingEncounter(encounter);
		_encounterTriggered = true;
		ShowHudStatus($"Engaging {encounter.EnemyDisplayName}.");

		var sceneChangeError = GetTree().ChangeSceneToFile(BattleScenePath);
		if (sceneChangeError == Error.Ok)
		{
			return;
		}

		SailingEncounterStore.ConsumePendingEncounter();
		_encounterTriggered = false;
		ShowHudStatus("Could not load combat scene.");
		GD.PushError($"SailingSandbox: failed to load battle scene '{BattleScenePath}' ({sceneChangeError}).");
	}

	private static float GetFlatDistance(Vector3 first, Vector3 second)
	{
		first.Y = 0.0f;
		second.Y = 0.0f;

		return first.DistanceTo(second);
	}

	private readonly record struct CourseGateDefinition(
		string Name,
		string Section,
		Vector3 Position,
		float Width,
		float Radius,
		GateVisualKind VisualKind);

	private enum GateVisualKind
	{
		Start,
		Straight,
		Turn,
		Hairpin,
		Chicane,
		Precision,
		Finish
	}

	private sealed class GuideSegmentVisual
	{
		public GuideSegmentVisual(int segmentIndex, Node3D root)
		{
			SegmentIndex = segmentIndex;
			Root = root;
		}

		public int SegmentIndex { get; }
		public Node3D Root { get; }
		public Node3D? ArrowRoot { get; set; }
		public List<MeshInstance3D> PortMarkers { get; } = new();
		public List<MeshInstance3D> StarboardMarkers { get; } = new();
	}
}
