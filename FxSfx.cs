using Godot;
using System;

public partial class FxSfx : AudioStreamPlayer
{
	[Export] public float Volume = 0.35f;     // âm lượng FX
	[Export] public float BasePitch = 880f;   // pitch cơ bản (A5)
	[Export] public float ComboStep = 70f;    // combo tăng pitch
	[Export] public float Duration = 0.11f;   // độ dài tiếng

	private AudioStreamGeneratorPlayback _pb;
	private int _sr;

	public override void _Ready()
	{
		var gen = Stream as AudioStreamGenerator;
		_sr = (int)(gen?.MixRate ?? 44100);
	}

	/// <summary>Âm "Perfect!" kiểu sparkle, pitch tăng theo combo</summary>
	public void PlayPerfect(int combo)
	{
		EnsureActive();

		// combo càng cao pitch càng lên + hơi "sáng" hơn
		float p = BasePitch + Mathf.Clamp(combo, 0, 12) * ComboStep;

		// 2 nốt nhanh (chime) + chút noise cho "sparkle"
		PushChime(p, 0.045f, 0.00f);
		PushChime(p * 1.25f, 0.050f, 0.03f);
	}

	/// <summary>Âm "Combo tick" nhẹ (tuỳ chọn, gọi khi combo>=2)</summary>
	public void PlayComboTick(int combo)
	{
		EnsureActive();

		float p = 720f + Mathf.Clamp(combo, 0, 12) * 55f;
		PushChime(p, 0.05f, 0.00f, noiseAmount: 0.10f);
	}

	public void PlayGameOver()
	{
		// Reset để tiếng luôn rõ, không bị chồng âm cũ
		Stop();
		Play();
		_pb = (AudioStreamGeneratorPlayback)GetStreamPlayback();

		// 7 nốt rơi kiểu modern (minor-ish, nghe "fail" rõ)
		// MIDI: E5, D5, B4, G4, E4, D4, C4
		int[] midi = { 76, 74, 71, 67, 64, 62, 60 };

		PushArpDownModern(midi,
			noteDur: 0.12f,     // độ dài mỗi nốt
			gap: 0.015f,        // khoảng cách nốt
			slideRatio: 0.22f,  // bend xuống mạnh hơn -> đã hơn
			detuneCents: 10f    // làm tiếng dày hơn
		);

		// Sub-drop + whoosh + thud chốt
		PushSubDrop(180f, 55f, 0.26f, 0.65f);      // tụt trầm rõ ràng
		PushNoiseSweepDown(0.22f, 0.22f);          // sweep nhẹ cho cảm giác "rơi"
		PushThudDeep(0.18f, 0.55f);                // cú chốt
	}

	private void PushFall(float fStart, float fEnd, float dur, float noiseAmount)
	{
		if (_pb == null) return;

		int samples = Mathf.Max(1, (int)(_sr * dur));
		for (int i = 0; i < samples; i++)
		{
			float t = (float)i / _sr;
			float k = (float)i / samples;

			// linear pitch slide down
			float f = Mathf.Lerp(fStart, fEnd, k);

			// envelope
			float env = Mathf.Exp(-t * 10f);

			float s1 = Mathf.Sin(Mathf.Tau * f * t);
			float s2 = Mathf.Sin(Mathf.Tau * f * 0.5f * t) * 0.25f; // sub

			float n = (GD.Randf() * 2f - 1f) * noiseAmount;

			float sample = (s1 * 0.85f + s2 + n) * env * Volume;

			sample = Mathf.Tanh(sample * 1.4f);
			_pb.PushFrame(new Vector2(sample, sample));
		}
	}

