using UnityEngine;
/// Paints the 1024 ship texture atlas (albedo + height for the normal map). Run at edit time by Builder.Setup.
public static class ShipArt {
  public const int N = 1024;
  // atlas cells in pixels (x, y, w, h), y up
  public static readonly RectInt HULL = new RectInt(0, 0, 512, 256), TRIM = new RectInt(512, 0, 512, 128), BRASS = new RectInt(512, 128, 128, 128), ROPE = new RectInt(640, 128, 128, 128),
    LATTICE = new RectInt(768, 128, 256, 128), SAIL = new RectInt(0, 256, 512, 512), DECK = new RectInt(512, 256, 256, 256), CRATE = new RectInt(768, 256, 256, 256),
    SACK = new RectInt(512, 512, 256, 256), JAR = new RectInt(768, 512, 256, 256), MAST = new RectInt(0, 768, 256, 256), GLASS = new RectInt(256, 768, 128, 128),
    PENNANT = new RectInt(384, 768, 256, 256), CABIN = new RectInt(640, 768, 256, 256), FIGURE = new RectInt(896, 768, 128, 256), DARK = new RectInt(256, 896, 128, 128);
  public static Rect UV(RectInt r) => new Rect((r.x + 2f) / N, (r.y + 2f) / N, (r.width - 4f) / N, (r.height - 4f) / N);
  static float Hash(int x, int y) { unchecked { int h = x * 374761393 + y * 668265263; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }
  static float Noise(float x, float y) { int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = x - xi, fy = y - yi; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
    return Mathf.Lerp(Mathf.Lerp(Hash(xi, yi), Hash(xi + 1, yi), fx), Mathf.Lerp(Hash(xi, yi + 1), Hash(xi + 1, yi + 1), fx), fy); }
  static float Fbm(float x, float y) => Noise(x, y) * 0.5f + Noise(x * 2.1f, y * 2.1f) * 0.3f + Noise(x * 4.3f, y * 4.3f) * 0.2f;
  static Color H(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

  public static void Paint(bool pirate, out Color[] albedo, out float[] height) {
    var a = new Color[N * N]; var h = new float[N * N];
    Color wood = pirate ? H("#3B2A20") : H("#7A4E2C"), woodDark = pirate ? H("#1E1510") : H("#4E3019"), trim = pirate ? H("#4A1A18") : H("#2E3478"), trimGold = pirate ? H("#6E5A3A") : H("#C9A44C");
    Color brass = pirate ? H("#6E5A3A") : H("#B8913E"), cream = pirate ? H("#1C1A19") : H("#F4E9CF"), stripe = pirate ? H("#8A2420") : H("#2BB0A8");
    void Cell(RectInt r, System.Func<float, float, int, int, (Color, float)> f) { for (int y = 0; y < r.height; y++) for (int x = 0; x < r.width; x++) { var (c, hh) = f(x / (float)r.width, y / (float)r.height, x, y); int i = (r.y + y) * N + r.x + x; a[i] = c; h[i] = hh; } }
    // planks: horizontal boards with seams, butt joints, wear and (pirate) scorch
    Cell(HULL, (u, v, x, y) => { float rows = 8; int row = Mathf.FloorToInt(v * rows); float fv = v * rows - row; float off = Hash(row, 7) * 0.5f;
      bool butt = Mathf.Abs(Mathf.Repeat(u * 2 + off, 1f) - 0.5f) < 0.004f; bool seam = fv < 0.06f || butt;
      float grain = Fbm(u * 40, v * 6 + row * 3) * 0.35f + Mathf.Sin(u * 90 + Fbm(u * 8, v * 8) * 6) * 0.05f; float wear = Fbm(u * 6 + 3, v * 6) ;
      var c = Color.Lerp(woodDark, wood, 0.55f + grain + Hash(row, 3) * 0.15f) * (0.85f + 0.15f * wear); if (!pirate && wear > 0.68f) c = Color.Lerp(c, H("#B8A68A"), (wear - 0.68f) * 1.6f); // salt-bleached wear
      if (pirate && Fbm(u * 3 + 9, v * 3) > 0.62f) c *= 0.45f; if (seam) c = woodDark * 0.55f; float hgt = seam ? 0f : 0.6f + grain * 0.5f;
      if (pirate) { // tar sheen + riveted iron patches
        c = Color.Lerp(c, H("#0E0B09"), 0.25f * Fbm(u * 12, v * 2));
        for (int i = 0; i < 4; i++) { float pu = 0.15f + i * 0.23f + Hash(i, 1) * 0.06f, pv = 0.3f + Hash(i, 2) * 0.4f; float du = Mathf.Abs(u - pu), dv = Mathf.Abs(v - pv);
          if (du < 0.05f && dv < 0.09f) { c = H("#4A4744") * (0.7f + 0.4f * Fbm(u * 25, v * 25)); if (Fbm(u * 9 + i, v * 9) > 0.6f) c = Color.Lerp(c, H("#6B3A1E"), 0.6f); hgt = 0.85f;
            if ((du > 0.038f || dv > 0.075f) && Mathf.Repeat((du > 0.038f ? v : u) * 60, 1) < 0.3f) { c *= 1.4f; hgt = 1f; } } } }
      return (c, hgt); });
    // indigo trim with gold geometric zigzag
    Cell(TRIM, (u, v, x, y) => { float zz = Mathf.Abs(Mathf.Repeat(u * 24, 1) - 0.5f) * 2; bool gold = Mathf.Abs(v - 0.5f - (zz - 0.5f) * 0.5f) < 0.07f || v < 0.08f || v > 0.92f;
      var c = (gold ? trimGold : trim) * (0.85f + 0.15f * Fbm(u * 30, v * 4)); return (c, gold ? 0.9f : 0.5f); });
    Cell(BRASS, (u, v, x, y) => { float d = new Vector2(u - .5f, v - .5f).magnitude; return (brass * (1.15f - d) * (0.9f + 0.2f * Fbm(u * 12, v * 12)), 1 - d); });
    Cell(ROPE, (u, v, x, y) => { float tw = Mathf.Repeat(u * 6 + v * 6, 1); return (H(pirate ? "#5E5241" : "#A88C5E") * (0.7f + 0.4f * Mathf.Sin(tw * Mathf.PI)), tw); });
    Cell(LATTICE, (u, v, x, y) => { float a1 = Mathf.Abs(Mathf.Repeat(u * 10 + v * 5, 1) - 0.5f), a2 = Mathf.Abs(Mathf.Repeat(u * 10 - v * 5, 1) - 0.5f); bool bar = a1 < 0.12f || a2 < 0.12f || u < 0.05f || u > 0.95f || v < 0.08f || v > 0.92f;
      return (bar ? wood * 1.1f : H("#140E0A"), bar ? 1 : 0); });
    // sail: cloth panels, stitched seams, patches, stripe and sun (Hasan) / crescent (pirate)
    Cell(SAIL, (u, v, x, y) => { float d = u - v; bool seam = Mathf.Abs(Mathf.Repeat((u + v) * 7, 1) - 0.5f) < 0.02f; var c = cream * (0.9f + 0.1f * Fbm(u * 20, v * 20));
      if (d > 0.08f && d < 0.24f) c = stripe * (0.9f + 0.1f * Fbm(u * 20, v * 20));
      bool patch = (Mathf.Abs(u - 0.62f) < 0.07f && Mathf.Abs(v - 0.2f) < 0.06f) || (Mathf.Abs(u - 0.85f) < 0.05f && Mathf.Abs(v - 0.55f) < 0.07f) || (pirate && Mathf.Abs(u - 0.4f) < 0.06f && Mathf.Abs(v - 0.12f) < 0.05f);
      if (patch) c = Color.Lerp(c, pirate ? H("#3A2E28") : H("#D8C9A2"), 0.7f);
      var o = new Vector2(u - 0.7f, v - 0.36f); float r = o.magnitude, ang = Mathf.Atan2(o.y, o.x);
      if (!pirate && (r < 0.07f || (r < 0.12f && r > 0.075f && Mathf.Repeat(ang * 16 / (2 * Mathf.PI), 1) < 0.45f))) c = trimGold;
      if (pirate) { // horned skull: cranium, jaw, dark sockets, curved horns
        var bone = H("#D9CDB0"); float sx = u - 0.7f, sy = v - 0.38f;
        bool cran = new Vector2(sx, sy * 1.1f).magnitude < 0.065f, jaw = Mathf.Abs(sx) < 0.035f && sy < -0.045f && sy > -0.1f;
        bool eye = new Vector2(Mathf.Abs(sx) - 0.026f, sy + 0.005f).magnitude < 0.017f, nose = Mathf.Abs(sx) < 0.008f && sy < -0.02f && sy > -0.04f, teeth = jaw && Mathf.Repeat(sx * 70, 1) < 0.18f;
        float hr = new Vector2(Mathf.Abs(sx) - 0.1f, sy - 0.02f).magnitude; bool horn = hr > 0.045f && hr < 0.045f + 0.02f * Mathf.Clamp01((0.11f - Mathf.Abs(sx)) * 12 + 0.3f) && sy > 0.0f && Mathf.Abs(sx) > 0.05f;
        if (r < 0.15f && r > 0.135f) c = H("#9A2A22");
        if (cran || jaw || horn) c = bone * (0.85f + 0.15f * Fbm(u * 30, v * 30)); if (eye || nose || teeth) c = H("#120E0C"); }
      if (seam) c *= 0.8f; c *= Mathf.Lerp(0.85f, 1f, Fbm(u * 3, v * 3)); return (c, seam ? 0.3f : 0.5f); });
    Cell(DECK, (u, v, x, y) => { int col = Mathf.FloorToInt(u * 10); bool seam = Mathf.Repeat(u * 10, 1) < 0.05f; var c = Color.Lerp(woodDark, wood * 1.25f, 0.5f + Fbm(u * 4, v * 30 + col) * 0.5f); return (seam ? woodDark * 0.6f : c, seam ? 0 : 0.6f); });
    Cell(CRATE, (u, v, x, y) => { bool edge = u < 0.08f || u > 0.92f || v < 0.08f || v > 0.92f || Mathf.Abs(u - v) < 0.06f; var c = H(pirate ? "#5A4330" : "#9A7448") * (0.8f + 0.3f * Fbm(u * 20, v * 3));
      return (edge ? c * 0.7f : c, edge ? 1 : 0.5f); });
    Cell(SACK, (u, v, x, y) => { var c = H("#8C7350") * (0.75f + 0.3f * Fbm(u * 30, v * 30)); if (Mathf.Repeat(u * 40 + v * 40, 1) < 0.15f) c *= 0.85f; if (Mathf.Abs(v - 0.78f) < 0.05f) c = H("#4E3B26"); c.a = 1; return (c, Fbm(u * 30, v * 30)); });
    Cell(JAR, (u, v, x, y) => { var c = H("#A0522D") * (0.85f + 0.2f * Fbm(u * 10, v * 10)); if (Mathf.Abs(v - 0.6f) < 0.04f) c = H("#E3C9A0"); return (c, 0.5f); });
    Cell(MAST, (u, v, x, y) => { float g = Fbm(u * 6, v * 50); return (Color.Lerp(woodDark, wood, 0.4f + g * 0.6f), g); });
    Cell(GLASS, (u, v, x, y) => { float d = new Vector2(u - .5f, v - .5f).magnitude; return (Color.Lerp(H("#FFE6A0"), H("#C8822A"), d * 2), 0.5f); });
    Cell(PENNANT, (u, v, x, y) => { var c = pirate ? (v > 0.5f ? H("#1A1716") : H("#8A2420")) : H("#2E3478"); if (!pirate && new Vector2(u - 0.35f, v - 0.5f).magnitude < 0.12f) c = trimGold; return (c, 0.5f); });
    Cell(CABIN, (u, v, x, y) => { bool frame = Mathf.Repeat(u * 4, 1) < 0.08f || v < 0.06f || v > 0.94f; var c = (frame ? woodDark : wood * 1.1f) * (0.85f + 0.2f * Fbm(u * 10, v * 30)); return (c, frame ? 1 : 0.4f); });
    Cell(FIGURE, (u, v, x, y) => { float carve = Mathf.Sin(u * 40 + Mathf.Sin(v * 30) * 2) * 0.5f + 0.5f; var c = Color.Lerp(woodDark, pirate ? woodDark : trimGold, carve * 0.6f); return (c, carve); });
    Cell(DARK, (u, v, x, y) => (H("#120D0A"), 0));
    albedo = a; height = h;
  }
  public static Color[] NormalFromHeight(float[] h, float strength) {
    var n = new Color[N * N];
    for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) {
      float l = h[y * N + Mathf.Max(0, x - 1)], r = h[y * N + Mathf.Min(N - 1, x + 1)], d = h[Mathf.Max(0, y - 1) * N + x], u = h[Mathf.Min(N - 1, y + 1) * N + x];
      var v = new Vector3((l - r) * strength, (d - u) * strength, 1).normalized; n[y * N + x] = new Color(v.x * 0.5f + 0.5f, v.y * 0.5f + 0.5f, v.z * 0.5f + 0.5f, 1); }
    return n; }
  public static Color[] Foam(int n) { var c = new Color[n * n]; for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float f = Fbm(x / 10f, y / 10f); float a = Mathf.Clamp01((f - 0.4f) * 3); c[y * n + x] = new Color(1, 1, 1, a); } return c; }
}
