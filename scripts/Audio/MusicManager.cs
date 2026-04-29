using Godot;

namespace TidesOfTime.Audio;

public partial class MusicManager : Node
{
	private const string SailingCombatMusicPath = "res://audio/music/thoughtful.mp3";
	private const float DefaultVolumeDb = -16.0f;
	private const float MutedVolumeDb = -80.0f;

	private readonly AudioStreamPlayer _musicPlayer = new();
	private string? _currentTrackPath;
	private float _volume = DbToLinearVolume(DefaultVolumeDb);

	public override void _Ready()
	{
		_musicPlayer.Name = "MusicPlayer";
		ApplyVolume();
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

	public float GetVolume()
	{
		return _volume;
	}

	public void SetVolume(float value)
	{
		_volume = Mathf.Clamp(value, 0.0f, 1.0f);
		ApplyVolume();
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
		ApplyVolume();
		_musicPlayer.Play();
		_currentTrackPath = trackPath;
	}

	private void ApplyVolume()
	{
		_musicPlayer.VolumeDb = _volume <= 0.001f
			? MutedVolumeDb
			: Mathf.LinearToDb(_volume);
	}

	private static float DbToLinearVolume(float db)
	{
		return Mathf.Clamp(Mathf.DbToLinear(db), 0.0f, 1.0f);
	}
}