	private void PushThud(float dur, float level)
	{
		if (_pb == null) return;

		int samples = Mathf.Max(1, (int)(_sr * dur));
		for (int i = 0; i < samples; i++)
		{
			float t = (float)i / _sr;

			// thud = low sine + noise, decay rất nhanh
			float env = Mathf.Exp(-t * 55f);
			float s = Mathf.Sin(Mathf.Tau * 90f * t); // low

			float n = (GD.Randf() * 2f - 1f) * 0.20f;

			float sample = (s * 0.9f + n) * env * (Volume * level);

			sample = Mathf.Tanh(sample * 1.6f);
			_pb.PushFrame(new Vector2(sample, sample));
		}
	}

	// ----- internals -----

	private void EnsureActive()
	{
		if (!Playing) Play();
		_pb ??= (AudioStreamGeneratorPlayback)GetStreamPlayback();
	}

	private void PushChime(float pitch, float dur, float delay, float noiseAmount = 0.18f)
	{
		if (_pb == null) return;

		int delaySamples = Mathf.Max(0, (int)(_sr * delay));
		int samples = Mathf.Max(1, (int)(_sr * dur));

		// đẩy silence để tạo delay (cho dễ nghe "2 tiếng")
		for (int i = 0; i < delaySamples; i++)
			_pb.PushFrame(Vector2.Zero);

		// chime: sine + harmonic nhẹ + pitch glide nhẹ
		for (int i = 0; i < samples; i++)
		{
			float t = (float)i / _sr;

			// envelope nhanh: attack rất nhanh, decay mềm
			float env = Mathf.Exp(-t * 18f);

			// pitch glide nhẹ cho "đã"
			float f = pitch * (1f + 0.08f * Mathf.Exp(-t * 35f));

			float s1 = Mathf.Sin(Mathf.Tau * f * t);
			float s2 = Mathf.Sin(Mathf.Tau * f * 2f * t) * 0.18f; // harmonic

			float n = (GD.Randf() * 2f - 1f) * noiseAmount;

			float sample = (s1 * 0.85f + s2 + n) * env * Volume;

			// soft clip nhẹ
			sample = Mathf.Tanh(sample * 1.5f);

			_pb.PushFrame(new Vector2(sample, sample));
		}
	}

	private void PushChimeDown(float fStart, float fEnd, float dur, float delay, float noiseAmount)
	{
		if (_pb == null) return;

		int delaySamples = Mathf.Max(0, (int)(_sr * delay));
		for (int i = 0; i < delaySamples; i++)
			_pb.PushFrame(Vector2.Zero);

		int samples = Mathf.Max(1, (int)(_sr * dur));
		for (int i = 0; i < samples; i++)
		{
			float t = (float)i / _sr;
			float k = (float)i / samples;

			// slide down + chút cong để nghe “fall” rõ
			float f = Mathf.Lerp(fStart, fEnd, k * k);

			// envelope: attack nhanh, decay chậm hơn (dài hơn bản cũ)
			float env = Mathf.Exp(-t * 8.5f);

			float s1 = Mathf.Sin(Mathf.Tau * f * t);
			float s2 = Mathf.Sin(Mathf.Tau * f * 2f * t) * 0.18f;  // harmonic
			float sub = Mathf.Sin(Mathf.Tau * (f * 0.5f) * t) * 0.12f;

			float n = (GD.Randf() * 2f - 1f) * noiseAmount;

			float sample = (s1 * 0.90f + s2 + sub + n) * env * Volume;
			sample = Mathf.Tanh(sample * 1.6f);

			_pb.PushFrame(new Vector2(sample, sample));
		}
	}

	private void PushThudDeep(float delay, float dur, float level)
	{
		if (_pb == null) return;

		int delaySamples = Mathf.Max(0, (int)(_sr * delay));
		for (int i = 0; i < delaySamples; i++)
			_pb.PushFrame(Vector2.Zero);

		int samples = Mathf.Max(1, (int)(_sr * dur));
		for (int i = 0; i < samples; i++)
		{
			float t = (float)i / _sr;

			// thud trầm + decay chậm hơn
			float env = Mathf.Exp(-t * 22f);

			float s = Mathf.Sin(Mathf.Tau * 70f * t); // thấp hơn (70Hz)
			float n = (GD.Randf() * 2f - 1f) * 0.18f;

			float sample = (s * 0.95f + n) * env * (Volume * level);
			sample = Mathf.Tanh(sample * 1.8f);

			_pb.PushFrame(new Vector2(sample, sample));
		}
	}

