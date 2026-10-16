using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// Town art kit: painted textures into the atlas, townsfolk (8 kinds, idle/walk loops), props, event hints, and the tutorial pointer.
public partial class Game {
  // ---- k4 per-town keeper skins (Game Designer): one shared code path, palette + height keyed by town name ----
  // Repaints townMat's atlas cells per port (matte, solid + faint noise, no stripes, no emission). Landmarks use lmMat's own atlas,
  // so Debal's pennant (#A8322A) stays the only red. Called from TownLayoutsK4.SharedHider when a town's layout is shown.
  Color[] k4AtlasBase; string k4SkinTown = "";
  static readonly Dictionary<string, (TCell cell, string hex)[]> K4Skins = new Dictionary<string, (TCell, string)[]> {
    { "Gwadar", new[] { (TCell.Plaster, "#8A5A3C"), (TCell.Ochre, "#8A5A3C"), (TCell.Stone, "#6B4430"), (TCell.Indigo, "#3E4A6E"), (TCell.Cloth, "#3E4A6E"),
                        (TCell.AwnRed, "#3E4A6E"), (TCell.AwnTeal, "#3E4A6E"), (TCell.Roof, "#9C8A5A"), (TCell.Rug, "#3E4A6E") } },
    { "Debal",  new[] { (TCell.Plaster, "#C9A46A"), (TCell.Ochre, "#C9A46A"), (TCell.Stone, "#C9A46A"), (TCell.AwnRed, "#D08A2E"), (TCell.Cloth, "#D08A2E"),
                        (TCell.AwnTeal, "#2F6E6A"), (TCell.Teak, "#6A4528"), (TCell.Roof, "#6A4528"), (TCell.Rug, "#D08A2E") } /* k4 fix: terracotta roof + rugs were the last reds */ },
    { "Muscat", new[] { (TCell.Plaster, "#ECE6D8"), (TCell.Ochre, "#ECE6D8"), (TCell.Stone, "#ECE6D8"), (TCell.Whitewash, "#ECE6D8"), (TCell.Paving, "#CFC7B5"),
                        (TCell.Roof, "#CFC7B5"), (TCell.AwnRed, "#8FAE8C"), (TCell.AwnTeal, "#8FAE8C"), (TCell.Cloth, "#8FAE8C"), (TCell.Marble, "#B8AE9A"), (TCell.Rock, "#9A9283"), (TCell.Rug, "#8FAE8C") } },
  };
  static readonly Dictionary<string, float> K4KeeperHeight = new Dictionary<string, float> { { "Gwadar", 0.75f }, { "Debal", 1f }, { "Muscat", 1f } };
  public void ApplyKeeperSkinK4(string town) {
    if (townMat == null || town == k4SkinTown) return; var tex = townMat.GetTexture("_BaseMap") as Texture2D; if (tex == null || !tex.isReadable) return;
    if (k4AtlasBase == null) k4AtlasBase = tex.GetPixels(0);
    var px = (Color[])k4AtlasBase.Clone();
    if (K4Skins.TryGetValue(town, out var cells)) foreach (var (cell, hex) in cells) { ColorUtility.TryParseHtmlString(hex, out var c);
      int cx = (int)cell % TN, cy = (int)cell / TN;
      for (int y = 0; y < TC; y++) for (int x = 0; x < TC; x++) { float n = 0.95f + 0.05f * Mathf.PerlinNoise(x * 0.09f, y * 0.09f); var o = c * n; o.a = 1; px[(cy * TC + y) * TA + cx * TC + x] = o; } }
    tex.SetPixels(px, 0); tex.Apply(true); k4SkinTown = town;
    float hs = K4KeeperHeight.TryGetValue(town, out var h) ? h : 1f;
    foreach (var b in bldgs) if (b.t) b.t.localScale = new Vector3(1, hs, 1);
    StartCoroutine(K4NeutralTint()); /* the shared palette tint would muddy these exact hexes; reset after ApplyTownPaletteV06k3 runs */
    Debug.Log($"GB k4 keeper skin {town} height x{hs}");
  }
  System.Collections.IEnumerator K4NeutralTint() { yield return null; if (townMat && townMat.HasProperty("_BaseColor")) townMat.SetColor("_BaseColor", Color.white); }

