using Godot;
using System;

public partial class ClickSfx : AudioStreamPlayer
{
	[Export] public float Volume = 0.6f;      // độ to click
	[Export] public float Pitch = 900f;       // tần số click
	[Export] public float Duration = 0.055f;  // 55ms

	private AudioStreamGeneratorPlayback _pb;
	private int _sr;

	public override void _Ready()
	{
		var gen = Stream as AudioStreamGenerator;
		_sr = (int)(gen?.MixRate ?? 44100);

		// KHÔNG gọi GetStreamPlayback() ở đây nữa
		_pb = null;
	}

	public void PlayClick()
	{
		// đảm bảo player active
		if (!Playing)
			Play();

		// lấy playback sau khi Play()
		_pb ??= (AudioStreamGeneratorPlayback)GetStreamPlayback();
		if (_pb == null) return;

		int samples = Mathf.Max(1, (int)(_sr * Duration));

		// push frames cho click ngắn
		for (int i = 0; i < samples; i++)
		{
			float t = (float)i / _sr;

			float env = Mathf.Exp(-t * 70f);
			float tone = Mathf.Sin(Mathf.Tau * Pitch * t);
			float noise = (float)(GD.Randf() * 2f - 1f);

			float s = (tone * 0.75f + noise * 0.25f) * env * Volume;

			_pb.PushFrame(new Vector2(s, s));
		}
	}
}
