using Godot;
using System;
using System.Collections.Generic;
using TidesOfTime.Audio;
using TidesOfTime.Encounters;

namespace TidesOfTime.Prototypes;

public partial class StationCombat3D : Node3D
{
	[Export] public NodePath ShipRootPath { get; set; } = new("PrototypeRoot/ShipBlockout");
	[Export] public NodePath EnemyShipRootPath { get; set; } = new("PrototypeRoot/EnemyShipBlockout");
	[Export] public NodePath StationRootPath { get; set; } = new("PrototypeRoot/Stations");
	[Export] public NodePath CrewRootPath { get; set; } = new("PrototypeRoot/Crew");
	[Export] public NodePath HudRootPath { get; set; } = new("HUD");
	[Export] public NodePath CameraPath { get; set; } = new("Camera3D");
	[Export] public Vector3 PlayerShipOffset { get; set; } = new(-2.2f, 0.0f, 0.35f);
	[Export] public Vector3 EnemyShipOffset { get; set; } = new(4.45f, 0.0f, 0.35f);
	[Export] public Vector3 PlayerShipRotationDegrees { get; set; } = new(0.0f, -90.0f, 0.0f);
	[Export] public float PlayerShipScale { get; set; } = 1.2f;
	[Export] public float EnemyShipScale { get; set; } = 0.86f;
	[Export] public Vector3 DefaultCameraPosition { get; set; } = new(-0.98f, 13.15f, 10.5f);
	[Export] public Vector3 DefaultCameraRotationDegrees { get; set; } = new(-56.0f, -10.0f, 0.0f);
	[Export] public float DefaultCameraSize { get; set; } = 12.8f;
	[Export] public float MinCameraSize { get; set; } = 6.2f;
	[Export] public float MaxCameraSize { get; set; } = 17.0f;
	[Export] public Vector2 EnemyCutoutScreenAnchor { get; set; } = new(0.81f, 0.49f);
	[Export] public float EnemyCutoutAnchorDepth { get; set; } = 10.5f;
	[Export] public float CameraPanSpeed { get; set; } = 5.0f;
	[Export] public float CameraZoomStep { get; set; } = 0.55f;
	[Export] public float PlayerCannonChargeDurationSeconds { get; set; } = 7.0f;
	[Export] public float EnemyCannonChargeDurationSeconds { get; set; } = 7.0f;
	[Export] public float PlayerCannonDamage { get; set; } = 20.0f;
	[Export] public float EnemyCannonDamage { get; set; } = 20.0f;
	[Export] public float CannonHullDamage { get; set; } = 10.0f;
	[Export] public float PlayerRepairRatePerSecond { get; set; } = 10.0f;

	private static readonly Color HatchColor = new(0.08f, 0.055f, 0.03f);
	private const string CaptainCrewName = "Captain";
	private const string HelmStationName = "Helm";
	private const string CannonsStationName = "Cannons";
	private const float PlayerHelmDodgeChance = 0.25f;
	private const float StationDamageFlashSeconds = 1.0f;
	private const string PlayerCannonModelPath = "res://art/models/cannon-test-1.glb";
	private const string PlayerHelmModelPath = "res://art/models/helm-test-1.glb";
	private const string CaptainCrewModelPath = "res://art/models/crew-test-1blue.glb";
	private const string DeckhandCrewModelPath = "res://art/models/crew-test-1green.glb";
	private const string GunnerCrewModelPath = "res://art/models/crew-test-1red.glb";
	private static readonly string[] EnemyTargetPriority =
	{
		"Cannons",
		"Helm",
		"Bilge",
		"Crow's Nest"
	};
	private static readonly ShipVisualStyle PlayerShipStyle = new(
		new Color(0.28f, 0.13f, 0.06f),
		new Color(0.57f, 0.34f, 0.15f),
		new Color(0.18f, 0.09f, 0.035f),
		new Color(0.82f, 0.76f, 0.58f),
		new Color(0.5f, 0.28f, 0.12f),
		new Color(0.34f, 0.48f, 0.78f));
	private static readonly ShipVisualStyle EnemyShipStyle = new(
		new Color(0.18f, 0.075f, 0.045f),
		new Color(0.38f, 0.19f, 0.11f),
		new Color(0.12f, 0.045f, 0.035f),
		new Color(0.48f, 0.38f, 0.32f),
		new Color(0.35f, 0.12f, 0.085f),
		new Color(0.68f, 0.20f, 0.16f));

	private static readonly StationDefinition[] StationDefinitions =
	{
		new(
			"Helm",
			new Vector3(0.0f, 1.16f, 2.96f),
			new Vector3(0.0f, 0.0f, 0.38f),
			new Color(0.34f, 0.52f, 0.78f)),
		new(
			"Cannons",
			new Vector3(1.34f, 1.06f, -1.08f),
			new Vector3(-0.74f, 0.0f, 0.18f),
			new Color(0.74f, 0.28f, 0.2f)),
		new(
			"Crow's Nest",
			new Vector3(-0.18f, 3.1f, -0.36f),
			Vector3.Zero,
			new Color(0.86f, 0.72f, 0.28f)),
		new(
			"Bilge",
			new Vector3(0.78f, 1.06f, 0.82f),
			new Vector3(-0.72f, 0.0f, 0.38f),
			new Color(0.28f, 0.58f, 0.44f))
	};

	private static readonly StationDefinition[] EnemyStationDefinitions =
	{
		new(
			"Helm",
			new Vector3(-1.18f, 1.16f, 2.02f),
			new Vector3(0.68f, 0.0f, 0.0f),
			new Color(0.34f, 0.52f, 0.78f)),
		new(
			"Cannons",
			new Vector3(-1.34f, 1.16f, -1.08f),
			new Vector3(0.82f, 0.0f, 0.0f),
			new Color(0.74f, 0.28f, 0.2f)),
		new(
			"Crow's Nest",
			new Vector3(1.16f, 1.28f, -1.72f),
			new Vector3(0.6f, 0.0f, 0.0f),
			new Color(0.86f, 0.72f, 0.28f)),
		new(
			"Bilge",
			new Vector3(1.24f, 1.16f, 0.72f),
			new Vector3(-0.76f, 0.0f, 0.0f),
			new Color(0.28f, 0.58f, 0.44f))
	};

	private static readonly CrewDefinition[] CrewDefinitions =
	{
		new(
			"Captain",
			"C",
			"Command",
			new Vector3(0.0f, 1.16f, 3.34f),
			new Color(0.26f, 0.46f, 0.82f),
			CaptainCrewModelPath),
		new(
			"Gunner",
			"G",
			"Gunnery",
			new Vector3(-0.18f, 3.18f, -0.36f),
			new Color(0.72f, 0.32f, 0.24f),
			GunnerCrewModelPath),
		new(
			"Deckhand",
			"D",
			"Repair",
			new Vector3(1.02f, 1.06f, 1.22f),
			new Color(0.26f, 0.62f, 0.42f),
			DeckhandCrewModelPath)
	};

	private static readonly CrewDefinition[] EnemyCrewDefinitions =
	{
		new(
			"Captain",
			"C",
			"Command",
			new Vector3(-0.58f, 1.04f, 1.54f),
			new Color(0.26f, 0.46f, 0.82f),
			string.Empty),
		new(
			"Gunner",
			"G",
			"Gunnery",
			new Vector3(-0.58f, 1.04f, -0.88f),
			new Color(0.72f, 0.32f, 0.24f),
			string.Empty),
		new(
			"Deckhand",
			"D",
			"Repair",
			new Vector3(0.58f, 1.04f, 0.12f),
			new Color(0.26f, 0.62f, 0.42f),
			string.Empty)
	};

	private readonly List<StationMarker3D> _stations = new();
	private readonly List<StationMarker3D> _enemyStations = new();
	private readonly List<CrewToken3D> _crew = new();
	private readonly Dictionary<string, string> _stationByCrewName = new();
	private readonly Dictionary<string, string> _crewByStationName = new();
	private readonly Dictionary<string, StationRuntimeState> _playerStationStatesByName = new();
	private readonly Dictionary<string, StationRuntimeState> _enemyStationStatesByName = new();
	private readonly Dictionary<StationMarker3D, StationRuntimeState> _stationStatesByMarker = new();
	private readonly Dictionary<string, float> _stationDamageFlashSecondsByName = new();
	private readonly Dictionary<string, Node3D> _playerDamageIndicatorsByStation = new();

	private Node3D? _shipRoot;
	private Node3D? _enemyShipRoot;
	private Node3D? _stationRoot;
	private Node3D? _crewRoot;
	private Node3D? _playerBattleAreaFrameRoot;
	private Node3D? _enemyCutoutFrameRoot;
	private CanvasLayer? _hudRoot;
	private Label? _playerHullLabel;
	private Label? _enemyHullLabel;
	private PanelContainer? _playerHullPanel;
	private ProgressBar? _playerHullUiBar;
	private HullBarVisual? _playerHullBar;
	private HullBarVisual? _enemyHullBar;
	private Label? _selectedSummaryLabel;
	private Label? _targetLabel;
	private Label? _statusLabel;
	private VBoxContainer? _crewRows;
	private VBoxContainer? _stationStatusRows;
	private Label? _playerWeaponLabel;
	private Label? _weaponTargetLabel;
	private ProgressBar? _playerWeaponChargeBar;
	private ProgressBar? _enemyWeaponChargeBar;
	private Label? _enemyWeaponLabel;
	private Control? _ordersOverlay;
	private Button? _giveNoQuarterButton;
	private Control? _pauseOverlay;
	private Button? _resumeButton;
	private Button? _resumeSailingButton;
	private Button? _restartButton;
	private Button? _quitGameButton;
	private HSlider? _volumeSlider;
	private readonly Dictionary<string, Button> _crewButtonsByName = new();
	private Camera3D? _camera;
	private SailingEncounterData? _activeEncounterData;
	private CrewToken3D? _selectedCrew;
	private StationMarker3D? _clickedStation;
	private StationRuntimeState? _currentCannonTarget;
	private StationRuntimeState? _currentEnemyCannonTarget;
	private string _statusText = "Awaiting assignment.";
	private float _playerCannonChargeSeconds;
	private float _enemyCannonChargeSeconds;
	private float _playerHull = 100.0f;
	private float _enemyHull = 100.0f;
	private bool _isDraggingCannonTarget;
	private bool _isCombatStarted;
	private bool _isOrdersOverlayOpen;
	private bool _isPauseMenuOpen;
	private readonly RandomNumberGenerator _random = new();

	public override void _Ready()
	{
		_activeEncounterData = SailingEncounterStore.ConsumePendingEncounter();
		_random.Randomize();
		_shipRoot = GetNodeOrNull<Node3D>(ShipRootPath);
		_enemyShipRoot = GetNodeOrNull<Node3D>(EnemyShipRootPath);
		_stationRoot = GetNodeOrNull<Node3D>(StationRootPath);
		_crewRoot = GetNodeOrNull<Node3D>(CrewRootPath);
		_hudRoot = GetNodeOrNull<CanvasLayer>(HudRootPath);
		_camera = GetNodeOrNull<Camera3D>(CameraPath);

		if (_shipRoot == null || _enemyShipRoot == null || _stationRoot == null || _crewRoot == null || _hudRoot == null || _camera == null)
		{
			GD.PushError("StationCombat3D: prototype roots, HUD, or camera are missing from the scene.");
			return;
		}

		PositionBattleRoots();
		ResetCamera();
		BuildHud();
		BuildBattleAreaFrames();
		BuildShipBlockout(_shipRoot, PlayerShipStyle, "Player");
		BuildShipBlockout(_enemyShipRoot, EnemyShipStyle, "Enemy");
		BuildStations();
		BuildEnemyStations();
		BuildCrew();
		BuildEnemyCrewVisuals();
		UpdateEnemyScreenPresentation();
		UpdateHud();
		_resumeButton!.Pressed += OnResumePressed;
		_resumeSailingButton!.Pressed += OnResumeSailingPressed;
		_restartButton!.Pressed += OnRestartPressed;
		_quitGameButton!.Pressed += OnQuitGamePressed;
		_volumeSlider!.ValueChanged += OnVolumeSliderValueChanged;
		_giveNoQuarterButton!.Pressed += OnGiveNoQuarterPressed;
		SetPauseMenuOpen(false);
		SetOrdersOverlayOpen(true);
	}