  /// copies the team's textures into atlas cells; walls get 2x2 mirrored tiling so the non-seamless edges meet themselves
  void PaintKitTextures(Texture2D atlas) {
    void Put(string n, TCell cell, bool mirror) { var src = Resources.Load<Texture2D>("TownTex/" + n); if (src == null || !src.isReadable) return; int cx = (int)cell % TN, cy = (int)cell / TN;
      for (int y = 0; y < TC; y++) for (int x = 0; x < TC; x++) { float u, v;
        if (mirror) { float a = x / (float)TC * 2, b = y / (float)TC * 2; u = a < 1 ? a : 2 - a; v = b < 1 ? b : 2 - b; } else { u = x / (float)TC; v = y / (float)TC; }
        var c = src.GetPixelBilinear(u, v); float e = Mathf.Min(Mathf.Min(x, TC - 1 - x), Mathf.Min(y, TC - 1 - y)) / (float)TC; c *= 0.86f + 0.14f * Mathf.SmoothStep(0, 0.1f, e); c.a = 1; atlas.SetPixel(cx * TC + x, cy * TC + y, c); } }
    Put("ochre_plaster", TCell.Plaster, true); Put("whitewash", TCell.Whitewash, true); Put("sandstone", TCell.Ochre, true); Put("dock_planks", TCell.Planks, false); atlas.Apply(true); }

