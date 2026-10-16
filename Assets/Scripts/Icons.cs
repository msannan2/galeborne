using System.Collections.Generic;
using UnityEngine;
/// Flat engraved-style line icons drawn at runtime (no external icon art): tea-brick, bowl, bolt, barrel, jar, ingot.
public static class Icons {
  static Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
  const int N = 96; static Color[] px; static Color ink;
  static void P(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < N && y < N) px[y * N + x] = c; }
  static void Rect(int x0, int y0, int x1, int y1, Color fill, bool stroke = true) { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) P(x, y, (stroke && (x - x0 < 3 || x1 - x < 3 || y - y0 < 3 || y1 - y < 3)) ? ink : fill); }
  static void Line(int x0, int y0, int x1, int y1, int w = 2) { int n = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)); for (int i = 0; i <= n; i++) { int x = x0 + (x1 - x0) * i / Mathf.Max(1, n), y = y0 + (y1 - y0) * i / Mathf.Max(1, n); for (int a = 0; a < w; a++) for (int b = 0; b < w; b++) P(x + a, y + b, ink); } }
  static void Disc(int cx, int cy, float r, Color fill, bool stroke = true) { for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) { float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)); if (d <= r) P(x, y, stroke && d > r - 3 ? ink : fill); } }
  static void Bowl(Color fill, Color heap) { // mound + bowl
    for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) { float dx = (x - 48) / 34f, dy = (y - 46) / 16f; if (y >= 46 && dx * dx + dy * dy <= 1) P(x, y, heap); }
    for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) { float dx = (x - 48) / 40f, dy = (y - 46) / 30f; if (y < 48 && dx * dx + dy * dy <= 1) P(x, y, dx * dx + dy * dy > 0.86f || y > 44 ? ink : fill); }
    Rect(34, 10, 62, 16, ink, false); }
  public static Sprite For(string id, Color inkColor) {
    if (cache.TryGetValue(id, out var s)) return s;
    var art = Resources.Load<Texture2D>("GoodsIcons/" + id); // hand-drawn icons, parchment keyed out
    if (art != null) return cache[id] = Sprite.Create(art, new Rect(0, 0, art.width, art.height), new Vector2(0.5f, 0.5f));
    ink = inkColor; px = new Color[N * N];
    Color parch = new Color(0.93f, 0.88f, 0.78f), warm = new Color(0.78f, 0.62f, 0.42f), dark = new Color(0.45f, 0.32f, 0.22f);
    switch (id) {
      case "tea": Rect(10, 24, 86, 70, new Color(0.55f, 0.40f, 0.26f)); Rect(22, 34, 74, 60, new Color(0.62f, 0.47f, 0.31f)); Line(48, 34, 48, 60); Line(22, 47, 74, 47); Line(30, 40, 40, 54); Line(56, 40, 66, 54); break; // pressed tea brick
      case "copper": Rect(8, 22, 60, 52, new Color(0.72f, 0.42f, 0.25f)); Rect(36, 46, 88, 76, new Color(0.78f, 0.48f, 0.28f)); Line(14, 30, 54, 30); Line(42, 54, 82, 54); break;
      case "salt": Bowl(warm, new Color(0.96f, 0.95f, 0.92f)); break;
      case "pepper": Bowl(warm, new Color(0.25f, 0.2f, 0.17f)); break;
      case "coffee": Bowl(warm, new Color(0.36f, 0.22f, 0.14f)); break;
      case "dates": Bowl(warm, new Color(0.45f, 0.24f, 0.14f)); break;
      case "pearls": Bowl(dark, parch); for (int i = 0; i < 5; i++) Disc(28 + i * 10, 56 + (i % 2) * 5, 5, Color.white); break;
      case "incense": Rect(30, 14, 66, 62, warm); Rect(24, 60, 72, 70, dark); Line(40, 72, 34, 86); Line(48, 72, 50, 90); Line(56, 72, 62, 86); break;
      case "gunpowder": Rect(22, 12, 74, 84, dark); Line(22, 30, 74, 30, 3); Line(22, 66, 74, 66, 3); Disc(48, 84, 6, ink, false); break;
      default: { // cloth bolt / rug roll
        Color c = id == "indigo" ? new Color(0.20f, 0.24f, 0.50f) : id == "silk" ? new Color(0.75f, 0.32f, 0.30f) : id == "rugs" ? new Color(0.55f, 0.20f, 0.18f) : parch;
        Rect(10, 30, 76, 66, c); Disc(78, 48, 18, c); Disc(78, 48, 7, ink, false);
        if (id == "rugs") for (int x = 14; x < 74; x += 8) Line(x, 26, x, 32); else { Line(14, 42, 72, 42); Line(14, 54, 72, 54); } break; }
    }
    var tex = new Texture2D(N, N, TextureFormat.RGBA32, false); tex.SetPixels(px); tex.Apply();
    return cache[id] = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f)); }
}
