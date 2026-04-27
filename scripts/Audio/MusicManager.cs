using Godot;

namespace TidesOfTime.Audio;

public partial class MusicManager : Node
{
	private const string SailingCombatMusicPath = "res://audio/music/sailing_combat_theme.mp3";
	private const float DefaultVolumeDb = -10.0f;

	private readonly AudioStreamPlayer _musicPlayer = new();
	private string? _currentTrackPath;

	public override void _Ready()
	{
		_musicPlayer.Name = "MusicPlayer";
		_musicPlayer.VolumeDb = DefaultVolumeDb;
		AddChild(_musicPlayer);
	}

	public void PlaySailingCombatMusic()
	{
		PlayMusic(SailingCombatMusicPath);
	}

	public void StopMusic()
	{
		_musicPlayer.Stop();
		_currentTrackPath = null;
	}

	private void PlayMusic(string trackPath)
	{
		if (_currentTrackPath == trackPath && _musicPlayer.Playing)
		{
			return;
		}

		if (!ResourceLoader.Exists(trackPath))
		{
			GD.PushWarning($"MusicManager: Music file not found: {trackPath}");
			return;
		}

		var stream = ResourceLoader.Load<AudioStream>(trackPath);
		if (stream == null)
		{
			GD.PushWarning($"MusicManager: Could not load music file: {trackPath}");
			return;
		}

		if (stream is AudioStreamMP3 mp3Stream)
		{
			mp3Stream.Loop = true;
		}

		_musicPlayer.Stream = stream;
		_musicPlayer.Play();
		_currentTrackPath = trackPath;
	}
}
