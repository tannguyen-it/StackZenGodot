using Godot;

public partial class MainController : Node2D
{
	private CanvasLayer _startScreen;
	private CanvasLayer _hud;
	private StackGame _game;

	private Control _titleWrap;
	private Control _tapWrap;
	private ColorRect _inputCatcher;
	private ClickSfx _clickSfx;
	private AudioStreamPlayer _menuBgm; // cái LoFiBgm đang chạy
	private GameBgmArcade _gameBgm;
	private AudioStreamPlayer _gameBgmPlayer; // nhạc gameplay (arcade)
	private Tween _musicTween;

	private bool _starting = false;
	private float _t = 0f;

	[Export] public float PreviewMoveRange = 140f;     // biên chạy qua lại
	[Export] public float PreviewMoveSpeed = 2.2f;     // tốc độ sin
	[Export] public float TransitionSeconds = 2.0f;    // đúng yêu cầu 2s

	// Vị trí menu (game nằm thấp) và vị trí gameplay (game bay lên giữa)
	[Export] public Vector2 MenuGameOffset = new Vector2(0, -220);     // so với (screen center X, screen bottom)
	[Export] public Vector2 PlayGameOffset = new Vector2(0, -260);     // so với (screen center X, screen center Y)

	
	private Node _ads;

	private void AdsCall(string methodName)
	{
		// Only for mobile exports
		if (!OS.HasFeature("android") && !OS.HasFeature("ios"))
			return;

		_ads ??= GetNodeOrNull<Node>("/root/Ads");
		_ads?.Call(methodName);
	}
public override void _Ready()
	{
		_startScreen = GetNode<CanvasLayer>("StartScreen");
		_hud = GetNode<CanvasLayer>("HUD");
		_game = GetNode<StackGame>("Game");

		_titleWrap = GetNode<Control>("StartScreen/UIRoot/TitleWrap");
		_tapWrap = GetNode<Control>("StartScreen/UIRoot/TapWrap");
		_inputCatcher = GetNode<ColorRect>("StartScreen/UIRoot/InputCatcher");
		_clickSfx = GetNode<ClickSfx>("SfxPlayer");
		_menuBgm = GetNode<AudioStreamPlayer>("BgmPlayer");
		_gameBgm = GetNode<GameBgmArcade>("GameBgmPlayer");
		_gameBgmPlayer = GetNode<AudioStreamPlayer>("GameBgmPlayer");

		// connect signal từ StackGame
		var game = GetNode<Node>("Game"); // node Game gắn script StackGame
		game.Connect("GameOverHappened", new Callable(this, nameof(OnGameOverMusic)));
		game.Connect("RestartRequested", new Callable(this, nameof(OnRestartRequestedMusic)));

		// Start app: show StartScreen, hide HUD
		_startScreen.Visible = true;
		_hud.Visible = false;


		AdsCall("on_start_screen");
		// Dựng đúng 2 block menu preview ngay trong StackGame
		_game.ShowMenuPreview();
		_game.StopGame();                 // menu: không chạy _Process của game
		_game.ExternalOffsetY = 320f;     // bạn chỉnh 280~380 tuỳ đẹp
										  // Đặt "Game" xuống dưới để giống StartScreen (2 block ở gần đáy)
		LayoutGameForMenu();
		GetViewport().SizeChanged += OnViewportSizeChanged;

		// Tap anywhere
		_inputCatcher.MouseFilter = Control.MouseFilterEnum.Stop;
		_inputCatcher.GuiInput += OnStartScreenGuiInput;

		SetProcess(true);
	}

	private void OnViewportSizeChanged()
	{
		if (_starting) return;

		// Nếu đang ở start screen thì giữ layout menu
		if (_startScreen.Visible)
			LayoutGameForMenu();
	}

	private void LayoutGameForMenu()
	{
		var s = GetViewportRect().Size;
		// Canh theo screen: X giữa, Y gần đáy
		_game.Position = new Vector2(s.X / 2f + MenuGameOffset.X, s.Y + MenuGameOffset.Y);
	}

	private Vector2 GetPlayGamePosition()
	{
		var s = GetViewportRect().Size;
		// Canh theo screen: X giữa, Y gần giữa (tuỳ bạn chỉnh offset)
		return new Vector2(s.X / 2f + PlayGameOffset.X, s.Y / 2f + PlayGameOffset.Y);
	}

	public override void _Process(double delta)
	{
		if (_starting || !_startScreen.Visible) return;

		// Cho moving block chạy qua lại ngay trong StackGame (menu preview)
		var moving = _game.MenuCurrentBlock;
		var baseBlock = _game.MenuBaseBlock;
		if (moving == null || baseBlock == null) return;

		_t += (float)delta;
		float x = Mathf.Sin(_t * PreviewMoveSpeed) * PreviewMoveRange;

		// baseBlock.Position.Y là BaseY trong StackGame (thường = 0 hoặc giá trị bạn đặt)
		moving.Position = new Vector2(x, baseBlock.Position.Y - baseBlock.Height);
	}

	private void OnStartScreenGuiInput(InputEvent e)
	{
		if (_starting) return;

		if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			BeginStartTransition();
		else if (e is InputEventScreenTouch st && st.Pressed)
			BeginStartTransition();
	}

