using Godot;

namespace TidesOfTime.Sailing;

public partial class SailingSandbox : Node3D
{
	[Export] public NodePath PlayerBoatPath { get; set; } = new("PlayerBoat");
	[Export] public NodePath HudLabelPath { get; set; } = new("HUD/PanelContainer/MarginContainer/InfoLabel");

	private PlayerBoatController? _playerBoat;
	private Label? _hudLabel;

	public override void _Ready()
	{
		_playerBoat = GetNodeOrNull<PlayerBoatController>(PlayerBoatPath);
		_hudLabel = GetNodeOrNull<Label>(HudLabelPath);
		UpdateHud();
	}

	public override void _Process(double delta)
	{
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
		}
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

		_hudLabel.Text = "Sailing Sandbox\n"
			+ "W / Up: accelerate\n"
			+ "S / Down: brake or reverse\n"
			+ "A/D or Left/Right: turn\n"
			+ "Space: hard brake\n"
			+ "R: reset boat\n"
			+ speedText;
	}
}
