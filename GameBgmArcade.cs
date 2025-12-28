using Godot;
using System;

public partial class GameBgmArcade : AudioStreamPlayer
{
	[Export] public int Bpm = 128;               // arcade/hyper casual
	[Export] public float MasterGain = 0.13f;    // tổng âm lượng
	[Export] public float Intensity = 0.0f;      // 0..1 (tăng dần theo score/combo)
	[Export] public int Seed = 777;

	private AudioStreamGeneratorPlayback _pb;
	private int _sr;
	private double _t;
	private Random _rng;

	// phases
	private double _bassPh, _leadPh, _arpPh;
	private float _leadLP;

	// progression: i - VI - III - VII (minor feel -> kịch tính hơn)
	// in semitones relative to root (A minor vibe if root=0 mapped to A)
	private readonly int[] _prog = { 0, 8, 3, 10 }; // Am, F, C, G-ish (shifted)

	public override void _Ready()
	{
		_rng = new Random(Seed);
		var gen = Stream as AudioStreamGenerator;
		_sr = (int)(gen?.MixRate ?? 44100);
		_pb = null; // lazy init
	}

	public void SetIntensity(float v) => Intensity = Mathf.Clamp(v, 0f, 1f);

	public override void _Process(double delta)
	{
		if (!Playing) return;

		_pb ??= (AudioStreamGeneratorPlayback)GetStreamPlayback();
		if (_pb == null) return;

		int frames = _pb.GetFramesAvailable();
		for (int i = 0; i < frames; i++)
			_pb.PushFrame(NextFrame());
	}

	private Vector2 NextFrame()
	{
		double beat = 60.0 / Bpm;
		double bar = 4.0 * beat;
		double eighth = 0.5 * beat;
		double sixteenth = 0.25 * beat;

		// current musical time
		double lt = _t;
		int barIndex = (int)(lt / bar);
		double tInBar = lt - barIndex * bar;

		int beatIndex = (int)(tInBar / beat);          // 0..3
		double tInBeat = tInBar - beatIndex * beat;

		int sixIndex = (int)(tInBar / sixteenth);      // 0..15
		double tInSix = tInBar - sixIndex * sixteenth;

		int eighthIndex = (int)(tInBar / eighth);      // 0..7
		double tInEighth = tInBar - eighthIndex * eighth;

		// chord root of this bar (minor-ish)
		int chord = _prog[barIndex % _prog.Length];

		// ---- DRUMS (arcade) ----
		float kick = Kick(tInBeat, beatIndex);
		float snare = Snare(tInBeat, beatIndex);
		float hat = Hat(tInEighth, eighthIndex);
		float perc = (Intensity > 0.55f) ? Perc(tInSix, sixIndex) : 0f;

		float drums = kick + snare + hat + perc;

		// ---- MUSIC ----
		float bass = Bass(chord, tInBeat, beatIndex);
		float arp = Arp(chord, tInSix, sixIndex);
		float lead = Lead(chord, tInSix, sixIndex);

		// Sidechain duck from kick
		float duck = Mathf.Clamp(1f - kick * 1.05f, 0.50f, 1f);
		bass *= duck;
		arp *= Mathf.Lerp(1f, duck, 0.45f);
		lead *= Mathf.Lerp(1f, duck, 0.25f);

		// A little noise bed (very low)
		float noise = (float)((_rng.NextDouble() * 2.0 - 1.0) * 0.0018);

		float mono = (drums + bass + arp + lead + noise) * MasterGain;

		// widen a bit
		float left = mono + arp * 0.02f + lead * 0.02f;
		float right = mono - arp * 0.02f - lead * 0.02f;

		left = SoftClip(left);
		right = SoftClip(right);

		_t += 1.0 / _sr;
		return new Vector2(left, right);
	}

	// ----------------- DRUMS -----------------

	private float Kick(double tInBeat, int beatIndex)
	{
		// 4-on-floor 느낌 (kick mỗi beat 0 và 2, intense thì thêm beat 1)
		bool on = (beatIndex == 0 || beatIndex == 2) || (Intensity > 0.75f && beatIndex == 1);
		if (!on) return 0f;

		double dur = 0.10;
		if (tInBeat > dur) return 0f;

		// pitch drop nhanh -> "punchy"
		double f0 = 140.0;
		double f1 = 55.0;
		double k = tInBeat / dur;
		double f = f0 + (f1 - f0) * k;

		double s = Math.Sin(Math.PI * 2.0 * f * tInBeat);
		float env = (float)Math.Exp(-tInBeat * 34.0);
		return (float)s * env * 0.95f;
	}