	private void BeginStartTransition()
	{
		AdsCall("on_gameplay_start");

		// start game bgm
		if (!_gameBgm.Playing) _gameBgm.Play();
		_gameBgm.VolumeDb = -60;

		_clickSfx?.PlayClick();

		if (_musicTween != null && _musicTween.IsRunning())
			_musicTween.Kill();

		_musicTween = CreateTween();

		if (!_gameBgmPlayer.Playing) _gameBgmPlayer.Play();

		_musicTween.TweenProperty(_menuBgm, "volume_db", -60f, 0.9f);
		_musicTween.TweenProperty(_gameBgmPlayer, "volume_db", -10f, 0.9f);


		_starting = true;

		// Không cho click lần 2
		_inputCatcher.MouseFilter = Control.MouseFilterEnum.Ignore;

		// Text bay lên + mờ dần
		var titleEnd = _titleWrap.Position + new Vector2(0, -80f);
		var tapEnd = _tapWrap.Position + new Vector2(0, -120f);

		// Game (2 block) bay lên giữa để vào gameplay
		var gameEnd = GetPlayGamePosition();

		Tween tw = CreateTween();
		tw.SetParallel(true);

		//// crossfade 1s
		//tw.TweenProperty(_menuBgm, "volume_db", -35, 1.0f);   // menu nhỏ lại (hoặc -60 để tắt hẳn)
		//tw.TweenProperty(_gameBgm, "volume_db", -10, 1.0f);   // game to lên
		// TitleWrap
		tw.TweenProperty(_titleWrap, "position", titleEnd, TransitionSeconds)
		  .SetTrans(Tween.TransitionType.Cubic)
		  .SetEase(Tween.EaseType.Out);
		tw.TweenProperty(_titleWrap, "modulate:a", 0f, TransitionSeconds);

		// TapWrap
		tw.TweenProperty(_tapWrap, "position", tapEnd, TransitionSeconds)
		  .SetTrans(Tween.TransitionType.Cubic)
		  .SetEase(Tween.EaseType.Out);
		tw.TweenProperty(_tapWrap, "modulate:a", 0f, TransitionSeconds);

		// Game node (2 block)
		tw.TweenProperty(_game, "position", gameEnd, TransitionSeconds)
		  .SetTrans(Tween.TransitionType.Cubic)
		  .SetEase(Tween.EaseType.Out);

		tw.TweenProperty(_game, "ExternalOffsetY", 0f, TransitionSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);

		tw.Finished += () =>
		{
			_startScreen.Visible = false;
			_inputCatcher.MouseFilter = Control.MouseFilterEnum.Ignore; // chắc chắn không chặn nữa

			_hud.Visible = true;

			// Bắt đầu chơi luôn từ 2 block đang có (không restart/spawn lại)
			_game.StartFromMenuBlocks();

			// Stop preview loop
			SetProcess(false);
		};
	}

	private void OnGameOverMusic()
	{
		AdsCall("on_game_over");

		// kill tween cũ nếu có
		if (_musicTween != null && _musicTween.IsRunning())
			_musicTween.Kill();

		_musicTween = CreateTween();
		_musicTween.SetParallel(true);

		// đảm bảo menu đang play (để fade lên)
		if (_menuBgm != null && !_menuBgm.Playing)
			_menuBgm.Play();

		// Fade: gameplay xuống, menu lên
		// Bạn chỉnh các mức db theo ý: 
		// menu nghe vừa: -12 đến -8; gameplay tắt: -60
		_musicTween.TweenProperty(_gameBgmPlayer, "volume_db", -60f, 0.9f)
				   .SetTrans(Tween.TransitionType.Sine)
				   .SetEase(Tween.EaseType.InOut);

		_musicTween.TweenProperty(_menuBgm, "volume_db", -10f, 0.9f)
				   .SetTrans(Tween.TransitionType.Sine)
				   .SetEase(Tween.EaseType.InOut);

		// Khi fade xong thì stop game bgm luôn cho nhẹ
		_musicTween.TweenCallback(Callable.From(() =>
		{
			if (_gameBgmPlayer != null)
				_gameBgmPlayer.Stop();
		}));
	}

	private void OnRestartRequestedMusic()
	{
		AdsCall("on_gameplay_start");

		FadeToGameMusic(); // hàm fade sang nhạc gameplay bạn đã có
	}

	private void FadeToGameMusic(float time = 0.6f)
	{
		// đảm bảo reference đúng
		if (_menuBgm == null || _gameBgmPlayer == null)
		{
			return;
		}

		// Kill tween cũ (quan trọng: tween phải luôn dùng chung biến _musicTween)
		if (_musicTween != null && _musicTween.IsRunning())
			_musicTween.Kill();

		// (tuỳ chọn) menu phải đang chạy để fade xuống (nếu bạn stop menu ở đâu đó)
		if (!_menuBgm.Playing) _menuBgm.Play();

		var gameBgmScript = _gameBgmPlayer as GameBgmArcade; // hoặc GetNode<GameBgmArcade>("GameBgmPlayer")
		if (gameBgmScript != null)
			gameBgmScript.RestartBgm();
		else
		{
			_gameBgmPlayer.Stop();
			_gameBgmPlayer.Play();
		}

		// đảm bảo game bgm đang chạy và xuất phát từ -60
		if (!_gameBgmPlayer.Playing) _gameBgmPlayer.Play();
		_gameBgmPlayer.VolumeDb = -60f;



		// TWEEN SONG SONG (parallel) để fade mượt
		_musicTween = CreateTween();
		_musicTween.SetParallel(true);

		_musicTween.TweenProperty(_menuBgm, "volume_db", -60f, time)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);

		_musicTween.TweenProperty(_gameBgmPlayer, "volume_db", -10f, time)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
	}
}