	public override void _Process(double delta)
	{
		if (_isPauseMenuOpen)
		{
			return;
		}

		var deltaSeconds = (float)delta;
		UpdateCameraPan(deltaSeconds);
		UpdatePlayerHullBarPresentation();
		UpdateEnemyScreenPresentation();
		UpdateHullBars();

		if (!_isCombatStarted)
		{
			return;
		}

		UpdatePlayerStationRepairs(deltaSeconds);
		UpdateStationDamageFeedback(deltaSeconds);
		UpdatePlayerCannonCharge(deltaSeconds);
		UpdateEnemyCannonCharge(deltaSeconds);
		UpdateWeaponHud();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
		{
			SetPauseMenuOpen(!_isPauseMenuOpen);
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_isPauseMenuOpen)
		{
			return;
		}

		if (_isOrdersOverlayOpen)
		{
			return;
		}

		if (@event is InputEventKey { Pressed: true, Echo: false } keyEvent &&
			(keyEvent.Keycode == Key.R || keyEvent.Keycode == Key.Home))
		{
			ResetCamera();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is not InputEventMouseButton mouseButton)
		{
			return;
		}

		if (!mouseButton.Pressed)
		{
			if (mouseButton.ButtonIndex == MouseButton.Left && _isDraggingCannonTarget)
			{
				FinishCannonDragTargeting(mouseButton.Position);
				GetViewport().SetInputAsHandled();
			}

			return;
		}

		if (mouseButton.ButtonIndex is MouseButton.Left or MouseButton.Right &&
			TryHandleWorldClick(mouseButton))
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		if (mouseButton.ButtonIndex == MouseButton.Left)
		{
			ClearSelection("Selection cleared.");
			GetViewport().SetInputAsHandled();
			return;
		}

		if (mouseButton.ButtonIndex == MouseButton.WheelUp)
		{
			AdjustCameraZoom(-CameraZoomStep);
			GetViewport().SetInputAsHandled();
			return;
		}

		if (mouseButton.ButtonIndex == MouseButton.WheelDown)
		{
			AdjustCameraZoom(CameraZoomStep);
			GetViewport().SetInputAsHandled();
		}
	}

	private bool TryHandleWorldClick(InputEventMouseButton mouseButton)
	{
		var target = PickInteractionTarget(mouseButton.Position);
		if (target is CrewToken3D crew)
		{
			if (mouseButton.ButtonIndex == MouseButton.Left)
			{
				OnCrewClicked(crew);
			}

			return true;
		}

		if (target is StationMarker3D station)
		{
			if (_enemyStations.Contains(station))
			{
				OnEnemyStationClicked(station, mouseButton.ButtonIndex);
			}
			else
			{
				OnStationClicked(station, mouseButton.ButtonIndex);
			}

			return true;
		}

		return false;
	}

	private Node? PickInteractionTarget(Vector2 screenPosition)
	{
		if (_camera == null)
		{
			return null;
		}

		var rayOrigin = _camera.ProjectRayOrigin(screenPosition);
		var rayEnd = rayOrigin + (_camera.ProjectRayNormal(screenPosition) * 100.0f);
		var query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayEnd);
		query.CollideWithAreas = true;
		query.CollideWithBodies = false;

		var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (result.Count == 0)
		{
			return null;
		}