  // ---------------- townsfolk ----------------
  public readonly List<(Transform t, int idx, Vector3 a, Vector3 b, float speed, float phase, int kind)> folk = new List<(Transform, int, Vector3, Vector3, float, float, int)>();
  static readonly string[] FolkNames = { "Porter", "Fisherman", "Spice vendor", "Woman with basket", "Running child", "Old man", "Guard", "Sailor" };
  /// duel fighters after duel_fighters_turnaround.jpg (adult, ~7.5 heads): Hasan lean in white kurta, brown belt, indigo turban with tail;
  /// raider broader in cream shirt, dark brown vest, grey baggy trousers, boots, crimson sash. Returns (body on the town atlas, accent in a flat colour).
  /// split by flat material (Mats/F_*): no atlas UVs, so no stretched plaid or black masks
  Dictionary<string, Mesh> FighterMesh(bool raider) => FighterArt.Build(raider); /* v0.6g sculpted fighters */
  Dictionary<string, Mesh> FighterMeshOld(bool raider) {
    var parts = new Dictionary<string, MeshB>(); MeshB M(string n) { if (!parts.TryGetValue(n, out var b)) parts[n] = b = new MeshB(); return b; }
    var R = new Rect(0, 0, 1, 1); float w = raider ? 1.18f : 1f; int S = 12;
    string shirt = raider ? "F_Shirt" : "F_Kurta", trou = raider ? "F_Trousers" : "F_Kurta";
    for (int sd = -1; sd <= 1; sd += 2) { float x = sd * 0.1f * w;
      if (raider) { M("F_Boot").Cyl(new Vector3(x, 0.0f, 0.02f), new Vector3(x, 0.36f, 0.0f), 0.065f, 0.07f, S, R); M(trou).Lathe(new Vector3(x, 0.34f, 0), new[] { 0.08f, 0.11f, 0.12f, 0.115f, 0.11f }, 0.56f, S, R); }
      else { M("F_Leather").Box(new Vector3(x, 0.035f, 0.04f), new Vector3(0.1f, 0.07f, 0.24f), R); M("F_Skin").Cyl(new Vector3(x, 0.07f, 0), new Vector3(x, 0.12f, 0), 0.05f, 0.05f, S, R); M(trou).Lathe(new Vector3(x, 0.12f, 0), new[] { 0.06f, 0.075f, 0.085f, 0.09f, 0.09f }, 0.6f, S, R); } }
    if (raider) M(shirt).Lathe(new Vector3(0, 0.84f, 0), new[] { 0.2f * w, 0.19f * w, 0.19f * w, 0.21f * w, 0.23f * w, 0.22f * w, 0.17f * w }, 0.62f, S, R);
    else M(shirt).Lathe(new Vector3(0, 0.6f, 0), new[] { 0.22f, 0.205f, 0.19f, 0.175f, 0.17f, 0.18f, 0.195f, 0.2f, 0.19f, 0.15f }, 0.86f, S, R);
    if (raider) { M("F_Vest").Lathe(new Vector3(0, 1.0f, -0.005f), new[] { 0.205f * w, 0.215f * w, 0.235f * w, 0.24f * w, 0.2f * w }, 0.44f, S, R);
      M("Sash").Lathe(new Vector3(0, 0.84f, 0), new[] { 0.215f * w, 0.215f * w, 0.21f * w }, 0.17f, S, R); M("Sash").Box(new Vector3(0.12f, 0.7f, 0.17f), new Vector3(0.09f, 0.32f, 0.03f), Quaternion.Euler(0, 0, 6), R); }
    else M("F_Leather").Lathe(new Vector3(0, 1.0f, 0), new[] { 0.183f, 0.183f, 0.18f }, 0.07f, S, R);
    float sh = 1.42f, sx = 0.22f * w;
    for (int sd = -1; sd <= 1; sd += 2) { var s0 = new Vector3(sd * sx, sh, 0); var el = new Vector3(sd * (sx + 0.06f), sh - 0.29f, sd > 0 ? 0.08f : 0.02f); var hd = sd > 0 ? new Vector3(sd * (sx + 0.02f), sh - 0.48f, 0.26f) : new Vector3(sd * (sx + 0.05f), sh - 0.58f, 0.04f);
      M(shirt).Cyl(s0, el, 0.065f * w, 0.055f * w, S, R); M(raider ? "F_Skin" : shirt).Cyl(el, hd, raider ? 0.055f : 0.05f, 0.04f, S, R); M("F_Skin").Lathe(hd + Vector3.down * 0.05f, new[] { 0.035f, 0.045f, 0.04f, 0.02f }, 0.1f, 8, R);
      if (sd > 0) { M("F_Steel").Cyl(hd, hd + new Vector3(0.02f, 0.25f, 0.62f), 0.018f, 0.008f, 4, R); M("F_Gold").Box(hd + new Vector3(0, 0.03f, 0.04f), new Vector3(0.12f, 0.025f, 0.03f), R); } }
    M("F_Skin").Cyl(new Vector3(0, sh - 0.02f, 0), new Vector3(0, sh + 0.08f, 0.01f), 0.05f, 0.048f, S, R);
    var hc = new Vector3(0, sh + 0.07f, 0.01f); M("F_Skin").Lathe(hc, new[] { 0.05f, 0.075f, 0.095f, 0.1f, 0.098f, 0.09f, 0.07f, 0.03f }, 0.26f, S, R);
    M("F_Skin").Box(hc + new Vector3(0, 0.14f, 0.097f), new Vector3(0.03f, 0.05f, 0.04f), R); /* nose */
    for (int sd = -1; sd <= 1; sd += 2) { M("F_Eye").Box(hc + new Vector3(sd * 0.035f, 0.165f, 0.094f), new Vector3(0.026f, 0.012f, 0.01f), R); M("F_Beard").Box(hc + new Vector3(sd * 0.036f, 0.186f, 0.095f), new Vector3(0.036f, 0.01f, 0.01f), R); }
    /* short brown beard only along the jaw and chin, skin shows above it */
    M("F_Beard").Box(hc + new Vector3(0, 0.035f, 0.075f), new Vector3(0.11f, 0.06f, 0.05f), R); for (int sd = -1; sd <= 1; sd += 2) M("F_Beard").Box(hc + new Vector3(sd * 0.075f, 0.07f, 0.04f), new Vector3(0.03f, 0.08f, 0.07f), Quaternion.Euler(0, sd * 20, 0), R);
    M("F_Beard").Box(hc + new Vector3(0, 0.115f, 0.096f), new Vector3(0.07f, 0.014f, 0.012f), R); /* moustache */ M("F_Lip").Box(hc + new Vector3(0, 0.095f, 0.097f), new Vector3(0.04f, 0.01f, 0.008f), R);
    if (raider) { M("F_Hair").Lathe(hc + new Vector3(0, 0.18f, -0.01f), new[] { 0.104f, 0.106f, 0.095f, 0.06f }, 0.11f, S, R); M("F_Hair").Box(hc + new Vector3(0, 0.12f, -0.09f), new Vector3(0.17f, 0.15f, 0.05f), R); }
    else { M("Turban").Lathe(hc + new Vector3(0, 0.17f, -0.01f), new[] { 0.115f, 0.14f, 0.15f, 0.145f, 0.12f, 0.06f }, 0.19f, S, R);
      M("Turban").Box(hc + new Vector3(0.05f, 0.02f, -0.11f), new Vector3(0.08f, 0.32f, 0.03f), Quaternion.Euler(8, 0, -6), R); M("Turban").Box(hc + new Vector3(0.07f, -0.2f, -0.12f), new Vector3(0.07f, 0.22f, 0.03f), Quaternion.Euler(14, 0, -10), R); }
    return parts.ToDictionary(kv => kv.Key, kv => kv.Value.Build()); }
  Mesh FolkMesh(int kind) {
    var mb = new MeshB(); float s = kind == 4 ? 0.62f : kind == 5 ? 0.93f : 1f; bool hasan = kind == 8, raider = kind == 9; if (kind >= 8) kind = 7; /* 8 Hasan, 9 raider: sailor build, own colours */
    TCell robe = hasan ? TCell.Cloth : raider ? TCell.Dark : new[] { TCell.Sand, TCell.Whitewash, TCell.Indigo, TCell.Indigo, TCell.Gold, TCell.Cloth, TCell.DarkTeak, TCell.Indigo }[kind];
    TCell wrap = hasan ? TCell.Indigo : raider ? TCell.Dark : new[] { TCell.Cloth, TCell.Whitewash, TCell.Cloth, TCell.Gold, TCell.AwnTeal, TCell.Whitewash, TCell.Indigo, TCell.Whitewash }[kind];
    if (kind == 2) { /* seated cross-legged with bowls of spice */ mb.Lathe(Vector3.zero, new[] { 0.42f, 0.36f, 0.22f, 0.2f, 0.12f }, 0.7f, 8, U(robe)); mb.Lathe(new Vector3(0, 0.68f, 0), new[] { 0.06f, 0.11f, 0.12f, 0.09f }, 0.26f, 7, U(TCell.Skin)); mb.Lathe(new Vector3(0, 0.88f, 0), new[] { 0.12f, 0.15f, 0.12f, 0.04f }, 0.16f, 7, U(wrap));
      mb.Box(new Vector3(0, 0.02f, -0.5f), new Vector3(1.3f, 0.04f, 0.7f), U(TCell.Rug)); TCell[] sp = { TCell.Gold, TCell.AwnRed, TCell.Roof, TCell.Green };
      for (int i = 0; i < 4; i++) { var b = new Vector3(-0.45f + i * 0.3f, 0.04f, -0.6f); mb.Lathe(b, new[] { 0.08f, 0.13f, 0.14f }, 0.1f, 8, U(TCell.Teak)); mb.Lathe(b + Vector3.up * 0.1f, new[] { 0.13f, 0.08f, 0.01f }, 0.07f, 8, U(sp[i])); } return mb.Build(); }
    for (int k = -1; k <= 1; k += 2) mb.Cyl(new Vector3(k * 0.08f * s, 0, 0), new Vector3(k * 0.09f * s, 0.5f * s, 0), 0.05f * s, 0.06f * s, 5, U(kind == 3 ? robe : TCell.Skin));
    mb.Lathe(new Vector3(0, 0.35f * s, 0), new[] { 0.2f * s, 0.18f * s, 0.16f * s, 0.19f * s, 0.11f * s }, 0.8f * s, 7, U(robe));
    if (kind == 3) mb.Lathe(Vector3.zero, new[] { 0.25f, 0.22f, 0.19f }, 0.5f, 7, U(robe)); /* long skirt */
    for (int k = -1; k <= 1; k += 2) mb.Cyl(new Vector3(k * 0.19f * s, 1.05f * s, 0), new Vector3(k * 0.25f * s, 0.62f * s, -0.07f * s), 0.05f * s, 0.045f * s, 4, U(robe));
    mb.Lathe(new Vector3(0, 1.12f * s, 0), new[] { 0.06f * s, 0.11f * s, 0.12f * s, 0.09f * s }, 0.26f * s, 7, U(TCell.Skin));
    if (kind == 3) mb.Lathe(new Vector3(0, 1.08f, 0), new[] { 0.2f, 0.15f, 0.15f, 0.1f, 0.02f }, 0.46f, 7, U(wrap)); else if (kind != 4) mb.Lathe(new Vector3(0, 1.32f * s, 0), new[] { 0.12f * s, 0.15f * s, 0.12f * s, 0.04f * s }, 0.16f * s, 7, U(wrap));
    if (kind == 0) mb.Lathe(new Vector3(0.22f, 1.18f, 0.05f), new[] { 0.2f, 0.27f, 0.26f, 0.18f }, 0.45f, 8, U(TCell.Cloth)); /* sack on the shoulder */
    if (kind == 1) { mb.Quad(new Vector3(-0.3f, 0.05f, -0.22f), new Vector3(0.35f, 0.05f, -0.22f), new Vector3(0.3f, 0.75f, -0.2f), new Vector3(-0.25f, 0.75f, -0.2f), U(TCell.Rope), true); mb.Box(new Vector3(0, 1.0f, -0.12f), new Vector3(0.3f, 0.35f, 0.05f), U(TCell.Gold)); }
    if (kind == 3) mb.Lathe(new Vector3(-0.3f, 0.62f, -0.12f), new[] { 0.1f, 0.17f, 0.18f }, 0.16f, 8, U(TCell.Teak));
    if (kind == 5) mb.Cyl(new Vector3(0.3f, 0, -0.1f), new Vector3(0.27f, 0.95f, -0.08f), 0.025f, 0.025f, 4, U(TCell.Wood));
    if (kind == 6) { mb.Cyl(new Vector3(0.3f, 0, 0), new Vector3(0.3f, 2.3f, 0), 0.025f, 0.025f, 4, U(TCell.Wood)); mb.Lathe(new Vector3(0.3f, 2.3f, 0), new[] { 0.05f, 0.01f }, 0.2f, 4, U(TCell.Dark));
      var sh = new GameObject(); mb.Lathe(new Vector3(-0.3f, 0.6f, -0.05f), new[] { 0.28f, 0.28f }, 0.05f, 10, U(TCell.Teak)); UnityEngine.Object.Destroy(sh); }
    if (kind == 7 && !hasan && !raider) mb.Lathe(new Vector3(0, 1.0f, -0.05f), new[] { 0.24f, 0.28f, 0.24f }, 0.1f, 10, U(TCell.Rope));
    if (hasan || raider) { mb.Cyl(new Vector3(0.24f, 0.62f, -0.1f), new Vector3(0.3f, 1.25f, -0.55f), 0.02f, 0.012f, 4, U(TCell.Marble)); /* drawn blades */ if (raider) mb.Box(new Vector3(0, 1.2f, -0.1f), new Vector3(0.22f, 0.1f, 0.06f), U(TCell.Dark)); /* mask */ }
    return mb.Build(); }
  void AddFolk(int kind, Vector3 a, Vector3 b, float speed) { if (a.y < 0.5f) { a.z = Mathf.Max(a.z, 0.8f); a.x = Mathf.Clamp(a.x, -8.5f, 8.5f); } if (b != Vector3.zero && b.y < 0.5f) { b.z = Mathf.Max(b.z, 0.8f); b.x = Mathf.Clamp(b.x, -8.5f, 8.5f); } /* quay folk snapped onto the pier, out of the harbour water */
    var g = new GameObject(FolkNames[kind]); g.transform.parent = town3D; g.AddComponent<MeshFilter>().sharedMesh = folkMeshes[kind]; g.AddComponent<MeshRenderer>().sharedMaterial = townMat; g.transform.position = a;
    folk.Add((g.transform, folk.Count, a, b, speed, UnityEngine.Random.value * 10, kind)); }
  Mesh[] folkMeshes;
  void BuildFolkAndProps(Func<int, float> RowY, Func<int, float> RowZ) {
    folkMeshes = Enumerable.Range(0, 8).Select(FolkMesh).ToArray(); folk.Clear();
    AddFolk(0, new Vector3(-9, 0, -0.6f), new Vector3(-3, 0, 1.2f), 1.1f); AddFolk(0, new Vector3(2, 0, -0.8f), new Vector3(9, 0, 0.4f), 1.0f);
    AddFolk(1, new Vector3(-4.6f, 0, -1.1f), Vector3.zero, 0); AddFolk(2, new Vector3(-2.2f, 0, 3.4f), Vector3.zero, 0); AddFolk(2, new Vector3(5.2f, 0, 4.4f), Vector3.zero, 0);
    AddFolk(3, new Vector3(-7, 0, 2.5f), new Vector3(-1.5f, 0, 4.5f), 0.8f); AddFolk(3, new Vector3(1.5f, RowY(1), RowZ(1) - 3.4f), new Vector3(7, RowY(1), RowZ(1) - 3.2f), 0.7f);
    AddFolk(4, new Vector3(-5, 0, 1.5f), new Vector3(4, 0, 2.8f), 2.6f); AddFolk(5, new Vector3(-1.4f, RowY(2), RowZ(2) - 3.5f), new Vector3(-7, RowY(2), RowZ(2) - 3.4f), 0.45f);
    AddFolk(6, new Vector3(1.6f, RowY(5), RowZ(5) - 4.1f), Vector3.zero, 0); AddFolk(6, new Vector3(-1.6f, RowY(5), RowZ(5) - 4.1f), Vector3.zero, 0); AddFolk(7, new Vector3(-7.4f, 0, -1.3f), Vector3.zero, 0); AddFolk(7, new Vector3(8.5f, 0, -1.2f), new Vector3(3.5f, 0, -1.0f), 0.9f);
    AddFolk(1, new Vector3(1.6f, RowY(3), RowZ(3) - 3.5f), new Vector3(7.5f, RowY(3), RowZ(3) - 3.4f), 0.6f); AddFolk(0, new Vector3(-1.4f, RowY(4), RowZ(4) - 3.5f), new Vector3(-8, RowY(4), RowZ(4) - 3.4f), 0.9f); AddFolk(5, new Vector3(0.8f, 0, 6), Vector3.zero, 0);
    // props from town_props_sheet: moored rowboats and a fishing dhow, nets drying, hand cart, lantern posts, potted palms, clay jars, spice baskets
    var mb = new MeshB(); var r = new System.Random(3);
    foreach (var (p, ry) in new[] { (new Vector3(-4.0f, -0.25f, -3.2f), 8f), (new Vector3(3.2f, -0.25f, -2.6f), -12f), (new Vector3(-12.5f, -0.25f, -3.0f), 20f), (new Vector3(-2.2f, 0.05f, -4.6f), 25f), (new Vector3(4.8f, 0.05f, -5.0f), -30f) }) Rowboat(mb, p, ry); /* v0.6e2: two more moored in the open water of the bottom third */
    var fr = new Vector3(9.5f, 0, 1.0f); for (int k = 0; k < 3; k++) mb.Cyl(fr + new Vector3(k * 1.2f, 0, 0), fr + new Vector3(k * 1.2f, 1.6f, 0), 0.05f, 0.05f, 4, U(TCell.Wood)); mb.Cyl(fr + Vector3.up * 1.6f, fr + new Vector3(2.4f, 1.6f, 0), 0.04f, 0.04f, 4, U(TCell.Wood));
    for (int k = 0; k < 2; k++) mb.Quad(fr + new Vector3(k * 1.2f + 0.05f, 0.3f, 0), fr + new Vector3(k * 1.2f + 1.15f, 0.3f, 0), fr + new Vector3(k * 1.2f + 1.15f, 1.6f, 0), fr + new Vector3(k * 1.2f + 0.05f, 1.6f, 0), new Rect(UVc(TCell.Rope, 0.1f, 0.1f), U(TCell.Rope).size * 0.4f), true);
    { var c = new Vector3(-10.5f, 0, 4.5f); mb.Box(c + new Vector3(0, 0.75f, 0), new Vector3(1.6f, 0.5f, 0.9f), U(TCell.Wood)); for (int k = -1; k <= 1; k += 2) mb.Lathe(c + new Vector3(k * 0.4f, 0.45f, 0.5f * 1), new[] { 0.45f, 0.45f }, 0.08f, 10, U(TCell.Teak));
      mb.Cyl(c + new Vector3(-0.8f, 0.8f, 0), c + new Vector3(-1.9f, 0.4f, 0), 0.04f, 0.04f, 4, U(TCell.Wood)); Sack(mb, c + new Vector3(0.2f, 1.0f, 0), TCell.Cloth, 0.7f); }
    foreach (var lx in new[] { -10.8f, -3.0f, 2.6f, 10.8f }) { var lp = new Vector3(lx, 0, 0.4f); mb.Cyl(lp, lp + Vector3.up * 2.4f, 0.06f, 0.05f, 6, U(TCell.Dark)); Lantern(mb, lp + new Vector3(0, 2.3f, 0)); }
    foreach (var pp in new[] { new Vector3(-1.9f, 0, 1.2f), new Vector3(1.9f, 0, 1.2f), new Vector3(-1.9f, RowY(3), RowZ(3) - 3.4f), new Vector3(1.9f, RowY(3), RowZ(3) - 3.4f) }) { Pot(mb, pp, 1.1f);
      for (int f = 0; f < 6; f++) { var top = pp + Vector3.up * 1.4f; var d = Quaternion.Euler(0, f * 60, 0) * Vector3.forward; mb.Tri(top, top + d * 0.9f + Vector3.down * 0.35f + Vector3.Cross(d, Vector3.up) * 0.2f, top + d * 0.9f + Vector3.down * 0.35f - Vector3.Cross(d, Vector3.up) * 0.2f, UVc(TCell.Green, 0, 0), UVc(TCell.Green, 1, 0), UVc(TCell.Green, .5f, 1)); }
      mb.Cyl(pp + Vector3.up * 0.6f, pp + Vector3.up * 1.4f, 0.05f, 0.04f, 4, U(TCell.Wood)); }
    for (int k = 0; k < 5; k++) Pot(mb, new Vector3(6.2f + k * 0.45f, 0, 2.6f + (k % 2) * 0.3f), 0.7f + (k % 3) * 0.15f);
    MeshObj("Props", mb, Vector3.zero, false);
    var fd = new GameObject("FishingDhow").transform; fd.parent = town3D; Dhow.Build(fd, T, false, "FishingDhow"); fd.position = new Vector3(-13.5f, -0.15f, -7.5f); fd.rotation = Quaternion.Euler(0, 160, 0); fd.localScale = Vector3.one * 0.5f;
    BuildHints(RowY, RowZ);
  }
  static void Rowboat(MeshB mb, Vector3 p, float ry) { var q = Quaternion.Euler(0, ry, 0); int n = 8;
    for (int i = 0; i < n; i++) { float t0 = i / (float)n, t1 = (i + 1f) / n; Vector3 S0(float t, float k) { float w = Mathf.Sin(Mathf.PI * t) * 0.65f + 0.05f; return p + q * new Vector3(k * w, k == 0 ? -0.25f : 0.3f, -1.4f + t * 2.8f); }
      for (int k = -1; k <= 1; k += 2) { mb.Quad(S0(t0, 0), S0(t1, 0), S0(t1, k), S0(t0, k), U(TCell.Teak), true); } }
    for (int k = 0; k < 2; k++) mb.Box(p + q * new Vector3(0, 0.15f, -0.4f + k * 0.8f), new Vector3(1.1f, 0.05f, 0.22f), q, U(TCell.Wood));
    mb.Cyl(p + q * new Vector3(-0.3f, 0.25f, 0), p + q * new Vector3(0.9f, 0.05f, -1.2f), 0.03f, 0.03f, 4, U(TCell.Wood)); mb.Cyl(p + q * new Vector3(0, 0.3f, 1.4f), p + new Vector3(0.2f, 0.6f, 2.4f), 0.015f, 0.015f, 3, U(TCell.Rope)); }