	private void PushWhoosh(float delay, float dur, float level)
	{
		if (_pb == null) return;

		int delaySamples = Mathf.Max(0, (int)(_sr * delay));
		for (int i = 0; i < delaySamples; i++)
			_pb.PushFrame(Vector2.Zero);

		int samples = Mathf.Max(1, (int)(_sr * dur));
		for (int i = 0; i < samples; i++)
		{
			float t = (float)i / _sr;

			// whoosh = noise lọc đơn giản (đầu mạnh, sau giảm)
			float env = Mathf.Exp(-t * 9.5f);

			float n = (GD.Randf() * 2f - 1f);

			// “lọc” thô bằng cách trộn thêm 1 low sine để noise mềm hơn
			float low = Mathf.Sin(Mathf.Tau * 120f * t) * 0.18f;

			float sample = (n * 0.55f + low) * env * (Volume * level);
			sample = Mathf.Tanh(sample * 1.2f);

			_pb.PushFrame(new Vector2(sample, sample));
		}
	}
	private void PushArpDown5(float[] notesHz, float noteDur, float gap, float slideRatio, float noiseAmount)
	{
		if (_pb == null) return;

		for (int n = 0; n < notesHz.Length; n++)
		{
			// gap giữa các nốt
			int gapSamples = Mathf.Max(0, (int)(_sr * gap));
			for (int i = 0; i < gapSamples; i++)
				_pb.PushFrame(Vector2.Zero);

			float fStart = notesHz[n];
			float fEnd = fStart * (1f - slideRatio); // slide xuống nhẹ trong mỗi nốt

			PushTone(fStart, fEnd, noteDur, noiseAmount, brightness: 0.22f);
		}
	}
	private void PushTone(float fStart, float fEnd, float dur, float noiseAmount, float brightness)
	{
		if (_pb == null) return;

		int samples = Mathf.Max(1, (int)(_sr * dur));
		for (int i = 0; i < samples; i++)
		{
			float t = (float)i / _sr;
			float k = (float)i / samples;

			// slide cong cho “rơi” rõ hơn
			float f = Mathf.Lerp(fStart, fEnd, k * k);

			// envelope: attack nhanh, decay vừa (rõ)
			float env = Mathf.Exp(-t * 10.5f);

			// fundamental + harmonic cho tiếng arcade rõ
			float s1 = Mathf.Sin(Mathf.Tau * f * t);
			float s2 = Mathf.Sin(Mathf.Tau * f * 2f * t) * brightness;
			float s3 = Mathf.Sin(Mathf.Tau * f * 3f * t) * (brightness * 0.35f);

			float n = (GD.Randf() * 2f - 1f) * noiseAmount;

			float sample = (s1 * 0.95f + s2 + s3 + n) * env * Volume;
			sample = Mathf.Tanh(sample * 1.7f);

			_pb.PushFrame(new Vector2(sample, sample));
		}
	}
	private void PushArpDownModern(int[] midiNotes, float noteDur, float gap, float slideRatio, float detuneCents)
	{
		if (_pb == null) return;

		for (int n = 0; n < midiNotes.Length; n++)
		{
			// gap
			int gapSamples = Mathf.Max(0, (int)(_sr * gap));
			for (int i = 0; i < gapSamples; i++)
				_pb.PushFrame(Vector2.Zero);

			float fStart = (float)MidiToHz(midiNotes[n]);
			float fEnd = fStart * (1f - slideRatio);

			PushModernNote(fStart, fEnd, noteDur, detuneCents, brightness: 0.28f);
		}
	}

