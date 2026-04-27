using Godot;
using System.Collections.Generic;
using TidesOfTime.Battle;
using TidesOfTime.Crew;
using TidesOfTime.Data;
using TidesOfTime.Encounters;
using TidesOfTime.Ships;
using TidesOfTime.UI;

namespace TidesOfTime.Prototypes;

public partial class SchematicBattleDebugScene : Control
{
	[Export] public ShipLayoutDef PlayerLayout { get; set; } = null!;
	[Export] public ShipLayoutDef EnemyLayout { get; set; } = null!;

	private static readonly Dictionary<string, CrewDisplayStats> CrewStatsByClass = new()
	{
		["Captain"] = new CrewDisplayStats(10, 2, 4, 2, 3),
		["Gunner"] = new CrewDisplayStats(10, 2, 1, 5, 2),
		["Fighter"] = new CrewDisplayStats(12, 5, 1, 1, 2)
	};

	private BattleState _battleState = null!;
	private SailingEncounterData? _activeEncounterData;
	private SchematicShipGridView _playerShipView = null!;
	private SchematicShipGridView _enemyShipView = null!;
	private Control _background = null!;
	private Label _titleLabel = null!;
	private Label _selectionSourceLabel = null!;
	private Label _selectionRoomLabel = null!;
	private Label _selectionSystemLabel = null!;
	private Label _actionStatusLabel = null!;
	private PanelContainer _crewStatsPopout = null!;
	private VBoxContainer _crewStatsRows = null!;
	private VBoxContainer _playerCrewRows = null!;
	private VBoxContainer _systemStatusRows = null!;
	private Label _weaponNameLabel = null!;
	private ProgressBar _weaponChargeBar = null!;
	private Label _weaponDetailLabel = null!;
	private Button _primaryActionButton = null!;
	private Button _secondaryActionButton = null!;
	private Button _resetButton = null!;
	private Control _ordersOverlay = null!;
	private Button _giveEmHellButton = null!;
	private Control _pauseOverlay = null!;
	private Button _resumeButton = null!;
	private Button _resumeSailingButton = null!;
	private Button _restartButton = null!;
	private Button _quitGameButton = null!;
	private BattleActionKind? _primaryActionKind;
	private BattleActionKind? _secondaryActionKind;
	private bool _isPauseMenuOpen;

