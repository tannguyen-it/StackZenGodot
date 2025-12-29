using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public partial class Top10Gallery : Control
{
	// Scene wiring (all are set in Main.tscn; keep fallbacks for safety)
	[Export] public NodePath ScrollPath = new NodePath("");
	[Export] public NodePath ListPath = new NodePath("");
	[Export] public NodePath DetailViewPath = new NodePath("");
	[Export] public NodePath DetailImagePath = new NodePath("");
	[Export] public NodePath ShareButtonPath = new NodePath("");

	[Export] public NodePath CloseButtonPath = new NodePath("");
	[Export] public NodePath BackButtonPath = new NodePath("");
	[Export] public NodePath LeftSpacerPath = new NodePath("");
	[Export] public NodePath TitlePath = new NodePath("");

	private ScrollContainer _scroll;
	private VBoxContainer _list;
	private VBoxContainer _detail;
	private TextureRect _detailImage;
	private Button _share;

	private Button _close;
	private Button _back;
	private Control _leftSpacer;
	private Label _title;

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

		_scroll = GetNodeOrNull<ScrollContainer>(ScrollPath)
				  ?? GetNodeOrNull<ScrollContainer>("Panel/RootVBox/Body/Scroll");

		_list = GetNodeOrNull<VBoxContainer>(ListPath)
				?? GetNodeOrNull<VBoxContainer>("Panel/RootVBox/Body/Scroll/ScrollRoot/List");

		_detail = GetNodeOrNull<VBoxContainer>(DetailViewPath)
				  ?? GetNodeOrNull<VBoxContainer>("Panel/RootVBox/Body/Scroll/ScrollRoot/Detail");

		_detailImage = GetNodeOrNull<TextureRect>(DetailImagePath)
					   ?? GetNodeOrNull<TextureRect>("Panel/RootVBox/Body/Scroll/ScrollRoot/Detail/DetailImage");

		_share = GetNodeOrNull<Button>(ShareButtonPath)
				 ?? GetNodeOrNull<Button>("Panel/RootVBox/Body/Scroll/ScrollRoot/Detail/DetailImage/BottomBar/ShareButton")
				 ?? GetNodeOrNull<Button>("Panel/RootVBox/Body/Scroll/ScrollRoot/Detail/BottomBar/ShareButton");

		_close = GetNodeOrNull<Button>(CloseButtonPath)
				 ?? GetNodeOrNull<Button>("Panel/RootVBox/HeaderBar/CloseButton");

		_back = GetNodeOrNull<Button>(BackButtonPath)
				?? GetNodeOrNull<Button>("Panel/RootVBox/HeaderBar/BackButton");

		_leftSpacer = GetNodeOrNull<Control>(LeftSpacerPath)
					  ?? GetNodeOrNull<Control>("Panel/RootVBox/HeaderBar/LeftSpacer");

		_title = GetNodeOrNull<Label>(TitlePath)
				 ?? GetNodeOrNull<Label>("Panel/RootVBox/HeaderBar/Title");

		if (_close != null) _close.Pressed += CloseAll;
		if (_back != null) _back.Pressed += CloseDetail;
		if (_share != null) _share.Pressed += ShareSelected;

		// Safety
		if (_list == null) GD.PushError("[Top10] List not found. Check ListPath.");
		if (_detail == null) GD.PushError("[Top10] Detail not found. Check DetailViewPath.");
		if (_scroll == null) GD.PushError("[Top10] Scroll not found. Check ScrollPath.");

		CloseAll();
	}

	// ====== PUBLIC API ======

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
		string userPngPath = ShotsDir + fileName; // user://...
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

	// ====== UI ======

	private void SetHeaderMode(bool detail)
	{
		if (_back != null) _back.Visible = detail;
		if (_leftSpacer != null) _leftSpacer.Visible = !detail;

		if (_title != null)
		{
			_title.Text = "TOP";
			_title.HorizontalAlignment = HorizontalAlignment.Center;
		}
	}

	private void ShowList()
	{
		_selected = null;
		SetHeaderMode(detail: false);

		if (_list != null) _list.Visible = true;
		if (_detail != null) _detail.Visible = false;

		ScrollToTop();
	}

	private void OpenDetail(Entry e)
	{
		_selected = e;
		SetHeaderMode(detail: true);

		if (_detailImage != null)
			_detailImage.Texture = LoadTexture(e.Png);

		if (_list != null) _list.Visible = false;
		if (_detail != null) _detail.Visible = true;

		ScrollToTop();
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

	private void ScrollToTop()
	{
		if (_scroll == null) return;

		// Set now + deferred (Godot sometimes updates scroll size next frame)
		_scroll.ScrollVertical = 0;
		_scroll.CallDeferred("set", "scroll_vertical", 0);
	}

	private void RefreshUI()
	{
		if (_list == null) return;

		foreach (var c in _list.GetChildren())
			c.QueueFree();

		var list = Load().OrderByDescending(x => x.Score).ToList();
		if (list.Count == 0)
		{
			var empty = new Label { Text = "" };
			ApplyGoldText(empty, 24);
			_list.AddChild(empty);
			return;
		}

		foreach (var e in list)
		{
			// Card: thumbnail + score badge (inside image)
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

			// Score badge (top-left)
			var badge = new PanelContainer();
			badge.MouseFilter = MouseFilterEnum.Ignore; // click-through
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

	// ====== STYLE HELPERS ======

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
		if (_uiFont != null)
			c.AddThemeFontOverride("font", _uiFont);
		c.AddThemeFontSizeOverride("font_size", fontSize);
	}

	// ====== SHARE ======

	private void ShareSelected()
	{
		if (_selected == null) return;
		if (string.IsNullOrEmpty(_selected.Png)) return;

		string abs = ProjectSettings.GlobalizePath(_selected.Png);
		if (!File.Exists(abs))
		{
			//OS.Alert("Không tìm thấy file ảnh để share.", "Share");
			return;
		}

		// TODO: share native on mobile (Android/iOS plugin). For now: open file / copy path.
		DisplayServer.ClipboardSet(abs);

		if (OS.HasFeature("windows") || OS.HasFeature("macos") || OS.HasFeature("linux"))
		{
			OS.ShellShowInFileManager(abs);
			return;
		}

		OS.ShellOpen(abs);
		//OS.Alert("Ảnh đã được lưu. Bạn có thể mở ảnh và dùng Share từ hệ điều hành.\n\nĐường dẫn:\n" + abs, "Share");
	}

	// ====== STORAGE ======

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
			//GD.PrintErr("[Top10] Save failed: " + ex.Message);
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
}
