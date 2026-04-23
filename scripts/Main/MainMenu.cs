using Godot;

namespace TidesOfTime.Main;

public partial class MainMenu : Control
{
	private const string GameRootScenePath = "res://scenes/main/game_root.tscn";

	public override void _Ready()
	{
		var startButton = GetNode<Button>("MarginContainer/VBoxContainer/ButtonStack/StartGameButton");
		var quitButton = GetNode<Button>("MarginContainer/VBoxContainer/ButtonStack/QuitGameButton");

		startButton.Pressed += OnStartGamePressed;
		quitButton.Pressed += OnQuitGamePressed;
	}

	private void OnStartGamePressed()
	{
		GetTree().ChangeSceneToFile(GameRootScenePath);
	}

	private void OnQuitGamePressed()
	{
		GetTree().Quit();
	}
}