  // ---------------- event hints in town ----------------
  readonly Dictionary<string, GameObject> hints = new Dictionary<string, GameObject>();
  void BuildHints(Func<int, float> RowY, Func<int, float> RowZ) {
    var r = new System.Random(9);
    { var mb = new MeshB(); for (int k = 0; k < 14; k++) Crate(mb, new Vector3(4 + (k % 5) * 0.75f, (k / 5) * 0.7f, 0.9f), 0.7f, k * 13); for (int k = 0; k < 6; k++) Sack(mb, new Vector3(-10 + k * 0.5f, 0, 0.9f), TCell.Cloth); hints["pilesOnDock"] = MeshObj("Hint glut", mb, Vector3.zero, false); }
    { var mb = new MeshB(); for (int i = 0; i < 4; i++) { float x = -8 + i * 4.6f; mb.Quad(new Vector3(x - 1.2f, 1.0f, 5.2f), new Vector3(x + 1.2f, 1.0f, 5.2f), new Vector3(x + 1.2f, 0.85f, 7.4f), new Vector3(x - 1.2f, 0.85f, 7.4f), U(TCell.Cloth), true); } hints["emptyStalls"] = MeshObj("Hint shortage", mb, Vector3.zero, false); }
    { var mb = new MeshB(); for (int row = 0; row <= 4; row++) { float y = RowY(row) + 3.6f, z = RowZ(row) - 3.3f; mb.Cyl(new Vector3(-10, y, z), new Vector3(10, y, z), 0.01f, 0.01f, 3, U(TCell.Dark));
        for (int i = 0; i < 14; i++) Lantern(mb, new Vector3(-9.5f + i * 1.45f, y - 0.35f, z)); for (int i = 0; i < 10; i++) { float x = -9 + i * 2; mb.Tri(new Vector3(x, y, z), new Vector3(x + 0.7f, y, z), new Vector3(x + 0.35f, y - 0.5f, z), UVc(i % 2 == 0 ? TCell.AwnRed : TCell.Gold, .05f, .5f), UVc(TCell.Gold, .1f, .5f), UVc(TCell.Gold, .07f, .2f)); } }
      hints["bannersLanterns"] = MeshObj("Hint festival", mb, Vector3.zero, false); }
    { var g = new GameObject("Hint blockade"); g.transform.parent = town3D; for (int i = 0; i < 6; i++) { var s = new GameObject("Soldier"); s.transform.parent = g.transform; s.AddComponent<MeshFilter>().sharedMesh = folkMeshes[6]; s.AddComponent<MeshRenderer>().sharedMaterial = townMat; s.transform.position = new Vector3(-7 + i * 2.6f, 0, 0.8f); s.transform.rotation = Quaternion.Euler(0, 180, 0); } hints["soldiersOnQuay"] = g; }
    { var t = new GameObject("Hint foreign ship").transform; t.parent = town3D; Dhow.Build(t, T, true, "ForeignShip"); t.position = new Vector3(13, -0.1f, -9); t.rotation = Quaternion.Euler(0, 200, 0); t.localScale = Vector3.one * 1.05f; hints["foreignShipInHarbor"] = t.gameObject; }
    FreeModels.SpawnDistantTownsfolk(town3D); /* v0.6j KayKit distant only */
    foreach (var h in hints.Values) h.SetActive(false); }
  void ShowTownHints() { string h = townHint != null && port < townHint.Length ? townHint[port] : null; foreach (var kv in hints) kv.Value.SetActive(kv.Key == h); }