	public override void _Ready()
	{
		_activeEncounterData = SailingEncounterStore.ConsumePendingEncounter();
		_playerShipView = GetNode<SchematicShipGridView>("MarginContainer/VBoxContainer/ShipRow/PlayerShipView");
		_enemyShipView = GetNode<SchematicShipGridView>("MarginContainer/VBoxContainer/ShipRow/EnemyShipView");
		_background = GetNode<Control>("Background");
		_titleLabel = GetNode<Label>("MarginContainer/VBoxContainer/StatusPanel/MarginContainer/VBoxContainer/TitleLabel");
		_selectionSourceLabel = GetNode<Label>("MarginContainer/VBoxContainer/StatusPanel/MarginContainer/VBoxContainer/SelectionDetails/SelectionSourceLabel");
		_selectionRoomLabel = GetNode<Label>("MarginContainer/VBoxContainer/StatusPanel/MarginContainer/VBoxContainer/SelectionDetails/SelectionRoomLabel");
		_selectionSystemLabel = GetNode<Label>("MarginContainer/VBoxContainer/StatusPanel/MarginContainer/VBoxContainer/SelectionDetails/SelectionSystemLabel");
		_actionStatusLabel = GetNode<Label>("MarginContainer/VBoxContainer/StatusPanel/MarginContainer/VBoxContainer/ActionStatusLabel");
		_crewStatsPopout = GetNode<PanelContainer>("CrewStatsPopout");
		_crewStatsRows = GetNode<VBoxContainer>("CrewStatsPopout/MarginContainer/StatsRows");
		_playerCrewRows = GetNode<VBoxContainer>("MarginContainer/VBoxContainer/ShipRow/PlayerCrewArea/PlayerCrewPanel/MarginContainer/VBoxContainer/PlayerCrewRows");
		_systemStatusRows = GetNode<VBoxContainer>("MarginContainer/VBoxContainer/CombatPanel/MarginContainer/HBoxContainer/SystemColumn/SystemStatusRows");
		_weaponNameLabel = GetNode<Label>("MarginContainer/VBoxContainer/CombatPanel/MarginContainer/HBoxContainer/WeaponColumn/WeaponStatusRows/WeaponRow/WeaponNameLabel");
		_weaponChargeBar = GetNode<ProgressBar>("MarginContainer/VBoxContainer/CombatPanel/MarginContainer/HBoxContainer/WeaponColumn/WeaponStatusRows/WeaponRow/WeaponChargeBar");
		_weaponDetailLabel = GetNode<Label>("MarginContainer/VBoxContainer/CombatPanel/MarginContainer/HBoxContainer/WeaponColumn/WeaponStatusRows/WeaponDetailLabel");
		_primaryActionButton = GetNode<Button>("MarginContainer/VBoxContainer/StatusPanel/MarginContainer/VBoxContainer/ButtonRow/PrimaryActionButton");
		_secondaryActionButton = GetNode<Button>("MarginContainer/VBoxContainer/StatusPanel/MarginContainer/VBoxContainer/ButtonRow/SecondaryActionButton");
		_resetButton = GetNode<Button>("MarginContainer/VBoxContainer/StatusPanel/MarginContainer/VBoxContainer/ButtonRow/ResetButton");
		_ordersOverlay = GetNode<Control>("OrdersOverlay");
		_giveEmHellButton = GetNode<Button>("OrdersOverlay/MarginContainer/VBoxContainer/ButtonStack/GiveEmHellButton");
		_pauseOverlay = GetNode<Control>("PauseOverlay");
		_resumeButton = GetNode<Button>("PauseOverlay/MarginContainer/VBoxContainer/ButtonStack/ResumeButton");
		_resumeSailingButton = GetNode<Button>("PauseOverlay/MarginContainer/VBoxContainer/ButtonStack/ResumeSailingButton");
		_restartButton = GetNode<Button>("PauseOverlay/MarginContainer/VBoxContainer/ButtonStack/RestartButton");
		_quitGameButton = GetNode<Button>("PauseOverlay/MarginContainer/VBoxContainer/ButtonStack/QuitGameButton");

		if (PlayerLayout == null || EnemyLayout == null)
		{
			GD.PushError("SchematicBattleDebugScene: PlayerLayout and EnemyLayout must be assigned.");
			return;
		}

		_playerShipView.SetShipVisualStyle(new Color(0.24f, 0.48f, 0.78f), true);
		_enemyShipView.SetShipVisualStyle(new Color(0.72f, 0.24f, 0.18f), false);
		_playerShipView.ShowInlineWeaponChargeBars = false;
		_enemyShipView.ShowInlineWeaponChargeBars = false;
		_playerShipView.TileClicked += (ship, x, y, button) => OnTileClicked("Player", ship, x, y, button);
		_playerShipView.BackgroundPressed += OnBoardBackgroundPressed;
		_playerShipView.CrewSelected += (ship, crew) => OnCrewSelected("Player", ship, crew);
		_enemyShipView.TileClicked += (ship, x, y, button) => OnTileClicked("Enemy", ship, x, y, button);
		_enemyShipView.BackgroundPressed += OnBoardBackgroundPressed;
		_enemyShipView.CrewSelected += (ship, crew) => OnCrewSelected("Enemy", ship, crew);
		_background.GuiInput += OnBackgroundGuiInput;
		_primaryActionButton.Pressed += () => RunAction(_primaryActionKind);
		_secondaryActionButton.Pressed += () => RunAction(_secondaryActionKind);
		_resetButton.Pressed += () => ResetBattleState();
		_giveEmHellButton.Pressed += StartBattleFromOrders;
		_resumeButton.Pressed += OnResumePressed;
		_resumeSailingButton.Pressed += OnResumeSailingPressed;
		_restartButton.Pressed += OnRestartPressed;
		_quitGameButton.Pressed += OnQuitGamePressed;

		SetPauseMenuOpen(false);
		ConfigureActionButtons([]);
		_actionStatusLabel.Text = "Awaiting orders.";
		_weaponChargeBar.Value = 0.0;
		_weaponDetailLabel.Text = "Awaiting orders.";
		HideCrewStatsPopout();
		_giveEmHellButton.GrabFocus();
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

		if (_battleState == null)
		{
			return;
		}

		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space })
		{
			var result = _battleState.ToggleTacticalPause();
			ShowSelectionState(_battleState.CurrentSelection);
			_actionStatusLabel.Text = result.StatusText;
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Q })
		{
			var result = _battleState.ToggleSlowTime();
			ShowSelectionState(_battleState.CurrentSelection);
			_actionStatusLabel.Text = result.StatusText;
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.R })
		{
			ResetBattleState();
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta)
	{
		if (_battleState == null || _isPauseMenuOpen)
		{
			return;
		}

		var updateResult = _battleState.Update(delta);
		UpdateCannonChargeBars();
		UpdateTimeControlStatusLabel();

		if (updateResult == null)
		{
			return;
		}

		RenderBattleViews();
		ShowSelectionState(_battleState.CurrentSelection);
		_actionStatusLabel.Text = updateResult.StatusText;
	}

	private void OnTileClicked(string shipSource, ShipState ship, int tileX, int tileY, MouseButton button)
	{
		if (button == MouseButton.Left)
		{
			_battleState.HandleRoomSelectionPressed(shipSource, ship, tileX, tileY);
		}
		else if (button == MouseButton.Right)
		{
			_battleState.HandleCrewMovePressed(shipSource, ship, tileX, tileY);
		}

		RenderBattleViews();
		ShowSelectionState(_battleState.CurrentSelection);
	}

	private void OnCrewSelected(string shipSource, ShipState ship, CrewState crew)
	{
		_battleState.SetCrewSelection(shipSource, ship, crew);
		RenderBattleViews();
		ShowSelectionState(_battleState.CurrentSelection);
	}

	private void OnBoardBackgroundPressed(ShipState _)
	{
		ClearCurrentSelection();
	}

	private void OnBackgroundGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			ClearCurrentSelection();
		}
	}

	private void RunAction(BattleActionKind? actionKind)
	{
		if (actionKind == null)
		{
			_actionStatusLabel.Text = "Select a room to see actions.";
			return;
		}

		var result = _battleState.ExecuteAction(actionKind.Value);
		RenderBattleViews();
		ShowSelectionState(_battleState.CurrentSelection);
		_actionStatusLabel.Text = result.StatusText;
	}

	private void ResetBattleState()
	{
		_battleState = BattleState.Create(PlayerLayout, EnemyLayout);
		RenderBattleViews();
		ShowSelectionState(_battleState.CurrentSelection);
		_actionStatusLabel.Text = string.IsNullOrEmpty(_battleState.OpeningStatusText)
			? "Schematic battle debug scene ready."
			: _battleState.OpeningStatusText;
	}

	private void StartBattleFromOrders()
	{
		_ordersOverlay.Visible = false;
		ResetBattleState();
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
		_ordersOverlay.Visible = false;
		ResetBattleState();
	}

	private void OnQuitGamePressed()
	{
		SetPauseMenuOpen(false);
		GetTree().Quit();
	}

	private void RenderBattleViews()
	{
		var selectedCrewId = _battleState.CurrentSelection?.Kind == BattleSelectionKind.Crew
			? _battleState.CurrentSelection.Crew?.Id
			: null;
		var playerSelectedCrewId = _battleState.CurrentSelection?.Ship == _battleState.PlayerShip
			? selectedCrewId
			: null;
		var enemySelectedCrewId = _battleState.CurrentSelection?.Ship == _battleState.EnemyShip
			? selectedCrewId
			: null;

		_playerShipView.Render(_battleState.PlayerShip, playerSelectedCrewId);
		_enemyShipView.Render(_battleState.EnemyShip, enemySelectedCrewId);
		RebuildPlayerCrewPanel(playerSelectedCrewId);
		UpdateCannonChargeBars();
		RebuildSystemStatusPanel();
	}

	private void ShowSelectionState(BattleSelection? selection)
	{
		RefreshSelectionDetails(selection);
		UpdateActionButtons(selection);
		UpdateTimeControlStatusLabel();
		UpdateCannonChargeBars();
	}

	private void RefreshSelectionDetails(BattleSelection? selection)
	{
		if (selection == null)
		{
			_selectionSourceLabel.Text = "Ship: None";
			_selectionRoomLabel.Text = "Room: None";
			_selectionSystemLabel.Text = "System: None";
			return;
		}

		_selectionSourceLabel.Text = $"Ship: {selection.ShipSource} ({selection.Ship.Name})";

		if (selection.Kind == BattleSelectionKind.Crew && selection.Crew != null)
		{
			var room = selection.Ship.GetRoomForCrew(selection.Crew);
			_selectionRoomLabel.Text = $"Crew: {selection.Crew.DisplayName} [{selection.Crew.ShortLabel}]";
			_selectionSystemLabel.Text =
				$"Role: {selection.Crew.CrewClass} | Allegiance: {selection.Crew.Allegiance}\n" +
				$"Room: {room?.DisplayName ?? "Deck"}\n" +
				$"{_battleState.GetCrewTaskStatusText(selection.Crew)}";
			return;
		}

		_selectionRoomLabel.Text = $"Room: {selection.Room?.DisplayName ?? "None"}";
		_selectionSystemLabel.Text = BuildRoomSelectionSummary(selection.Ship, selection.Room);
	}

	private void UpdateActionButtons(BattleSelection? selection)
	{
		if (_battleState.IsBattleOver)
		{
			ConfigureActionButton(_primaryActionButton, ref _primaryActionKind, "Battle Over", null);
			ConfigureActionButton(_secondaryActionButton, ref _secondaryActionKind, "Action 2", null);
			_actionStatusLabel.Text = _battleState.BattleOverStatusText ?? "Battle is over.";
			return;
		}

		if (_battleState.LastMovementFeedback != null)
		{
			ConfigureActionButtons([]);
			_actionStatusLabel.Text = _battleState.LastMovementFeedback.ToStatusText();
			return;
		}

		if (selection == null || selection.Kind != BattleSelectionKind.Room)
		{
			ConfigureActionButtons([]);
			_battleState.SetLastIssuedIntent(null);
			_actionStatusLabel.Text = selection?.Kind == BattleSelectionKind.Crew
				? "Crew selected. Right-click a walkable player tile to queue movement."
				: "Select a room to see actions.";
			return;
		}

		ConfigureActionButtons(_battleState.GetAvailableActions());
		_actionStatusLabel.Text = selection.Room?.Disabled == true
			? $"{selection.Room.DisplayName} is disabled."
			: $"Ready: {selection.Room?.DisplayName ?? "Room"} on {selection.Ship.Name}";
	}

	private void ConfigureActionButtons(IReadOnlyList<BattleAvailableAction> actions)
	{
		ConfigureActionButton(
			_primaryActionButton,
			ref _primaryActionKind,
			actions.Count > 0 ? actions[0].DisplayLabel : "Action 1",
			actions.Count > 0 ? actions[0].Kind : null);
		ConfigureActionButton(
			_secondaryActionButton,
			ref _secondaryActionKind,
			actions.Count > 1 ? actions[1].DisplayLabel : "Action 2",
			actions.Count > 1 ? actions[1].Kind : null);
	}

	private static void ConfigureActionButton(
		Button button,
		ref BattleActionKind? storedKind,
		string text,
		BattleActionKind? actionKind)
	{
		storedKind = actionKind;
		button.Text = text;
		button.Disabled = actionKind == null;
	}

	private void UpdateCannonChargeBars()
	{
		_playerShipView.SetCannonChargeBar(_battleState.GetPlayerCannonChargeBarState());
		_enemyShipView.SetCannonChargeBar(_battleState.GetEnemyCannonChargeBarState());
		UpdateWeaponStatusPanel();
	}

	private void RebuildSystemStatusPanel()
	{
		ClearChildren(_systemStatusRows);

		foreach (var room in _battleState.PlayerShip.Grid.Rooms)
		{
			AddSystemStatusRow(room);
		}
	}

	private void RebuildPlayerCrewPanel(string? selectedCrewId)
	{
		ClearChildren(_playerCrewRows);

		foreach (var crew in _battleState.PlayerShip.GetCrewOnBoard())
		{
			if (crew.Allegiance != CrewAllegiance.Player)
			{
				continue;
			}

			AddPlayerCrewRow(crew, crew.Id == selectedCrewId);
		}
	}

	private void AddPlayerCrewRow(CrewState crew, bool isSelected)
	{
		var row = new PanelContainer
		{
			CustomMinimumSize = new Vector2(0.0f, 28.0f),
			MouseFilter = MouseFilterEnum.Stop,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		row.AddThemeStyleboxOverride("panel", CreateCrewRowStyle(isSelected, false));
		row.MouseEntered += () => OnPlayerCrewRowHovered(row, crew, isSelected);
		row.MouseExited += () => OnPlayerCrewRowUnhovered(row, isSelected);

		var margin = new MarginContainer
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		margin.AddThemeConstantOverride("margin_left", 4);
		margin.AddThemeConstantOverride("margin_top", 3);
		margin.AddThemeConstantOverride("margin_right", 4);
		margin.AddThemeConstantOverride("margin_bottom", 3);

		var contents = new HBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		contents.AddThemeConstantOverride("separation", 5);

		var marker = new Label
		{
			CustomMinimumSize = new Vector2(20.0f, 20.0f),
			Text = crew.ShortLabel,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		marker.AddThemeStyleboxOverride("normal", CreateCrewMarkerStyle(isSelected));
		marker.AddThemeColorOverride("font_color", Colors.White);
		marker.AddThemeFontSizeOverride("font_size", 11);

		var roleLabel = new Label
		{
			Text = crew.CrewClass,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center,
			ClipText = true,
			TooltipText = crew.DisplayName,
			MouseFilter = MouseFilterEnum.Ignore
		};
		roleLabel.AddThemeColorOverride(
			"font_color",
			isSelected ? new Color(1.0f, 0.94f, 0.62f) : new Color(0.86f, 0.84f, 0.76f));
		roleLabel.AddThemeFontSizeOverride("font_size", 12);

		contents.AddChild(marker);
		contents.AddChild(roleLabel);
		margin.AddChild(contents);
		row.AddChild(margin);
		_playerCrewRows.AddChild(row);
	}

	private void OnPlayerCrewRowHovered(PanelContainer row, CrewState crew, bool isSelected)
	{
		row.AddThemeStyleboxOverride("panel", CreateCrewRowStyle(isSelected, true));
		ShowCrewStatsPopout(crew, row);
	}

	private void OnPlayerCrewRowUnhovered(PanelContainer row, bool isSelected)
	{
		row.AddThemeStyleboxOverride("panel", CreateCrewRowStyle(isSelected, false));
		HideCrewStatsPopout();
	}

	private void ShowCrewStatsPopout(CrewState crew, Control hoveredRow)
	{
		if (!CrewStatsByClass.TryGetValue(crew.CrewClass, out var stats))
		{
			HideCrewStatsPopout();
			return;
		}

		ClearChildren(_crewStatsRows);
		AddCrewStatRow("HP", stats.HitPoints);
		AddCrewStatRow("Fite", stats.Fighting);
		AddCrewStatRow("Sail", stats.PilotingSailing);
		AddCrewStatRow("Gun", stats.Gunning);
		AddCrewStatRow("Fix", stats.Repair);
		PositionCrewStatsPopout(hoveredRow);
		_crewStatsPopout.AddThemeStyleboxOverride("panel", CreateCrewStatsPopoutStyle());
		_crewStatsPopout.Modulate = Colors.White;
	}

	private void PositionCrewStatsPopout(Control hoveredRow)
	{
		var rowRect = hoveredRow.GetGlobalRect();
		var desiredGlobalPosition = new Vector2(rowRect.End.X + 4.0f, rowRect.Position.Y);
		_crewStatsPopout.GlobalPosition = desiredGlobalPosition;
	}

	private void HideCrewStatsPopout()
	{
		_crewStatsPopout.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		ClearChildren(_crewStatsRows);
	}

	private void AddCrewStatRow(string label, int value)
	{
		var row = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(0.0f, 18.0f),
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("separation", 3);

		var labelNode = new Label
		{
			CustomMinimumSize = new Vector2(30.0f, 0.0f),
			Text = label,
			ClipText = true,
			MouseFilter = MouseFilterEnum.Ignore
		};
		labelNode.AddThemeColorOverride("font_color", new Color(0.76f, 0.8f, 0.76f));
		labelNode.AddThemeFontSizeOverride("font_size", 11);

		var valueNode = new Label
		{
			Text = value.ToString(),
			HorizontalAlignment = HorizontalAlignment.Right,
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		valueNode.AddThemeColorOverride("font_color", Colors.White);
		valueNode.AddThemeFontSizeOverride("font_size", 11);

		row.AddChild(labelNode);
		row.AddChild(valueNode);
		_crewStatsRows.AddChild(row);
	}

	private void AddSystemStatusRow(ShipRoomState room)
	{
		var row = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(0.0f, 24.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("separation", 8);

		var nameLabel = new Label
		{
			CustomMinimumSize = new Vector2(170.0f, 0.0f),
			Text = room.DisplayName,
			ClipText = true,
			TooltipText = $"{room.DisplayName} | {room.SystemType}"
		};

		var integrityBar = new ProgressBar
		{
			CustomMinimumSize = new Vector2(128.0f, 10.0f),
			MaxValue = ShipRoomState.MaxIntegrity,
			Value = room.Integrity,
			ShowPercentage = false
		};

		var statusLabel = new Label
		{
			CustomMinimumSize = new Vector2(190.0f, 0.0f),
			Text = $"{room.Integrity}/{ShipRoomState.MaxIntegrity} | {GetRoomStatusText(room)}",
			ClipText = true
		};

		row.AddChild(nameLabel);
		row.AddChild(integrityBar);
		row.AddChild(statusLabel);
		_systemStatusRows.AddChild(row);
	}

	private void UpdateWeaponStatusPanel()
	{
		var weaponStatus = _battleState.GetPlayerCannonChargeStatus();
		if (!weaponStatus.IsVisible)
		{
			_weaponNameLabel.Text = $"{weaponStatus.WeaponName}: unavailable";
			_weaponChargeBar.Value = 0.0;
			_weaponDetailLabel.Text = "No player cannon system is available.";
			return;
		}

		_weaponNameLabel.Text =
			$"{weaponStatus.WeaponName}: {weaponStatus.ChargeSeconds:0.0}/{weaponStatus.ChargeDurationSeconds:0.0}s";
		_weaponChargeBar.MaxValue = weaponStatus.ChargeDurationSeconds;
		_weaponChargeBar.Value = Mathf.Clamp(
			(float)weaponStatus.ChargeSeconds,
			0.0f,
			(float)weaponStatus.ChargeDurationSeconds);
		_weaponDetailLabel.Text = BuildWeaponDetailText(weaponStatus);
	}

	private static string BuildWeaponDetailText(BattleWeaponChargeStatus weaponStatus)
	{
		if (!weaponStatus.HasTarget)
		{
			return "No target selected.";
		}

		return weaponStatus.IsActive
			? $"Charging shot on {weaponStatus.TargetName}."
			: $"Targeting {weaponStatus.TargetName}; awaiting operational, manned cannons.";
	}

	private static StyleBoxFlat CreateCrewRowStyle(bool isSelected, bool isHovered)
	{
		return new StyleBoxFlat
		{
			BgColor = isHovered
				? (isSelected
					? new Color(0.28f, 0.38f, 0.43f, 0.96f)
					: new Color(0.1f, 0.16f, 0.17f, 0.76f))
				: isSelected
				? new Color(0.24f, 0.32f, 0.38f, 0.92f)
				: new Color(0.05f, 0.08f, 0.085f, 0.42f),
			BorderColor = isHovered
				? new Color(1.0f, 0.96f, 0.62f, 1.0f)
				: isSelected
				? new Color(1.0f, 0.86f, 0.42f, 0.94f)
				: new Color(0.56f, 0.48f, 0.34f, 0.52f),
			BorderWidthLeft = isHovered ? 3 : isSelected ? 2 : 1,
			BorderWidthTop = isSelected || isHovered ? 2 : 1,
			BorderWidthRight = isSelected || isHovered ? 2 : 1,
			BorderWidthBottom = isSelected || isHovered ? 2 : 1,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4
		};
	}

	private static StyleBoxFlat CreateCrewMarkerStyle(bool isSelected)
	{
		return new StyleBoxFlat
		{
			BgColor = isSelected
				? new Color(0.22f, 0.45f, 0.78f, 1.0f)
				: new Color(0.18f, 0.34f, 0.58f, 1.0f),
			BorderColor = new Color(0.96f, 0.88f, 0.66f, 0.95f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusBottomLeft = 10
		};
	}

	private static StyleBoxFlat CreateCrewStatsPopoutStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.04f, 0.065f, 0.07f, 0.92f),
			BorderColor = new Color(1.0f, 0.96f, 0.62f, 0.94f),
			BorderWidthLeft = 2,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4
		};
	}

	private static void ClearChildren(Node parent)
	{
		foreach (var child in parent.GetChildren())
		{
			parent.RemoveChild(child);
			child.QueueFree();
		}
	}

	private void UpdateTimeControlStatusLabel()
	{
		_titleLabel.Text = $"Schematic Battle Debug | {_battleState.GetTimeControlStatus().DisplayText}";
	}

	private string BuildRoomSelectionSummary(ShipState ship, ShipRoomState? room)
	{
		if (room == null)
		{
			return "System: None";
		}

		var allegiance = ship == _battleState.PlayerShip
			? CrewAllegiance.Player
			: CrewAllegiance.Enemy;

		return
			$"System: {room.SystemType} ({GetManningText(ship, room, allegiance)})\n" +
			$"Integrity: {room.Integrity}/{ShipRoomState.MaxIntegrity} | Status: {GetRoomStatusText(room)}\n" +
			$"Occupants: {FormatCrewList(ship.GetCrewInRoom(room))}";
	}

	private static string GetManningText(ShipState ship, ShipRoomState room, CrewAllegiance allegiance)
	{
		if (!ship.IsRoomOperational(room))
		{
			return "Offline";
		}

		return ship.IsRoomManned(room, allegiance) ? "Manned" : "Unmanned";
	}

	private static string GetRoomStatusText(ShipRoomState room)
	{
		if (room.Disabled)
		{
			return "Disabled";
		}

		return room.IsDamaged ? "Damaged" : "Operational";
	}

	private static string FormatCrewList(IReadOnlyList<CrewState> crewMembers)
	{
		if (crewMembers.Count == 0)
		{
			return "None";
		}

		var labels = new List<string>();
		foreach (var crew in crewMembers)
		{
			labels.Add($"{crew.DisplayName} [{crew.ShortLabel}]");
		}

		return string.Join(", ", labels);
	}

	private void ClearCurrentSelection()
	{
		_battleState.ClearSelection();
		RenderBattleViews();
		ShowSelectionState(_battleState.CurrentSelection);
	}

	private void ReturnToSailing()
	{
		var returnScenePath = _activeEncounterData?.ReturnScenePath;
		if (string.IsNullOrWhiteSpace(returnScenePath))
		{
			_actionStatusLabel.Text = "No sailing scene return path is available.";
			return;
		}

		var sceneChangeError = GetTree().ChangeSceneToFile(returnScenePath);
		if (sceneChangeError != Error.Ok)
		{
			_actionStatusLabel.Text = $"Could not return to sailing: {sceneChangeError}.";
		}
	}

	private bool CanReturnToSailing()
	{
		return !string.IsNullOrWhiteSpace(_activeEncounterData?.ReturnScenePath);
	}

	private void SetPauseMenuOpen(bool isOpen)
	{
		_isPauseMenuOpen = isOpen;
		_pauseOverlay.Visible = isOpen;
		_resumeSailingButton.Visible = CanReturnToSailing();

		if (isOpen)
		{
			_resumeButton.GrabFocus();
		}
	}

	private sealed record CrewDisplayStats(
		int HitPoints,
		int Fighting,
		int PilotingSailing,
		int Gunning,
		int Repair);
}
