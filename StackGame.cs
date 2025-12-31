using Godot;
using System.Collections.Generic;

public partial class StackGame : Node2D
{
	[Export] public PackedScene BlockScene { get; set; }

	[Export] public float BlockHeight { get; set; } = 40f;
	[Export] public float StartWidth { get; set; } = 300f;
	[Export] public float MinWidth { get; set; } = 5f;
	[Export] public float MinNextWidth { get; set; } = 0f;
	[Export] public float MoveRange { get; set; } = 160f;
	[Export] public float BaseSpeed { get; set; } = 260f;
	[Export] public float PerfectWindow { get; set; } = 2f;
	[Signal]
	public delegate void RestartRequestedEventHandler();
	[Signal]
	public delegate void GameOverHappenedEventHandler();
	[Export] public float BaseY { get; set; } = 0f;

	private Top10Gallery _top10;
	private CanvasLayer _hud;

	private const float Gravity = 900f;
	private const float FadeSpeed = 1.1f;
	private const float DespawnY = 1400f;
	private ClickSfx _clickSfx;
	private FxSfx _fxSfx;

	public BlockNode MenuBaseBlock => _placed.Count > 0 ? _placed[0] : null;
	public BlockNode MenuCurrentBlock => _current;

	private readonly List<BlockNode> _placed = new();
	private BlockNode _current;
	private int _dir = 1;
	private float _speed;
	private bool _isGameOver;

	private int _score;
	private int _best;
	private int _combo;

	private Label _scoreLabel;
	private Label _bestLabel;
	private Label _comboLabel;
	private Label _perfectLabel;
	private Button _restartButton;
	private Button _topButton;
	private Control _titleWrap;

	private class FallingPiece
	{
		public BlockNode Node;
		public Vector2 Velocity;
		public float RotationSpeed;
		public float Opacity;
	}

	private readonly List<FallingPiece> _fallingPieces = new();

	private static readonly Color[] Palette =
	{
		Color.FromHtml("#F97373"),
		Color.FromHtml("#F9A873"),
		Color.FromHtml("#F9D973"),
		Color.FromHtml("#6EE7B7"),
		Color.FromHtml("#60A5FA"),
		Color.FromHtml("#A78BFA"),
		Color.FromHtml("#F472B6"),
	};

	private Vector2 _viewSize;
	private float _cameraOffsetY;
	private float _externalOffsetY = 0f;
	private ColorRect _gameOverOverlay;

	public override void _Ready()
	{
		_scoreLabel = GetNode<Label>("../HUD/TopCenterBox/ScoreBestRow/ScoreLabel");
		_bestLabel = GetNode<Label>("../HUD/TopCenterBox/ScoreBestRow/BestLabel");
		_restartButton = GetNode<Button>("../HUD/RestartButton");
		_topButton = GetNode<Button>("../HUD/Top10Button");
		_comboLabel = GetNode<Label>("../HUD/TopCenterBox/ComboLabel");
		_perfectLabel = GetNode<Label>("../HUD/TopCenterBox/PerfectLabel");
		_gameOverOverlay = GetNode<ColorRect>("../HUD/GameOverOverlay");
		_clickSfx = GetNode<ClickSfx>("../SfxPlayer");
		_fxSfx = GetNode<FxSfx>("../FxPlayer");
		_hud = GetNode<CanvasLayer>("../HUD");
		_top10 = GetNode<Top10Gallery>("../HUD/Top10Overlay");
		_titleWrap = GetNode<Control>("../HUD/LogoWrap");
		_titleWrap.Visible = false;
		_restartButton.Pressed += OnRestartPressed;
		_restartButton.Visible = false;

		_topButton.Pressed += () => _top10.ShowGallery();
		_topButton.Visible = false;

		_viewSize = GetViewportRect().Size;

		BaseY = 0f;
		_cameraOffsetY = 0f;
		Position = new Vector2(_viewSize.X / 2f, _cameraOffsetY);

		_best = (int)ProjectSettings.GetSetting("stackzen/best_score", 0);
		//_best = 0;
		UpdateHud();

		//RestartGame();
		ShowMenuPreview();
		SetProcess(false);
		SetProcessUnhandledInput(false);
		StopGame();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		if (!_isGameOver && _current != null)
		{
			Vector2 pos = _current.Position;
			pos.X += _dir * _speed * dt;

			if (pos.X > MoveRange)
			{
				pos.X = MoveRange;
				_dir = -1;
			}
			else if (pos.X < -MoveRange)
			{
				pos.X = -MoveRange;
				_dir = 1;
			}

			_current.Position = pos;
		}

		for (int i = _fallingPieces.Count - 1; i >= 0; i--)
		{
			var fp = _fallingPieces[i];

			fp.Velocity.Y += Gravity * dt;
			fp.Node.Position += fp.Velocity * dt;
			fp.Node.RotationDegrees += fp.RotationSpeed * dt;

			fp.Opacity -= FadeSpeed * dt;
			if (fp.Opacity < 0f) fp.Opacity = 0f;

			Color col = fp.Node.BaseColor;
			col.A = fp.Opacity;
			fp.Node.BaseColor = col;
			fp.Node.QueueRedraw();

			if (fp.Node.Position.Y > DespawnY || fp.Opacity <= 0f)
			{
				fp.Node.QueueFree();
				_fallingPieces.RemoveAt(i);
			}
		}

		UpdateCamera();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_isGameOver)
			return;