	private float Snare(double tInBeat, int beatIndex)
	{
		// snare on 2 & 4
		if (beatIndex != 1 && beatIndex != 3) return 0f;

		double dur = 0.12;
		if (tInBeat > dur) return 0f;

		float env = (float)Math.Exp(-tInBeat * 18.0);
		float n = (float)(_rng.NextDouble() * 2.0 - 1.0);

		// tonal body + noise
		float body = (float)Math.Sin(Math.PI * 2.0 * 220.0 * tInBeat) * 0.12f;
		float snap = n * 0.65f;

		return (snap + body) * env * 0.55f;
	}

	private float Hat(double tInEighth, int eighthIndex)
	{
		// 8th hats, intense thì dày hơn (accent offbeat)
		double dur = 0.028;
		if (tInEighth > dur) return 0f;

		float env = (float)Math.Exp(-tInEighth * 140.0);
		float n = (float)(_rng.NextDouble() * 2.0 - 1.0);

		float baseLevel = (eighthIndex % 2 == 0) ? 0.11f : 0.09f;
		float level = Mathf.Lerp(baseLevel, baseLevel * 1.6f, Intensity);

		return n * env * level;
	}

	private float Perc(double tInSix, int sixIndex)
	{
		// tiny click perc ở 16th (tạo cảm giác “nhịp chạy”)
		if (sixIndex % 4 != 2) return 0f;

		double dur = 0.020;
		if (tInSix > dur) return 0f;

		float env = (float)Math.Exp(-tInSix * 220.0);
		float n = (float)(_rng.NextDouble() * 2.0 - 1.0);
		return n * env * 0.07f;
	}

	// ----------------- MUSIC -----------------

	private float Bass(int chord, double tInBeat, int beatIndex)
	{
		// Bass pulse/square: root + fifth luân phiên -> driving
		int semitone = chord + ((beatIndex % 2 == 0) ? 0 : 7);

		double f = MidiToHz(33 + semitone); // low

		_bassPh += Math.PI * 2.0 * f / _sr;

		// pulse wave (square mềm)
		double s = Math.Sin(_bassPh);
		double pulse = Math.Tanh(s * 2.8);

		float env = (float)Math.Exp(-tInBeat * 3.0);
		return (float)pulse * env * Mathf.Lerp(0.42f, 0.60f, Intensity);
	}

	private float Arp(int chord, double tInSix, int sixIndex)
	{
		// Arp 16th: chord tones -> rất “kích thích”
		// pattern: 0, 7, 12, 7, 3, 10... (biến hoá)
		int[] chordTones = { 0, 3, 7, 10 }; // minor7 feel
		int pick = chordTones[sixIndex % chordTones.Length];

		// octave bounce
		int oct = (sixIndex % 8 < 4) ? 12 : 0;

		double f = MidiToHz(72 + chord + pick + oct);

		// envelope pluck rất nhanh
		double dur = 0.08;
		if (tInSix > dur) return 0f;

		_arpPh += Math.PI * 2.0 * f / _sr;

		float env = (float)Math.Exp(-tInSix * 55.0);
		float s1 = (float)Math.Sin(_arpPh);
		float s2 = (float)Math.Sin(_arpPh * 2.0) * 0.12f;

		float amp = Mathf.Lerp(0.14f, 0.24f, Intensity);
		return (s1 + s2) * env * amp;
	}

	private float Lead(int chord, double tInSix, int sixIndex)
	{
		// Lead chỉ xuất hiện khi intense hơn (để “đẩy”)
		if (Intensity < 0.30f) return 0f;

		// đánh thưa hơn arp: mỗi 2 steps
		if (sixIndex % 2 != 0) return 0f;

		// scale minor pentatonic-ish
		int[] scale = { 0, 3, 5, 7, 10 };
		int note = scale[(sixIndex / 2) % scale.Length];

		double f = MidiToHz(79 + chord + note);

		double dur = 0.11;
		if (tInSix > dur) return 0f;

		_leadPh += Math.PI * 2.0 * f / _sr;

		// saw-ish bằng cách cộng harmonic nhẹ
		float env = (float)Math.Exp(-tInSix * 28.0f);
		float s1 = (float)Math.Sin(_leadPh);
		float s2 = (float)Math.Sin(_leadPh * 2.0) * 0.22f;
		float s3 = (float)Math.Sin(_leadPh * 3.0) * 0.10f;

		float raw = (s1 * 0.75f + s2 + s3) * env * Mathf.Lerp(0.08f, 0.18f, Intensity);

		// lowpass nhẹ để đỡ chói
		float cutoff = Mathf.Lerp(0.22f, 0.14f, Intensity);
		_leadLP = _leadLP + cutoff * (raw - _leadLP);

		return _leadLP;
	}

	public void RestartBgm()
	{
		Stop();
		Play();
		_pb = null;   // bắt buộc: để lấy playback mới
	}

	// ----------------- helpers -----------------

	private static double MidiToHz(int midi)
		=> 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);

	private static float SoftClip(float x)
		=> (float)Math.Tanh(x * 1.7);
}
