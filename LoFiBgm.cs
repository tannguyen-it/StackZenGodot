using Godot;
using System;

public partial class LoFiBgm : AudioStreamPlayer
{
	// ---- Tuning ----
	[Export] public int Bpm = 92;                 // upbeat casual (88-100)
	[Export] public float MasterGain = 0.17f;     // tổng âm lượng
	[Export] public float VinylNoise = 0.006f;    // noise nền
	[Export] public float Swing = 0.56f;          // 0.5 thẳng, 0.55-0.60 swing nhẹ
	[Export] public int Seed = 1337;              // đổi để đổi vibe

	private AudioStreamGeneratorPlayback _pb;
	private int _sr;
	private double _t;
	private double _loopLen;

	private Random _rng;

	// oscillator phases
	private double _bassPhase;
	private double _padPhase1;
	private double _padPhase2;
	private double _leadPhase;

	// lowpass state
	private float _padLP;

	// ---- Progression (casual/pop): I - V - vi - IV ----
	// in semitones from base key root
	private readonly int[] _padChords = { 0, 7, 9, 5 };        // C, G, A, F
	private readonly int[] _bassNotes = { 0, 0, 7, 7, 9, 9, 5, 5 };

	private const int Bars = 4;
	private const int BeatsPerBar = 4;

	public override void _Ready()
	{
		_rng = new Random(Seed);

		var gen = Stream as AudioStreamGenerator;
		if (gen == null)
		{
			GD.PrintErr("LoFiBgm: Stream must be AudioStreamGenerator.");
			return;
		}

		_sr = (int)gen.MixRate;
		_pb = (AudioStreamGeneratorPlayback)GetStreamPlayback();

		double beatLen = 60.0 / Bpm;
		_loopLen = Bars * BeatsPerBar * beatLen;

		if (!Playing) Play();
	}

	public override void _Process(double delta)
	{
		if (_pb == null) return;

		int frames = _pb.GetFramesAvailable();
		for (int i = 0; i < frames; i++)
		{
			_pb.PushFrame(NextFrame());
		}
	}

	private Vector2 NextFrame()
	{
		double beatLen = 60.0 / Bpm;
		double barLen = BeatsPerBar * beatLen;
		double eighthLen = beatLen * 0.5;

		double lt = _t % _loopLen;

		int bar = (int)(lt / barLen);
		double tInBar = lt - bar * barLen;

		int beat = (int)(tInBar / beatLen);          // 0..3
		double tInBeat = tInBar - beat * beatLen;

		// Swinged 8th position (simple swing feel)
		double rawEighthPos = tInBar / eighthLen;
		int eighth = (int)rawEighthPos;              // 0..7
		double frac = rawEighthPos - eighth;

		// push offbeats later
		double swingFrac = frac;
		if (eighth % 2 == 1)
		{
			// move toward end of the 8th a bit (Swing-0.5 => 0..0.1)
			double push = Math.Clamp(Swing - 0.5, 0.0, 0.15);
			swingFrac = Math.Min(1.0, frac + push);
		}

		double swungTimeInBar = (eighth + swingFrac) * eighthLen;
		double tInEighth = swungTimeInBar - eighth * eighthLen;

		// ---- DRUMS ----
		float kick = Kick(tInBeat, beat, bar);
		float clap = Clap(tInBeat, beat);
		float hat = HiHat(tInEighth, eighth);
		float openHat = 0f;
		if (eighth == 3 || eighth == 7) openHat = OpenHat(tInEighth);

		float drums = kick + clap + hat + openHat;

		// ---- BASS / PAD / LEAD ----
		float bass = Bass(bar, beat, tInBeat);
		float pad = Pad(bar, tInBar, beatLen);
		float lead = Pluck(bar, tInBar, beatLen);

		// ---- Sidechain (duck by kick) ----
		// kick is already envelope-shaped, use it to duck music slightly
		float duck = Mathf.Clamp(1f - kick * 0.9f, 0.60f, 1f);
		bass *= duck;
		pad *= duck;
		lead *= Mathf.Lerp(1f, duck, 0.35f);

		// ---- VINYL ----
		float noise = (float)((_rng.NextDouble() * 2.0 - 1.0) * VinylNoise);

		float mono = (drums + bass + pad + lead + noise) * MasterGain;

		// Stereo widen a touch (keep subtle)
		float left = mono + pad * 0.05f + lead * 0.02f;
		float right = mono - pad * 0.05f - lead * 0.02f;

		left = SoftClip(left);
		right = SoftClip(right);

		_t += 1.0 / _sr;
		return new Vector2(left, right);
	}

	// --------- DRUMS ----------

	private float Kick(double tInBeat, int beat, int bar)
	{
		// kick on 1 and 3 (beat 0 & 2), with tiny variation
		bool on = (beat == 0 || beat == 2);
		if (bar % 2 == 1 && beat == 2) on = true;

		if (!on) return 0f;

		double dur = 0.12;
		if (tInBeat > dur) return 0f;

		// pitch drop
		double f0 = 100.0;
		double f1 = 48.0;
		double k = tInBeat / dur;
		double f = f0 + (f1 - f0) * k;

		double s = Math.Sin(2.0 * Math.PI * f * tInBeat);
		float env = (float)Math.Exp(-tInBeat * 28.0);

		return (float)s * env * 0.95f;
	}

