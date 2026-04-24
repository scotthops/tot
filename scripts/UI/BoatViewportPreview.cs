using Godot;

namespace TidesOfTime.UI;

public partial class BoatViewportPreview : SubViewportContainer
{
	[Export] public PackedScene? BoatVisualScene { get; set; }
	[Export] public Vector2I ViewportSize { get; set; } = new(512, 512);
	[Export] public float CameraHeight { get; set; } = 9.0f;
	[Export] public float OrthographicSize { get; set; } = 6.2f;
	[Export] public Vector3 BoatOffset { get; set; } = Vector3.Zero;
	[Export] public Vector3 BoatRotationDegrees { get; set; } = Vector3.Zero;
	[Export] public Vector3 BoatScale { get; set; } = Vector3.One;

	private SubViewport? _viewport;

	public override void _Ready()
	{
		MouseFilter = Control.MouseFilterEnum.Ignore;
		Stretch = true;

		if (BoatVisualScene == null)
		{
			GD.PushError("BoatViewportPreview: BoatVisualScene is not assigned.");
			return;
		}

		_viewport = new SubViewport
		{
			Name = "BoatPreviewViewport",
			Size = ViewportSize,
			TransparentBg = true,
			OwnWorld3D = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always
		};
		AddChild(_viewport);

		var previewRoot = new Node3D { Name = "PreviewRoot" };
		_viewport.AddChild(previewRoot);

		var boatVisual = BoatVisualScene.Instantiate<Node3D>();
		boatVisual.Name = "BoatVisual";
		boatVisual.Position = BoatOffset;
		boatVisual.RotationDegrees = BoatRotationDegrees;
		boatVisual.Scale = BoatScale;
		previewRoot.AddChild(boatVisual);

		var light = new DirectionalLight3D
		{
			Name = "PreviewLight",
			RotationDegrees = new Vector3(-58.0f, -32.0f, 0.0f),
			LightEnergy = 1.8f
		};
		previewRoot.AddChild(light);

		var camera = new Camera3D
		{
			Name = "PreviewCamera",
			Projection = Camera3D.ProjectionType.Orthogonal,
			Size = OrthographicSize,
			Position = new Vector3(0.0f, CameraHeight, 0.0f),
			RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
			Current = true
		};
		previewRoot.AddChild(camera);
	}

	public override void _Notification(int what)
	{
		if (what != NotificationResized || _viewport == null)
		{
			return;
		}

		_viewport.Size = new Vector2I(
			Mathf.Max(1, Mathf.RoundToInt(Size.X)),
			Mathf.Max(1, Mathf.RoundToInt(Size.Y)));
	}
}