  // ---------------- animation + tutorial pointer ----------------
  RectTransform pointer; GameObject outline; int outlineFor = -99; MaterialPropertyBlock mpb;
  void TownAnimate() {
    float t = Time.time;
    foreach (var f in folk) { if (f.t == null || !f.t.gameObject.activeSelf) continue;
      if (f.speed > 0) { float L = Vector3.Distance(f.a, f.b), u = Mathf.PingPong((t + f.phase) * f.speed / Mathf.Max(0.1f, L), 1); var pos = Vector3.Lerp(f.a, f.b, u); var dir = (f.b - f.a) * (Mathf.Repeat((t + f.phase) * f.speed / Mathf.Max(0.1f, L), 2) < 1 ? 1 : -1);
        float step = (t + f.phase) * f.speed * (f.kind == 4 ? 7 : 5); f.t.position = pos + Vector3.up * Mathf.Abs(Mathf.Sin(step)) * (f.kind == 4 ? 0.12f : 0.05f); f.t.rotation = SafeRot.Look(-dir, f.t.rotation) /* gamedev_t8: folk standing still */ * Quaternion.Euler(0, 0, Mathf.Sin(step) * 3); }
      else { f.t.position = f.a + Vector3.up * Mathf.Sin((t + f.phase) * 1.7f) * 0.012f; f.t.rotation = Quaternion.Euler(0, 180 + Mathf.Sin((t + f.phase) * 0.6f) * 18, Mathf.Sin((t + f.phase) * 1.2f) * 1.5f); } }
    TutorialPointer(t);
  }
  void TutorialPointer(float t) {
    int target = TutorialTarget(); int bi = target >= 0 ? bldgs.FindIndex(b => b.tab == target) : -1;
    if (mpb == null) mpb = new MaterialPropertyBlock();
    for (int i = 0; i < bldgs.Count; i++) { var rd = bldgs[i].t.GetComponent<Renderer>(); if (!rd) continue; float k = i == bi ? 1f + 0.22f * (0.5f + 0.5f * Mathf.Sin(t * 2.0f)) : 1f; mpb.SetColor("_BaseColor", new Color(k * 1.04f, k * 0.97f, k * 0.86f)); rd.SetPropertyBlock(mpb); }
    if (outlineFor != bi) { if (outline) Destroy0(outline); outline = null; outlineFor = bi;
      /* inverted-hull outline dropped: the building meshes are open/two-sided, so it rendered as a solid blob; the warm pulse carries the highlight */ }

    if (signLayer == null) return; if (pointer == null || pointer.parent != signLayer) pointer = BuildPointer(signLayer);
    Vector2 at; bool show = false;
    if (bi >= 0) { var plaque = signs.FirstOrDefault(s => s.i == bi).r; if (plaque != null && plaque.gameObject.activeSelf) { at = plaque.anchoredPosition + new Vector2(0, plaque.sizeDelta.y + 56); show = true; pointer.anchoredPosition = at + new Vector2(0, Mathf.Sin(t * 3f) * 14); } }
    else if (target == -2 && chartBtn != null) { var cr = chartBtn; Vector3[] w = new Vector3[4]; cr.GetWorldCorners(w); RectTransformUtility.ScreenPointToLocalPointInRectangle(signLayer, (w[1] + w[2]) / 2, null, out var lp); pointer.anchoredPosition = lp - signLayer.rect.min + new Vector2(0, 14 + Mathf.Sin(t * 3f) * 14); show = true;
      float k = 1 + 0.03f * Mathf.Sin(t * 2.4f); cr.localScale = new Vector3(k, k, 1); }
    pointer.gameObject.SetActive(show); }
  public RectTransform chartBtn;
  /// brass pointer: a teardrop pin with a dark rim and a bright face, pointing down at the target
  RectTransform BuildPointer(Transform p) {
    var r = R(p, "TutorialPointer", 0, 0, 0, 0); r.pivot = new Vector2(0.5f, 0); r.sizeDelta = new Vector2(84, 120);
    var rim = R(r, "rim", 0.5f, 0, 0.5f, 0); rim.pivot = new Vector2(0.5f, 0); rim.sizeDelta = new Vector2(84, 66); rim.anchoredPosition = new Vector2(0, 0); var ri = rim.gameObject.AddComponent<Image>(); ri.sprite = Tri(); ri.color = UITheme.C("#5A3A16"); rim.localScale = new Vector3(1, -1, 1);
    var tip = R(r, "tip", 0.5f, 0, 0.5f, 0); tip.pivot = new Vector2(0.5f, 0); tip.sizeDelta = new Vector2(64, 50); tip.anchoredPosition = new Vector2(0, 6); var ti = tip.gameObject.AddComponent<Image>(); ti.sprite = Tri(); ti.color = UITheme.C("#D9AE5C"); tip.localScale = new Vector3(1, -1, 1);
    var head = R(r, "head", 0.5f, 0, 0.5f, 0); head.pivot = new Vector2(0.5f, 0); head.sizeDelta = new Vector2(84, 84); head.anchoredPosition = new Vector2(0, 52); var hi = head.gameObject.AddComponent<Image>(); hi.sprite = Dot(); hi.color = UITheme.C("#5A3A16");
    var face = R(head, "face", 0.5f, 0.5f, 0.5f, 0.5f); face.sizeDelta = new Vector2(64, 64); var fi = face.gameObject.AddComponent<Image>(); fi.sprite = Dot(); fi.color = UITheme.C("#E6BE6A");
    var gem = R(face, "gem", 0.5f, 0.5f, 0.5f, 0.5f); gem.sizeDelta = new Vector2(24, 24); var gi = gem.gameObject.AddComponent<Image>(); gi.sprite = Dot(); gi.color = UITheme.C("#8A5A12");
    foreach (var im in r.GetComponentsInChildren<Image>()) im.raycastTarget = false; return r; }
}
