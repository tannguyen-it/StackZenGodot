using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public partial class Top10Gallery : Control
{
	[Export] public NodePath ListPath;
	[Export] public NodePath CloseButtonPath;

	[Export] public NodePath BackButtonPath = new NodePath("");
	[Export] public NodePath LeftSpacerPath = new NodePath("");
	[Export] public NodePath TitlePath = new NodePath("");

	[Export] public NodePath ScrollPath = new NodePath("");
	[Export] public NodePath DetailViewPath = new NodePath("");
	[Export] public NodePath DetailImagePath = new NodePath("");
	[Export] public NodePath DetailCloseBgPath = new NodePath("");
	[Export] public NodePath ShareButtonPath = new NodePath("");

	private VBoxContainer _list;
	private Button _close;
	private Button _back;
	private Control _leftSpacer;
	private Label _title;

	private ScrollContainer _scroll;
	private Control _detailView;
	private TextureRect _detailImage;
	private Button _detailCloseBg;
	private Button _share;

	private Control _headerBar;
	private PanelContainer _panel;

	private Entry? _selected;
	private FontFile? _uiFont;

	private const int MaxItems = 10;
	private string DataPath => "user://top10.json";
	private string ShotsDir => "user://top10_shots/";

	private class Entry
	{
		public int Score { get; set; }
		public string Png { get; set; } = "";
		public long UtcTicks { get; set; }
	}

	public override void _Ready()
	{
		_uiFont = GD.Load<FontFile>("res://UI/Fredoka-VariableFont_wdth,wght.ttf");

		// ---- robust get nodes (không chết nếu NodePath rỗng / bị rename nhẹ) ----
		_panel = GetNodeOrNull<PanelContainer>("Panel");
		_headerBar = GetNodeOrNull<Control>("Panel/RootVBox/HeaderBar");

		_list = SafeGetNode<VBoxContainer>(ListPath, "Panel/RootVBox/Body/Scroll/List");
		_close = SafeGetNode<Button>(CloseButtonPath, "Panel/RootVBox/HeaderBar/CloseButton");

		_back = SafeGetNodeOrNull<Button>(BackButtonPath, "Panel/RootVBox/HeaderBar/BackButton");
		_leftSpacer = SafeGetNodeOrNull<Control>(LeftSpacerPath, "Panel/RootVBox/HeaderBar/LeftSpacer");
		_title = SafeGetNodeOrNull<Label>(TitlePath, "Panel/RootVBox/HeaderBar/Title");

		_scroll = SafeGetNodeOrNull<ScrollContainer>(ScrollPath, "Panel/RootVBox/Body/Scroll");
		_detailView = SafeGetNodeOrNull<Control>(DetailViewPath, "Panel/RootVBox/Body/DetailView");
		_detailImage = SafeGetNodeOrNull<TextureRect>(DetailImagePath, "Panel/RootVBox/Body/DetailView/DetailImage");
		_detailCloseBg = SafeGetNodeOrNull<Button>(DetailCloseBgPath, "Panel/RootVBox/Body/DetailView/DetailCloseBg");
		_share = SafeGetNodeOrNull<Button>(ShareButtonPath, "Panel/RootVBox/Body/DetailView/BottomBar/ShareButton");

		// ---- wire events ----
		_close.Pressed += CloseAll;
		if (_back != null) _back.Pressed += CloseDetail;
		if (_detailCloseBg != null) _detailCloseBg.Pressed += CloseDetail;
		if (_share != null) _share.Pressed += ShareSelected;

		// ---- detail view visual rules ----
		if (_detailImage != null)
		{
			_detailImage.MouseFilter = MouseFilterEnum.Stop;
			_detailImage.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		}

		// shrink share button by code (phòng khi scene chưa update)
		if (_share != null)
		{
			_share.CustomMinimumSize = new Vector2(110, 36);
			_share.AddThemeFontSizeOverride("font_size", 18);
		}

		CloseAll();
	}

	// ===== PUBLIC API =====

	public void ShowGallery()
	{
		RefreshUI();
		ShowList();
		Show();
	}

	public bool WouldQualify(int score)
	{
		var list = Load();
		if (list.Count < MaxItems) return true;
		return score > list.Min(x => x.Score);
	}

	public void AddSnapshot(int score, Image shot)
	{
		EnsureDirs();

		string fileName = $"score_{score}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png";
		string userPngPath = ShotsDir + fileName;
		string absPngPath = ProjectSettings.GlobalizePath(userPngPath);

		shot.SavePng(absPngPath);

		var list = Load();
		list.Add(new Entry { Score = score, Png = userPngPath, UtcTicks = DateTime.UtcNow.Ticks });

		list = list
			.OrderByDescending(e => e.Score)
			.ThenByDescending(e => e.UtcTicks)
			.Take(MaxItems)
			.ToList();

		Save(list);
	}

	// ===== UI =====

	private void EnsureHeaderVisible()
	{
		if (_panel != null) _panel.Visible = true;
		if (_headerBar != null) _headerBar.Visible = true;
	}

	private void SetHeaderMode(bool detail)
	{
		EnsureHeaderVisible();

		if (_back != null) _back.Visible = detail;
		if (_leftSpacer != null) _leftSpacer.Visible = !detail;

		if (_title != null)
		{
			_title.Text = "TOP 10";
			_title.HorizontalAlignment = HorizontalAlignment.Center;
		}

		// Close luôn visible
		if (_close != null) _close.Visible = true;
	}

	private void RefreshUI()
	{
		foreach (var c in _list.GetChildren())
			c.QueueFree();

		var list = Load().OrderByDescending(x => x.Score).ToList();
		if (list.Count == 0)
		{
			var empty = new Label { Text = "Chưa có dữ liệu. Hãy chơi để tạo Top 10!" };
			ApplyGoldText(empty, 24);
			_list.AddChild(empty);
			return;
		}

		foreach (var e in list)
		{
			var card = new Control();
			card.CustomMinimumSize = new Vector2(0, 220);
			card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			card.MouseFilter = MouseFilterEnum.Ignore;

			var thumbBtn = new TextureButton
			{
				StretchMode = TextureButton.StretchModeEnum.KeepAspectCovered,
				IgnoreTextureSize = true,
				FocusMode = FocusModeEnum.None,
			};
			thumbBtn.SetAnchorsPreset(LayoutPreset.FullRect);

			var tex = LoadTexture(e.Png);
			if (tex != null) thumbBtn.TextureNormal = tex;
			else thumbBtn.Disabled = true;

			// score badge INSIDE image
			var badge = new PanelContainer();
			badge.MouseFilter = MouseFilterEnum.Ignore;
			badge.SetAnchorsPreset(LayoutPreset.TopLeft);
			badge.OffsetLeft = 14;
			badge.OffsetTop = 14;
			badge.OffsetRight = 110;
			badge.OffsetBottom = 54;
			badge.AddThemeStyleboxOverride("panel", CreateBadgeStyle());

			var scoreLabel = new Label
			{
				Text = e.Score.ToString(),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
			};
			ApplyGoldText(scoreLabel, 28);
			badge.AddChild(scoreLabel);

			thumbBtn.Pressed += () => OpenDetail(e);

			card.AddChild(thumbBtn);
			card.AddChild(badge);
			_list.AddChild(card);
		}
	}

	private void ShowList()
	{
		_selected = null;
		SetHeaderMode(detail: false);

		if (_detailView != null) _detailView.Visible = false;
		if (_scroll != null) _scroll.Visible = true;
	}

	private void OpenDetail(Entry e)
	{
		_selected = e;
		SetHeaderMode(detail: true);

		if (_detailImage != null)
		{
			_detailImage.Texture = LoadTexture(e.Png);
			// ép lại padding để vẫn còn header + bottom bar
			_detailImage.OffsetLeft = 48;
			_detailImage.OffsetTop = 18;
			_detailImage.OffsetRight = -48;
			_detailImage.OffsetBottom = -120;
		}

		if (_share != null)
		{
			_share.CustomMinimumSize = new Vector2(110, 36);
			_share.AddThemeFontSizeOverride("font_size", 18);
		}

		if (_scroll != null) _scroll.Visible = false;
		if (_detailView != null) _detailView.Visible = true;
	}

	private void CloseDetail()
	{
		ShowList();
	}

	private void CloseAll()
	{
		ShowList();
		Hide();
	}

	// ===== STYLE =====

	private StyleBoxFlat CreateBadgeStyle()
	{
		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.06f, 0.09f, 0.12f, 0.65f);
		sb.BorderColor = new Color(0.9490196f, 0.8509804f, 0.6509804f, 1f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 2;
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 14;
		sb.ContentMarginLeft = sb.ContentMarginRight = 8;
		sb.ContentMarginTop = sb.ContentMarginBottom = 4;
		return sb;
	}

	private void ApplyGoldText(Control c, int fontSize)
	{
		c.AddThemeColorOverride("font_color", new Color(0.9843137f, 0.89411765f, 0.6862745f, 1f));
		if (_uiFont != null) c.AddThemeFontOverride("font", _uiFont);
		c.AddThemeFontSizeOverride("font_size", fontSize);
	}

	// ===== SHARE =====

	private void ShareSelected()
	{
		if (_selected == null) return;
		if (string.IsNullOrEmpty(_selected.Png)) return;

		string abs = ProjectSettings.GlobalizePath(_selected.Png);
		if (!File.Exists(abs))
		{
			OS.Alert("Không tìm thấy file ảnh để share.", "Share");
			return;
		}

		DisplayServer.ClipboardSet(abs);

		if (OS.HasFeature("windows") || OS.HasFeature("macos") || OS.HasFeature("linux"))
		{
			OS.ShellShowInFileManager(abs);
			return;
		}

		OS.ShellOpen(abs);
		OS.Alert("Ảnh đã được lưu. Bạn có thể mở ảnh và dùng Share từ hệ điều hành.\n\nĐường dẫn:\n" + abs, "Share");
	}

	// ===== STORAGE =====

	private void EnsureDirs()
	{
		string dir = ProjectSettings.GlobalizePath(ShotsDir);
		if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
	}

	private List<Entry> Load()
	{
		string abs = ProjectSettings.GlobalizePath(DataPath);
		if (!File.Exists(abs)) return new List<Entry>();

		try
		{
			string json = File.ReadAllText(abs);
			return JsonSerializer.Deserialize<List<Entry>>(json) ?? new List<Entry>();
		}
		catch
		{
			return new List<Entry>();
		}
	}

	private void Save(List<Entry> list)
	{
		string abs = ProjectSettings.GlobalizePath(DataPath);
		try
		{
			string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
			File.WriteAllText(abs, json);
		}
		catch (Exception ex)
		{
			GD.PrintErr("[Top10] Save failed: " + ex.Message);
		}
	}

	private Texture2D LoadTexture(string userPath)
	{
		if (string.IsNullOrEmpty(userPath)) return null;

		string abs = ProjectSettings.GlobalizePath(userPath);
		if (!File.Exists(abs)) return null;

		try
		{
			var img = Image.LoadFromFile(abs);
			return ImageTexture.CreateFromImage(img);
		}
		catch
		{
			return null;
		}
	}

	// ===== helpers =====

	private T SafeGetNode<T>(NodePath exported, string fallbackPath) where T : Node
	{
		if (exported.ToString() != "")
		{
			var n = GetNodeOrNull<T>(exported);
			if (n != null) return n;
		}
		return GetNode<T>(fallbackPath);
	}

	private T SafeGetNodeOrNull<T>(NodePath exported, string fallbackPath) where T : Node
	{
		if (exported.ToString() != "")
		{
			var n = GetNodeOrNull<T>(exported);
			if (n != null) return n;
		}
		return GetNodeOrNull<T>(fallbackPath);
	}
}
