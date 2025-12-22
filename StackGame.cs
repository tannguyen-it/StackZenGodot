using Godot;
using System.Collections.Generic;

public partial class StackGame : Node2D
{
	[Export] public PackedScene BlockScene { get; set; }

	[Export] public float BlockHeight { get; set; } = 40f;
	[Export] public float StartWidth { get; set; } = 260f;
	[Export] public float MinWidth { get; set; } = 24f;
	[Export] public float MoveRange { get; set; } = 160f;
	[Export] public float BaseSpeed { get; set; } = 260f;
	[Export] public float PerfectWindow { get; set; } = 4f;

	[Export] public float BaseY { get; set; } = 0f;

	private const float Gravity = 900f;
	private const float FadeSpeed = 1.1f;
	private const float DespawnY = 1400f;

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

	public override void _Ready()
	{
		_scoreLabel = GetNode<Label>("../HUD/TopCenterBox/ScoreLabel");
		_bestLabel = GetNode<Label>("../HUD/BestLabel");
		_restartButton = GetNode<Button>("../HUD/RestartButton");
		_comboLabel = GetNode<Label>("../HUD/TopCenterBox/ComboLabel");
		_perfectLabel = GetNode<Label>("../HUD/TopCenterBox/PerfectLabel");

		_restartButton.Pressed += OnRestartPressed;
		_restartButton.Visible = false;

		_viewSize = GetViewportRect().Size;

		BaseY = 0f;
		_cameraOffsetY = 0f;
		Position = new Vector2(_viewSize.X / 2f, _cameraOffsetY);

		_best = (int)ProjectSettings.GetSetting("stackzen/best_score", 0);
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

		if (@event is InputEventMouseButton mbb && mbb.Pressed) GD.Print("GAME CLICK");
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

		BlockNode baseBlock = CreateBlock(StartWidth, BlockHeight, GetPaletteColor(0));
		baseBlock.Position = new Vector2(0, BaseY);
		_placed.Add(baseBlock);

		SpawnCurrent();
		SnapCameraToTop();
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

	private void SpawnCurrent()
	{
		BlockNode last = _placed[_placed.Count - 1];

		_speed = BaseSpeed;
		_dir = 1;

		int levelIndex = _placed.Count;
		var color = GetPaletteColor(levelIndex);

		_current = CreateBlock(last.Width, BlockHeight, color);
		_current.Position = new Vector2(-MoveRange, last.Position.Y - BlockHeight);
	}

	private void PlaceCurrent()
	{
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

		// ❗ DÙNG CHÍNH _current LÀ BLOCK ĐÃ ĐẶT
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
			ShowPerfectToast();
		}
		else
		{
			_combo = 0;
		}

		_score++;
		if (_score > _best)
		{
			_best = _score;
			ProjectSettings.SetSetting("stackzen/best_score", _best);
			ProjectSettings.Save();
		}

		UpdateHud();
		SpawnCurrent();
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
	}

	private void GameOver()
	{
		_isGameOver = true;
		_restartButton.Visible = true;
	}

	private void OnRestartPressed()
	{
		RestartGame();
	}

	private void UpdateHud()
	{
		_scoreLabel.Text = _score.ToString();
		_bestLabel.Text = $"BEST {_best}";

		if (_combo >= 2)
			_comboLabel.Text = $"COMBO x{_combo}";
		else
			_comboLabel.Text = string.Empty;
	}

	private void ShowPerfectToast()
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
}