	private void PushModernNote(float fStart, float fEnd, float dur, float detuneCents, float brightness)
	{
		if (_pb == null) return;

		int samples = Mathf.Max(1, (int)(_sr * dur));
		float detuneRatio = Mathf.Pow(2f, detuneCents / 1200f); // cents -> ratio

		for (int i = 0; i < samples; i++)
		{
			float t = (float)i / _sr;
			float k = (float)i / samples;

			// pitch glide cong cho cảm giác "rơi" rõ
			float f = Mathf.Lerp(fStart, fEnd, k * k);

			// envelope: attack nhanh, decay vừa (rõ và đã)
			float env = Mathf.Exp(-t * 11.0f);

			// vibrato nhẹ để tiếng sống động hơn
			float vib = 1f + 0.012f * Mathf.Sin(Mathf.Tau * 8f * t);

			float f1 = f * vib;
			float f2 = f1 * detuneRatio;

			// 2 voice detune + harmonic (nghe dày như game hiện nay)
			float s1 = Mathf.Sin(Mathf.Tau * f1 * t);
			float s2 = Mathf.Sin(Mathf.Tau * f2 * t) * 0.85f;

			// harmonic giúp tiếng “arcade modern” rõ hơn
			float h2 = Mathf.Sin(Mathf.Tau * f1 * 2f * t) * brightness;
			float h3 = Mathf.Sin(Mathf.Tau * f1 * 3f * t) * (brightness * 0.35f);

			// noise chút xíu cho sparkle
			float n = (GD.Randf() * 2f - 1f) * 0.05f;

			float sample = (s1 * 0.70f + s2 * 0.55f + h2 + h3 + n) * env * Volume;

			// soft clip để "đã" và không chói
			sample = Mathf.Tanh(sample * 1.9f);

			_pb.PushFrame(new Vector2(sample, sample));
		}
	}

	private void PushSubDrop(float fStart, float fEnd, float dur, float level)
	{
		if (_pb == null) return;

		int samples = Mathf.Max(1, (int)(_sr * dur));
		for (int i = 0; i < samples; i++)
		{
			float t = (float)i / _sr;
			float k = (float)i / samples;

			float f = Mathf.Lerp(fStart, fEnd, k * k); // rơi cong
			float env = Mathf.Exp(-t * 9.0f);

			float s = Mathf.Sin(Mathf.Tau * f * t);
			float sample = s * env * (Volume * level);

			sample = Mathf.Tanh(sample * 2.2f);
			_pb.PushFrame(new Vector2(sample, sample));
		}
	}

	private void PushNoiseSweepDown(float dur, float level)
	{
		if (_pb == null) return;

		int samples = Mathf.Max(1, (int)(_sr * dur));
		float lp = 0f;

		for (int i = 0; i < samples; i++)
		{
			float t = (float)i / _sr;
			float k = (float)i / samples;

			float env = Mathf.Exp(-t * 7.5f);

			// noise
			float n = (GD.Randf() * 2f - 1f);

			// lowpass đơn giản: đầu sáng hơn, cuối tối hơn
			float cutoff = Mathf.Lerp(0.18f, 0.03f, k);
			lp = lp + cutoff * (n - lp);

			float sample = lp * env * (Volume * level);
			sample = Mathf.Tanh(sample * 1.3f);

			_pb.PushFrame(new Vector2(sample, sample));
		}
	}

	private void PushThudDeep(float dur, float level)
	{
		if (_pb == null) return;

		int samples = Mathf.Max(1, (int)(_sr * dur));
		for (int i = 0; i < samples; i++)
		{
			float t = (float)i / _sr;

			float env = Mathf.Exp(-t * 16f);
			float s = Mathf.Sin(Mathf.Tau * 58f * t);     // rất trầm
			float n = (GD.Randf() * 2f - 1f) * 0.12f;

			float sample = (s * 0.95f + n) * env * (Volume * level);
			sample = Mathf.Tanh(sample * 2.0f);

			_pb.PushFrame(new Vector2(sample, sample));
		}
	}

	private static double MidiToHz(int midi)
		=> 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);

}