		var collider = result["collider"].As<Node>();
		return FindInteractionOwner(collider);
	}

	private static Node? FindInteractionOwner(Node? node)
	{
		while (node != null)
		{
			if (node is CrewToken3D or StationMarker3D)
			{
				return node;
			}

			node = node.GetParent();
		}

		return null;
	}

	private void FinishCannonDragTargeting(Vector2 screenPosition)
	{
		_isDraggingCannonTarget = false;

		if (PickInteractionTarget(screenPosition) is not StationMarker3D station ||
			!_enemyStations.Contains(station))
		{
			return;
		}

		TrySetCannonTarget(station);
	}

	private void PositionBattleRoots()
	{
		if (_shipRoot == null || _enemyShipRoot == null || _stationRoot == null || _crewRoot == null)
		{
			return;
		}

		var playerRotation = DegreesToRadians(PlayerShipRotationDegrees);
		var playerScale = Vector3.One * Mathf.Max(0.1f, PlayerShipScale);
		var enemyScale = Vector3.One * Mathf.Max(0.1f, EnemyShipScale);

		_shipRoot.Position = PlayerShipOffset;
		_shipRoot.Rotation = playerRotation;
		_shipRoot.Scale = playerScale;

		_stationRoot.Position = PlayerShipOffset;
		_stationRoot.Rotation = playerRotation;
		_stationRoot.Scale = playerScale;

		_crewRoot.Position = PlayerShipOffset;
		_crewRoot.Rotation = playerRotation;
		_crewRoot.Scale = playerScale;

		_enemyShipRoot.Position = EnemyShipOffset;
		_enemyShipRoot.Rotation = new Vector3(0.0f, Mathf.Pi, 0.0f);
		_enemyShipRoot.Scale = enemyScale;
	}

	private void BuildBattleAreaFrames()
	{
		if (_enemyShipRoot?.GetParent() is not Node3D prototypeRoot)
		{
			return;
		}

		RemoveNamedChild(prototypeRoot, "PlayerBattleAreaFrame");
		RemoveNamedChild(prototypeRoot, "EnemyCutoutFrame");
		_playerBattleAreaFrameRoot = null;
		_enemyCutoutFrameRoot = null;
		CreatePlayerBattleAreaFrame(prototypeRoot);
		CreateEnemyCutoutFrame(prototypeRoot);
	}

	private void CreatePlayerBattleAreaFrame(Node3D parent)
	{
		var root = new Node3D
		{
			Name = "PlayerBattleAreaFrame",
			Position = PlayerShipOffset + new Vector3(0.0f, 0.025f, 0.0f),
			Rotation = DegreesToRadians(PlayerShipRotationDegrees)
		};
		parent.AddChild(root);
		_playerBattleAreaFrameRoot = root;

		CreateBox(root, "PlayerDeckShadow", new Vector3(4.82f, 0.045f, 9.05f), Vector3.Zero, new Color(0.045f, 0.04f, 0.034f));
		CreateBox(root, "PlayerBowGuide", new Vector3(0.16f, 0.07f, 1.84f), new Vector3(0.0f, 0.03f, -4.06f), PlayerShipStyle.AccentColor.Darkened(0.2f));
		CreateBox(root, "PlayerSternGuide", new Vector3(0.16f, 0.07f, 1.84f), new Vector3(0.0f, 0.03f, 3.94f), PlayerShipStyle.AccentColor.Darkened(0.2f));
	}

	private void CreateEnemyCutoutFrame(Node3D parent)
	{
		var root = new Node3D
		{
			Name = "EnemyCutoutFrame",
			Position = EnemyShipOffset + new Vector3(0.0f, 0.035f, 0.0f)
		};
		parent.AddChild(root);
		_enemyCutoutFrameRoot = root;

		var panelColor = new Color(0.025f, 0.13f, 0.16f);
		var borderColor = EnemyShipStyle.AccentColor.Darkened(0.08f);
		CreateBox(root, "EnemyPanelShadow", new Vector3(5.62f, 0.045f, 8.74f), new Vector3(0.0f, -0.012f, 0.0f), new Color(0.012f, 0.014f, 0.014f));
		CreateBox(root, "EnemyPanelWater", new Vector3(5.3f, 0.055f, 8.42f), Vector3.Zero, panelColor);
		CreateBox(root, "EnemyWaterBandA", new Vector3(4.5f, 0.058f, 0.05f), new Vector3(0.0f, 0.018f, -2.3f), panelColor.Lightened(0.18f));
		CreateBox(root, "EnemyWaterBandB", new Vector3(3.9f, 0.058f, 0.05f), new Vector3(0.2f, 0.018f, 0.14f), panelColor.Lightened(0.14f));
		CreateBox(root, "EnemyWaterBandC", new Vector3(4.26f, 0.058f, 0.05f), new Vector3(-0.12f, 0.018f, 2.54f), panelColor.Lightened(0.16f));
		CreateBox(root, "EnemyPanelNorth", new Vector3(5.44f, 0.1f, 0.12f), new Vector3(0.0f, 0.055f, -4.21f), borderColor);
		CreateBox(root, "EnemyPanelSouth", new Vector3(5.44f, 0.1f, 0.12f), new Vector3(0.0f, 0.055f, 4.21f), borderColor);
		CreateBox(root, "EnemyPanelWest", new Vector3(0.12f, 0.1f, 8.42f), new Vector3(-2.72f, 0.055f, 0.0f), borderColor);
		CreateBox(root, "EnemyPanelEast", new Vector3(0.12f, 0.1f, 8.42f), new Vector3(2.72f, 0.055f, 0.0f), borderColor);
	}

	private void BuildShipBlockout(Node3D shipRoot, ShipVisualStyle style, string displayName)
	{
		ClearChildren(shipRoot);

		if (displayName == "Player")
		{
			BuildPlayerMediumShipBlockout(shipRoot, style, displayName);
			return;
		}

		BuildCompactShipBlockout(shipRoot, style, displayName);
	}

	private void BuildCompactShipBlockout(Node3D shipRoot, ShipVisualStyle style, string displayName)
	{
		CreateBox(shipRoot, "Hull", new Vector3(3.35f, 0.72f, 6.2f), new Vector3(0.0f, 0.36f, 0.0f), style.HullColor);
		CreateBox(shipRoot, "Deck", new Vector3(2.72f, 0.16f, 5.28f), new Vector3(0.0f, 0.82f, 0.12f), style.DeckColor);
		CreateBox(shipRoot, "PortRail", new Vector3(0.16f, 0.36f, 5.35f), new Vector3(-1.44f, 1.05f, 0.12f), style.RailColor);
		CreateBox(shipRoot, "StarboardRail", new Vector3(0.16f, 0.36f, 5.35f), new Vector3(1.44f, 1.05f, 0.12f), style.RailColor);
		CreateBox(shipRoot, "SternRail", new Vector3(2.86f, 0.36f, 0.16f), new Vector3(0.0f, 1.05f, 2.84f), style.RailColor);
		CreateBeamBetween(shipRoot, "PortBowRail", new Vector2(-1.44f, -2.5f), new Vector2(0.0f, -3.24f), 1.05f, style.RailColor);
		CreateBeamBetween(shipRoot, "StarboardBowRail", new Vector2(1.44f, -2.5f), new Vector2(0.0f, -3.24f), 1.05f, style.RailColor);

		for (var plank = -2; plank <= 2; plank++)
		{
			CreateBox(
				shipRoot,
				$"DeckPlankLine_{plank}",
				new Vector3(0.025f, 0.022f, 4.92f),
				new Vector3(plank * 0.42f, 0.925f, 0.18f),
				new Color(0.22f, 0.12f, 0.055f));
		}

		CreateBox(shipRoot, "FactionStripe", new Vector3(2.08f, 0.035f, 0.16f), new Vector3(0.0f, 0.935f, -1.88f), style.AccentColor);
		CreateBox(shipRoot, "SternCabin", new Vector3(1.18f, 0.54f, 0.86f), new Vector3(0.0f, 1.18f, 1.48f), style.CabinColor);
		CreateBox(shipRoot, "HelmWheelBlockout", new Vector3(0.58f, 0.42f, 0.12f), new Vector3(0.0f, 1.22f, 2.08f), style.RailColor);
		CreateBox(shipRoot, "BilgeHatch", new Vector3(0.68f, 0.05f, 0.62f), new Vector3(0.42f, 0.94f, 0.72f), HatchColor);
		CreateBox(shipRoot, "HatchSlatA", new Vector3(0.52f, 0.035f, 0.045f), new Vector3(0.42f, 0.985f, 0.58f), style.RailColor);
		CreateBox(shipRoot, "HatchSlatB", new Vector3(0.52f, 0.035f, 0.045f), new Vector3(0.42f, 0.985f, 0.72f), style.RailColor);
		CreateBox(shipRoot, "HatchSlatC", new Vector3(0.52f, 0.035f, 0.045f), new Vector3(0.42f, 0.985f, 0.86f), style.RailColor);

		CreateCylinder(shipRoot, "Mast", 0.12f, 2.85f, new Vector3(0.0f, 2.17f, -0.66f), style.RailColor, 8);
		CreateBox(shipRoot, "FurledSail", new Vector3(1.72f, 0.34f, 0.08f), new Vector3(0.0f, 2.1f, -0.66f), style.SailColor);
		CreateBox(shipRoot, "CrowNestPlatform", new Vector3(0.9f, 0.12f, 0.9f), new Vector3(0.0f, 2.78f, -0.66f), style.DeckColor);
		CreateBox(shipRoot, "CrowNestRailPort", new Vector3(0.08f, 0.22f, 0.9f), new Vector3(-0.45f, 2.93f, -0.66f), style.RailColor);
		CreateBox(shipRoot, "CrowNestRailStarboard", new Vector3(0.08f, 0.22f, 0.9f), new Vector3(0.45f, 2.93f, -0.66f), style.RailColor);

		CreateCannon(shipRoot, "PortCannonForward", new Vector3(-1.54f, 1.05f, -0.72f), pointsPort: true);
		CreateCannon(shipRoot, "PortCannonAft", new Vector3(-1.54f, 1.05f, 0.28f), pointsPort: true);
		CreateCannon(shipRoot, "StarboardCannonForward", new Vector3(1.54f, 1.05f, -0.72f), pointsPort: false);
		CreateCannon(shipRoot, "StarboardCannonAft", new Vector3(1.54f, 1.05f, 0.28f), pointsPort: false);
		CreateShipLabel(shipRoot, displayName, new Vector3(0.0f, 1.16f, -3.62f), style.AccentColor);
		var isEnemyShip = displayName == "Enemy";
		var isPlayerShip = displayName == "Player";
		var hullBarParent = isEnemyShip && _enemyCutoutFrameRoot != null
			? _enemyCutoutFrameRoot
			: isPlayerShip && shipRoot.GetParent() is Node3D prototypeRoot
				? prototypeRoot
				: shipRoot;
		var hullBarPosition = isEnemyShip
			? new Vector3(0.0f, 0.18f, 3.18f)
			: isPlayerShip
				? Vector3.Zero
				: new Vector3(0.0f, 1.06f, -4.08f);
		var hullBar = CreateHullBar(hullBarParent, $"{displayName}HullBar", $"{displayName} Hull", hullBarPosition, style.AccentColor);
		if (displayName == "Player")
		{
			_playerHullBar = hullBar;
			UpdatePlayerHullBarPresentation();
		}
		else if (isEnemyShip)
		{
			_enemyHullBar = hullBar;
		}
	}

	private void BuildPlayerMediumShipBlockout(Node3D shipRoot, ShipVisualStyle style, string displayName)
	{
		CreateBox(shipRoot, "Hull", new Vector3(4.02f, 0.76f, 8.18f), new Vector3(0.0f, 0.36f, 0.0f), style.HullColor);
		CreateBox(shipRoot, "Deck", new Vector3(3.28f, 0.16f, 7.08f), new Vector3(0.0f, 0.84f, 0.08f), style.DeckColor);
		CreateBox(shipRoot, "PortRail", new Vector3(0.16f, 0.38f, 7.16f), new Vector3(-1.7f, 1.08f, 0.08f), style.RailColor);
		CreateBox(shipRoot, "StarboardRail", new Vector3(0.16f, 0.38f, 7.16f), new Vector3(1.7f, 1.08f, 0.08f), style.RailColor);
		CreateBox(shipRoot, "SternRail", new Vector3(3.34f, 0.38f, 0.16f), new Vector3(0.0f, 1.08f, 3.68f), style.RailColor);
		CreateBeamBetween(shipRoot, "PortBowRail", new Vector2(-1.7f, -3.28f), new Vector2(0.0f, -4.18f), 1.08f, style.RailColor);
		CreateBeamBetween(shipRoot, "StarboardBowRail", new Vector2(1.7f, -3.28f), new Vector2(0.0f, -4.18f), 1.08f, style.RailColor);

		for (var plank = -3; plank <= 3; plank++)
		{
			CreateBox(
				shipRoot,
				$"DeckPlankLine_{plank}",
				new Vector3(0.022f, 0.022f, 6.76f),
				new Vector3(plank * 0.39f, 0.945f, 0.04f),
				new Color(0.22f, 0.12f, 0.055f));
		}

		for (var seam = -4; seam <= 4; seam++)
		{
			CreateBox(
				shipRoot,
				$"DeckCrossSeam_{seam}",
				new Vector3(2.82f, 0.018f, 0.025f),
				new Vector3(0.0f, 0.955f, seam * 0.78f),
				new Color(0.18f, 0.095f, 0.045f));
		}

		CreatePlayerStationStandSpots(shipRoot);

		CreateBox(shipRoot, "FactionStripe", new Vector3(2.48f, 0.035f, 0.16f), new Vector3(0.0f, 0.96f, -2.62f), style.AccentColor);
		CreateBox(shipRoot, "Quarterdeck", new Vector3(2.6f, 0.18f, 1.34f), new Vector3(-0.08f, 1.02f, 2.86f), style.DeckColor.Lightened(0.08f));
		CreateBox(shipRoot, "HelmStationPad", new Vector3(1.32f, 0.026f, 1.08f), new Vector3(0.0f, 1.125f, 3.08f), new Color(0.12f, 0.22f, 0.34f));
		CreatePlayerHelmModel(shipRoot, "PlayerHelmWheel", new Vector3(0.0f, 1.24f, 2.9f));

		CreateBox(shipRoot, "BilgeStationPad", new Vector3(1.22f, 0.026f, 1.06f), new Vector3(0.76f, 0.982f, 0.84f), new Color(0.08f, 0.22f, 0.16f));
		CreateBox(shipRoot, "BilgeHatch", new Vector3(0.86f, 0.055f, 0.76f), new Vector3(0.74f, 0.96f, 0.8f), HatchColor);
		CreateBox(shipRoot, "HatchSlatA", new Vector3(0.68f, 0.035f, 0.045f), new Vector3(0.74f, 1.005f, 0.62f), style.RailColor);
		CreateBox(shipRoot, "HatchSlatB", new Vector3(0.68f, 0.035f, 0.045f), new Vector3(0.74f, 1.005f, 0.8f), style.RailColor);
		CreateBox(shipRoot, "HatchSlatC", new Vector3(0.68f, 0.035f, 0.045f), new Vector3(0.74f, 1.005f, 0.98f), style.RailColor);

		CreateCylinder(shipRoot, "Mast", 0.14f, 1.78f, new Vector3(-0.18f, 1.88f, -0.34f), style.RailColor, 10);
		CreateBox(shipRoot, "MastBase", new Vector3(0.58f, 0.16f, 0.58f), new Vector3(-0.18f, 1.02f, -0.34f), style.RailColor.Darkened(0.08f));
		CreateBox(shipRoot, "FurledSail", new Vector3(2.1f, 0.38f, 0.08f), new Vector3(-0.18f, 2.22f, -0.34f), style.SailColor);
		CreateBox(shipRoot, "LowerYard", new Vector3(2.72f, 0.08f, 0.08f), new Vector3(-0.18f, 2.18f, -0.34f), style.RailColor);
		CreateBox(shipRoot, "RiggingPort", new Vector3(0.045f, 1.5f, 0.045f), new Vector3(-0.76f, 1.78f, -0.34f), style.RailColor.Darkened(0.1f), new Vector3(0.0f, 0.0f, 0.42f));
		CreateBox(shipRoot, "RiggingStarboard", new Vector3(0.045f, 1.5f, 0.045f), new Vector3(0.42f, 1.78f, -0.34f), style.RailColor.Darkened(0.1f), new Vector3(0.0f, 0.0f, -0.42f));
		CreateBox(shipRoot, "CrowNestPlatform", new Vector3(0.98f, 0.12f, 0.98f), new Vector3(-0.18f, 2.9f, -0.34f), style.DeckColor);
		CreateBox(shipRoot, "CrowNestRailPort", new Vector3(0.08f, 0.22f, 0.98f), new Vector3(-0.67f, 3.05f, -0.34f), style.RailColor);
		CreateBox(shipRoot, "CrowNestRailStarboard", new Vector3(0.08f, 0.22f, 0.98f), new Vector3(0.31f, 3.05f, -0.34f), style.RailColor);

		CreateBox(shipRoot, "CannonStationPad", new Vector3(1.24f, 0.028f, 2.84f), new Vector3(1.22f, 0.982f, -1.08f), new Color(0.34f, 0.12f, 0.08f));
		CreatePlayerCannonModel(shipRoot, "BroadsideCannonAft", new Vector3(1.82f, 1.08f, 0.12f));
		CreatePlayerCannonModel(shipRoot, "BroadsideCannonMid", new Vector3(1.82f, 1.08f, -1.08f));
		CreatePlayerCannonModel(shipRoot, "BroadsideCannonForward", new Vector3(1.82f, 1.08f, -2.28f));

		CreatePowderBarrel(shipRoot, "PowderBarrelA", new Vector3(0.76f, 1.08f, -0.86f));
		CreatePowderBarrel(shipRoot, "PowderBarrelB", new Vector3(0.84f, 1.08f, -1.28f));

		CreateCylinder(shipRoot, "RepairBucket", 0.18f, 0.22f, new Vector3(0.98f, 1.1f, 1.62f), new Color(0.36f, 0.32f, 0.24f), 10);

		CreateShipLabel(shipRoot, displayName, new Vector3(0.0f, 1.18f, -4.22f), style.AccentColor);

		var hullBarParent = shipRoot.GetParent() is Node3D prototypeRoot
			? prototypeRoot
			: shipRoot;
		RemoveNamedChild(hullBarParent, $"{displayName}HullBar");
		_playerHullBar = null;
	}

	private static void CreatePlayerStationStandSpots(Node3D parent)
	{
		foreach (var station in StationDefinitions)
		{
			if (station.Name == "Crow's Nest")
			{
				continue;
			}

			CreateStationStandSpot(
				parent,
				$"{SanitizeNodeName(station.Name)}StandSpot",
				station.Position + station.AssignmentOffset,
				station.Color);
		}
	}

	private static void CreateStationStandSpot(Node3D parent, string nodeName, Vector3 position, Color color)
	{
		CreateCylinder(
			parent,
			nodeName,
			0.25f,
			0.026f,
			new Vector3(position.X, position.Y - 0.006f, position.Z),
			color.Darkened(0.28f),
			14);
	}

	private void BuildStations()
	{
		if (_stationRoot == null)
		{
			return;
		}

		ClearChildren(_stationRoot);
		_stations.Clear();
		_clickedStation = null;
		_crewByStationName.Clear();
		_playerStationStatesByName.Clear();
		_stationStatesByMarker.Clear();
		_stationDamageFlashSecondsByName.Clear();
		_playerDamageIndicatorsByStation.Clear();

		foreach (var definition in StationDefinitions)
		{
			var marker = new StationMarker3D
			{
				Name = $"{SanitizeNodeName(definition.Name)}Station",
				StationName = definition.Name,
				MarkerColor = definition.Color,
				Position = definition.Position,
				AssignmentOffset = definition.AssignmentOffset,
				ClickShapeSize = definition.Name == CannonsStationName
					? new Vector3(1.65f, 0.95f, 2.95f)
					: new Vector3(0.88f, 0.9f, 0.88f)
			};

			_stationRoot.AddChild(marker);
			marker.Clicked += OnStationClicked;
			_stations.Add(marker);
			AddStationState(marker, definition.Name, isEnemy: false);
		}
	}

	private void BuildEnemyStations()
	{
		if (_enemyShipRoot == null)
		{
			return;
		}

		RemoveNamedChild(_enemyShipRoot, "EnemyStations");
		var enemyStationRoot = new Node3D { Name = "EnemyStations" };
		_enemyShipRoot.AddChild(enemyStationRoot);

		_enemyStations.Clear();
		_enemyStationStatesByName.Clear();

		foreach (var definition in EnemyStationDefinitions)
		{
			var marker = new StationMarker3D
			{
				Name = $"Enemy{SanitizeNodeName(definition.Name)}Station",
				StationName = definition.Name,
				MarkerColor = definition.Color.Darkened(0.28f).Lerp(new Color(0.72f, 0.18f, 0.14f), 0.22f),
				Position = definition.Position,
				Scale = Vector3.One * 1.12f,
				AssignmentOffset = -definition.AssignmentOffset,
				ClickShapeSize = new Vector3(1.18f, 1.05f, 1.18f),
				ClickShapeOffset = new Vector3(0.0f, 0.48f, 0.0f)
			};

			enemyStationRoot.AddChild(marker);
			marker.Clicked += OnEnemyStationClicked;
			_enemyStations.Add(marker);
			AddStationState(marker, definition.Name, isEnemy: true);
		}
	}

	private void AddStationState(StationMarker3D marker, string stationName, bool isEnemy)
	{
		var state = new StationRuntimeState(stationName, marker, isEnemy);
		if (isEnemy)
		{
			_enemyStationStatesByName[stationName] = state;
		}
		else
		{
			_playerStationStatesByName[stationName] = state;
		}

		_stationStatesByMarker[marker] = state;
		marker.SetDurabilityPercent(state.Durability);
	}

	private void BuildCrew()
	{
		if (_crewRoot == null)
		{
			return;
		}

		ClearChildren(_crewRoot);
		_crew.Clear();
		_stationByCrewName.Clear();
		_selectedCrew = null;

		foreach (var definition in CrewDefinitions)
		{
			var crew = new CrewToken3D
			{
				Name = $"{SanitizeNodeName(definition.Name)}Token",
				CrewName = definition.Name,
				ShortLabel = definition.ShortLabel,
				CrewRole = definition.Role,
				CrewColor = definition.Color,
				ModelPath = definition.ModelPath,
				Position = definition.HomePosition,
				HomePosition = definition.HomePosition
			};

			_crewRoot.AddChild(crew);
			crew.Clicked += OnCrewClicked;
			_crew.Add(crew);
		}

		AssignCaptainToHelmByDefault();
	}

	private void BuildEnemyCrewVisuals()
	{
		if (_enemyShipRoot == null)
		{
			return;
		}

		RemoveNamedChild(_enemyShipRoot, "EnemyCrew");
		var enemyCrewRoot = new Node3D { Name = "EnemyCrew" };
		_enemyShipRoot.AddChild(enemyCrewRoot);

		foreach (var definition in EnemyCrewDefinitions)
		{
			CreateCrewPlaceholder(
				enemyCrewRoot,
				$"Enemy{SanitizeNodeName(definition.Name)}",
				definition.ShortLabel,
				definition.HomePosition,
				new Color(0.56f, 0.16f, 0.14f));
		}
	}

	private void OnCrewClicked(CrewToken3D crew)
	{
		_selectedCrew = crew;
		_clickedStation = null;
		_isDraggingCannonTarget = false;
		_statusText = $"{crew.CrewName} selected.";
		UpdateSelectionVisuals();
		UpdateHud();
	}

	private void AssignCaptainToHelmByDefault()
	{
		var captain = FindCrew(CaptainCrewName);
		var helm = FindStation(HelmStationName);
		if (captain == null || helm == null)
		{
			return;
		}

		AssignCrewToStation(captain, helm);
	}

	private void OnStationClicked(StationMarker3D station, MouseButton button)
	{
		if (button == MouseButton.Left)
		{
			_selectedCrew = null;
			_clickedStation = station;
			_isDraggingCannonTarget = station.StationName == CannonsStationName;
			_statusText = $"{station.StationName} selected.";
			UpdateSelectionVisuals();
			UpdateHud();
			return;
		}

		if (button != MouseButton.Right || _selectedCrew == null)
		{
			return;
		}

		var stoppedRepairStationName = AssignCrewToStation(_selectedCrew, station);
		_clickedStation = null;
		_statusText = string.IsNullOrEmpty(stoppedRepairStationName)
			? $"{_selectedCrew.CrewName} assigned to {station.StationName}."
			: $"Repair stopped: no crew assigned to {stoppedRepairStationName}.";
		UpdateSelectionVisuals();
		UpdateHud();
	}

	private void OnEnemyStationClicked(StationMarker3D station, MouseButton button)
	{
		if (button == MouseButton.Left)
		{
			return;
		}

		if (button != MouseButton.Right || _selectedCrew != null || _clickedStation?.StationName != CannonsStationName)
		{
			return;
		}

		TrySetCannonTarget(station);
	}

	private string AssignCrewToStation(CrewToken3D crew, StationMarker3D station)
	{
		var stoppedRepairStationName = string.Empty;
		if (_stationByCrewName.TryGetValue(crew.CrewName, out var oldStationName))
		{
			_crewByStationName.Remove(oldStationName);
			FindStation(oldStationName)?.SetAssignedCrew(null);
			if (StopRepairIfUncrewed(oldStationName))
			{
				stoppedRepairStationName = oldStationName;
			}
		}

		if (_crewByStationName.TryGetValue(station.StationName, out var displacedCrewName) &&
			displacedCrewName != crew.CrewName)
		{
			_stationByCrewName.Remove(displacedCrewName);
			if (StopRepairIfUncrewed(station.StationName))
			{
				stoppedRepairStationName = station.StationName;
			}
			var displacedCrew = FindCrew(displacedCrewName);
			if (displacedCrew != null)
			{
				displacedCrew.SetAssignedStation(null);
				displacedCrew.Position = displacedCrew.HomePosition;
			}
		}

		_stationByCrewName[crew.CrewName] = station.StationName;
		_crewByStationName[station.StationName] = crew.CrewName;
		station.SetAssignedCrew(crew.CrewName);
		crew.SetAssignedStation(station.StationName);
		crew.GlobalPosition = station.AssignmentSlotGlobalPosition;
		ResetCannonChargeIfUnableToFire();
		return stoppedRepairStationName;
	}

	private void StartRepair(string stationName)
	{
		if (!_playerStationStatesByName.TryGetValue(stationName, out var station))
		{
			return;
		}

		if (station.Durability >= 100.0f)
		{
			_statusText = $"{station.Name} is already fully repaired.";
			UpdateHud();
			return;
		}

		if (!IsPlayerStationCrewed(stationName))
		{
			_statusText = $"Assign crew to {station.Name} before repairing.";
			UpdateHud();
			return;
		}

		station.IsRepairing = true;
		_statusText = $"{GetCrewAssignedToStation(stationName) ?? "Crew"} started repairing {station.Name}.";
		UpdateHud();
	}

	private void UpdatePlayerStationRepairs(float deltaSeconds)
	{
		var hadRepairUpdate = false;
		foreach (var station in _playerStationStatesByName.Values)
		{
			if (!station.IsRepairing)
			{
				continue;
			}

			if (!IsPlayerStationCrewed(station.Name))
			{
				station.IsRepairing = false;
				_statusText = $"Repair stopped: no crew assigned to {station.Name}.";
				hadRepairUpdate = true;
				continue;
			}

			station.Repair(PlayerRepairRatePerSecond * deltaSeconds);
			station.Marker.SetDurabilityPercent(station.Durability);
			RefreshPlayerStationDamageIndicator(station);
			hadRepairUpdate = true;

			if (station.Durability >= 100.0f)
			{
				station.IsRepairing = false;
				_statusText = $"{station.Name} repaired.";
			}
		}

		if (hadRepairUpdate)
		{
			UpdateHud();
		}
	}

	private void StartPlayerStationDamageFeedback(StationRuntimeState station)
	{
		if (station.IsEnemy)
		{
			return;
		}

		_stationDamageFlashSecondsByName[station.Name] = StationDamageFlashSeconds;
		RefreshPlayerStationDamageIndicator(station);
		RebuildStationStatusRows();
	}

	private void UpdateStationDamageFeedback(float deltaSeconds)
	{
		if (_stationDamageFlashSecondsByName.Count == 0)
		{
			return;
		}

		var stationNames = new List<string>(_stationDamageFlashSecondsByName.Keys);
		foreach (var stationName in stationNames)
		{
			var remainingSeconds = _stationDamageFlashSecondsByName[stationName] - deltaSeconds;
			if (remainingSeconds <= 0.0f)
			{
				_stationDamageFlashSecondsByName.Remove(stationName);
				continue;
			}

			_stationDamageFlashSecondsByName[stationName] = remainingSeconds;
		}

		RebuildStationStatusRows();
	}

	private void RefreshPlayerStationDamageIndicator(StationRuntimeState station)
	{
		if (station.IsEnemy)
		{
			return;
		}

		if (station.Durability >= 99.9f)
		{
			RemovePlayerStationDamageIndicator(station.Name);
			return;
		}

		if (_playerDamageIndicatorsByStation.ContainsKey(station.Name))
		{
			return;
		}

		var indicator = CreateStationDamageIndicator();
		station.Marker.AddChild(indicator);
		_playerDamageIndicatorsByStation[station.Name] = indicator;
	}

	private void RemovePlayerStationDamageIndicator(string stationName)
	{
		if (!_playerDamageIndicatorsByStation.Remove(stationName, out var indicator))
		{
			return;
		}

		indicator.GetParent()?.RemoveChild(indicator);
		indicator.QueueFree();
	}

	private bool StopRepairIfUncrewed(string stationName)
	{
		if (!_playerStationStatesByName.TryGetValue(stationName, out var station) ||
			!station.IsRepairing ||
			IsPlayerStationCrewed(stationName))
		{
			return false;
		}

		station.IsRepairing = false;
		_statusText = $"Repair stopped: no crew assigned to {station.Name}.";
		return true;
	}

	private bool IsPlayerStationCrewed(string stationName)
	{
		return _crewByStationName.ContainsKey(stationName);
	}

	private string? GetCrewAssignedToStation(string stationName)
	{
		return _crewByStationName.TryGetValue(stationName, out var crewName)
			? crewName
			: null;
	}

	private void TrySetCannonTarget(StationMarker3D enemyStation)
	{
		if (!_stationStatesByMarker.TryGetValue(enemyStation, out var targetState) || !targetState.IsEnemy)
		{
			return;
		}

		if (!IsPlayerCannonsCrewed())
		{
			_statusText = "Cannons need crew before targeting.";
			_playerCannonChargeSeconds = 0.0f;
			UpdateHud();
			return;
		}

		if (!IsPlayerCannonsOperational())
		{
			_statusText = "Player Cannons disabled.";
			_playerCannonChargeSeconds = 0.0f;
			UpdateHud();
			return;
		}

		if (targetState.IsDisabled)
		{
			_statusText = $"Enemy {targetState.Name} is disabled.";
			UpdateHud();
			return;
		}

		_currentCannonTarget = targetState;
		_playerCannonChargeSeconds = 0.0f;
		_statusText = $"Cannons targeting Enemy {targetState.Name}.";
		UpdateSelectionVisuals();
		UpdateHud();
	}

	private bool IsPlayerCannonsCrewed()
	{
		return _crewByStationName.ContainsKey(CannonsStationName);
	}

	private bool IsPlayerCannonsOperational()
	{
		return _playerStationStatesByName.TryGetValue(CannonsStationName, out var cannons) && !cannons.IsDisabled;
	}

	private bool CanPlayerCannonsCharge()
	{
		return IsPlayerCannonsCrewed() &&
			IsPlayerCannonsOperational() &&
			_currentCannonTarget != null &&
			!_currentCannonTarget.IsDisabled &&
			_playerHull > 0.0f &&
			_enemyHull > 0.0f;
	}

	private void ResetCannonChargeIfUnableToFire()
	{
		if (CanPlayerCannonsCharge())
		{
			return;
		}

		_playerCannonChargeSeconds = 0.0f;
	}

	private void UpdatePlayerCannonCharge(float deltaSeconds)
	{
		if (!CanPlayerCannonsCharge())
		{
			if (!IsPlayerCannonsOperational() && _playerCannonChargeSeconds > 0.0f)
			{
				_statusText = "Player Cannons disabled.";
				UpdateHud();
			}

			_playerCannonChargeSeconds = 0.0f;
			return;
		}

		_playerCannonChargeSeconds += deltaSeconds;
		if (_playerCannonChargeSeconds < PlayerCannonChargeDurationSeconds)
		{
			return;
		}

		FirePlayerCannons();
		_playerCannonChargeSeconds = 0.0f;
	}

	private void FirePlayerCannons()
	{
		if (_currentCannonTarget == null)
		{
			return;
		}

		_currentCannonTarget.ApplyDamage(PlayerCannonDamage);
		_currentCannonTarget.Marker.SetDurabilityPercent(_currentCannonTarget.Durability);
		_enemyHull = Mathf.Clamp(_enemyHull - CannonHullDamage, 0.0f, 100.0f);
		var hitTargetName = _currentCannonTarget.Name;
		var targetDisabled = _currentCannonTarget.IsDisabled;

		_statusText = _enemyHull <= 0.0f
			? "Enemy hull broken!"
			: targetDisabled
			? $"Enemy {hitTargetName} disabled."
			: $"Cannons hit Enemy {hitTargetName} for {PlayerCannonDamage:0}, hull -{CannonHullDamage:0}.";
		if (targetDisabled || _enemyHull <= 0.0f)
		{
			_currentCannonTarget = null;
		}

		UpdateSelectionVisuals();
		UpdateHullBars();
		UpdateHud();
	}

	private bool CanEnemyCannonsCharge()
	{
		return _enemyStationStatesByName.TryGetValue(CannonsStationName, out var enemyCannons) &&
			!enemyCannons.IsDisabled &&
			_enemyHull > 0.0f &&
			_playerHull > 0.0f;
	}

	private void UpdateEnemyCannonCharge(float deltaSeconds)
	{
		if (!CanEnemyCannonsCharge())
		{
			_enemyCannonChargeSeconds = 0.0f;
			return;
		}

		_currentEnemyCannonTarget = ChooseEnemyCannonTarget();
		if (_currentEnemyCannonTarget == null)
		{
			_enemyCannonChargeSeconds = 0.0f;
			return;
		}

		_enemyCannonChargeSeconds += deltaSeconds;
		if (_enemyCannonChargeSeconds < EnemyCannonChargeDurationSeconds)
		{
			return;
		}

		FireEnemyCannons();
		_enemyCannonChargeSeconds = 0.0f;
	}

	private StationRuntimeState? ChooseEnemyCannonTarget()
	{
		foreach (var stationName in EnemyTargetPriority)
		{
			if (_playerStationStatesByName.TryGetValue(stationName, out var station) && !station.IsDisabled)
			{
				return station;
			}
		}

		return _playerStationStatesByName.TryGetValue(CannonsStationName, out var fallback)
			? fallback
			: null;
	}

	private void FireEnemyCannons()
	{
		if (_currentEnemyCannonTarget == null)
		{
			return;
		}

		if (TryDodgeEnemyCannonFire())
		{
			_statusText = "Player dodged enemy cannon fire! Captain at Helm.";
			UpdateSelectionVisuals();
			UpdateHullBars();
			UpdateHud();
			return;
		}

		if (!_currentEnemyCannonTarget.IsDisabled)
		{
			_currentEnemyCannonTarget.ApplyDamage(EnemyCannonDamage);
			_currentEnemyCannonTarget.Marker.SetDurabilityPercent(_currentEnemyCannonTarget.Durability);
			StartPlayerStationDamageFeedback(_currentEnemyCannonTarget);
		}

		_playerHull = Mathf.Clamp(_playerHull - CannonHullDamage, 0.0f, 100.0f);
		_statusText = _playerHull <= 0.0f
			? "Player ship defeated!"
			: _currentEnemyCannonTarget.IsDisabled
			? $"Enemy disabled Player {_currentEnemyCannonTarget.Name}."
			: $"Enemy hit Player {_currentEnemyCannonTarget.Name} for {EnemyCannonDamage:0}, hull -{CannonHullDamage:0}.";

		if (_currentEnemyCannonTarget.Name == CannonsStationName && _currentEnemyCannonTarget.IsDisabled)
		{
			_playerCannonChargeSeconds = 0.0f;
		}

		_currentEnemyCannonTarget = ChooseEnemyCannonTarget();
		UpdateSelectionVisuals();
		UpdateHullBars();
		UpdateHud();
	}

	private bool TryDodgeEnemyCannonFire()
	{
		if (!CanPlayerDodgeEnemyFire())
		{
			return false;
		}

		return _random.Randf() < PlayerHelmDodgeChance;
	}

	private bool CanPlayerDodgeEnemyFire()
	{
		return _playerStationStatesByName.TryGetValue(HelmStationName, out var helm) &&
			!helm.IsDisabled &&
			_crewByStationName.TryGetValue(HelmStationName, out var crewName) &&
			crewName == CaptainCrewName;
	}

	private void UpdateSelectionVisuals()
	{
		foreach (var crew in _crew)
		{
			crew.SetSelected(crew == _selectedCrew);
		}

		foreach (var station in _stations)
		{
			station.SetHighlighted(station == _clickedStation);
		}

		foreach (var enemyStation in _enemyStations)
		{
			var isTargeted =
				_currentCannonTarget != null &&
				_stationStatesByMarker.TryGetValue(enemyStation, out var state) &&
				state == _currentCannonTarget;
			enemyStation.SetHighlighted(isTargeted);
			enemyStation.SetTargeted(isTargeted);
		}
	}

	private void ClearSelection(string statusText)
	{
		_selectedCrew = null;
		_clickedStation = null;
		_isDraggingCannonTarget = false;
		_statusText = statusText;
		UpdateSelectionVisuals();
		UpdateHud();
	}

	private void OnResumePressed()
	{
		SetPauseMenuOpen(false);
	}

	private void OnResumeSailingPressed()
	{
		SetPauseMenuOpen(false);
		ReturnToSailing();
	}

	private void OnRestartPressed()
	{
		SetPauseMenuOpen(false);
		ResetStationCombat();
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

	private void OnGiveNoQuarterPressed()
	{
		_isCombatStarted = true;
		SetOrdersOverlayOpen(false);
		_statusText = "Battle joined. Give 'em no quarter!";
		UpdateHud();
	}

	private void ResetStationCombat()
	{
		var shipRoot = _shipRoot;
		var enemyShipRoot = _enemyShipRoot;
		if (shipRoot == null || enemyShipRoot == null)
		{
			return;
		}

		_selectedCrew = null;
		_clickedStation = null;
		_currentCannonTarget = null;
		_currentEnemyCannonTarget = null;
		_isDraggingCannonTarget = false;
		_playerCannonChargeSeconds = 0.0f;
		_enemyCannonChargeSeconds = 0.0f;
		_playerHull = 100.0f;
		_enemyHull = 100.0f;
		_statusText = "Awaiting assignment.";
		_isCombatStarted = false;
		_playerHullBar = null;
		_enemyHullBar = null;

		if (shipRoot.GetParent() is Node3D prototypeRoot)
		{
			RemoveNamedChild(prototypeRoot, "PlayerHullBar");
		}

		PositionBattleRoots();
		ResetCamera();
		BuildBattleAreaFrames();
		BuildShipBlockout(shipRoot, PlayerShipStyle, "Player");
		BuildShipBlockout(enemyShipRoot, EnemyShipStyle, "Enemy");
		BuildStations();
		BuildEnemyStations();
		BuildCrew();
		BuildEnemyCrewVisuals();
		UpdateEnemyScreenPresentation();
		UpdateSelectionVisuals();
		UpdateHud();
		SetOrdersOverlayOpen(true);
	}

	private void ReturnToSailing()
	{
		var returnScenePath = _activeEncounterData?.ReturnScenePath;
		if (string.IsNullOrWhiteSpace(returnScenePath))
		{
			_statusText = "No sailing scene return path is available.";
			UpdateHud();
			return;
		}

		var sceneChangeError = GetTree().ChangeSceneToFile(returnScenePath);
		if (sceneChangeError != Error.Ok)
		{
			_statusText = $"Could not return to sailing: {sceneChangeError}.";
			UpdateHud();
		}
	}

	private bool CanReturnToSailing()
	{
		return !string.IsNullOrWhiteSpace(_activeEncounterData?.ReturnScenePath);
	}

	private void SetPauseMenuOpen(bool isOpen)
	{
		_isPauseMenuOpen = isOpen;

		if (_pauseOverlay != null)
		{
			_pauseOverlay.Visible = isOpen;
		}

		if (_resumeSailingButton != null)
		{
			_resumeSailingButton.Visible = CanReturnToSailing();
		}

		if (isOpen)
		{
			_resumeButton?.GrabFocus();
		}
	}

	private void SetOrdersOverlayOpen(bool isOpen)
	{
		_isOrdersOverlayOpen = isOpen;

		if (_ordersOverlay != null)
		{
			_ordersOverlay.Visible = isOpen;
		}

		if (isOpen)
		{
			_giveNoQuarterButton?.GrabFocus();
		}
	}

	private void ResetCamera()
	{
		if (_camera == null)
		{
			return;
		}

		_camera.Projection = Camera3D.ProjectionType.Orthogonal;
		_camera.Position = DefaultCameraPosition;
		_camera.RotationDegrees = DefaultCameraRotationDegrees;
		_camera.Size = Mathf.Clamp(DefaultCameraSize, MinCameraSize, MaxCameraSize);
		UpdatePlayerHullBarPresentation();
		UpdateEnemyScreenPresentation();
	}

	private void UpdateCameraPan(float deltaSeconds)
	{
		if (_camera == null)
		{
			return;
		}

		var input = Vector2.Zero;
		if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
		{
			input.X -= 1.0f;
		}

		if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
		{
			input.X += 1.0f;
		}

		if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
		{
			input.Y -= 1.0f;
		}

		if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
		{
			input.Y += 1.0f;
		}

		if (input.LengthSquared() <= 0.0f)
		{
			return;
		}

		input = input.Normalized();
		var zoomScale = _camera.Size / Mathf.Max(0.01f, DefaultCameraSize);
		var panDistance = CameraPanSpeed * zoomScale * deltaSeconds;
		var nextPosition = _camera.Position + new Vector3(input.X, 0.0f, input.Y) * panDistance;
		_camera.Position = new Vector3(
			Mathf.Clamp(nextPosition.X, -5.6f, 6.2f),
			nextPosition.Y,
			Mathf.Clamp(nextPosition.Z, 4.6f, 14.2f));
	}

	private void AdjustCameraZoom(float sizeDelta)
	{
		if (_camera == null)
		{
			return;
		}

		_camera.Size = Mathf.Clamp(_camera.Size + sizeDelta, MinCameraSize, MaxCameraSize);
		UpdatePlayerHullBarPresentation();
		UpdateEnemyScreenPresentation();
	}

	private void UpdatePlayerHullBarPresentation()
	{
		if (_camera == null)
		{
			return;
		}

		var screenDown = -_camera.GlobalTransform.Basis.Y;
		screenDown.Y = 0.0f;
		if (screenDown.LengthSquared() <= 0.001f)
		{
			screenDown = Vector3.Forward;
		}
		else
		{
			screenDown = screenDown.Normalized();
		}

		var zoomScale = _camera.Size / Mathf.Max(0.01f, DefaultCameraSize);
		var hullBarWorldPosition = PlayerShipOffset +
			(screenDown * 4.45f * Mathf.Max(0.1f, PlayerShipScale)) +
			new Vector3(0.0f, 1.25f, 0.0f);

		if (_playerHullPanel != null)
		{
			var viewportSize = GetViewport().GetVisibleRect().Size;
			var panelSize = _playerHullPanel.CustomMinimumSize;
			var screenPosition = _camera.UnprojectPosition(hullBarWorldPosition);
			var panelX = Mathf.Clamp(screenPosition.X - (panelSize.X * 0.5f), 188.0f, Mathf.Max(188.0f, viewportSize.X - panelSize.X - 16.0f));
			var panelY = Mathf.Clamp(screenPosition.Y + 8.0f, 104.0f, Mathf.Max(104.0f, viewportSize.Y - 204.0f));

			_playerHullPanel.Position = new Vector2(panelX, panelY);
			_playerHullPanel.Size = panelSize;
		}

		if (_playerHullBar?.Root != null)
		{
			_playerHullBar.Root.GlobalPosition = hullBarWorldPosition;
			_playerHullBar.Root.GlobalRotation = Vector3.Zero;
			_playerHullBar.Root.Scale = Vector3.One * zoomScale;
		}
	}

	private void UpdateEnemyScreenPresentation()
	{
		if (_camera == null || _enemyShipRoot == null)
		{
			return;
		}

		var anchor = GetWorldPointForEnemyCutoutAnchor();
		var zoomScale = _camera.Size / Mathf.Max(0.01f, DefaultCameraSize);
		var previewYaw = _camera.GlobalRotation.Y;

		_enemyShipRoot.GlobalPosition = anchor + new Vector3(0.0f, 0.08f * zoomScale, 0.0f);
		_enemyShipRoot.GlobalRotation = new Vector3(0.0f, previewYaw + Mathf.Pi, 0.0f);
		_enemyShipRoot.Scale = Vector3.One * Mathf.Max(0.1f, EnemyShipScale) * zoomScale;

		if (_enemyCutoutFrameRoot != null)
		{
			_enemyCutoutFrameRoot.GlobalPosition = anchor + new Vector3(0.0f, -0.04f * zoomScale, 0.0f);
			_enemyCutoutFrameRoot.GlobalRotation = new Vector3(0.0f, previewYaw, 0.0f);
			_enemyCutoutFrameRoot.Scale = Vector3.One * zoomScale;
		}
	}

	private Vector3 GetWorldPointForEnemyCutoutAnchor()
	{
		if (_camera == null)
		{
			return EnemyShipOffset;
		}

		var viewportSize = GetViewport().GetVisibleRect().Size;
		var clampedAnchor = new Vector2(
			Mathf.Clamp(EnemyCutoutScreenAnchor.X, 0.0f, 1.0f),
			Mathf.Clamp(EnemyCutoutScreenAnchor.Y, 0.0f, 1.0f));
		var screenPoint = new Vector2(
			viewportSize.X * clampedAnchor.X,
			viewportSize.Y * clampedAnchor.Y);

		return _camera.ProjectRayOrigin(screenPoint) +
			_camera.ProjectRayNormal(screenPoint) * EnemyCutoutAnchorDepth;
	}

	private void UpdateHullBars()
	{
		if (_playerHullUiBar != null)
		{
			_playerHullUiBar.Value = Mathf.Clamp(_playerHull, 0.0f, 100.0f);
		}

		UpdateHullBar(_playerHullBar, _playerHull, "Player Hull");
		UpdateHullBar(_enemyHullBar, _enemyHull, "Enemy Hull");
	}

	private static void UpdateHullBar(HullBarVisual? bar, float hullPercent, string label)
	{
		if (bar == null)
		{
			return;
		}

		var ratio = Mathf.Clamp(hullPercent / 100.0f, 0.0f, 1.0f);
		bar.Fill.Scale = new Vector3(ratio, 1.0f, 1.0f);
		bar.Fill.Position = new Vector3(
			bar.FillCenterX - (bar.FullWidth * (1.0f - ratio) * 0.5f),
			bar.Fill.Position.Y,
			bar.Fill.Position.Z);
		bar.Label.Text = $"{label}: {hullPercent:0}%";
	}

	private void BuildHud()
	{
		if (_hudRoot == null)
		{
			return;
		}

		ClearChildren(_hudRoot);
		_crewButtonsByName.Clear();

		BuildTopBattlePanel(_hudRoot);
		BuildPlayerHullPanel(_hudRoot);
		BuildCrewPanel(_hudRoot);
		BuildQuickTipsPanel(_hudRoot);
		BuildBottomBattlePanel(_hudRoot);
		BuildOrdersOverlay(_hudRoot);
		BuildPauseOverlay(_hudRoot);
	}

	private void BuildTopBattlePanel(CanvasLayer hudRoot)
	{
		var panel = CreateHudPanel("TopBattlePanel");
		panel.AnchorRight = 1.0f;
		panel.OffsetLeft = 188.0f;
		panel.OffsetTop = 12.0f;
		panel.OffsetRight = -16.0f;
		panel.OffsetBottom = 92.0f;
		hudRoot.AddChild(panel);

		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("separation", 18);
		AddPanelMargin(panel, row, 12, 8);

		_enemyHullLabel = AddHudValue(row, "Enemy Hull: 100");
		_selectedSummaryLabel = AddHudValue(row, "Selected: None");
		_targetLabel = AddHudValue(row, $"Targets: {GetBattleTargetSummaryText()}");
		_statusLabel = AddHudValue(row, $"Status: {_statusText}", expand: true);
	}

	private void BuildPlayerHullPanel(CanvasLayer hudRoot)
	{
		var panel = CreateHudPanel("PlayerHullPanel");
		panel.CustomMinimumSize = new Vector2(230.0f, 46.0f);
		panel.OffsetLeft = 360.0f;
		panel.OffsetTop = 430.0f;
		panel.OffsetRight = 590.0f;
		panel.OffsetBottom = 476.0f;
		hudRoot.AddChild(panel);
		_playerHullPanel = panel;

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 4);
		AddPanelMargin(panel, column, 10, 6);

		_playerHullLabel = CreateHudLabel("Player Hull: 100%", 13, new Color(0.86f, 0.92f, 1.0f));
		_playerHullLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_playerHullUiBar = CreateHudHullBar(PlayerShipStyle.AccentColor);
		_playerHullUiBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		column.AddChild(_playerHullLabel);
		column.AddChild(_playerHullUiBar);
	}

	private void BuildCrewPanel(CanvasLayer hudRoot)
	{
		var panel = CreateHudPanel("CrewPanel");
		panel.OffsetLeft = 16.0f;
		panel.OffsetTop = 108.0f;
		panel.OffsetRight = 176.0f;
		panel.OffsetBottom = 324.0f;
		hudRoot.AddChild(panel);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 6);
		AddPanelMargin(panel, column, 10, 8);

		var title = CreateHudLabel("Crew", 15, new Color(0.98f, 0.9f, 0.62f));
		column.AddChild(title);

		_crewRows = new VBoxContainer();
		_crewRows.AddThemeConstantOverride("separation", 5);
		column.AddChild(_crewRows);
		RebuildCrewRows();
	}

	private void BuildQuickTipsPanel(CanvasLayer hudRoot)
	{
		var panel = CreateHudPanel("QuickTipsPanel");
		panel.OffsetLeft = 16.0f;
		panel.OffsetTop = 336.0f;
		panel.OffsetRight = 306.0f;
		panel.OffsetBottom = 486.0f;
		hudRoot.AddChild(panel);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 5);
		AddPanelMargin(panel, column, 10, 8);

		column.AddChild(CreateHudLabel("Quick Tips", 14, new Color(0.98f, 0.9f, 0.62f)));
		AddQuickTipLabels(column, 12);
	}

	private void BuildBottomBattlePanel(CanvasLayer hudRoot)
	{
		var panel = CreateHudPanel("BottomBattlePanel");
		panel.AnchorRight = 1.0f;
		panel.AnchorTop = 1.0f;
		panel.AnchorBottom = 1.0f;
		panel.OffsetLeft = 188.0f;
		panel.OffsetTop = -152.0f;
		panel.OffsetRight = -16.0f;
		panel.OffsetBottom = -16.0f;
		hudRoot.AddChild(panel);

		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("separation", 24);
		AddPanelMargin(panel, row, 12, 8);

		var stationColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		stationColumn.AddThemeConstantOverride("separation", 4);
		stationColumn.AddChild(CreateHudLabel("Station Status", 15, new Color(0.98f, 0.9f, 0.62f)));
		_stationStatusRows = new VBoxContainer();
		_stationStatusRows.AddThemeConstantOverride("separation", 2);
		stationColumn.AddChild(_stationStatusRows);

		var weaponColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		weaponColumn.AddThemeConstantOverride("separation", 6);
		weaponColumn.AddChild(CreateHudLabel("Weapons", 15, new Color(0.98f, 0.9f, 0.62f)));
		_weaponTargetLabel = CreateHudLabel("Target: None");
		_playerWeaponLabel = CreateHudLabel("Player Cannons: 0.0 / 7.0s");
		_playerWeaponChargeBar = new ProgressBar
		{
			MinValue = 0.0,
			MaxValue = PlayerCannonChargeDurationSeconds,
			Value = 0.0,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0.0f, 12.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_enemyWeaponLabel = CreateHudLabel("Enemy Cannons: inactive");
		_enemyWeaponChargeBar = new ProgressBar
		{
			MinValue = 0.0,
			MaxValue = EnemyCannonChargeDurationSeconds,
			Value = 0.0,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0.0f, 12.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		weaponColumn.AddChild(_weaponTargetLabel);
		weaponColumn.AddChild(_playerWeaponLabel);
		weaponColumn.AddChild(_playerWeaponChargeBar);
		weaponColumn.AddChild(_enemyWeaponLabel);
		weaponColumn.AddChild(_enemyWeaponChargeBar);

		row.AddChild(stationColumn);
		row.AddChild(weaponColumn);
		RebuildStationStatusRows();
	}

	private void BuildOrdersOverlay(CanvasLayer hudRoot)
	{
		var overlay = new Control
		{
			Name = "OrdersOverlay",
			Visible = false,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical = Control.GrowDirection.Both,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		hudRoot.AddChild(overlay);
		_ordersOverlay = overlay;

		var background = new ColorRect
		{
			Name = "Background",
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical = Control.GrowDirection.Both,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Color = new Color(0.028f, 0.042f, 0.06f, 0.96f)
		};
		overlay.AddChild(background);

		var margin = new MarginContainer
		{
			Name = "MarginContainer",
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -310.0f,
			OffsetTop = -245.0f,
			OffsetRight = 310.0f,
			OffsetBottom = 245.0f,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical = Control.GrowDirection.Both
		};
		margin.AddThemeConstantOverride("margin_left", 24);
		margin.AddThemeConstantOverride("margin_top", 24);
		margin.AddThemeConstantOverride("margin_right", 24);
		margin.AddThemeConstantOverride("margin_bottom", 24);
		overlay.AddChild(margin);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 20);
		margin.AddChild(column);

		var title = new Label
		{
			Text = "Orders, Cap'n?",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 42);
		title.AddThemeColorOverride("font_color", new Color(1.0f, 0.92f, 0.62f));
		column.AddChild(title);

		var instructionPanel = CreateHudPanel("InstructionPanel");
		column.AddChild(instructionPanel);

		var instructions = new VBoxContainer();
		instructions.AddThemeConstantOverride("separation", 8);
		AddPanelMargin(instructionPanel, instructions, 16, 14);
		instructions.AddChild(CreateHudLabel("You'll be under attack immediately.", 14, new Color(1.0f, 0.82f, 0.62f)));
		instructions.AddChild(CreateHudLabel("1. Left-click friendly red Gunner then right click friendly cannons to assign him.", 13));
		instructions.AddChild(CreateHudLabel("2. Once he's there, left-click cannons and right-click an enemy square to attack it.", 13));
		instructions.AddChild(CreateHudLabel("   (Enemy cannons [red square] could be dece)", 13));
		instructions.AddChild(CreateHudLabel("3. Keep the Captain at the Helm for dodge chance.", 13));
		instructions.AddChild(CreateHudLabel("4. Use Repair in the bottom UI to restore disabled stations. ESC opens Pause.", 13));

		_giveNoQuarterButton = new Button
		{
			Name = "GiveNoQuarterButton",
			Text = "GIVE 'EM NO QUARTER",
			CustomMinimumSize = new Vector2(0.0f, 54.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_giveNoQuarterButton.AddThemeFontSizeOverride("font_size", 18);
		column.AddChild(_giveNoQuarterButton);
	}

	private void BuildPauseOverlay(CanvasLayer hudRoot)
	{
		var overlay = new Control
		{
			Name = "PauseOverlay",
			Visible = false,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical = Control.GrowDirection.Both,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		hudRoot.AddChild(overlay);
		_pauseOverlay = overlay;

		var background = new ColorRect
		{
			Name = "Background",
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical = Control.GrowDirection.Both,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Color = new Color(0.043f, 0.055f, 0.09f, 1.0f)
		};
		overlay.AddChild(background);

		var margin = new MarginContainer
		{
			Name = "MarginContainer",
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -220.0f,
			OffsetTop = -200.0f,
			OffsetRight = 220.0f,
			OffsetBottom = 200.0f,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical = Control.GrowDirection.Both
		};
		margin.AddThemeConstantOverride("margin_left", 24);
		margin.AddThemeConstantOverride("margin_top", 24);
		margin.AddThemeConstantOverride("margin_right", 24);
		margin.AddThemeConstantOverride("margin_bottom", 24);
		overlay.AddChild(margin);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 28);
		margin.AddChild(column);

		var title = new Label
		{
			Text = "Paused",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 42);
		column.AddChild(title);

		var buttonStack = new VBoxContainer
		{
			Name = "ButtonStack"
		};
		buttonStack.AddThemeConstantOverride("separation", 14);
		column.AddChild(buttonStack);

		_volumeSlider = AddVolumeSlider(buttonStack);
		_resumeButton = AddPauseButton(buttonStack, "ResumeButton", "Resume");
		_resumeSailingButton = AddPauseButton(buttonStack, "ResumeSailingButton", "Resume Sailing");
		_restartButton = AddPauseButton(buttonStack, "RestartButton", "Restart");
		_quitGameButton = AddPauseButton(buttonStack, "QuitGameButton", "Quit Game");
	}

	private void UpdateHud()
	{
		if (_playerHullLabel != null)
		{
			_playerHullLabel.Text = $"Player Hull: {_playerHull:0}%";
		}

		if (_enemyHullLabel != null)
		{
			_enemyHullLabel.Text = $"Enemy Hull: {_enemyHull:0}%";
		}

		UpdateHullBars();

		if (_selectedSummaryLabel != null)
		{
			_selectedSummaryLabel.Text = $"Selected: {GetSelectedSummaryText()}";
		}

		if (_targetLabel != null)
		{
			_targetLabel.Text = $"Targets: {GetBattleTargetSummaryText()}";
		}

		if (_statusLabel != null)
		{
			_statusLabel.Text = $"Status: {_statusText}";
		}

		UpdateWeaponHud();

		RebuildCrewRows();
		RebuildStationStatusRows();
	}

	private void UpdateWeaponHud()
	{
		if (_targetLabel != null)
		{
			_targetLabel.Text = $"Targets: {GetBattleTargetSummaryText()}";
		}

		if (_weaponTargetLabel != null)
		{
			_weaponTargetLabel.Text = $"Target: {GetTargetSummaryText()}";
		}

		if (_playerWeaponLabel != null)
		{
			var readiness = CanPlayerCannonsCharge()
				? string.Empty
				: !IsPlayerCannonsOperational()
					? " disabled"
					: IsPlayerCannonsCrewed()
					? " idle"
					: " needs crew";
			_playerWeaponLabel.Text = $"Player Cannons: {_playerCannonChargeSeconds:0.0} / {PlayerCannonChargeDurationSeconds:0.0}s{readiness}";
		}

		if (_playerWeaponChargeBar != null)
		{
			_playerWeaponChargeBar.MaxValue = PlayerCannonChargeDurationSeconds;
			_playerWeaponChargeBar.Value = Mathf.Clamp(_playerCannonChargeSeconds, 0.0f, PlayerCannonChargeDurationSeconds);
		}

		if (_enemyWeaponLabel != null)
		{
			var targetText = _currentEnemyCannonTarget == null ? "None" : $"Player {_currentEnemyCannonTarget.Name}";
			var readiness = CanEnemyCannonsCharge() ? string.Empty : " inactive";
			_enemyWeaponLabel.Text = $"Enemy Cannons: {_enemyCannonChargeSeconds:0.0} / {EnemyCannonChargeDurationSeconds:0.0}s{readiness} -> {targetText}";
		}

		if (_enemyWeaponChargeBar != null)
		{
			_enemyWeaponChargeBar.MaxValue = EnemyCannonChargeDurationSeconds;
			_enemyWeaponChargeBar.Value = Mathf.Clamp(_enemyCannonChargeSeconds, 0.0f, EnemyCannonChargeDurationSeconds);
		}
	}

	private string GetTargetSummaryText()
	{
		if (_currentCannonTarget == null)
		{
			return "None";
		}

		var status = _currentCannonTarget.IsDisabled ? "disabled" : $"{_currentCannonTarget.Durability:0}%";
		return $"Enemy {_currentCannonTarget.Name} {status}";
	}

	private string GetBattleTargetSummaryText()
	{
		var enemyTarget = _currentEnemyCannonTarget == null
			? "None"
			: $"Player {_currentEnemyCannonTarget.Name}";
		return $"Player -> {GetTargetSummaryText()} | Enemy -> {enemyTarget}";
	}

	private string GetSelectedSummaryText()
	{
		if (_selectedCrew != null)
		{
			return _selectedCrew.CrewName;
		}

		if (_clickedStation != null)
		{
			return _clickedStation.StationName;
		}

		return "None";
	}

	private void RebuildCrewRows()
	{
		if (_crewRows == null)
		{
			return;
		}

		ClearChildren(_crewRows);
		_crewButtonsByName.Clear();

		foreach (var definition in CrewDefinitions)
		{
			var crewName = definition.Name;
			var button = new Button
			{
				Name = $"{SanitizeNodeName(crewName)}CrewButton",
				Text = $"{crewName}\n{GetCrewAssignmentText(crewName)}",
				CustomMinimumSize = new Vector2(0.0f, 44.0f),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				Alignment = HorizontalAlignment.Left
			};
			button.AddThemeFontSizeOverride("font_size", 12);
			button.AddThemeStyleboxOverride("normal", CreateCrewButtonStyle(IsCrewSelected(crewName), false));
			button.AddThemeStyleboxOverride("hover", CreateCrewButtonStyle(IsCrewSelected(crewName), true));
			button.AddThemeStyleboxOverride("pressed", CreateCrewButtonStyle(true, true));
			button.Pressed += () => SelectCrewFromUi(crewName);
			_crewRows.AddChild(button);
			_crewButtonsByName[crewName] = button;
		}
	}

	private void RebuildStationStatusRows()
	{
		if (_stationStatusRows == null)
		{
			return;
		}

		ClearChildren(_stationStatusRows);
		foreach (var station in StationDefinitions)
		{
			var state = _playerStationStatesByName.TryGetValue(station.Name, out var stationState)
				? stationState
				: null;
			var durability = state?.Durability ?? 100.0f;
			var status = state?.IsDisabled == true ? "Disabled" : "Operational";
			var repairText = state?.IsRepairing == true ? " - Repairing" : string.Empty;
			_stationDamageFlashSecondsByName.TryGetValue(station.Name, out var flashSeconds);
			var rowPanel = new PanelContainer
			{
				CustomMinimumSize = new Vector2(0.0f, 28.0f),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			rowPanel.AddThemeStyleboxOverride("panel", CreateStationStatusRowStyle(durability, flashSeconds));

			var row = new HBoxContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			row.AddThemeConstantOverride("separation", 6);
			AddPanelMargin(rowPanel, row, 6, 2);

			var stationLabel = CreateHudLabel(
				$"{station.Name}: {durability:0}% {status}{repairText}",
				13,
				GetStationStatusLabelColor(state, flashSeconds));
			stationLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

			var repairButton = new Button
			{
				Text = "Repair",
				CustomMinimumSize = new Vector2(72.0f, 24.0f)
			};
			repairButton.AddThemeFontSizeOverride("font_size", 12);
			var stationName = station.Name;
			repairButton.Pressed += () => StartRepair(stationName);

			row.AddChild(stationLabel);
			row.AddChild(repairButton);
			_stationStatusRows.AddChild(rowPanel);
		}
	}

	private void SelectCrewFromUi(string crewName)
	{
		var crew = FindCrew(crewName);
		if (crew == null)
		{
			return;
		}

		OnCrewClicked(crew);
	}

	private static Color GetStationStatusLabelColor(StationRuntimeState? state, float flashSeconds)
	{
		if (flashSeconds > 0.0f)
		{
			return Colors.White;
		}

		if (state?.IsRepairing == true)
		{
			return new Color(0.52f, 0.95f, 0.68f);
		}

		if (state?.IsDisabled == true)
		{
			return new Color(1.0f, 0.58f, 0.5f);
		}

		if ((state?.Durability ?? 100.0f) < 100.0f)
		{
			return new Color(1.0f, 0.76f, 0.58f);
		}

		return new Color(0.9f, 0.88f, 0.78f);
	}

	private string GetCrewAssignmentText(string crewName)
	{
		return _stationByCrewName.TryGetValue(crewName, out var stationName)
			? $"-> {stationName}"
			: "-> Unassigned";
	}

	private bool IsCrewSelected(string crewName)
	{
		return _selectedCrew?.CrewName == crewName;
	}

	private StationMarker3D? FindStation(string stationName)
	{
		foreach (var station in _stations)
		{
			if (station.StationName == stationName)
			{
				return station;
			}
		}

		return null;
	}

	private CrewToken3D? FindCrew(string crewName)
	{
		foreach (var crew in _crew)
		{
			if (crew.CrewName == crewName)
			{
				return crew;
			}
		}

		return null;
	}

	private static PanelContainer CreateHudPanel(string nodeName)
	{
		var panel = new PanelContainer
		{
			Name = nodeName,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
		return panel;
	}

	private static void AddPanelMargin(PanelContainer panel, Control contents, int horizontalMargin, int verticalMargin)
	{
		var margin = new MarginContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		margin.AddThemeConstantOverride("margin_left", horizontalMargin);
		margin.AddThemeConstantOverride("margin_top", verticalMargin);
		margin.AddThemeConstantOverride("margin_right", horizontalMargin);
		margin.AddThemeConstantOverride("margin_bottom", verticalMargin);
		margin.AddChild(contents);
		panel.AddChild(margin);
	}

	private static Label AddHudValue(Container parent, string text, bool expand = false)
	{
		var label = CreateHudLabel(text);
		if (expand)
		{
			label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		}

		parent.AddChild(label);
		return label;
	}

	private static void AddQuickTipLabels(Container parent, int fontSize)
	{
		parent.AddChild(CreateHudLabel("Left-click Gunner -> right-click Cannons", fontSize, new Color(0.95f, 0.88f, 0.72f)));
		parent.AddChild(CreateHudLabel("Left-click Cannons -> right-click enemy square", fontSize, new Color(0.95f, 0.88f, 0.72f)));
		parent.AddChild(CreateHudLabel("Cap'n at Helm dodges", fontSize, new Color(0.86f, 0.92f, 1.0f)));
		parent.AddChild(CreateHudLabel("Repair is on bottom UI", fontSize, new Color(0.74f, 0.95f, 0.74f)));
		parent.AddChild(CreateHudLabel("ESC - Pause Menu", fontSize, new Color(1.0f, 0.82f, 0.62f)));
	}

	private HSlider AddVolumeSlider(Container parent)
	{
		var row = new HBoxContainer
		{
			Name = "VolumeRow",
			CustomMinimumSize = new Vector2(0.0f, 34.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("separation", 12);

		var label = CreateHudLabel("Volume");
		label.CustomMinimumSize = new Vector2(76.0f, 0.0f);
		row.AddChild(label);

		var slider = new HSlider
		{
			Name = "VolumeSlider",
			MinValue = 0.0,
			MaxValue = 100.0,
			Value = GetMusicVolumePercent(),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		row.AddChild(slider);
		parent.AddChild(row);
		return slider;
	}

	private float GetMusicVolumePercent()
	{
		return (GetNodeOrNull<MusicManager>("/root/MusicManager")?.GetVolume() ?? 1.0f) * 100.0f;
	}

	private static Button AddPauseButton(Container parent, string nodeName, string text)
	{
		var button = new Button
		{
			Name = nodeName,
			Text = text,
			CustomMinimumSize = new Vector2(0.0f, 48.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		parent.AddChild(button);
		return button;
	}

	private static ProgressBar CreateHudHullBar(Color fillColor)
	{
		var bar = new ProgressBar
		{
			MinValue = 0.0,
			MaxValue = 100.0,
			Value = 100.0,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(132.0f, 12.0f)
		};
		bar.AddThemeStyleboxOverride("background", CreateProgressBarStyle(new Color(0.03f, 0.035f, 0.04f, 0.95f)));
		bar.AddThemeStyleboxOverride("fill", CreateProgressBarStyle(fillColor.Lightened(0.16f)));
		return bar;
	}

	private static Label CreateHudLabel(string text, int fontSize = 14, Color? color = null)
	{
		var label = new Label
		{
			Text = text,
			ClipText = true,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeColorOverride("font_color", color ?? new Color(0.9f, 0.88f, 0.78f));
		label.AddThemeFontSizeOverride("font_size", fontSize);
		return label;
	}

	private static StyleBoxFlat CreatePanelStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.035f, 0.05f, 0.055f, 0.9f),
			BorderColor = new Color(0.78f, 0.67f, 0.42f, 0.75f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4
		};
	}

	private static StyleBoxFlat CreateProgressBarStyle(Color color)
	{
		return new StyleBoxFlat
		{
			BgColor = color,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2
		};
	}

	private static StyleBoxFlat CreateStationStatusRowStyle(float durability, float flashSeconds)
	{
		var isDisabled = durability <= 0.0f;
		var isDamaged = durability < 99.9f;
		var bgColor = new Color(0.035f, 0.05f, 0.055f, 0.0f);
		var borderColor = new Color(0.54f, 0.46f, 0.32f, 0.0f);
		var borderWidth = 0;

		if (isDamaged)
		{
			var damageRatio = 1.0f - Mathf.Clamp(durability / 100.0f, 0.0f, 1.0f);
			bgColor = new Color(0.22f, 0.045f, 0.035f, 0.34f + damageRatio * 0.32f);
			borderColor = new Color(0.74f, 0.18f, 0.12f, 0.55f + damageRatio * 0.3f);
			borderWidth = 1;
		}

		if (isDisabled)
		{
			bgColor = new Color(0.36f, 0.025f, 0.025f, 0.82f);
			borderColor = new Color(1.0f, 0.12f, 0.08f, 0.94f);
			borderWidth = 2;
		}

		if (flashSeconds > 0.0f)
		{
			var pulse = 0.55f + (MathF.Sin(flashSeconds * 24.0f) * 0.45f);
			bgColor = bgColor.Lerp(new Color(0.82f, 0.035f, 0.025f, 0.9f), pulse);
			borderColor = new Color(1.0f, 0.12f, 0.08f, 0.98f);
			borderWidth = 2;
		}

		return new StyleBoxFlat
		{
			BgColor = bgColor,
			BorderColor = borderColor,
			BorderWidthLeft = borderWidth,
			BorderWidthTop = borderWidth,
			BorderWidthRight = borderWidth,
			BorderWidthBottom = borderWidth,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3
		};
	}

	private static StyleBoxFlat CreateCrewButtonStyle(bool isSelected, bool isHovered)
	{
		return new StyleBoxFlat
		{
			BgColor = isSelected
				? new Color(0.18f, 0.28f, 0.34f, 0.96f)
				: isHovered
					? new Color(0.11f, 0.16f, 0.17f, 0.88f)
					: new Color(0.055f, 0.075f, 0.078f, 0.74f),
			BorderColor = isSelected
				? new Color(0.98f, 0.86f, 0.36f, 0.95f)
				: new Color(0.54f, 0.46f, 0.32f, 0.52f),
			BorderWidthLeft = isSelected ? 2 : 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4
		};
	}

	private static void CreateShipLabel(Node3D parent, string text, Vector3 position, Color color)
	{
		var label = new Label3D
		{
			Name = $"{text}Label",
			Text = text,
			Position = position,
			FontSize = 32,
			PixelSize = 0.014f,
			Modulate = color.Lightened(0.25f),
			OutlineSize = 8,
			OutlineModulate = new Color(0.02f, 0.018f, 0.014f),
			NoDepthTest = true,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
		};
		parent.AddChild(label);
	}

	private static HullBarVisual CreateHullBar(Node3D parent, string nodeName, string labelText, Vector3 position, Color fillColor)
	{
		const float fullWidth = 2.05f;
		var root = new Node3D
		{
			Name = nodeName,
			Position = position
		};
		parent.AddChild(root);

		var label = new Label3D
		{
			Name = "Label",
			Text = $"{labelText}: 100%",
			Position = new Vector3(0.0f, 0.24f, 0.0f),
			FontSize = 22,
			PixelSize = 0.01f,
			Modulate = new Color(0.95f, 0.92f, 0.78f),
			OutlineSize = 6,
			OutlineModulate = new Color(0.02f, 0.018f, 0.014f),
			NoDepthTest = true,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
		};
		root.AddChild(label);

		var background = new MeshInstance3D
		{
			Name = "Background",
			Mesh = new BoxMesh { Size = new Vector3(fullWidth + 0.12f, 0.08f, 0.14f) },
			MaterialOverride = CreateMaterial(new Color(0.035f, 0.025f, 0.022f))
		};
		root.AddChild(background);

		var fill = new MeshInstance3D
		{
			Name = "Fill",
			Position = new Vector3(0.0f, 0.015f, 0.0f),
			Mesh = new BoxMesh { Size = new Vector3(fullWidth, 0.09f, 0.16f) },
			MaterialOverride = CreateMaterial(fillColor.Lightened(0.18f))
		};
		root.AddChild(fill);

		return new HullBarVisual(root, fill, label, fullWidth, fill.Position.X);
	}

	private static void CreateCrewPlaceholder(Node3D parent, string nodeName, string shortLabel, Vector3 position, Color color)
	{
		var root = new Node3D
		{
			Name = nodeName,
			Position = position
		};
		parent.AddChild(root);

		var baseColor = color.Darkened(0.08f);
		var selectionRing = new MeshInstance3D
		{
			Name = "BaseRing",
			Position = new Vector3(0.0f, 0.02f, 0.0f),
			Mesh = new CylinderMesh
			{
				TopRadius = 0.3f,
				BottomRadius = 0.3f,
				Height = 0.03f,
				RadialSegments = 10
			},
			MaterialOverride = CreateMaterial(new Color(0.34f, 0.08f, 0.065f))
		};
		root.AddChild(selectionRing);

		var body = new MeshInstance3D
		{
			Name = "Body",
			Position = new Vector3(0.0f, 0.28f, 0.0f),
			Mesh = new CylinderMesh
			{
				TopRadius = 0.2f,
				BottomRadius = 0.2f,
				Height = 0.42f,
				RadialSegments = 10
			},
			MaterialOverride = CreateMaterial(baseColor)
		};
		root.AddChild(body);

		var label = new Label3D
		{
			Name = "ShortLabel",
			Text = shortLabel,
			Position = new Vector3(0.0f, 0.94f, 0.0f),
			FontSize = 24,
			PixelSize = 0.011f,
			Modulate = new Color(0.96f, 0.72f, 0.68f),
			OutlineSize = 7,
			OutlineModulate = new Color(0.035f, 0.012f, 0.01f),
			NoDepthTest = true,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
		};
		root.AddChild(label);
	}

	private static void CreateCannon(Node3D parent, string nodeName, Vector3 position, bool pointsPort)
	{
		var rotation = new Vector3(0.0f, 0.0f, Mathf.Pi * 0.5f);
		var barrelOffset = pointsPort ? -0.16f : 0.16f;

		CreateCylinder(
			parent,
			nodeName,
			0.12f,
			0.62f,
			position + new Vector3(barrelOffset, 0.0f, 0.0f),
			new Color(0.075f, 0.075f, 0.075f),
			10,
			rotation);
	}

	private static void CreatePlayerCannonModel(Node3D parent, string nodeName, Vector3 position)
	{
		var root = new Node3D
		{
			Name = nodeName,
			Position = position,
			Rotation = new Vector3(0.0f, Mathf.Pi, 0.0f),
			Scale = Vector3.One * 0.72f
		};
		parent.AddChild(root);

		var cannonScene = ResourceLoader.Load<PackedScene>(PlayerCannonModelPath);
		if (cannonScene?.Instantiate() is Node3D cannonModel)
		{
			cannonModel.Name = "Model";
			root.AddChild(cannonModel);
			return;
		}

		CreateCannon(parent, $"{nodeName}Fallback", position, pointsPort: false);
	}

	private static void CreatePlayerHelmModel(Node3D parent, string nodeName, Vector3 position)
	{
		var root = new Node3D
		{
			Name = nodeName,
			Position = position,
			Rotation = new Vector3(0.0f, Mathf.Pi, 0.0f),
			Scale = Vector3.One * 0.48f
		};
		parent.AddChild(root);

		var helmScene = ResourceLoader.Load<PackedScene>(PlayerHelmModelPath);
		if (helmScene?.Instantiate() is Node3D helmModel)
		{
			helmModel.Name = "Model";
			root.AddChild(helmModel);
			return;
		}

		CreateBox(parent, $"{nodeName}Fallback", new Vector3(0.62f, 0.48f, 0.12f), position, new Color(0.5f, 0.28f, 0.12f));
	}

	private static void CreatePowderBarrel(Node3D parent, string nodeName, Vector3 position)
	{
		var root = new Node3D
		{
			Name = nodeName,
			Position = position
		};
		parent.AddChild(root);

		CreateCylinder(root, "BarrelBody", 0.18f, 0.34f, Vector3.Zero, new Color(0.24f, 0.11f, 0.045f), 10);
		CreateBox(root, "TopBand", new Vector3(0.38f, 0.045f, 0.38f), new Vector3(0.0f, 0.14f, 0.0f), new Color(0.06f, 0.055f, 0.05f));
		CreateBox(root, "BottomBand", new Vector3(0.38f, 0.045f, 0.38f), new Vector3(0.0f, -0.14f, 0.0f), new Color(0.06f, 0.055f, 0.05f));
		CreateBox(root, "PowderMark", new Vector3(0.22f, 0.035f, 0.035f), new Vector3(0.0f, 0.19f, -0.12f), new Color(0.9f, 0.74f, 0.26f));
	}

	private static Node3D CreateStationDamageIndicator()
	{
		var root = new Node3D
		{
			Name = "DamageIndicator",
			Position = new Vector3(0.0f, 0.18f, 0.0f)
		};

		var smokeA = CreateCylinder(root, "SmokePuffA", 0.18f, 0.14f, new Vector3(-0.12f, 0.36f, 0.04f), new Color(0.08f, 0.075f, 0.068f), 12);
		var smokeB = CreateCylinder(root, "SmokePuffB", 0.23f, 0.16f, new Vector3(0.1f, 0.58f, -0.02f), new Color(0.055f, 0.052f, 0.05f), 12);
		var smokeC = CreateCylinder(root, "SmokePuffC", 0.16f, 0.12f, new Vector3(0.0f, 0.82f, 0.11f), new Color(0.12f, 0.112f, 0.1f), 12);
		smokeA.Scale = new Vector3(1.35f, 1.0f, 0.8f);
		smokeB.Scale = new Vector3(0.9f, 1.0f, 1.25f);
		smokeC.Scale = new Vector3(1.15f, 1.0f, 0.95f);

		var sparkColor = new Color(0.95f, 0.12f, 0.06f);
		CreateBox(root, "DamageSlashA", new Vector3(0.64f, 0.055f, 0.055f), new Vector3(0.0f, 0.2f, 0.0f), sparkColor, new Vector3(0.0f, Mathf.Pi * 0.25f, 0.0f));
		CreateBox(root, "DamageSlashB", new Vector3(0.64f, 0.055f, 0.055f), new Vector3(0.0f, 0.2f, 0.0f), sparkColor, new Vector3(0.0f, -Mathf.Pi * 0.25f, 0.0f));

		return root;
	}

	private static MeshInstance3D CreateBox(
		Node3D parent,
		string nodeName,
		Vector3 size,
		Vector3 position,
		Color color,
		Vector3? rotation = null)
	{
		var node = new MeshInstance3D
		{
			Name = nodeName,
			Position = position,
			Rotation = rotation ?? Vector3.Zero,
			Mesh = new BoxMesh { Size = size },
			MaterialOverride = CreateMaterial(color)
		};

		parent.AddChild(node);
		return node;
	}

	private static MeshInstance3D CreateCylinder(
		Node3D parent,
		string nodeName,
		float radius,
		float height,
		Vector3 position,
		Color color,
		int radialSegments,
		Vector3? rotation = null)
	{
		var node = new MeshInstance3D
		{
			Name = nodeName,
			Position = position,
			Rotation = rotation ?? Vector3.Zero,
			Mesh = new CylinderMesh
			{
				TopRadius = radius,
				BottomRadius = radius,
				Height = height,
				RadialSegments = radialSegments
			},
			MaterialOverride = CreateMaterial(color)
		};

		parent.AddChild(node);
		return node;
	}

	private static void CreateBeamBetween(Node3D parent, string nodeName, Vector2 start, Vector2 end, float y, Color railColor)
	{
		var direction = end - start;
		var length = direction.Length();
		var angle = MathF.Atan2(direction.X, direction.Y);
		var midpoint = (start + end) * 0.5f;

		CreateBox(
			parent,
			nodeName,
			new Vector3(0.16f, 0.36f, length),
			new Vector3(midpoint.X, y, midpoint.Y),
			railColor,
			new Vector3(0.0f, angle, 0.0f));
	}

	private static StandardMaterial3D CreateMaterial(Color color)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = 0.78f
		};
	}

	private static Vector3 DegreesToRadians(Vector3 degrees)
	{
		return new Vector3(
			Mathf.DegToRad(degrees.X),
			Mathf.DegToRad(degrees.Y),
			Mathf.DegToRad(degrees.Z));
	}

	private static void ClearChildren(Node parent)
	{
		foreach (var child in parent.GetChildren())
		{
			parent.RemoveChild(child);
			child.QueueFree();
		}
	}

	private static void RemoveNamedChild(Node parent, string childName)
	{
		var child = parent.GetNodeOrNull<Node>(childName);
		if (child == null)
		{
			return;
		}

		parent.RemoveChild(child);
		child.QueueFree();
	}

	private static string SanitizeNodeName(string value)
	{
		return value.Replace(" ", string.Empty).Replace("'", string.Empty);
	}

	private readonly record struct StationDefinition(
		string Name,
		Vector3 Position,
		Vector3 AssignmentOffset,
		Color Color);

	private readonly record struct CrewDefinition(
		string Name,
		string ShortLabel,
		string Role,
		Vector3 HomePosition,
		Color Color,
		string ModelPath);

	private readonly record struct ShipVisualStyle(
		Color HullColor,
		Color DeckColor,
		Color RailColor,
		Color SailColor,
		Color CabinColor,
		Color AccentColor);

	private sealed class StationRuntimeState
	{
		public StationRuntimeState(string name, StationMarker3D marker, bool isEnemy)
		{
			Name = name;
			Marker = marker;
			IsEnemy = isEnemy;
		}

		public string Name { get; }
		public StationMarker3D Marker { get; }
		public bool IsEnemy { get; }
		public float Durability { get; private set; } = 100.0f;
		public bool IsRepairing { get; set; }
		public bool IsDisabled => Durability <= 0.0f;

		public void ApplyDamage(float damage)
		{
			Durability = Mathf.Clamp(Durability - Mathf.Max(0.0f, damage), 0.0f, 100.0f);
		}

		public void Repair(float amount)
		{
			Durability = Mathf.Clamp(Durability + Mathf.Max(0.0f, amount), 0.0f, 100.0f);
		}
	}

	private sealed record HullBarVisual(
		Node3D Root,
		MeshInstance3D Fill,
		Label3D Label,
		float FullWidth,
		float FillCenterX);
}