	private float Clap(double tInBeat, int beat)
	{
		// clap on 2 and 4 (beat 1 & 3)
		if (beat != 1 && beat != 3) return 0f;

		double dur = 0.14;
		if (tInBeat > dur) return 0f;

		float env = (float)Math.Exp(-tInBeat * 20.0);
		float n = (float)(_rng.NextDouble() * 2.0 - 1.0);

		// bursty clap
		float burst = 0f;
		burst += (tInBeat < 0.02) ? 1.0f : 0f;
		burst += (tInBeat > 0.03 && tInBeat < 0.05) ? 0.8f : 0f;
		burst += (tInBeat > 0.06 && tInBeat < 0.08) ? 0.6f : 0f;

		// tiny tonal body for presence
		float body = (float)Math.Sin(2.0 * Math.PI * 210.0 * tInBeat) * 0.12f;

		return (n * burst * 0.55f + body) * env * 0.55f;
	}

	private float HiHat(double tInEighth, int eighth)
	{
		// 8th hats, softer on offbeats
		double dur = 0.03;
		if (tInEighth > dur) return 0f;

		float env = (float)Math.Exp(-tInEighth * 120.0);
		float n = (float)(_rng.NextDouble() * 2.0 - 1.0);

		float level = (eighth % 2 == 0) ? 0.16f : 0.11f;
		return n * env * level;
	}

	private float OpenHat(double t)
	{
		double dur = 0.10;
		if (t > dur) return 0f;

		float env = (float)Math.Exp(-t * 28.0);
		float n = (float)(_rng.NextDouble() * 2.0 - 1.0);

		return n * env * 0.07f;
	}

	// --------- BASS / PAD / LEAD ----------

	private float Bass(int bar, int beat, double tInBeat)
	{
		// change every 2 beats => 8 steps across 4 bars
		int step = (bar * BeatsPerBar + beat) / 2;
		step = Math.Clamp(step, 0, _bassNotes.Length - 1);

		int semitone = _bassNotes[step];
		double freq = MidiToHz(36 + semitone); // C2-ish

		float env = (float)Math.Exp(-tInBeat * 3.2);

		_bassPhase += 2.0 * Math.PI * freq / _sr;

		double s = Math.Sin(_bassPhase);
		double tri = 2.0 / Math.PI * Math.Asin(Math.Sin(_bassPhase));

		return (float)(s * 0.55 + tri * 0.35) * env * 0.55f;
	}

	private float Pad(int bar, double tInBar, double beatLen)
	{
		int chordRoot = _padChords[bar % _padChords.Length];

		// keep it warm and simple (minor-ish flavor on vi)
		// chord: root, 3rd, 5th (use major-ish overall but soften)
		double f1 = MidiToHz(60 + chordRoot);        // C4 range
		double f2 = MidiToHz(60 + chordRoot + 4);    // major 3rd
		double f3 = MidiToHz(60 + chordRoot + 7);    // fifth

		// for vi chord (A), make it minor: 3rd -> +3
		if ((bar % _padChords.Length) == 2)
			f2 = MidiToHz(60 + chordRoot + 3);

		_padPhase1 += 2.0 * Math.PI * f1 / _sr;
		_padPhase2 += 2.0 * Math.PI * f2 / _sr;

		double p3 = 2.0 * Math.PI * f3 * _t;

		float s = (float)(
			Math.Sin(_padPhase1) * 0.45 +
			Math.Sin(_padPhase2) * 0.35 +
			Math.Sin(p3) * 0.25
		);

		// tiny tremolo
		float trem = 0.92f + 0.08f * (float)Math.Sin(2.0 * Math.PI * (1.0 / 6.0) * _t);

		// one-pole lowpass for lofi smoothness
		float cutoff = 0.09f;
		_padLP = _padLP + cutoff * (s - _padLP);

		// fade edges inside bar to avoid clicks
		double barPos = tInBar / (BeatsPerBar * beatLen);
		float edge = SmoothStep01((float)barPos) * SmoothStep01(1f - (float)barPos);

		return _padLP * trem * edge * 0.33f;
	}

	private float Pluck(int bar, double tInBar, double beatLen)
	{
		// 16th-note pluck pattern (simple, upbeat)
		double sixteenthLen = beatLen * 0.25;
		int step = (int)(tInBar / sixteenthLen);
		double t = tInBar - step * sixteenthLen;

		// trigger pattern: accents
		bool trig = (step % 4 == 0) || (step == 6) || (step == 10) || (step == 14);
		if (!trig) return 0f;

		int chordRoot = _padChords[bar % _padChords.Length];

		// pentatonic-ish
		int[] scale = { 0, 2, 4, 7, 9 };
		int pick = scale[(step + bar) % scale.Length] + chordRoot;

		double freq = MidiToHz(72 + pick); // C5-ish

		_leadPhase += 2.0 * Math.PI * freq / _sr;

		// fast pluck envelope
		float env = (float)Math.Exp(-t * 65.0f);

		// sine + tiny harmonic
		float s = (float)Math.Sin(_leadPhase);
		float h = (float)Math.Sin(_leadPhase * 2.0) * 0.15f;

		return (s + h) * env * 0.18f;
	}

	// --------- Helpers ----------

	private static double MidiToHz(int midi)
		=> 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);

	private static float SoftClip(float x)
		=> (float)Math.Tanh(x * 1.4);

	private static float SmoothStep01(float x)
	{
		x = Mathf.Clamp(x, 0f, 1f);
		return x * x * (3f - 2f * x);
	}
}