		if (@event.IsActionPressed("ui_accept"))
			PlaceCurrent();

		if (@event is InputEventMouseButton mb &&
			mb.ButtonIndex == MouseButton.Left &&
			mb.Pressed)
		{
			PlaceCurrent();
		}
	}

	// ===== CAMERA: giữ block trên cùng ở ~38% màn hình =====

	// offset phụ để menu đẩy game xuống, gameplay kéo về 0
	public float ExternalOffsetY
	{
		get => _externalOffsetY;
		set
		{
			_externalOffsetY = value;
			ApplyCameraPosition();
		}
	}

	private void ApplyCameraPosition()
	{
		Position = new Vector2(_viewSize.X / 2f, _cameraOffsetY + _externalOffsetY);
	}

	private void UpdateCamera()
	{
		if (_isGameOver) return;
		if (_placed.Count == 0)
			return;

		float topY = _placed[0].Position.Y;
		foreach (var b in _placed)
		{
			if (b.Position.Y < topY)
				topY = b.Position.Y;
		}

		if (_current != null && _current.Position.Y < topY)
			topY = _current.Position.Y;

		float targetTopScreenY = _viewSize.Y * 0.38f;
		float targetOffset = targetTopScreenY - topY;

		_cameraOffsetY = Mathf.Lerp(_cameraOffsetY, targetOffset, 0.15f);
		ApplyCameraPosition();
	}

	private void SnapCameraToTop()
	{
		if (_placed.Count == 0)
			return;

		float topY = _placed[0].Position.Y;
		foreach (var b in _placed)
		{
			if (b.Position.Y < topY)
				topY = b.Position.Y;
		}

		if (_current != null && _current.Position.Y < topY)
			topY = _current.Position.Y;

		float targetTopScreenY = _viewSize.Y * 0.38f;
		_cameraOffsetY = targetTopScreenY - topY;

		ApplyCameraPosition();
	}

	// ===== GAMEPLAY CORE =====

	private void RestartGame()
	{
		EmitSignal(SignalName.RestartRequested);
		foreach (Node child in GetChildren())
		{
			if (child is BlockNode block)
				block.QueueFree();
		}

		_placed.Clear();
		_fallingPieces.Clear();
		_current = null;
		_isGameOver = false;
		_score = 0;
		_combo = 0;

		UpdateHud();
		_restartButton.Visible = false;
		_gameOverOverlay.Visible = false;
		_topButton.Visible = false;
		_titleWrap.Visible = false;

		BlockNode baseBlock = CreateBlock(StartWidth, BlockHeight, GetPaletteColor(0));
		baseBlock.Position = new Vector2(0, BaseY);
		_placed.Add(baseBlock);

		SpawnCurrent();
		Scale = Vector2.One;
		_externalOffsetY = 0f; // hoặc ExternalOffsetY = 0f;
		SnapCameraToTop();
		ApplyCameraPosition();
	}

	private Color GetPaletteColor(int levelIndex)
	{
		if (Palette.Length == 0)
			return Colors.White;
		if (levelIndex < 0)
			levelIndex = 0;
		return Palette[levelIndex % Palette.Length];
	}

	private BlockNode CreateBlock(float width, float height, Color color)
	{
		var block = BlockScene.Instantiate<BlockNode>();
		block.Width = width;
		block.Height = height;
		block.BaseColor = color;
		AddChild(block);
		block.QueueRedraw();
		return block;
	}

	private void SpawnCurrent(bool isPerfect = false)
	{
		BlockNode last = _placed[_placed.Count - 1];

		_speed = BaseSpeed;
		_dir = 1;

		int levelIndex = _placed.Count;
		var color = GetPaletteColor(levelIndex);
		var nextWidth = isPerfect ? last.Width : last.Width - MinNextWidth;
		_current = CreateBlock(nextWidth, BlockHeight, color);
		_current.Position = new Vector2(-MoveRange, last.Position.Y - BlockHeight);
	}

	private void PlaceCurrent()
	{
		_clickSfx?.PlayClick();

		// ---- SCORE (combo nhân điểm) ----
		int basePoints = 1;
		if (_isGameOver || _current == null)
			return;

		BlockNode prev = _placed[_placed.Count - 1];

		float prevLeft = prev.Position.X - prev.Width / 2f;
		float prevRight = prev.Position.X + prev.Width / 2f;
		float currLeft = _current.Position.X - _current.Width / 2f;
		float currRight = _current.Position.X + _current.Width / 2f;

		float overlapLeft = Mathf.Max(prevLeft, currLeft);
		float overlapRight = Mathf.Min(prevRight, currRight);
		float overlapWidth = overlapRight - overlapLeft;
		if (overlapWidth <= 0 || overlapWidth < MinWidth)
		{
			GameOver();
			return;
		}

		float newCenterX = (overlapLeft + overlapRight) / 2f;

		Color pieceColor = _current.BaseColor;

		float leftCutWidth = overlapLeft - currLeft;
		if (leftCutWidth > 0.1f)
		{
			float leftCenter = currLeft + leftCutWidth / 2f;
			SpawnFallingPiece(leftCenter, _current.Position.Y,
							  leftCutWidth, BlockHeight, -1f, pieceColor);
		}

		float rightCutWidth = currRight - overlapRight;
		if (rightCutWidth > 0.1f)
		{
			float rightCenter = currRight - rightCutWidth / 2f;
			SpawnFallingPiece(rightCenter, _current.Position.Y,
							  rightCutWidth, BlockHeight, +1f, pieceColor);
		}

		// DÙNG CHÍNH _current LÀ BLOCK ĐÃ ĐẶT
		_current.Position = new Vector2(newCenterX, _current.Position.Y);
		_current.Width = overlapWidth;
		_current.QueueRedraw();

		_placed.Add(_current);
		_current = null;

		float centerDelta = Mathf.Abs(newCenterX - prev.Position.X);
		bool isPerfect = centerDelta <= PerfectWindow;
		if (isPerfect)
		{
			_combo++;
			ShowPerfectAndCombo();
		}
		else
		{
			_combo = 0;
		}

		// combo tăng khi perfect, còn không perfect thì combo đã reset về 0 ở trên.
		// multiplier: combo=0 => 1, combo=3 => x3
		int multiplier = (_combo > 0) ? _combo : 1;
		// bonus nhẹ cho perfect để "đã tay"
		int perfectBonus = isPerfect ? 0 : 0;

		//_score++;
		_score += basePoints * multiplier + perfectBonus;
		if (_score > _best)
		{
			_best = _score;
			ProjectSettings.SetSetting("stackzen/best_score", _best);
			ProjectSettings.Save();
		}

		UpdateHud();
		SpawnCurrent(isPerfect);
	}

	private void SpawnFallingPiece(
		float centerX, float centerY,
		float width, float height,
		float direction, Color color)
	{
		var piece = CreateBlock(width, height, color);
		piece.Position = new Vector2(centerX, centerY);

		float vx = direction * (float)GD.RandRange(120.0, 190.0);
		float vy = (float)GD.RandRange(30.0, 80.0);
		float rotSpeed = direction * (float)GD.RandRange(80.0, 150.0);

		var fp = new FallingPiece
		{
			Node = piece,
			Velocity = new Vector2(vx, vy),
			RotationSpeed = rotSpeed,
			Opacity = 1f
		};

		_fallingPieces.Add(fp);
		piece.AddToGroup("falling_piece");
	}

	private void GameOver()
	{
		_isGameOver = true;
		EmitSignal(SignalName.GameOverHappened);
		_restartButton.Visible = true;
		_gameOverOverlay.Visible = true;
		_topButton.Visible = true;
		_titleWrap.Visible = true;

		_gameOverOverlay.Modulate = new Color(1, 1, 1, 0);
		//_restartButton.Modulate = new Color(1, 1, 1, 0);
		//_restartButton.Scale = new Vector2(0.95f, 0.95f);
		_fxSfx?.PlayGameOver();
		var tw = CreateTween();
		tw.SetParallel(true);
		tw.TweenProperty(_gameOverOverlay, "modulate:a", 1f, 0.2f);
		//tw.TweenProperty(_restartButton, "modulate:a", 1f, 0.25f);
		//tw.TweenProperty(_restartButton, "scale", Vector2.One, 0.25f)
		//  .SetTrans(Tween.TransitionType.Cubic)
		//  .SetEase(Tween.EaseType.Out);
		GameOverZoomOutToFit();
	}

	private void OnRestartPressed()
	{
		RestartGame();
	}

	private void UpdateHud()
	{
		_scoreLabel.Text = _score.ToString();
		_bestLabel.Text = $"BEST {_best}";
	}

	private void ShowPerfectAndCombo()
	{
		if (_perfectLabel == null)
			return;

		_perfectLabel.Text = "Perfect!";
		Color c = _perfectLabel.Modulate;
		c.A = 1f;
		_perfectLabel.Modulate = c;
		_perfectLabel.Visible = true;

		Tween tween = CreateTween();
		tween.TweenProperty(_perfectLabel, "modulate:a", 0f, 0.35f)
			 .SetDelay(0.35f)
			 .SetTrans(Tween.TransitionType.Cubic)
			 .SetEase(Tween.EaseType.In);

		_fxSfx?.PlayPerfect(_combo);

		if (_combo >= 2)
		{
			_comboLabel.Text = $"COMBO x{_combo}";
			// intensity tăng theo score (0..1)
			float intensity = Mathf.Clamp(_score / 40f, 0f, 1f);

			// hoặc theo combo
			var g = GetNodeOrNull<GameBgmArcade>("../GameBgmPlayer");
			if (g != null)
			{
				float byScore = Mathf.Clamp(_score / 40f, 0f, 1f);
				float byCombo = Mathf.Clamp(_combo / 8f, 0f, 1f);
				g.SetIntensity(Mathf.Max(byScore, byCombo));
			}
		}
		else
			_comboLabel.Text = string.Empty;

		if (_combo >= 4)
		{
			_fxSfx?.PlayComboTick(_combo);
		}

		Color cc = _comboLabel.Modulate;
		cc.A = 1f;
		_comboLabel.Modulate = cc;
		_comboLabel.Visible = true;

		//Tween tween = CreateTween();
		tween.TweenProperty(_comboLabel, "modulate:a", 0f, 0.35f)
			 .SetDelay(0.35f)
			 .SetTrans(Tween.TransitionType.Cubic)
			 .SetEase(Tween.EaseType.In);
	}

	public void BeginGameFromMenu()
	{
		RestartGame();              // giữ logic cũ
		SetProcess(true);
		SetProcessUnhandledInput(true);
	}

	public void ShowMenuPreview()
	{
		// clear toàn bộ BlockNode
		foreach (var child in GetChildren())
			if (child is BlockNode) child.QueueFree();

		_placed.Clear();
		_fallingPieces.Clear();
		_current = null;

		// khoá input khi ở menu
		_isGameOver = true;

		// base (đỏ)
		var baseBlock = CreateBlock(StartWidth, BlockHeight, Palette[0]);
		baseBlock.Position = new Vector2(0, BaseY);
		_placed.Add(baseBlock);

		// moving (cam) - block chơi luôn
		_current = CreateBlock(baseBlock.Width, BlockHeight, Palette[1]);
		_current.Position = new Vector2(-MoveRange, baseBlock.Position.Y - BlockHeight);

		_dir = 1;
		_speed = BaseSpeed;

		// tính cameraOffsetY theo 2 block hiện có
		SnapCameraToTop();
	}

	public void StartFromMenuBlocks()
	{
		// Không gọi RestartGame() nữa
		_isGameOver = false;
		_score = 0;
		_combo = 0;

		UpdateHud();
		_restartButton.Visible = false;

		SetProcess(true);
		SetProcessUnhandledInput(true);
	}

	public void StopGame()
	{
		SetProcess(false);
		SetProcessUnhandledInput(false);
	}

	public void StartGame()
	{
		// Nếu bạn có hàm RestartGame() thì gọi ở đây
		RestartGame();

		SetProcess(true);
		SetProcessUnhandledInput(true);
	}

	private Rect2 GetTowerBounds()
	{
		bool hasAny = false;
		float minX = 0, minY = 0, maxX = 0, maxY = 0;

		foreach (var child in GetChildren())
		{
			if (child is not BlockNode b) continue;

			// BlockNode Position là tâm theo X, và Y theo BaseY bạn set (local)
			// Width/Height bạn có sẵn trong BlockNode
			float left = b.Position.X - b.Width * 0.5f;
			float right = b.Position.X + b.Width * 0.5f;
			float top = b.Position.Y - b.Height;   // mặt trên
			float bottom = b.Position.Y;              // mặt dưới

			if (!hasAny)
			{
				minX = left; maxX = right; minY = top; maxY = bottom;
				hasAny = true;
			}
			else
			{
				minX = Mathf.Min(minX, left);
				maxX = Mathf.Max(maxX, right);
				minY = Mathf.Min(minY, top);
				maxY = Mathf.Max(maxY, bottom);
			}
		}

		if (!hasAny) return new Rect2(0, 0, 1, 1);

		return new Rect2(minX, minY, maxX - minX, maxY - minY);
	}
	private Tween _gameOverTween;

	private void GameOverZoomOutToFit()
	{
		var view = GetViewportRect().Size;

		// 1) Lấy bounds toàn tháp (local space của Game)
		Rect2 bounds = GetTowerBounds();

		// 2) Chừa margin để nhìn thoáng
		float margin = 90f;
		float availW = Mathf.Max(1f, view.X - margin * 2f);
		float availH = Mathf.Max(1f, view.Y - margin * 2f);

		// 3) Tính scale để fit
		float scaleX = availW / bounds.Size.X;
		float scaleY = availH / bounds.Size.Y;
		float targetScale = Mathf.Min(scaleX, scaleY);

		// Không zoom-in nếu đang to quá; chỉ zoom-out từ từ
		targetScale = Mathf.Min(1f, targetScale);

		Vector2 targetScaleV = new Vector2(targetScale, targetScale);

		// 4) Tính target position để bounds nằm giữa màn hình
		Vector2 boundsCenterLocal = bounds.Position + bounds.Size * 0.5f;

		// Ta muốn: (Game.Position) + (boundsCenterLocal * targetScale) = screenCenter
		Vector2 screenCenter = view * 0.5f;
		Vector2 targetPos = screenCenter - boundsCenterLocal * targetScale;

		// 5) Tween scale + position
		_gameOverTween?.Kill();
		_gameOverTween = CreateTween();
		_gameOverTween.SetParallel(true);

		_gameOverTween.TweenProperty(this, "scale", targetScaleV, 0.9f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);

		_gameOverTween.TweenProperty(this, "position", targetPos, 0.9f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);

		_gameOverTween.TweenCallback(Callable.From(() =>
		{
			CallDeferred(nameof(TrySaveTop10Snapshot));
		}));
	}

	private async void TrySaveTop10Snapshot()
	{
		if (_top10 == null) return;
		if (!_top10.WouldQualify(_score)) return;

		// --- 1) Đợi falling pieces rớt xong (tối đa 120 frame ~ 2s) ---
		for (int i = 0; i < 120; i++)
		{
			var stillFalling = GetTree().GetNodesInGroup("falling_piece").Count > 0;
			if (!stillFalling) break;
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		// --- 2) Ẩn UI không muốn chụp (TOP + RESTART), nhưng giữ Score + BEST ---
		var topBtn = GetNodeOrNull<Control>("../HUD/Top10Button");
		var restartBtn = GetNodeOrNull<Control>("../HUD/RestartButton");
		// Lưu trạng thái visible hiện tại
		bool bestWasVisible = _bestLabel != null && _bestLabel.Visible;
		//bool scoreWasVisible = _scoreLabel != null && _scoreLabel.Visible;
		bool comboWasVisible = _comboLabel != null && _comboLabel.Visible;
		bool perfectWasVisible = _perfectLabel != null && _perfectLabel.Visible;

		bool topWasVisible = topBtn != null && topBtn.Visible;
		bool restartWasVisible = restartBtn != null && restartBtn.Visible;

		// Ẩn đúng những thứ che tháp
		if (_restartButton != null) _restartButton.Visible = false;
		if (_bestLabel != null) _bestLabel.Visible = false;

		// (tuỳ chọn) muốn ảnh “sạch” hơn thì ẩn luôn score/combo/perfect:
		//if (_scoreLabel != null) _scoreLabel.Visible = false;
		if (_comboLabel != null) _comboLabel.Visible = false;
		if (_perfectLabel != null) _perfectLabel.Visible = false;
		if (topBtn != null) topBtn.Visible = false;
		//if (_titleWrap != null) _titleWrap.Visible = true;
		
		//Save pose hiện tại (để restore)
		Vector2 oldPos = Position;
		Vector2 oldScale = Scale;

		//Pose riêng để snapshot full đáy (không ảnh hưởng in-game vì sẽ restore ngay)
		_gameOverTween?.Kill();
		ApplyGameOverFitPoseForSnapshot();

		// cho UI kịp update 1 frame
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		// --- 3) Chụp sau khi render xong ---
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

		Image img = GetViewport().GetTexture().GetImage();

		// Fix hướng ảnh: bạn báo “ngược + lật” => flip cả 2 trục (xoay 180)
		//img.FlipY();
		//img.FlipX();

		// --- 4) Restore UI ---
		if (topBtn != null) topBtn.Visible = topWasVisible;
		if (restartBtn != null) restartBtn.Visible = restartWasVisible;
		//if (_restartButton != null) _restartButton.Visible = restartWasVisible;
		if (_bestLabel != null) _bestLabel.Visible = bestWasVisible;
		//if (_scoreLabel != null) _scoreLabel.Visible = scoreWasVisible;
		if (_comboLabel != null) _comboLabel.Visible = comboWasVisible;
		if (_perfectLabel != null) _perfectLabel.Visible = perfectWasVisible;

		_top10.AddSnapshot(_score, img);
	}

	private void ApplyGameOverFitPoseForSnapshot()
	{
		Vector2 view = GetViewportRect().Size;
		Rect2 bounds = GetTowerBounds();

		// Pad an toàn cho snapshot (tăng/giảm tuỳ bạn)
		float marginX = 70f;
		float marginTop = 90f;
		float marginBottom = 120f; // ✅ nên để 220~260 để không bao giờ cắt đáy

		float availW = Mathf.Max(1f, view.X - marginX * 2f);
		float availH = Mathf.Max(1f, view.Y - marginTop - marginBottom);

		// scale fit theo vùng usable
		float scaleX = availW / Mathf.Max(1f, bounds.Size.X);
		float scaleY = availH / Mathf.Max(1f, bounds.Size.Y);
		float s = Mathf.Min(scaleX, scaleY);
		s = Mathf.Min(1f, s);

		// Tính các điểm quan trọng
		float boundsCenterX = bounds.Position.X + bounds.Size.X * 0.5f;
		float boundsBottomY = bounds.Position.Y + bounds.Size.Y;

		// ✅ Canh X: giữa màn hình
		float targetPosX = view.X * 0.5f - boundsCenterX * s;

		// ✅ Canh Y: đáy tháp nằm trên "đường đáy an toàn"
		float safeBottomY = view.Y - marginBottom;
		float targetPosY = safeBottomY - boundsBottomY * s;

		Scale = new Vector2(s, s);
		Position = new Vector2(targetPosX, targetPosY);
	}
}
