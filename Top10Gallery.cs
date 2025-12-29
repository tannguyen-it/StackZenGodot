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

	private VBoxContainer _list;
	private Button _close;

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
		_list = GetNode<VBoxContainer>(ListPath);
		_close = GetNode<Button>(CloseButtonPath);

		_close.Pressed += () => Hide();
		Hide();
	}

	// ====== PUBLIC API ======

	public void ShowGallery()
	{
		RefreshUI();
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

		// lưu PNG
		string fileName = $"score_{score}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png";
		string userPngPath = ShotsDir + fileName;                 // user://...
		string absPngPath = ProjectSettings.GlobalizePath(userPngPath);

		shot.SavePng(absPngPath);

		// update JSON
		var list = Load();
		list.Add(new Entry { Score = score, Png = userPngPath, UtcTicks = DateTime.UtcNow.Ticks });

		// sort desc & keep top 10
		list = list
			.OrderByDescending(e => e.Score)
			.ThenByDescending(e => e.UtcTicks)
			.Take(MaxItems)
			.ToList();

		Save(list);
	}

	// ====== INTERNAL ======

	private void RefreshUI()
	{
		foreach (var c in _list.GetChildren())
			c.QueueFree();
		var list = Load().OrderByDescending(x => x.Score).ToList();

		//var list = Load().OrderByDescending(x => x.Score).ToList();
		if (list.Count == 0)
		{
			var empty = new Label { Text = "Chưa có dữ liệu. Hãy chơi để tạo Top 10!" };
			_list.AddChild(empty);
			return;
		}

		foreach (var e in list)
		{
			var row = new HBoxContainer();
			row.CustomMinimumSize = new Vector2(0, 140);

			var texRect = new TextureRect
			{
				CustomMinimumSize = new Vector2(260, 150),
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};

			var scoreLabel = new Label();
			scoreLabel.Text = $"SCORE {e.Score}";
			scoreLabel.VerticalAlignment = VerticalAlignment.Center;

			// load png
			var tex = LoadTexture(e.Png);
			if (tex != null) texRect.Texture = tex;
			else
			{
				texRect.Texture = null;
				// fallback: hiện text để biết item có nhưng ảnh fail
				var ph = new Label { Text = "NO IMAGE", VerticalAlignment = VerticalAlignment.Center };
				row.AddChild(ph);
			}

			row.AddChild(texRect);
			row.AddChild(scoreLabel);

			// click row to view larger
			var btn = new Button();
			btn.Text = "VIEW";
			btn.Pressed += () => ShowPreview(e);
			row.AddChild(btn);

			_list.AddChild(row);
		}
	}

	private void ShowPreview(Entry e)
	{
		// Preview đơn giản: mở một Window dialog/AcceptDialog
		var dlg = new AcceptDialog();
		dlg.Title = $"SCORE {e.Score}";
		dlg.DialogText = "";

		var tex = LoadTexture(e.Png);
		var tr = new TextureRect
		{
			Texture = tex,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(900, 600)
		};

		dlg.AddChild(tr);
		AddChild(dlg);
		dlg.PopupCentered();
		dlg.Confirmed += () => dlg.QueueFree();
		dlg.Canceled += () => dlg.QueueFree();
	}

	private Texture2D LoadTexture(string userPath)
	{
		if (string.IsNullOrEmpty(userPath)) return null;

		string abs = ProjectSettings.GlobalizePath(userPath);

		if (!System.IO.File.Exists(abs))
		{
			return null;
		}

		try
		{
			byte[] bytes = System.IO.File.ReadAllBytes(abs);

			var img = new Image();
			// Godot 4: dùng LoadPngFromBuffer chắc chắn nhất
			var err = img.LoadPngFromBuffer(bytes);
			if (err != Error.Ok)
			{
				return null;
			}

			var tex = ImageTexture.CreateFromImage(img);
			return tex;
		}
		catch (System.Exception ex)
		{
			return null;
		}
	}

	private void EnsureDirs()
	{
		var absDir = ProjectSettings.GlobalizePath(ShotsDir);
		if (!Directory.Exists(absDir))
			Directory.CreateDirectory(absDir);
	}

	private List<Entry> Load()
	{
		try
		{
			string abs = ProjectSettings.GlobalizePath(DataPath);
			if (!File.Exists(abs)) return new List<Entry>();
			var json = File.ReadAllText(abs);
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
		var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
		File.WriteAllText(abs, json);
	}
}
