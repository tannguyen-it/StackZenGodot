using Godot;

public partial class BlockNode : Node2D
{
	[Export] public float Width { get; set; } = 260f;
	[Export] public float Height { get; set; } = 40f;

	// Màu “thân” block – Game sẽ gán màu theo level
	[Export] public Color BaseColor { get; set; } = new Color(0.9f, 0.9f, 0.9f);

	public override void _Draw()
	{
		DrawIsometricBlock();
	}

	public void UpdateVisual()
	{
		QueueRedraw();
	}

	private void DrawIsometricBlock()
	{
		// Front rect: tâm node là giữa mặt trước
		float w = Width;
		float h = Height;

		float x = -w / 2f;
		float y = -h / 2f;

		// Độ sâu isometric
		float depth = h * 1.0f;
		float dx = depth * 0.6f;
		float dy = depth * 0.6f;

		// Các điểm top
		Vector2 topP1 = new Vector2(x, y);
		Vector2 topP2 = new Vector2(x + w, y);
		Vector2 topP3 = new Vector2(x + w + dx, y - dy);
		Vector2 topP4 = new Vector2(x + dx, y - dy);

		// Các điểm side phải
		Vector2 sideP1 = new Vector2(x + w, y);
		Vector2 sideP2 = new Vector2(x + w, y + h);
		Vector2 sideP3 = new Vector2(x + w + dx, y + h - dy);
		Vector2 sideP4 = new Vector2(x + w + dx, y - dy);

		Color main = BaseColor;
		Color topColor = Lighten(main, 1.08f);
		Color sideColor = Darken(main, 0.85f);
		Color frontColor = main;

		// Top
		DrawPolygon(
			new Vector2[] { topP1, topP2, topP3, topP4 },
			new Color[] { topColor, topColor, topColor, topColor });

		// Side
		DrawPolygon(
			new Vector2[] { sideP1, sideP2, sideP3, sideP4 },
			new Color[] { sideColor, sideColor, sideColor, sideColor });

		// Front
		Rect2 frontRect = new Rect2(x, y, w, h);
		DrawRect(frontRect, frontColor);

		// Viền nhẹ
		DrawRect(frontRect, new Color(0, 0, 0, 0.25f), false, 1.0f);
	}

	private static Color Darken(Color c, float factor)
		=> new Color(
			Clamp01(c.R * factor),
			Clamp01(c.G * factor),
			Clamp01(c.B * factor),
			c.A);

	private static Color Lighten(Color c, float factor)
	{
		float r = 1f - (1f - c.R) / factor;
		float g = 1f - (1f - c.G) / factor;
		float b = 1f - (1f - c.B) / factor;

		return new Color(
			Clamp01(r),
			Clamp01(g),
			Clamp01(b),
			c.A);
	}

	private static float Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);
}
