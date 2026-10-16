using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Rnd = UnityEngine.Random;

[Serializable] public class KeeperLine { public string trigger, mood, text; }
[Serializable] public class KeeperBook { public KeeperLine[] banker, moneyLender, shipwright, bazaarMerchant, farida, innkeeper, priest, harbourmaster, salim; }

/// v0.6: tall scrolling 3D town with tappable buildings, keepers with mood faces, bank, money lender, shipwright, tavern, palace, chart-table map.
public partial class Game {
  // ---- persistent v0.6 state (saved in SaveData) ----
  public int difficulty = 1, fame, bankCoins, bankPort = -1, loanOwed, loanDueWeek, loanCap = 0, fightsFought; public float loanRate = 0; public int[] shipTier = new int[4]; public bool papersGiven;
  int ShipTierSum => shipTier.Sum();
  public int PirateTierNow => (fame >= S.combat.lateFame || ShipTierSum > 0) ? 1 : 0; // late pirates only once you have some fame or an upgraded ship

  // ---- town scene ----
  Transform town3D; readonly List<(Transform t, int tab, string name, float h)> bldgs = new List<(Transform, int, string, float)>();
  float townS = 0f, townSMin = 0f, townSMax = 40f; bool townDrag; Vector2 townDown; float townDownS; RectTransform signLayer; readonly List<(RectTransform r, int i)> signs = new List<(RectTransform, int)>();
  KeeperBook book; string keeperTrig = "greet"; int keeperTab = -99;
  static readonly string[] TabNames = { "Market", "Back Alley", "Warehouse", "Temple & Chandler", "Chart Table", "Bank", "Money Lender", "Shipwright", "Tavern", "Governor's Palace" };

  const int TA = 1280, TC = 256, TN = 5; Material townMat;
  static Rect TU(int cell) { int cx = cell % TN, cy = cell / TN; return new Rect((cx * TC + 2f) / TA, (cy * TC + 2f) / TA, (TC - 4f) / TA, (TC - 4f) / TA); }
  enum TCell { Stone, Plaster, Roof, AwnRed, AwnTeal, Gold, Rock, Wood, Rug, Dark, Marble, Paving, Sign, Green, Teak, Cloth, Whitewash, DarkTeak, Ochre, Planks, Puddle, Skin, Indigo, Rope, Sand }
  Texture2D PaintTownAtlas() {
    var tex = new Texture2D(TA, TA, TextureFormat.RGBA32, true); var px = new Color[TA * TA];
    Color H(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    float N(float x, float y) => Mathf.PerlinNoise(x, y);
    for (int cell = 0; cell < TN * TN; cell++) for (int y = 0; y < TC; y++) for (int x = 0; x < TC; x++) {
      float u = x / (float)TC, v = y / (float)TC; Color c;
      switch ((TCell)cell) {
        case TCell.Stone: { int row = (int)(v * 6); float off = row % 2 * 0.5f; bool joint = (v * 6 % 1) < 0.07f || ((u * 3 + off) % 1) < 0.04f; c = Color.Lerp(H("#C29A66"), H("#E2C08C"), N(u * 9 + row, v * 9)); if (joint) c = H("#8A6844"); break; }
        case TCell.Plaster: { c = Color.Lerp(H("#D9B886"), H("#F0DDB6"), N(u * 5, v * 5) * 0.8f + N(u * 23, v * 23) * 0.2f); float grime = Mathf.Clamp01(0.35f - v) * 0.9f * (0.6f + 0.4f * N(u * 14, 3)); c = Color.Lerp(c, H("#9C7A52"), grime); if (N(u * 40, v * 40) > 0.78f) c *= 0.93f; break; }
        case TCell.Roof: { bool g = (v * 8 % 1) < 0.15f; c = Color.Lerp(H("#9C4A2C"), H("#C0663E"), N(u * 12, v * 4)); if (g) c *= 0.7f; break; }
        case TCell.AwnRed: c = H("#D9CDB0") * (0.9f + 0.1f * N(u * 20, v * 20)); break; /* k4: plain canvas, no stripes; ApplyKeeperSkinK4 tints it per town (#3E4A6E / #D08A2E / #8FAE8C) */
        case TCell.AwnTeal: c = H("#2A8F8A") * (0.9f + 0.1f * N(u * 20, v * 20)); break; /* designer: plain trim teal, no stripes (skins retint per town) */
        case TCell.Gold: c = Color.Lerp(H("#B8862E"), H("#F2CC6A"), Mathf.Pow(N(u * 4, v * 4), 0.7f)); break;
        case TCell.Rock: c = Color.Lerp(H("#8A6A48"), H("#C7A27A"), N(u * 5, v * 5) * 0.7f + N(u * 17, v * 17) * 0.3f); break;
        case TCell.Wood: { float g = N(u * 3, v * 40); c = Color.Lerp(H("#5A3C24"), H("#8E6440"), g); if ((u * 5 % 1) < 0.05f) c *= 0.6f; break; }
        case TCell.Rug: { float d = Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f); c = d < 0.18f ? H("#E2B24C") : d < 0.3f ? H("#2E3478") : ((int)(d * 20) % 2 == 0 ? H("#8E2A26") : H("#B8463A")); if (u < 0.06f || u > 0.94f) c = H("#E8DCC0"); break; }
        case TCell.Dark: c = H("#2A1C14") * (0.8f + 0.3f * N(u * 8, v * 8)); break;
        case TCell.Marble: { bool flute = (u * 6 % 1) < 0.2f; c = Color.Lerp(H("#E2CFAE"), H("#F5EAD3"), N(u * 8, v * 8) * 0.7f); if (flute) c *= 0.86f; break; }
        case TCell.Paving: { bool j = (u * 4 % 1) < 0.05f || (v * 4 % 1) < 0.05f; c = Color.Lerp(H("#A88E6A"), H("#C6AD86"), N(u * 7, v * 7)); if (j) c *= 0.75f; break; }
        case TCell.Sign: c = H("#5A3A1E"); if (Mathf.Abs(u - 0.5f) > 0.42f || Mathf.Abs(v - 0.5f) > 0.42f) c = H("#C9A44C"); break;
        case TCell.Green: c = Color.Lerp(H("#3E6B2C"), H("#6E9A3E"), N(u * 10, v * 10)); break;
        case TCell.Teak: { float g = N(u * 2.5f, v * 26) * 0.7f + N(u * 9, v * 70) * 0.3f; c = Color.Lerp(H("#7A4524"), H("#B87A44"), g); if ((u * 4 % 1) < 0.03f) c *= 0.7f; break; }
        case TCell.Whitewash: c = Color.Lerp(H("#E9E2D2"), H("#FAF6EC"), N(u * 6, v * 6)) * (0.94f + 0.06f * N(u * 35, v * 35)); c = Color.Lerp(c, H("#B8A888"), Mathf.Clamp01(0.3f - v) * 1.2f); break;
        case TCell.DarkTeak: { float g = N(u * 2, v * 22) * 0.7f + N(u * 8, v * 60) * 0.3f; c = Color.Lerp(H("#3A2214"), H("#6A4022"), g); if ((u * 3 % 1) < 0.025f) c *= 0.6f; break; }
        case TCell.Ochre: { int row = (int)(v * 7); float off = row % 2 * 0.5f; bool joint = (v * 7 % 1) < 0.06f || ((u * 3.5f + off) % 1) < 0.035f; c = Color.Lerp(H("#B9823E"), H("#DDA75E"), N(u * 8 + row * 1.7f, v * 8)); if (joint) c = H("#7A5228"); break; }
        case TCell.Planks: { /* six planks of uneven width and tone, gaps and nail heads: reads as non-repeating when sub-rects are picked at random */
          float[] edges = { 0, 0.14f, 0.31f, 0.45f, 0.63f, 0.8f, 1f }; int k = 0; while (k < 5 && v > edges[k + 1]) k++; float pv = (v - edges[k]) / (edges[k + 1] - edges[k]);
          float tone = N(k * 7.3f, 1.3f); c = Color.Lerp(H("#6B4A2C"), H("#A27748"), tone) * (0.85f + 0.25f * N(u * 3 + k * 5, pv * 30)); if (pv < 0.07f || pv > 0.95f) c *= 0.45f;
          float seam = ((u + N(k, 3) * 0.7f) * 2) % 1; if (seam < 0.012f) c *= 0.5f; if (Mathf.Abs(seam - 0.04f) < 0.008f && Mathf.Abs(pv - 0.5f) < 0.1f) c = H("#3A3A3A"); break; }
        case TCell.Puddle: { float d = new Vector2(u - 0.5f, (v - 0.5f) * 1.3f).magnitude + (N(u * 6, v * 6) - 0.5f) * 0.18f; c = d < 0.36f ? Color.Lerp(H("#5E7486"), H("#9DB2BE"), N(u * 3, v * 9)) : H("#7E6A50"); break; }
        case TCell.Skin: c = Color.Lerp(H("#8A5A3A"), H("#A87048"), N(u * 4, v * 4)); break;
        case TCell.Indigo: c = Color.Lerp(H("#283A6A"), H("#3E5592"), N(u * 8, v * 8)); break;
        case TCell.Rope: c = Color.Lerp(H("#8E7550"), H("#C2A372"), ((u * 14 + v * 6) % 1)); break;
        case TCell.Sand: c = Color.Lerp(H("#C9AE82"), H("#E0C99E"), N(u * 9, v * 9)) * (0.92f + 0.08f * N(u * 50, v * 50)); break;
        default: c = Color.Lerp(H("#D9C7A0"), H("#EADBB8"), N(u * 10, v * 3)); break; }
      if (cell != (int)TCell.Dark && cell != (int)TCell.Puddle && cell != (int)TCell.Skin) { /* wear: grimy edges, dirt blotches, scuffs */
        float e = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v)); c *= 0.8f + 0.2f * Mathf.SmoothStep(0, 0.14f, e);
        float blot = N(u * 5 + cell * 3.1f, v * 5 + cell); if (blot > 0.62f) c = Color.Lerp(c, c * 0.72f, (blot - 0.62f) * 2.2f);
        if (N(u * 60 + cell, v * 60) > 0.82f) c *= 0.9f; }
      c.a = 1; px[(cell / TN * TC + y) * TA + cell % TN * TC + x] = c; }
    tex.SetPixels(px); tex.Apply(true); tex.wrapMode = TextureWrapMode.Clamp; PaintKitTextures(tex); return tex;
  }

  void BuildTown() {
    if (town3D != null) return; town3D = new GameObject("Town3D").transform; bldgs.Clear();
    var baseMat = Resources.Load<Material>("Mats/ShipHasan"); townMat = new Material(baseMat); townMat.SetTexture("_BaseMap", PaintTownAtlas()); townMat.mainTexture = townMat.GetTexture("_BaseMap");
    townMat.DisableKeyword("_NORMALMAP"); townMat.SetTexture("_BumpMap", null); townMat.SetColor("_BaseColor", Color.white); if (townMat.HasProperty("_Smoothness")) townMat.SetFloat("_Smoothness", 0.08f);
    { /* the sea scene's sun is switched off in town, so the town carries its own low warm sun from the sea side-left */ var sun = new GameObject("TownSun").AddComponent<Light>(); sun.type = LightType.Directional; sun.transform.parent = town3D;
      sun.transform.rotation = Quaternion.LookRotation(new Vector3(0.55f, -0.62f, 0.56f)); sun.color = new Color(1f, 0.86f, 0.64f); sun.intensity = 0.92f; sun.shadowBias = 0.04f; sun.shadowNormalBias = 0.3f; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.55f; }
    // sea in front of the quay, sky, light come from the sea scene (shared); town has its own water strip
    var wat = new GameObject("TownWater"); wat.transform.parent = town3D; wat.AddComponent<MeshFilter>().sharedMesh = Grid(70, 6f); var twm = new Material(waterMat) { name = "TownWater" }; twm.SetFloat("_Amp", 0.3f); wat.AddComponent<MeshRenderer>().sharedMaterial = twm; wat.transform.position = new Vector3(-210, -0.3f, -260); /* harbour chop only: open-sea swell (up to ~1 m) was washing over the quay and the townsfolk */
    float RowY(int r) => r * 3.2f; float RowZ(int r) => 6 + r * 9f;
    // cliff and terraces
    { var mb = new MeshB();
      for (int r = 0; r <= 5; r++) { float y = RowY(r), z = RowZ(r);
        mb.Box(new Vector3(0, y - 1.6f, z + 1), new Vector3(24, 3.2f + 0.1f, 9.6f), TU((int)TCell.Paving));                      // terrace floor
        mb.Box(new Vector3(0, y - 1.6f, z - 3.7f), new Vector3(24.2f, 3.3f, 0.6f), TU((int)TCell.Stone)); }                        // retaining wall
      { var bb = new MeshB(); /* k4: shared back blocks + side rock walls in their own object so TownLayoutsK4 can hide them per port */
      for (int i = 0; i < 9; i++) bb.Box(new Vector3(-16 + i * 4f, RowY(6) + 4 + (i % 3) * 3, RowZ(6) + 6), new Vector3(7, 16 + (i % 4) * 4, 8), Quaternion.Euler(0, i * 17, (i % 2) * 6), TU((int)(i % 2 == 0 ? TCell.Ochre : TCell.Plaster))); /* upper town in ochre/sandstone plaster, not grey */
      bb.Box(new Vector3(-13, 3, 22), new Vector3(4, 14, 40), TU((int)TCell.Rock)); bb.Box(new Vector3(13, 3, 22), new Vector3(4, 14, 40), TU((int)TCell.Rock));
        MeshObj(TownLayoutsK4.SharedBackName, bb, Vector3.zero, false); }
      // stairs between terraces
      for (int r = 0; r < 5; r++) for (int k = 0; k < 6; k++) mb.Box(new Vector3(0, RowY(r) + k * 0.53f + 0.27f, RowZ(r) + 4.0f + k * 0.85f), new Vector3(2.6f, 0.54f, 0.85f), TU((int)TCell.Paving));
      // quay and pier
      mb.Box(new Vector3(0, -0.6f, 0.5f), new Vector3(24, 1.2f, 4), TU((int)TCell.Stone));
      var pr = new System.Random(11); for (int k = 0; k < 18; k++) mb.Box(new Vector3(-6 + (float)(pr.NextDouble() - 0.5) * 0.06f, -0.2f, -1.5f - k * 0.5f), new Vector3(2.4f, 0.16f + (float)pr.NextDouble() * 0.04f, 0.46f), Quaternion.Euler(0, (float)(pr.NextDouble() - 0.5) * 2, 0), SubU(TCell.Planks, pr, 0.5f));
      for (int k = 0; k < 4; k++) mb.Cyl(new Vector3(-7.1f, -1.5f, -2 - k * 2.4f), new Vector3(-7.1f, 0.3f, -2 - k * 2.4f), 0.15f, 0.15f, 6, TU((int)TCell.Wood));
      // palms
      foreach (var p in new[] { new Vector3(-9, RowY(1), RowZ(1) - 2), new Vector3(9.5f, RowY(3), RowZ(3) - 2), new Vector3(-9.5f, RowY(4), RowZ(4) - 1) }) {
        mb.Cyl(p, p + new Vector3(0.4f, 4.5f, 0), 0.22f, 0.14f, 6, TU((int)TCell.Wood));
        for (int f = 0; f < 6; f++) { var top = p + new Vector3(0.4f, 4.5f, 0); var d = Quaternion.Euler(0, f * 60, 0) * Vector3.forward; mb.Tri(top, top + d * 2.4f + Vector3.down * 0.9f + Vector3.Cross(d, Vector3.up) * 0.5f, top + d * 2.4f + Vector3.down * 0.9f - Vector3.Cross(d, Vector3.up) * 0.5f, new Vector2(TU((int)TCell.Green).xMin, TU((int)TCell.Green).yMin), new Vector2(TU((int)TCell.Green).xMax, TU((int)TCell.Green).yMin), new Vector2(TU((int)TCell.Green).center.x, TU((int)TCell.Green).yMax)); } }
      DressTown(mb, RowY, RowZ);
      MeshObj("Terraces", mb, Vector3.zero, false); }
    // buildings (row, x)
    Bldg(0, "Market", 1, RowY(1), RowZ(1), -3.5f, Bazaar);
    Bldg(2, "Warehouse", 1, RowY(1), RowZ(1), 5f, Warehouse);
    Bldg(1, "Back Alley", 2, RowY(2), RowZ(2), -4.5f, Alley);
    Bldg(7, "Shipwright", 2, RowY(2), RowZ(2), 5f, Yard);
    Bldg(5, "Bank", 3, RowY(3), RowZ(3), -4f, BankB);
    Bldg(8, "Tavern", 3, RowY(3), RowZ(3), 4.5f, TavernB);
    Bldg(3, "Temple", 4, RowY(4), RowZ(4), -4.5f, TempleB);
    Bldg(6, "Money Lender", 4, RowY(4), RowZ(4), 4.5f, LenderB);
    Bldg(9, "Governor's Palace", 5, RowY(5), RowZ(5), 0f, PalaceB);
    BuildFolkAndProps(RowY, RowZ);
    { /* sky gradient dome and distant Makran hills behind the town */
      var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy0(sky.GetComponent<Collider>()); sky.transform.parent = town3D; sky.transform.localScale = Vector3.one * 1500; sky.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>("Mats/Sky");
      var hb = new MeshB(); var rnd = new System.Random(7);
      for (int i = 0; i < 14; i++) { float x = -150 + i * 23 + (float)rnd.NextDouble() * 10, h = 25 + (float)rnd.NextDouble() * 35, z = 150 + (float)rnd.NextDouble() * 60;
        hb.Lathe(new Vector3(x, -2, z), new[] { 28f + h * 0.4f, 20f, 11f, 3f }, h, 7, U(TCell.Rock)); }
      for (int i = 0; i < 8; i++) { float x = -60 + i * 17, h = 12 + (i * 37 % 10); hb.Lathe(new Vector3(x, 8, 95 + (i % 3) * 8), new[] { 16f, 11f, 5f, 1f }, h, 6, U(TCell.Rock)); }
      MeshObj("Hills", hb, Vector3.zero, false); }
    // Hasan's dhow at the pier
    var td = new GameObject("TownDhow").transform; td.parent = town3D; td.position = new Vector3(-8.7f, -0.1f, -5.5f); td.rotation = Quaternion.Euler(0, 178, 2); td.localScale = Vector3.one * 0.78f;
    if (FreeModels.SpawnKenneyDhow(td, "TownKenney") == null) Dhow.Build(td, T, false, "TownDhow");
    foreach (var (bp, ry, sc) in new[] { (new Vector3(7.5f, -0.15f, -5.2f), 95f, 0.45f), (new Vector3(11.5f, -0.15f, -8.5f), 60f, 0.4f) }) { var bt = new GameObject("Boat").transform; bt.parent = town3D; Dhow.Build(bt, T, false, "TownBoat"); bt.position = bp; bt.rotation = Quaternion.Euler(0, ry, 0); bt.localScale = Vector3.one * sc; }
    BuildLandmarksV06k(); /* v0.6k port landmarks */
    town3D.gameObject.SetActive(false);
  }
  GameObject MeshObj(string n, MeshB mb, Vector3 pos, bool collider) {
    var g = new GameObject(n); g.transform.parent = town3D; g.transform.position = pos; var m = mb.Build(); g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = townMat;
    if (collider) { var bc = g.AddComponent<BoxCollider>(); bc.center = m.bounds.center; bc.size = m.bounds.size; } return g; }
  void Bldg(int tab, string name, int row, float y, float z, float x, Func<MeshB, float> build) {
    var mb = new MeshB(); float h = build(mb); var g = MeshObj(name, mb, new Vector3(x, y, z), true); bldgs.Add((g.transform, tab, name, h)); }
  static Rect U(TCell c) => TU((int)c);
  static Rect SubU(TCell c, System.Random r, float f) { var u = U(c); float w = u.width * f, h = u.height * f; return new Rect(u.xMin + (float)r.NextDouble() * (u.width - w), u.yMin + (float)r.NextDouble() * (u.height - h), w, h); }
  static void Person(MeshB mb, Vector3 p, System.Random r) { TCell[] robe = { TCell.Cloth, TCell.Indigo, TCell.AwnRed, TCell.Whitewash, TCell.Teak, TCell.AwnTeal, TCell.Sand }; TCell[] wrap = { TCell.Whitewash, TCell.Cloth, TCell.Gold, TCell.AwnRed, TCell.Indigo };
    float s = 0.85f + (float)r.NextDouble() * 0.25f; var rc = robe[r.Next(robe.Length)];
    mb.Lathe(p, new[] { 0.24f * s, 0.21f * s, 0.18f * s, 0.2f * s, 0.12f * s }, 1.12f * s, 7, U(rc)); /* robe and shoulders */
    for (int k = -1; k <= 1; k += 2) mb.Cyl(p + new Vector3(k * 0.2f * s, 1.0f * s, 0), p + new Vector3(k * 0.26f * s, 0.55f * s, -0.08f * s), 0.06f * s, 0.05f * s, 4, U(rc));
    mb.Lathe(p + Vector3.up * 1.1f * s, new[] { 0.06f * s, 0.11f * s, 0.12f * s, 0.09f * s }, 0.26f * s, 7, U(TCell.Skin));
    if (r.NextDouble() < 0.8) mb.Lathe(p + Vector3.up * 1.3f * s, new[] { 0.12f * s, 0.15f * s, 0.12f * s, 0.04f * s }, 0.16f * s, 7, U(wrap[r.Next(wrap.Length)])); }
  static void RopeCoil(MeshB mb, Vector3 p) { mb.Lathe(p, new[] { 0.32f, 0.36f, 0.3f }, 0.14f, 10, U(TCell.Rope)); mb.Lathe(p + Vector3.up * 0.14f, new[] { 0.2f, 0.12f, 0.02f }, 0.03f, 8, U(TCell.Rope)); }
  /* worn paving laid as many tiles with random sub-rects, puddles, the sandy quay foot, and street life: people, crates, sacks, barrels, rope */
  void DressTown(MeshB mb, Func<int, float> RowY, Func<int, float> RowZ) {
    var r = new System.Random(5);
    for (int row = 0; row <= 5; row++) { float y = RowY(row) + 0.012f, z0 = RowZ(row) - 3.4f;
      for (int ix = 0; ix < 12; ix++) for (int iz = 0; iz < 5; iz++) { float x = -12 + ix * 2, z = z0 + iz * 1.9f; var uv = SubU(TCell.Paving, r, 0.45f); float lift = (float)r.NextDouble() * 0.015f;
        mb.Quad(new Vector3(x, y + lift, z), new Vector3(x + 2, y + lift, z), new Vector3(x + 2, y + lift, z + 1.9f), new Vector3(x, y + lift, z + 1.9f), uv); }
      for (int k = 0; k < 3; k++) { float x = -9 + (float)r.NextDouble() * 18, z = z0 + 0.2f + (float)r.NextDouble() * 0.5f, w = 0.7f + (float)r.NextDouble() * 0.8f; mb.Quad(new Vector3(x, y + 0.03f, z), new Vector3(x + w, y + 0.03f, z), new Vector3(x + w, y + 0.03f, z + w * 0.6f), new Vector3(x, y + 0.03f, z + w * 0.6f), U(TCell.Puddle)); } }
    { /* quay: worn planked deck strip, then a sandy, rocky foot that slips under the water so there is no hard seam */
      for (int k = 0; k < 40; k++) { float x = -12 + k * 0.6f; mb.Quad(new Vector3(x, 0.013f, -1.5f), new Vector3(x + 0.6f, 0.013f, -1.5f), new Vector3(x + 0.6f, 0.013f, 0.3f), new Vector3(x, 0.013f, 0.3f), SubU(TCell.Planks, r, 0.4f)); }
      mb.Quad(new Vector3(-30, -1.6f, -6.5f), new Vector3(30, -1.6f, -6.5f), new Vector3(30, -0.9f, -1.6f), new Vector3(-30, -0.9f, -1.6f), U(TCell.Sand));
      for (int k = 0; k < 36; k++) { float x = -16 + k * 0.95f + (float)r.NextDouble() * 0.5f; if (x > -7.6f && x < -4.4f) continue; float sz = 0.5f + (float)r.NextDouble() * 0.7f;
        mb.Box(new Vector3(x, -0.85f, -1.9f - (float)r.NextDouble() * 1.4f), new Vector3(sz, sz * 0.7f, sz), Quaternion.Euler((float)r.NextDouble() * 30, (float)r.NextDouble() * 90, (float)r.NextDouble() * 30), U(TCell.Rock)); } }
    /* harbour-front stalls on the quay terrace */
    TCell[] aw = { TCell.AwnTeal, TCell.AwnRed, TCell.AwnRed, TCell.AwnTeal };
    float[] sx = { -8.6f, -5.0f, 3.4f, 7.6f }; /* left stall pulled in onto the paving */
    for (int i = 0; i < 4; i++) { float x = sx[i], z = 3.6f + (i % 2) * 0.8f; var m2 = new MeshB(); /* on the paved quay, clear of the water */
      BigAwning(mb, x - 1.4f, x + 1.4f, 2.3f, z + 0.9f, 1.9f, z - 1.0f, aw[i]); foreach (var px in new[] { x - 1.3f, x + 1.3f }) mb.Cyl(new Vector3(px, 0, z + 0.9f), new Vector3(px, 2.3f, z + 0.9f), 0.06f, 0.06f, 5, U(TCell.Teak));
      mb.Box(new Vector3(x, 0.42f, z), new Vector3(2.2f, 0.84f, 0.8f), U(TCell.Teak)); for (int j = 0; j < 4; j++) Sack(mb, new Vector3(x - 0.8f + j * 0.53f, 0.84f, z), j % 2 == 0 ? TCell.Gold : TCell.Roof, 0.6f);
      Pot(mb, new Vector3(x + 1.6f, 0, z - 0.4f), 0.9f); Person(mb, new Vector3(x + 0.3f, 0, z + 0.5f), r); }
    var spots = new List<Vector3>();
    for (int k = 0; k < 16; k++) spots.Add(new Vector3(-10 + (float)r.NextDouble() * 20, 0, 0.6f + (float)r.NextDouble() * 7.5f));
    for (int row = 1; row <= 5; row++) for (int k = 0; k < 3; k++) spots.Add(new Vector3((k - 1) * 1.6f + (float)r.NextDouble() * 0.6f, RowY(row), RowZ(row) - 3.55f + (float)r.NextDouble() * 0.3f));
    foreach (var sp in spots) { var q = sp; if (Mathf.Abs(q.x + 6) < 1.4f && q.z < 0.4f) q.x += 3; Person(mb, q, r); }
    for (int k = 0; k < 18; k++) { var p = new Vector3(-11 + (float)r.NextDouble() * 22, 0, 0.5f + (float)r.NextDouble() * 7.5f); if (Mathf.Abs(p.x + 6) < 1.5f && p.z < 0.5f) continue; int kind = r.Next(4);
      if (kind == 0) { Crate(mb, p, 0.55f + (float)r.NextDouble() * 0.2f, r.Next(90)); if (r.NextDouble() < 0.5) Crate(mb, p + Vector3.up * 0.6f, 0.5f, r.Next(90)); }
      else if (kind == 1) { Sack(mb, p, TCell.Cloth, 0.9f); Sack(mb, p + new Vector3(0.45f, 0, 0.1f), TCell.Cloth, 0.85f); } else if (kind == 2) Barrel(mb, p, 0.8f); else RopeCoil(mb, p); }
    foreach (var bx in new[] { -11.5f, -2.5f, 3.5f, 10.5f }) { mb.Cyl(new Vector3(bx, 0, -1.3f), new Vector3(bx, 0.55f, -1.3f), 0.14f, 0.12f, 6, U(TCell.Dark)); RopeCoil(mb, new Vector3(bx + 0.45f, 0, -1.0f)); }
  }
  // ---------- shared pieces ----------
  static Vector2 UVc(TCell c, float fx, float fy) { var r = U(c); return new Vector2(Mathf.Lerp(r.xMin, r.xMax, fx), Mathf.Lerp(r.yMin, r.yMax, fy)); }
  /* round-topped opening (door, arcade) set on the face z = zf, facing -z */
  static void Arch(MeshB mb, float x, float y0, float zf, float w, float h, TCell c = TCell.Dark) {
    float rr = w / 2, hs = h - rr; mb.Box(new Vector3(x, y0 + hs / 2, zf), new Vector3(w, hs, 0.06f), U(c));
    var ctr = new Vector3(x, y0 + hs, zf - 0.035f); int n = 8;
    for (int i = 0; i < n; i++) { float a0 = Mathf.PI * i / n, a1 = Mathf.PI * (i + 1) / n;
      mb.Tri(ctr, ctr + new Vector3(Mathf.Cos(a1) * rr, Mathf.Sin(a1) * rr, 0), ctr + new Vector3(Mathf.Cos(a0) * rr, Mathf.Sin(a0) * rr, 0), UVc(c, .5f, .2f), UVc(c, .2f, .8f), UVc(c, .8f, .8f)); } }
  /* frame of teak around an opening */
  static void Frame(MeshB mb, float x, float y0, float zf, float w, float h, float t = 0.14f) {
    mb.Box(new Vector3(x - w / 2 - t / 2, y0 + h / 2, zf), new Vector3(t, h, 0.16f), U(TCell.Teak)); mb.Box(new Vector3(x + w / 2 + t / 2, y0 + h / 2, zf), new Vector3(t, h, 0.16f), U(TCell.Teak));
    mb.Box(new Vector3(x, y0 + h + t / 2, zf), new Vector3(w + 2 * t, t, 0.18f), U(TCell.Teak)); }
  static void Window(MeshB mb, float x, float y, float zf, float w = 0.6f, float h = 0.8f) { mb.Box(new Vector3(x, y, zf), new Vector3(w, h, 0.05f), U(TCell.Dark)); Frame(mb, x, y - h / 2, zf - 0.02f, w, h, 0.1f);
    mb.Box(new Vector3(x, y - h / 2 - 0.06f, zf - 0.1f), new Vector3(w + 0.3f, 0.1f, 0.25f), U(TCell.Stone)); }
  static void Lantern(MeshB mb, Vector3 p) { mb.Cyl(p + Vector3.up * 0.45f, p + Vector3.up * 0.25f, 0.02f, 0.02f, 3, U(TCell.Dark)); mb.Lathe(p - Vector3.up * 0.05f, new[] { 0.06f, 0.13f, 0.13f, 0.05f }, 0.3f, 6, U(TCell.Gold)); }
  static void Sack(MeshB mb, Vector3 p, TCell spice, float s = 1f) { mb.Lathe(p, new[] { 0.24f * s, 0.3f * s, 0.29f * s, 0.24f * s }, 0.5f * s, 8, U(TCell.Cloth)); mb.Lathe(p + Vector3.up * 0.5f * s, new[] { 0.24f * s, 0.16f * s, 0.02f }, 0.16f * s, 8, U(spice)); }
  static void Pot(MeshB mb, Vector3 p, float s = 1f) => mb.Lathe(p, new[] { 0.14f * s, 0.26f * s, 0.3f * s, 0.24f * s, 0.12f * s, 0.15f * s }, 0.65f * s, 8, U(TCell.Roof));
  static void Barrel(MeshB mb, Vector3 p, float s = 1f) => mb.Lathe(p, new[] { 0.26f * s, 0.32f * s, 0.34f * s, 0.32f * s, 0.26f * s }, 0.8f * s, 10, U(TCell.Wood));
  static void Crate(MeshB mb, Vector3 p, float s, float rot = 0) { var q = Quaternion.Euler(0, rot, 0); mb.Box(p + Vector3.up * s / 2, Vector3.one * s, q, U(TCell.Wood));
    mb.Box(p + Vector3.up * s / 2, new Vector3(s * 1.02f, s * 0.12f, s * 1.02f), q, U(TCell.Teak)); }
  /* flat-roofed plaster block with a parapet lip and grime base */
  static void Block(MeshB mb, Vector3 c, Vector3 s, TCell wall = TCell.Plaster) { mb.Box(c + Vector3.up * s.y / 2, s, U(wall));
    mb.Box(c + Vector3.up * (s.y + 0.1f), new Vector3(s.x + 0.2f, 0.2f, s.z + 0.2f), U(TCell.Stone)); mb.Box(c + Vector3.up * 0.2f, new Vector3(s.x + 0.06f, 0.4f, s.z + 0.06f), U(TCell.Stone)); }
  static void Gable(MeshB mb, Vector3 c, float w, float d, float rise, TCell roof) { float h = c.y; var r = U(roof);
    mb.Quad(new Vector3(c.x - w / 2, h, c.z - d / 2), new Vector3(c.x + w / 2, h, c.z - d / 2), new Vector3(c.x + w / 2, h + rise, c.z), new Vector3(c.x - w / 2, h + rise, c.z), r, true);
    mb.Quad(new Vector3(c.x - w / 2, h + rise, c.z), new Vector3(c.x + w / 2, h + rise, c.z), new Vector3(c.x + w / 2, h, c.z + d / 2), new Vector3(c.x - w / 2, h, c.z + d / 2), r, true);
    for (int i = -1; i <= 1; i += 2) mb.Tri(new Vector3(c.x + i * (w / 2 - 0.15f), h, c.z - d / 2 + 0.1f), new Vector3(c.x + i * (w / 2 - 0.15f), h + rise - 0.05f, c.z), new Vector3(c.x + i * (w / 2 - 0.15f), h, c.z + d / 2 - 0.1f), UVc(TCell.Plaster, 0, 0), UVc(TCell.Plaster, .5f, 1), UVc(TCell.Plaster, 1, 0)); }
  /* plain awning on posts (cloth canopy), with a scalloped valance in trim teal */
  static void BigAwning(MeshB mb, float x0, float x1, float yWall, float zWall, float yFront, float zFront, TCell cloth, bool posts = true) {
    mb.Quad(new Vector3(x0, yFront, zFront), new Vector3(x1, yFront, zFront), new Vector3(x1, yWall, zWall), new Vector3(x0, yWall, zWall), U(cloth), true);
    int n = Mathf.Max(3, Mathf.RoundToInt((x1 - x0) / 0.45f)); for (int i = 0; i < n; i++) { float a = Mathf.Lerp(x0, x1, i / (float)n), b = Mathf.Lerp(x0, x1, (i + 1f) / n);
      mb.Tri(new Vector3(a, yFront, zFront - 0.01f), new Vector3(b, yFront, zFront - 0.01f), new Vector3((a + b) / 2, yFront - 0.28f, zFront - 0.03f), UVc(TCell.AwnTeal, 0.05f, .5f), UVc(TCell.AwnTeal, 0.1f, .5f), UVc(TCell.AwnTeal, 0.07f, .2f)); } /* designer: valance all trim colour — alternating cloth/teal scallops read as stripes */
    if (posts) foreach (var px in new[] { x0 + 0.1f, x1 - 0.1f, (x0 + x1) / 2 }) mb.Cyl(new Vector3(px, 0, zFront + 0.05f), new Vector3(px, yFront + 0.05f, zFront + 0.05f), 0.07f, 0.06f, 6, U(TCell.Teak)); }
  static TCell merlonCell = TCell.Stone; static void Merlons(MeshB mb, float x0, float x1, float y, float z, float depth, float step = 0.7f) { for (float x = x0 + step / 4; x < x1 - step / 4; x += step) mb.Box(new Vector3(x + step / 4, y + 0.25f, z), new Vector3(step / 2, 0.5f, depth), U(merlonCell)); }
  static void Flag(MeshB mb, Vector3 top) { mb.Cyl(top, top + Vector3.up * 1.4f, 0.03f, 0.03f, 4, U(TCell.Dark)); var a = top + Vector3.up * 1.4f;
    mb.Tri(a, a + new Vector3(0.75f, -0.18f, 0), a + Vector3.down * 0.42f, UVc(TCell.AwnRed, .05f, .2f), UVc(TCell.AwnRed, .1f, .5f), UVc(TCell.AwnRed, .05f, .8f)); }
  static void Steps(MeshB mb, float w, float zFront, int n, float rise = 0.18f, float run = 0.35f, TCell c = TCell.Stone) { for (int k = 0; k < n; k++) mb.Box(new Vector3(0, rise * (n - k) / 2, zFront + k * run), new Vector3(w - k * 0.1f, rise * (n - k), run), U(c)); }

  // ---------- the nine Gwadar buildings (after gwadar_buildings_sheet.jpg); front faces -z ----------
  float Bazaar(MeshB mb) { // stone-and-plaster stall house, big red/cream striped awning on teak posts, spice sacks, pots and lanterns
    Block(mb, new Vector3(0, 0, 0.6f), new Vector3(5.2f, 3.4f, 3.2f));
    mb.Box(new Vector3(0, 3.65f, -0.95f), new Vector3(5.4f, 0.35f, 0.25f), U(TCell.Teak)); for (int i = 0; i < 9; i++) mb.Box(new Vector3(-2.4f + i * 0.6f, 3.65f, -1.15f), new Vector3(0.14f, 0.2f, 0.3f), U(TCell.Teak));
    Window(mb, -1.6f, 2.6f, -1.01f); Window(mb, 1.6f, 2.6f, -1.01f); Arch(mb, 0, 0, -1.01f, 1.3f, 2.2f); Frame(mb, 0, 0, -1.04f, 1.3f, 1.6f);
    BigAwning(mb, -2.9f, 2.9f, 2.9f, -1.0f, 2.05f, -3.3f, TCell.AwnRed);
    mb.Box(new Vector3(0, 0.45f, -2.4f), new Vector3(4.4f, 0.9f, 0.9f), U(TCell.Teak)); mb.Box(new Vector3(0, 0.92f, -2.4f), new Vector3(4.6f, 0.06f, 1.0f), U(TCell.Wood));
    TCell[] sp = { TCell.Gold, TCell.Roof, TCell.Green, TCell.AwnRed, TCell.Gold, TCell.Cloth };
    for (int i = 0; i < 6; i++) Sack(mb, new Vector3(-1.9f + i * 0.76f, 0.95f, -2.4f), sp[i], 0.8f);
    for (int i = 0; i < 5; i++) Sack(mb, new Vector3(-2.2f + i * 1.1f, 0, -3.15f), sp[(i + 2) % 6], 0.95f);
    Pot(mb, new Vector3(-2.75f, 0, -1.6f), 1.2f); Pot(mb, new Vector3(2.75f, 0, -1.7f), 1.0f); Pot(mb, new Vector3(2.45f, 0, -1.3f), 0.8f);
    Lantern(mb, new Vector3(-2.8f, 1.6f, -3.25f)); Lantern(mb, new Vector3(2.8f, 1.6f, -3.25f)); return 4.4f; }
  float Warehouse(MeshB mb) { // stone godown with a tiled gable, double teak doors and a timber cargo crane
    mb.Box(new Vector3(0, 1.8f, 0), new Vector3(5.2f, 3.6f, 4), U(TCell.Stone)); mb.Box(new Vector3(0, 0.2f, 0), new Vector3(5.3f, 0.4f, 4.1f), U(TCell.Rock));
    Gable(mb, new Vector3(0, 3.6f, 0), 5.7f, 4.6f, 1.3f, TCell.Roof);
    mb.Box(new Vector3(-0.6f, 1.25f, -2.03f), new Vector3(1.15f, 2.5f, 0.1f), U(TCell.Teak)); mb.Box(new Vector3(0.6f, 1.25f, -2.03f), new Vector3(1.15f, 2.5f, 0.1f), U(TCell.Teak));
    for (int i = -1; i <= 1; i += 2) { mb.Box(new Vector3(i * 0.6f, 1.25f, -2.1f), new Vector3(0.12f, 2.6f, 0.05f), Quaternion.Euler(0, 0, 25 * i), U(TCell.Wood)); mb.Box(new Vector3(i * 0.6f, 0.55f, -2.1f), new Vector3(1.1f, 0.1f, 0.05f), U(TCell.Wood)); mb.Box(new Vector3(i * 0.6f, 2.0f, -2.1f), new Vector3(1.1f, 0.1f, 0.05f), U(TCell.Wood)); }
    Frame(mb, 0, 0, -2.08f, 2.4f, 2.55f, 0.2f); Window(mb, -1.9f, 2.9f, -2.01f, 0.5f, 0.5f);
    var cp = new Vector3(3.1f, 0, -1.6f); mb.Cyl(cp, cp + Vector3.up * 5.2f, 0.16f, 0.13f, 6, U(TCell.Wood)); mb.Cyl(cp + Vector3.up * 4.9f, cp + new Vector3(-0.2f, 5.4f, -2.2f), 0.1f, 0.08f, 5, U(TCell.Wood));
    mb.Cyl(cp + Vector3.up * 2.4f, cp + new Vector3(-0.1f, 4.95f, -1.4f), 0.07f, 0.07f, 5, U(TCell.Wood)); var tip = cp + new Vector3(-0.2f, 5.4f, -2.2f);
    mb.Cyl(tip, tip + Vector3.down * 3.1f, 0.025f, 0.025f, 3, U(TCell.Cloth)); Crate(mb, tip + Vector3.down * 3.6f, 0.55f, 15); mb.Cyl(cp + Vector3.up * 1.2f, cp + new Vector3(0.45f, 1.2f, 0), 0.25f, 0.25f, 8, U(TCell.Teak));
    for (int k = 0; k < 6; k++) Crate(mb, new Vector3(-2.4f + (k % 3) * 0.75f, (k / 3) * 0.7f, -2.7f), 0.7f, k * 9); Sack(mb, new Vector3(1.6f, 0, -2.6f), TCell.Cloth); Sack(mb, new Vector3(2.1f, 0, -2.9f), TCell.Cloth); Barrel(mb, new Vector3(1.5f, 0, -3.3f));
    return 5.6f; }
  float Alley(MeshB mb) { // a narrow lane between two tall houses, washing lines of dyed cloth, lanterns and a teak overhang
    for (int s = -1; s <= 1; s += 2) { float x = s * 1.75f; Block(mb, new Vector3(x, 0, 0.3f), new Vector3(1.9f, 4.6f + (s > 0 ? 0.6f : 0), 3.6f), s < 0 ? TCell.Plaster : TCell.Stone);
      Window(mb, x, 3.4f, -1.52f, 0.5f, 0.7f); Arch(mb, x + s * 0.2f, 0, -1.52f, 0.8f, 1.8f); Frame(mb, x + s * 0.2f, 0, -1.55f, 0.8f, 1.4f, 0.1f);
      mb.Box(new Vector3(x - s * 0.4f, 2.5f, -1.75f), new Vector3(1.1f, 0.1f, 0.6f), U(TCell.Teak)); mb.Box(new Vector3(x - s * 0.9f, 2.3f, -1.7f), new Vector3(0.1f, 0.5f, 0.1f), U(TCell.Teak)); Lantern(mb, new Vector3(x - s * 0.82f, 1.95f, -1.75f)); }
    mb.Box(new Vector3(0, 0.02f, 0.3f), new Vector3(1.6f, 0.04f, 3.6f), U(TCell.Paving)); mb.Box(new Vector3(0, 2.4f, 1.9f), new Vector3(1.6f, 4.8f, 0.2f), U(TCell.Dark));
    TCell[] cl = { TCell.AwnRed, TCell.AwnTeal, TCell.Gold, TCell.Rug, TCell.Cloth };
    for (int line = 0; line < 2; line++) { float y = 3.9f + line * 0.7f, z = -0.9f + line * 1.4f; mb.Cyl(new Vector3(-0.85f, y, z), new Vector3(0.85f, y, z), 0.015f, 0.015f, 3, U(TCell.Dark));
      for (int i = 0; i < 4; i++) { float a = -0.75f + i * 0.4f; var cc = cl[(i + line * 2) % cl.Length]; mb.Quad(new Vector3(a, y - 0.6f, z), new Vector3(a + 0.32f, y - 0.6f, z), new Vector3(a + 0.32f, y, z), new Vector3(a, y, z), new Rect(UVc(cc, 0.1f, 0.1f), U(cc).size * 0.1f), true); } }
    Pot(mb, new Vector3(-0.55f, 0, -1.3f), 0.8f); Barrel(mb, new Vector3(0.6f, 0, -1.2f), 0.7f); Crate(mb, new Vector3(0.45f, 0, -0.4f), 0.45f, 20); return 5.6f; }
  float Yard(MeshB mb) { // dhow under construction on a cradle: planked bottom, open ribs above, scaffold, ladder, shade roof and shear-leg crane
    mb.Box(new Vector3(0, 0.12f, 0), new Vector3(6.6f, 0.24f, 4.6f), U(TCell.Wood)); for (int i = 0; i < 5; i++) mb.Box(new Vector3(-2.6f + i * 1.3f, 0.32f, 0), new Vector3(0.3f, 0.2f, 4.2f), U(TCell.Teak));
    int N = 11; float L = 5.6f; var mid = new List<Vector3[]>();
    for (int i = 0; i <= N; i++) { float t = i / (float)N; float x = -L / 2 + t * L; float w = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Lerp(0.12f, 0.95f, t)), 0.7f) * 1.25f; float sheer = 2.3f + Mathf.Pow(t, 3) * 1.0f + (1 - t) * 0.35f;
      mid.Add(new[] { new Vector3(x, sheer, -w), new Vector3(x, 1.25f, -w * 0.85f), new Vector3(x, 0.55f, 0), new Vector3(x, 1.25f, w * 0.85f), new Vector3(x, sheer, w) }); }
    for (int i = 0; i < N; i++) for (int k = 1; k < 3; k++) mb.Quad(mid[i][k], mid[i + 1][k], mid[i + 1][k + 1], mid[i][k + 1], U(TCell.Teak), true); // planked lower hull
    for (int i = 0; i < N; i++) mb.Quad(mid[i][1], mid[i + 1][1], mid[i + 1][2], mid[i][2], U(TCell.Teak), true);
    for (int i = 0; i <= N; i++) for (int k = 0; k < 2; k++) { mb.Cyl(mid[i][k == 0 ? 0 : 4], mid[i][k == 0 ? 1 : 3], 0.06f, 0.06f, 4, U(TCell.Wood)); }
    for (int i = 0; i < N; i++) { mb.Cyl(mid[i][0], mid[i + 1][0], 0.07f, 0.07f, 4, U(TCell.Wood)); mb.Cyl(mid[i][4], mid[i + 1][4], 0.07f, 0.07f, 4, U(TCell.Wood)); }
    mb.Cyl(mid[N][2], mid[N][2] + new Vector3(0.7f, 3.0f, 0), 0.09f, 0.07f, 5, U(TCell.Teak)); mb.Cyl(mid[0][2], mid[0][2] + new Vector3(-0.3f, 2.3f, 0), 0.09f, 0.08f, 5, U(TCell.Teak));
    for (int i = 0; i < 4; i++) { float x = -2.4f + i * 1.6f; mb.Box(new Vector3(x, 0.55f, 0), new Vector3(0.25f, 0.5f, 1.6f), U(TCell.Wood)); }
    /* scaffold only on the far side so the hull reads from the street; two light props on the near side */
    for (int i = 0; i < 4; i++) { var b = new Vector3(-2.7f + i * 1.8f, 0.2f, 1.9f); mb.Cyl(b, b + Vector3.up * 3.6f, 0.06f, 0.06f, 4, U(TCell.Wood)); }
    mb.Box(new Vector3(0, 1.9f, 1.9f), new Vector3(5.8f, 0.07f, 0.45f), U(TCell.Wood)); mb.Cyl(new Vector3(-2.7f, 3.6f, 1.9f), new Vector3(2.7f, 3.6f, 1.9f), 0.05f, 0.05f, 4, U(TCell.Wood));
    foreach (var px in new[] { -1.2f, 1.4f }) mb.Cyl(new Vector3(px, 0.25f, -1.9f), new Vector3(px, 1.35f, -1.05f), 0.06f, 0.06f, 4, U(TCell.Wood));
    mb.Quad(new Vector3(1.4f, 3.65f, 0.2f), new Vector3(2.9f, 3.65f, 0.2f), new Vector3(2.9f, 3.9f, 1.9f), new Vector3(1.4f, 3.9f, 1.9f), U(TCell.Cloth), true);
    for (int r = 0; r < 7; r++) { float y = 0.2f + r * 0.3f; mb.Box(new Vector3(-1.2f, y, -2.25f - r * 0.02f), new Vector3(0.5f, 0.05f, 0.06f), U(TCell.Wood)); }
    mb.Cyl(new Vector3(-1.45f, 0, -2.3f), new Vector3(-1.45f, 2.1f, -2.1f), 0.04f, 0.04f, 4, U(TCell.Wood)); mb.Cyl(new Vector3(-0.95f, 0, -2.3f), new Vector3(-0.95f, 2.1f, -2.1f), 0.04f, 0.04f, 4, U(TCell.Wood));
    var apex = new Vector3(-3.0f, 4.8f, -0.6f); mb.Cyl(new Vector3(-3.6f, 0.2f, -1.6f), apex, 0.09f, 0.07f, 5, U(TCell.Wood)); mb.Cyl(new Vector3(-3.6f, 0.2f, 0.4f), apex, 0.09f, 0.07f, 5, U(TCell.Wood));
    mb.Cyl(apex, apex + Vector3.down * 2.6f, 0.02f, 0.02f, 3, U(TCell.Cloth)); mb.Cyl(apex + Vector3.down * 3.2f, apex + new Vector3(0.2f, -3.2f, 0), 0.09f, 0.09f, 5, U(TCell.Teak));
    for (int k = 0; k < 5; k++) mb.Cyl(new Vector3(2.2f, 0.32f + (k / 3) * 0.2f, -2.75f + (k % 3) * 0.2f), new Vector3(3.4f, 0.32f + (k / 3) * 0.2f, -2.75f + (k % 3) * 0.2f), 0.1f, 0.1f, 6, U(TCell.Teak));
    return 5.4f; }
  float BankB(MeshB mb) { // classical sandstone bank: stepped plinth, four columns with capitals, entablature and pediment, gilt scales
    Steps(mb, 5.8f, -2.3f, 3, 0.18f, 0.32f, TCell.Whitewash); mb.Box(new Vector3(0, 0.3f, 0.3f), new Vector3(5.8f, 0.6f, 3.8f), U(TCell.Whitewash));
    mb.Box(new Vector3(0, 2.3f, 0.75f), new Vector3(4.6f, 3.4f, 2.6f), U(TCell.Plaster)); Arch(mb, 0, 0.6f, -0.57f, 1.1f, 2.2f); Frame(mb, 0, 0.6f, -0.6f, 1.1f, 1.65f, 0.12f);
    Window(mb, -1.55f, 2.3f, -0.57f, 0.55f, 1.0f); Window(mb, 1.55f, 2.3f, -0.57f, 0.55f, 1.0f);
    for (int i = 0; i < 4; i++) { float x = -2.1f + i * 1.4f; var b = new Vector3(x, 0.6f, -1.3f); mb.Box(b + Vector3.up * 0.1f, new Vector3(0.62f, 0.2f, 0.62f), U(TCell.Whitewash));
      mb.Cyl(b + Vector3.up * 0.2f, b + Vector3.up * 3.15f, 0.25f, 0.21f, 10, U(TCell.Whitewash)); mb.Lathe(b + Vector3.up * 3.15f, new[] { 0.22f, 0.3f, 0.36f }, 0.22f, 10, U(TCell.Whitewash)); mb.Box(b + Vector3.up * 3.42f, new Vector3(0.72f, 0.1f, 0.72f), U(TCell.Whitewash)); }
    mb.Box(new Vector3(0, 4.2f, 0.25f), new Vector3(5.6f, 0.5f, 3.4f), U(TCell.Whitewash)); mb.Box(new Vector3(0, 4.5f, 0.25f), new Vector3(5.9f, 0.12f, 3.6f), U(TCell.Stone));
    for (int i = 0; i < 12; i++) mb.Box(new Vector3(-2.6f + i * 0.47f, 4.02f, -1.47f), new Vector3(0.14f, 0.16f, 0.06f), U(TCell.Stone));
    var pa = new Vector3(-2.95f, 4.56f, -1.55f); var pb = new Vector3(2.95f, 4.56f, -1.55f); var pc = new Vector3(0, 5.55f, -1.55f);
    mb.Tri(pa, pc, pb, UVc(TCell.Whitewash, 0, 0), UVc(TCell.Whitewash, .5f, 1), UVc(TCell.Whitewash, 1, 0)); var pc2 = pc + new Vector3(0, 0, 3.6f);
    mb.Quad(pa, pc, pc2, pa + new Vector3(0, 0, 3.6f), U(TCell.Roof), true); mb.Quad(pc, pb, pb + new Vector3(0, 0, 3.6f), pc2, U(TCell.Roof), true);
    var sc = new Vector3(0, 4.95f, -1.6f); mb.Cyl(sc + Vector3.down * 0.3f, sc + Vector3.up * 0.3f, 0.03f, 0.03f, 4, U(TCell.Gold)); mb.Cyl(sc + new Vector3(-0.35f, 0.25f, 0), sc + new Vector3(0.35f, 0.25f, 0), 0.025f, 0.025f, 4, U(TCell.Gold));
    for (int s = -1; s <= 1; s += 2) mb.Lathe(sc + new Vector3(s * 0.35f, 0.02f, 0), new[] { 0.02f, 0.13f, 0.14f }, 0.08f, 8, U(TCell.Gold));
    foreach (var px in new[] { -2.7f, 2.7f }) mb.Lathe(new Vector3(px, 0.6f, -1.95f), new[] { 0.2f, 0.25f, 0.18f, 0.4f, 0.32f }, 0.7f, 8, U(TCell.Green)); return 5.9f; }
  float TavernB(MeshB mb) { // two storeys: arcaded plaster ground floor, teak balcony with balusters, red-and-cream swags, barrels and tables
    Block(mb, new Vector3(0, 0, 0.3f), new Vector3(5.0f, 2.4f, 3.4f)); for (int i = 0; i < 3; i++) Arch(mb, -1.6f + i * 1.6f, 0, -1.42f, 1.1f, 1.9f);
    mb.Box(new Vector3(0, 2.55f, -0.3f), new Vector3(5.6f, 0.15f, 4.6f), U(TCell.Teak)); for (int i = 0; i < 10; i++) mb.Box(new Vector3(-2.5f + i * 0.56f, 2.4f, -2.45f), new Vector3(0.14f, 0.14f, 0.5f), U(TCell.Teak));
    mb.Box(new Vector3(0, 3.9f, 0.5f), new Vector3(4.6f, 2.6f, 2.6f), U(TCell.Teak)); for (int i = 0; i < 4; i++) { Window(mb, -1.7f + i * 1.13f, 3.9f, -0.82f, 0.55f, 1.1f); }
    for (int i = 0; i <= 18; i++) mb.Cyl(new Vector3(-2.7f + i * 0.3f, 2.62f, -2.5f), new Vector3(-2.7f + i * 0.3f, 3.35f, -2.5f), 0.035f, 0.045f, 5, U(TCell.Wood));
    mb.Box(new Vector3(0, 3.4f, -2.5f), new Vector3(5.6f, 0.1f, 0.12f), U(TCell.Teak));
    for (int i = 0; i < 4; i++) { float x = -2.7f + i * 1.8f; mb.Cyl(new Vector3(x, 0, -2.5f), new Vector3(x, 5.3f, -2.5f), 0.1f, 0.09f, 6, U(TCell.Teak)); }
    mb.Box(new Vector3(0, 5.3f, -0.4f), new Vector3(6.0f, 0.2f, 4.8f), U(TCell.Wood)); for (int i = 0; i < 12; i++) mb.Box(new Vector3(-2.75f + i * 0.5f, 5.2f, -2.85f), new Vector3(0.12f, 0.12f, 0.3f), U(TCell.Teak));
    for (int i = 0; i < 3; i++) { float a = -2.7f + i * 1.8f, b = a + 1.8f; var c1 = TCell.AwnRed; mb.Quad(new Vector3(a, 5.15f, -2.6f), new Vector3(b, 5.15f, -2.6f), new Vector3((a + b) / 2 + 0.4f, 4.5f, -2.62f), new Vector3((a + b) / 2 - 0.4f, 4.5f, -2.62f), new Rect(UVc(i == 1 ? TCell.Gold : c1, 0, 0.2f), U(c1).size * 0.12f), true); }
    for (int k = 0; k < 4; k++) Barrel(mb, new Vector3(-2.4f + (k % 2) * 0.7f, 0, -2.9f - (k / 2) * 0.6f), 0.8f);
    for (int k = 0; k < 2; k++) { var tp = new Vector3(0.6f + k * 1.5f, 0, -3.0f); mb.Cyl(tp, tp + Vector3.up * 0.7f, 0.06f, 0.06f, 5, U(TCell.Wood)); mb.Lathe(tp + Vector3.up * 0.7f, new[] { 0.45f, 0.45f }, 0.06f, 10, U(TCell.Teak));
      for (int s = -1; s <= 1; s += 2) mb.Box(tp + new Vector3(s * 0.6f, 0.22f, 0), new Vector3(0.3f, 0.44f, 0.3f), U(TCell.Wood)); }
    Lantern(mb, new Vector3(-0.9f, 2.0f, -2.5f)); Lantern(mb, new Vector3(0.9f, 2.0f, -2.5f)); return 5.7f; }
  float TempleB(MeshB mb) { // whitewashed shrine with a blue-trimmed drum, onion dome and four corner domelets, arched doors, wide steps, gilt finial
    Steps(mb, 4.6f, -2.85f, 4, 0.15f, 0.3f, TCell.Marble); mb.Box(new Vector3(0, 0.3f, 0.3f), new Vector3(5.2f, 0.6f, 4.4f), U(TCell.Marble));
    mb.Box(new Vector3(0, 2.1f, 0.3f), new Vector3(4.4f, 3.0f, 3.8f), U(TCell.Marble)); mb.Box(new Vector3(0, 3.65f, 0.3f), new Vector3(4.6f, 0.15f, 4.0f), U(TCell.AwnTeal));
    Arch(mb, 0, 0.6f, -1.62f, 1.2f, 2.3f); Arch(mb, -1.45f, 0.6f, -1.62f, 0.7f, 1.6f); Arch(mb, 1.45f, 0.6f, -1.62f, 0.7f, 1.6f);
    mb.Box(new Vector3(0, 2.95f, -1.64f), new Vector3(1.5f, 0.12f, 0.06f), U(TCell.AwnTeal));
    mb.Cyl(new Vector3(0, 3.6f, 0.3f), new Vector3(0, 4.3f, 0.3f), 1.45f, 1.45f, 16, U(TCell.Marble)); mb.Cyl(new Vector3(0, 4.15f, 0.3f), new Vector3(0, 4.3f, 0.3f), 1.5f, 1.5f, 16, U(TCell.AwnTeal));
    mb.Lathe(new Vector3(0, 4.3f, 0.3f), new[] { 1.45f, 1.6f, 1.55f, 1.3f, 0.9f, 0.45f, 0.12f, 0.02f }, 2.3f, 16, U(TCell.Marble));
    mb.Lathe(new Vector3(0, 6.55f, 0.3f), new[] { 0.06f, 0.12f, 0.04f, 0.1f, 0.01f }, 0.7f, 6, U(TCell.Gold));
    foreach (var c in new[] { new Vector3(-2.0f, 0.6f, -1.4f), new Vector3(2.0f, 0.6f, -1.4f), new Vector3(-2.0f, 0.6f, 2.0f), new Vector3(2.0f, 0.6f, 2.0f) }) {
      mb.Cyl(c, c + Vector3.up * 3.7f, 0.32f, 0.3f, 8, U(TCell.Marble)); mb.Cyl(c + Vector3.up * 3.6f, c + Vector3.up * 3.75f, 0.36f, 0.36f, 8, U(TCell.AwnTeal));
      mb.Lathe(c + Vector3.up * 3.75f, new[] { 0.32f, 0.36f, 0.25f, 0.08f, 0.01f }, 0.7f, 8, U(TCell.Marble)); mb.Lathe(c + Vector3.up * 4.45f, new[] { 0.04f, 0.01f }, 0.2f, 4, U(TCell.Gold)); }
    foreach (var px in new[] { -2.6f, 2.6f }) mb.Lathe(new Vector3(px, 0, -2.6f), new[] { 0.18f, 0.3f, 0.45f, 0.4f, 0.1f }, 1.1f, 8, U(TCell.Green)); return 6.9f; }
  float LenderB(MeshB mb) { // narrow stone house with a carved teak portal, iron-studded door, hanging sign board with scales, lantern and strongbox
    Block(mb, new Vector3(0, 0, 0.3f), new Vector3(3.8f, 4.6f, 3.2f), TCell.DarkTeak); mb.Box(new Vector3(0, 4.75f, 0.3f), new Vector3(4.1f, 0.25f, 3.5f), U(TCell.Teak));
    mb.Box(new Vector3(-0.35f, 1.3f, -1.32f), new Vector3(1.4f, 2.6f, 0.08f), U(TCell.Teak)); for (int r = 0; r < 4; r++) for (int c = 0; c < 3; c++) mb.Box(new Vector3(-0.8f + c * 0.45f, 0.4f + r * 0.6f, -1.38f), new Vector3(0.07f, 0.07f, 0.05f), U(TCell.Dark));
    for (int k = 0; k < 3; k++) Frame(mb, -0.35f, 0, -1.36f - k * 0.05f, 1.4f + k * 0.3f, 2.6f + k * 0.15f, 0.14f);
    mb.Box(new Vector3(-0.35f, 3.25f, -1.5f), new Vector3(2.4f, 0.35f, 0.2f), U(TCell.Teak)); mb.Lathe(new Vector3(-0.35f, 3.42f, -1.5f), new[] { 0.6f, 0.3f, 0.02f }, 0.4f, 6, U(TCell.Teak));
    Window(mb, 1.25f, 2.0f, -1.31f, 0.45f, 0.65f); Window(mb, 0, 3.9f, -1.31f, 0.9f, 0.6f);
    mb.Box(new Vector3(2.0f, 3.1f, -1.1f), new Vector3(1.2f, 0.08f, 0.08f), U(TCell.Dark)); mb.Box(new Vector3(2.35f, 2.35f, -1.1f), new Vector3(1.15f, 1.2f, 0.08f), U(TCell.Teak)); mb.Box(new Vector3(2.35f, 2.35f, -1.16f), new Vector3(0.95f, 1.0f, 0.04f), U(TCell.Cloth));
    var sc = new Vector3(2.35f, 2.35f, -1.2f); mb.Box(sc, new Vector3(0.05f, 0.65f, 0.03f), U(TCell.Dark)); mb.Box(sc + Vector3.up * 0.25f, new Vector3(0.7f, 0.04f, 0.03f), U(TCell.Dark));
    for (int s = -1; s <= 1; s += 2) mb.Box(sc + new Vector3(s * 0.33f, -0.05f, 0), new Vector3(0.25f, 0.05f, 0.03f), U(TCell.Dark));
    Lantern(mb, new Vector3(-1.5f, 2.6f, -1.55f)); mb.Box(new Vector3(-1.4f, 0.45f, -1.9f), new Vector3(0.9f, 0.5f, 0.55f), U(TCell.Teak)); mb.Box(new Vector3(-1.4f, 0.72f, -1.9f), new Vector3(0.95f, 0.06f, 0.6f), U(TCell.Gold));
    mb.Box(new Vector3(1.1f, 0.25f, -1.85f), new Vector3(1.3f, 0.12f, 0.45f), U(TCell.Teak)); foreach (var px in new[] { 0.6f, 1.6f }) mb.Box(new Vector3(px, 0.1f, -1.85f), new Vector3(0.1f, 0.2f, 0.4f), U(TCell.Wood));
    Pot(mb, new Vector3(1.7f, 0, -2.3f), 0.9f); mb.Lathe(new Vector3(1.7f, 0.55f, -2.3f), new[] { 0.1f, 0.3f, 0.2f }, 0.35f, 6, U(TCell.Green)); return 5.6f; }
  float PalaceB(MeshB mb) { merlonCell = TCell.Ochre; // sandstone fort-palace: crenellated curtain wall, five domed round towers with flags, arched gatehouse, upper keep, grand stairs
    Steps(mb, 4.0f, -3.9f, 4, 0.2f, 0.35f, TCell.Ochre);
    mb.Box(new Vector3(0, 2.0f, 0), new Vector3(11.5f, 4.0f, 5.5f), U(TCell.Ochre)); Merlons(mb, -5.75f, 5.75f, 4.0f, -2.7f, 0.3f); Merlons(mb, -5.75f, 5.75f, 4.0f, 2.7f, 0.3f);
    for (int i = 0; i < 6; i++) Window(mb, -4.6f + i * (i < 3 ? 1.2f : 1.2f) + (i >= 3 ? 2.4f : 0), 2.6f, -2.77f, 0.35f, 0.6f);
    mb.Box(new Vector3(0, 2.6f, -2.95f), new Vector3(3.0f, 5.2f, 0.6f), U(TCell.Ochre)); Arch(mb, 0, 0, -3.27f, 1.6f, 2.8f); Frame(mb, 0, 0, -3.3f, 1.6f, 2.0f, 0.18f); Merlons(mb, -1.5f, 1.5f, 5.2f, -3.0f, 0.5f, 0.6f);
    mb.Box(new Vector3(0, 3.85f, -3.3f), new Vector3(1.6f, 0.6f, 0.15f), U(TCell.Teak)); for (int k = 0; k < 3; k++) mb.Box(new Vector3(-0.5f + k * 0.5f, 3.85f, -3.4f), new Vector3(0.25f, 0.4f, 0.05f), U(TCell.Dark));
    mb.Box(new Vector3(0, 5.5f, 0.9f), new Vector3(6.0f, 3.0f, 3.2f), U(TCell.Ochre)); Merlons(mb, -3.0f, 3.0f, 7.0f, -0.7f, 0.3f); for (int i = 0; i < 4; i++) Arch(mb, -2.1f + i * 1.4f, 4.6f, -0.72f, 0.55f, 1.2f);
    float[] tx = { -5.8f, -3.0f, 3.0f, 5.8f }; float[] th = { 6.0f, 7.0f, 7.0f, 6.0f };
    for (int i = 0; i < 4; i++) { var c = new Vector3(tx[i], 0, -2.8f); float h = th[i]; mb.Cyl(c, c + Vector3.up * h, 0.95f, 0.85f, 12, U(TCell.Ochre)); mb.Cyl(c + Vector3.up * (h - 0.05f), c + Vector3.up * (h + 0.3f), 1.05f, 1.05f, 12, U(TCell.Ochre));
      for (int k = 0; k < 10; k++) { float a = k * Mathf.PI * 2 / 10; mb.Box(c + new Vector3(Mathf.Cos(a) * 1.0f, h + 0.55f, Mathf.Sin(a) * 1.0f), new Vector3(0.3f, 0.5f, 0.3f), Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0), U(TCell.Ochre)); }
      mb.Lathe(c + Vector3.up * (h + 0.3f), new[] { 0.7f, 0.75f, 0.6f, 0.3f, 0.02f }, 1.1f, 10, U(TCell.Plaster)); Flag(mb, c + Vector3.up * (h + 1.35f));
      Window(mb, c.x, h * 0.6f, c.z - 0.9f, 0.3f, 0.55f); }
    { var c = new Vector3(0, 7.0f, 0.9f); mb.Cyl(c, c + Vector3.up * 1.6f, 1.1f, 1.0f, 12, U(TCell.Ochre)); mb.Lathe(c + Vector3.up * 1.6f, new[] { 0.95f, 1.05f, 0.85f, 0.45f, 0.02f }, 1.6f, 12, U(TCell.Plaster)); Flag(mb, c + Vector3.up * 3.2f); }
    foreach (var px in new[] { -2.3f, 2.3f }) mb.Lathe(new Vector3(px, 0, -3.6f), new[] { 0.2f, 0.3f, 0.5f, 0.45f, 0.1f }, 1.2f, 8, U(TCell.Green));
    return 10.4f; }

  // ---- town view (tab -1) ----
  Vector3 TownFocus(float s) => new Vector3(0, s * 0.355f + 1.0f, s);
  /* signs stay below the goals strip: strip bottom sits 330 design px under the safe-area top, a sign is 84 tall */
  float SignMaxY() { var sa = SafeRect(); float k = canvasRoot ? canvasRoot.localScale.x : Screen.width / 1080f; return sa.yMax - ((goalsOpen ? 600 : 320) + 64 + 16) * k; } /* v0.6k: plaques clear of title+goals */
  bool goalsOpen = true;
  /* small brass plaque with a pointer that drops onto the building it names */
  RectTransform Plaque(Transform p, string name, Action on) {
    var rt = R(p, "Plaque " + name, 0, 0, 0, 0); rt.pivot = new Vector2(0.5f, 0); float w = Mathf.Clamp(28 + name.Length * 19, 150, 330); rt.sizeDelta = new Vector2(w, 64);
    var stem = R(rt, "stem", 0.5f, 0, 0.5f, 0); stem.pivot = new Vector2(0.5f, 1); stem.sizeDelta = new Vector2(5, 40); stem.gameObject.AddComponent<Image>().color = UITheme.C(T.brass);
    var tip = R(rt, "tip", 0.5f, 0, 0.5f, 0); tip.pivot = new Vector2(0.5f, 1); tip.sizeDelta = new Vector2(26, 20); var ti = tip.gameObject.AddComponent<Image>(); ti.sprite = Tri(); ti.color = UITheme.C(T.gold); tip.localScale = new Vector3(1, -1, 1);
    var face = R(rt, "face", 0, 0, 1, 1); var fi = face.gameObject.AddComponent<Image>(); fi.sprite = Panel(UITheme.C("#B8893C"), UITheme.C("#5A3A16")); fi.type = Image.Type.Sliced;
    var inner = Stretch(face, "in", 5, 5, 5, 5); var ii = inner.gameObject.AddComponent<Image>(); ii.sprite = Panel(UITheme.C("#D9AE5C"), UITheme.C("#8A6224")); ii.type = Image.Type.Sliced; ii.raycastTarget = false;
    var t = Txt(R(face, "L", 0, 0, 1, 1), name, T.fontSizeSmall - 6, "#2A1A0C", TextAnchor.MiddleCenter, true); t.raycastTarget = false;
    var b = face.gameObject.AddComponent<Button>(); float pressT = -9f; b.onClick.AddListener(() => { if (Time.unscaledTime - pressT < 1f) return; if (!busy) on(); }); /* gamedev_t7: bot/keyboard click path kept; skipped if the press already opened it */ b.transition = Selectable.Transition.None; /* gamedev_t7: open on press, so a plaque that moves (post-dock camera settle) between press and release still opens */
    var et = face.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>(); var en = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown }; en.callback.AddListener(_ => { if (!busy && !ModalUp && dialog == null) { pressT = Time.unscaledTime; townSnapTap = true; Debug.Log("GB PLAQUE tap " + name); on(); } }); et.triggers.Add(en); return rt; } /* gamedev_t7 */
  bool townSnapTap; /* gamedev_t7: a plaque press cancels any town drag/settle so the press is never eaten as a swipe */
  int townClose = -1; /* shot mode: frame one building close */
  void TownCamera() { if (townClose >= 0 && townClose < bldgs.Count) { var b = bldgs[townClose]; var c = b.t.position + Vector3.up * (b.h * 0.42f); float d = Mathf.Max(12f, b.h * 2.1f);
      cam.transform.position = c + new Vector3(d * 0.32f, d * 0.38f, -d); cam.transform.LookAt(c); return; }
    var f = TownFocus(townS); cam.transform.position = f + new Vector3(3.2f, 3.6f, -15.5f);
    /* v0.6k3: look slightly up/shore so port landmark at far terrace edge stays in frame */
    float lookY = 5.5f; float lookZ = 6f;
    if (S != null && port >= 0 && port < S.ports.Length) {
      var pn = S.ports[port].name;
      if (pn == "Debal") { lookY = 9f; lookZ = 8f; }
      else if (pn == "Muscat") { lookY = 10f; lookZ = 9f; }
      else { lookY = 8f; lookZ = 7f; } /* Gwadar */
    }
    cam.transform.LookAt(f + new Vector3(0.6f, lookY, lookZ)); }
  void TownScreen(RectTransform main) {
    if (town3D == null) BuildTown(); BuildLandmarksV06k(); ShowTownLandmark(); town3D.gameObject.SetActive(true); if (sea3D && !sailing && !fighting) sea3D.gameObject.SetActive(false); /* open-sea water must never cover the quay (Muscat after customs) */ RenderSettings.ambientLight = new Color(0.34f, 0.34f, 0.37f); RenderSettings.fogColor = new Color(0.80f, 0.74f, 0.64f); /* softer sky tint so far walls read sandstone, not blue-grey */ RenderSettings.fogEndDistance = 420; sea3D.gameObject.SetActive(false); backdrop.color = Color.clear; dimmer.gameObject.SetActive(false);
    // goals: short / medium / long always on screen
    /* v0.6i: town name plaque at the top of the town screen */
    var tn = Px(content, "TownTitle", 16, 172, 1048, 70); Box(tn, T.panel, T.gold); Label(tn, S.ports[port].name.ToUpper(), 24, 0, 1000, 70, T.fontSizeTitle + 4, T.parchment, TextAnchor.MiddleCenter, true);
    var (gs, gm, gl) = Goals3(); goalsOpen = true; /* v0.6h: all three goals always shown */ string storyShort = ShortStoryGoal(gl); var gp = Px(content, "Goals", 16, 248, 1048, 132); Box(gp, T.ink, T.brass); gp.GetComponent<Image>().raycastTarget = true;
    Label(gp, $"<color={T.gold}>Now</color>  {gs}", 22, 4, 1000, 42, T.fontSizeSmall, T.text);
    Label(gp, $"<color={T.gold}>Next</color>  {gm}", 22, 46, 1000, 42, T.fontSizeSmall, T.text); Label(gp, $"<color={T.gold}>Story</color>  {storyShort}", 22, 88, 1000, 42, T.fontSizeSmall, T.textMuted);
    var gb = gp.gameObject.AddComponent<Button>(); gb.onClick.AddListener(() => { goalsOpen = !goalsOpen; if (InOpening) { msg = "Next: " + OpeningMediumGoalAt(storyStep); /* gamedev_t10_next: medium goal */ nudgeT = Time.unscaledTime; } Refresh(); }); if (Time.unscaledTime - nudgeT < 4f) { var glow = Px(gp, "nudge", -6, -6, 1060, 162); Box(glow, "#00000000", T.gold); glow.GetComponent<Image>().raycastTarget = false; StartCoroutine(PulseChip(gp)); } YusufCheck();
    signLayer = R(content, "Signs", 0, 0, 1, 1); signs.Clear();
    for (int i = 0; i < bldgs.Count; i++) { int k = i; if (Hidden(bldgs[i].tab)) continue; string nm = bldgs[i].name; if (bldgs[i].tab == 7 && ShipwrightMode(S.ports[port].name) == "none") nm = "Harbour Master"; signs.Add((Plaque(signLayer, nm, () => OpenBuilding(bldgs[k].tab)), i)); }
    var cb = Bottom1920(content, "TownBottom");
    /* v0.6i: SET SAIL art button from ui_setsail_button.png */
    var sail = SetSailBtn(cb, 16, 1660, 680, 140, "SET SAIL", () => { if (ModalUp) return; /* final ask_modal */ if (!CanLeavePort(out var why)) { msg = why; Refresh(); } else OpenBuilding(4); }); chartBtn = sail.GetComponent<RectTransform>(); if (!JournalUnlocked) ShowTownHints(); else if (signLayer) { /* v0.6j: map/quest markers off after opening */ }
    /* v0.6k2: Journal/Options chips — ink fill + cream label (never empty teal) */
    {
      var jr = Px(cb, "JournalChip", 712, 1660, 168, 140); Box(jr, T.ink, T.brass);
      var ji = jr.GetComponent<Image>(); if (ji) { var c = ji.color; c.a = 1f; ji.color = c; }
      Label(jr, "Journal", 6, 45, 156, 50, T.fontSizeSmall, T.parchment /* gamedev_t7 */, TextAnchor.MiddleCenter, true);
      var jb = jr.gameObject.AddComponent<Button>(); jb.interactable = true; /* gamedev_t7: Journal always opens in town (was gated on OpeningDone) */ jb.onClick.AddListener(() => { if (ModalUp && dialog == null && creditsCard == null) { Debug.Log("GB JOURNAL modalDepth " + modalDepth + " with no card up: reset"); modalDepth = 0; } /* gamedev_t7: stale depth never locks the chip */ if (ModalUp) { Debug.Log("GB JOURNAL blocked (card up)"); return; } Debug.Log("GB JOURNAL open"); OpenJournal(); });
      var orr = Px(cb, "OptionsChip", 896, 1660, 168, 140); Box(orr, T.ink, T.brass);
      Label(orr, "Options", 6, 45, 156, 50, T.fontSizeSmall, T.parchment, TextAnchor.MiddleCenter, true);
      orr.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (ModalUp) return; /* final ask_modal */ OptionsMenu(); });
    }
    var hintBg = Px(cb, "swipeBg", 16, 1588, 1048, 56); Box(hintBg, T.ink, T.brass); var hi = hintBg.GetComponent<Image>(); hi.raycastTarget = false; hi.color = new Color(0.06f, 0.04f, 0.03f, 0.92f);
    Label(hintBg, "Swipe up and down to walk the town · tap a building", 8, 4, 1032, 48, T.fontSizeSmall - 2, T.parchment, TextAnchor.MiddleCenter);
  }
  (string, string, string) Goals() {
    if (InOpening) return StoryGoals();
    string s = HoldUsed == 0 ? (port == GwadarIdx ? "Buy cheap dates and salt at the bazaar" : "Buy what this port sells cheap") : "Sell your cargo where it is wanted (check the chart)";
    string m = wh.All(w => w.tier == 0) ? "Lease a warehouse to stockpile before a price spike" : ShipTierSum == 0 ? "Upgrade your dhow at the shipwright" : "Save up for a bigger ship";
    string l = "Find who burned your ship: the Qarin seal"; return (s, m, l); }
  int GwadarIdx => Array.FindIndex(S.ports, p => p.name == "Gwadar");
  string tavernStory;
  void OpenBuilding(int tab) { int prevTab = this.tab; string prevStory = tavernStory; if (Hidden(tab)) { msg = "That door stays shut for now. Finish the Muscat run first."; Refresh(); return; } this.tab = tab; keeperTrig = "greet"; keeperTab = tab; tavernStory = tab == 8 ? (prevTab == 8 && prevStory != null ? prevStory /* a second open of the same tavern must not blank the line */ : TavernStory()) : null; if (playthrough && tab == 8 && tavernStory != prevStory) PLog2("tavern line", tavernStory ?? "(no story line)"); Refresh(); }
  void TownInput() {
    if (town3D == null || !town3D.gameObject.activeSelf || dialog != null || sheet != null) return;
    bool overUI = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    if (Input.GetMouseButtonDown(0) && !overUI) { townDrag = true; townDown = Input.mousePosition; townDownS = townS; }
    if (townSnapTap) { townSnapTap = false; townDrag = false; townDownS = townS; } /* gamedev_t7 */ if (townDrag && Input.GetMouseButton(0)) townS = Mathf.Clamp(townDownS - (Input.mousePosition.y - townDown.y) / Screen.height * 40f, townSMin, townSMax);
    if (townDrag && Input.GetMouseButtonUp(0)) { townDrag = false;
      if (((Vector2)Input.mousePosition - townDown).magnitude < Screen.height * 0.015f && Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out var hit, 300f)) {
        var b = bldgs.FirstOrDefault(x => x.t == hit.transform); if (b.t != null) OpenBuilding(b.tab); } }
    TownCamera();
    if (signLayer == null) return; var rect = signLayer.rect;
    float k = canvasRoot ? canvasRoot.localScale.x : 1f; var sa = SafeRect(); float bottom = sa.yMin + 330 * k;
    var placed = new List<(float x, float y, float half)>(); /* designer: Tavern/Money Lender/Palace share a column (x~4.5, rows 3-5); a later plaque that overlaps an earlier one lifts just above it */
    foreach (var (r, i) in signs) { if (r == null) continue; var b = bldgs[i]; var bc = b.t.GetComponent<BoxCollider>(); var bb = bc ? bc.bounds : new Bounds(b.t.position, Vector3.one);
      var anchor = new Vector3(bb.center.x, bb.max.y + 0.15f, bb.center.z - bb.extents.z * 0.4f); var sp = cam.WorldToScreenPoint(anchor) + new Vector3(0, 46 * k, 0);
      float half = r.sizeDelta.x / 2 * k + 12 * k; float cx = cam.WorldToScreenPoint(bb.center).x; bool inside = cx > sa.xMin + 20 * k && cx < sa.xMax - 20 * k; /* show whenever the building itself is on screen; the plaque is clamped inside */
      bool vis = sp.z > 0 && sp.y > bottom && sp.y < SignMaxY() && inside; r.gameObject.SetActive(vis); if (!vis) continue;
      sp.x = Mathf.Clamp(sp.x, sa.xMin + half, sa.xMax - half); /* plaque kept inside the safe area; its stem still drops on the building */
      for (int pass = 0; pass < 4; pass++) { bool moved = false; float ph = 68 * k;
        foreach (var q in placed) if (Mathf.Abs(sp.x - q.x) < half + q.half - 24 * k && sp.y < q.y + ph && sp.y + ph > q.y) { sp.y = q.y + ph; moved = true; }
        if (!moved) break; }
      placed.Add((sp.x, sp.y, half));
      RectTransformUtility.ScreenPointToLocalPointInRectangle(signLayer, sp, null, out var lp); r.anchoredPosition = lp - rect.min; }
  }

  const int SalimRow = 8; /* art_salim_portrait: Salim's own face (row 7 reserved for Rashid) */
  public const string MuscatFence = "Salim the fence"; /* research_gp3: Game Designer ruling */
  // ---- keepers ----
  static readonly Dictionary<int, (string key, string name, int row)> Keepers = new Dictionary<int, (string, string, int)> {
    { 0, ("bazaarMerchant", "Jalal the bazaar merchant", 0) }, { 1, ("farida", "Farida of the back alley", 2) }, { 3, ("shipwright", "Temple priest", 4) }, /* reuse face row until temple art */
    { 5, ("banker", "Master Idris, banker", 3) },
    { 6, ("moneyLender", "Rafiq the lender", 1) }, { 7, ("shipwright", "Old Bashir, shipwright", 4) },
    { 8, ("innkeeper", "Innkeeper of the Salt & Lantern", 0) } /* own lines; face: Art/Portraits/innkeeper when painted, else mirrored + tinted row 0 (see KeeperHeader) */, { 9, ("banker", "Governor's chamberlain", 3) } };
  static readonly string[] Moods = { "neutral", "delighted", "offended", "suspicious" };
  KeeperLine Line(string key, string trig) { if (book == null) { var ta = Resources.Load<TextAsset>("keeper_lines"); book = ta ? JsonUtility.FromJson<KeeperBook>(ta.text) : new KeeperBook(); }
    var arr = (KeeperLine[])typeof(KeeperBook).GetField(key).GetValue(book); if (arr == null || arr.Length == 0) return new KeeperLine { mood = "neutral", text = "" };
    return arr.FirstOrDefault(l => l.trigger == trig) ?? arr[0]; }
  public void Say(string trig) { keeperTrig = trig; }
  const float KeeperH = 400;
  bool KeeperHeader(int tab) {
    if (!Keepers.TryGetValue(tab, out var k)) return false;
    bool harbour = tab == 7 && (S.ports[port].name == "Muscat" || ShipwrightMode(S.ports[port].name) == "none"); /* designer: same rule as the "Harbour Master" plaque */
    if (tab == 1 && port == Muscat) { k.name = MuscatFence; k.key = "salim"; } /* research_gp3; research_gp4: own lines */
    if (tab == 3) k.key = "priest"; else if (harbour) { k.key = "harbourmaster"; k.name = "Harbour master"; } /* own lines + optional own portrait instead of Bashir's */
    var line = Line(k.key, k.key == "salim" && openingGpSold && keeperTrig == "greet" ? "sold" : keeperTrig); /* research_gp5: Salim greets with the after-sale line from saved state (openingGpSold), on every visit and after a relaunch */ if (tab == 0 && S.ports[port].name != "Gwadar") { k.name = $"{S.ports[port].name} bazaar merchant"; if (line.trigger == "greet") line = new KeeperLine { trigger = line.trigger, mood = line.mood, text = "Everything except a discount!" }; } /* gamedev_t19_merchant: Jalal and his Gwadar goods cry stay in Gwadar */ int mood = Mathf.Max(0, Array.IndexOf(Moods, line.mood));
    var r = Px(content, "Keeper", 16, 172, 1048, KeeperH); Box(r, T.ledgerBg, T.frame);
    var fr = Px(r, "face", 14, 14, KeeperH - 28, KeeperH - 28); Box(fr, T.ink, T.gold);
    /* v0.6h4: ally Farida until turned in; rival portrait from FaridaState == 2 */
    var tex = tab == 1 && port == Muscat ? Resources.Load<Texture2D>($"Keepers/k{SalimRow}_{mood}") : k.key == "farida" ? FaridaPortrait() : Resources.Load<Texture2D>($"Keepers/k{k.row}_{mood}"); /* art_salim_portrait */
    /* designer: optional own portraits, Assets/Resources/Art/Portraits/<file>.png (Builder.cs:115 import rule); until painted, the borrowed face is mirrored + tinted */
    string own = k.key == "innkeeper" ? "innkeeper" : k.key == "priest" ? "priest" : k.key == "harbourmaster" ? "harbourmaster" : null;
    var ownTex = own != null ? Resources.Load<Texture2D>("Art/Portraits/" + own) : null; if (ownTex) tex = ownTex;
    Color tint = k.key == "innkeeper" ? new Color(0.78f, 0.84f, 0.95f) /* cool lamplight */ : k.key == "priest" ? new Color(1f, 0.93f, 0.78f) /* warm candle */ : new Color(0.82f, 0.95f, 0.86f) /* sea-green dock light */;
    if (tex) { var im = Px(fr, "img", 5, 5, KeeperH - 38, KeeperH - 38).gameObject.AddComponent<RawImage>(); im.texture = tex;
      if (own != null && !ownTex) { im.uvRect = new Rect(1, 0, -1, 1); im.color = tint; } }
    float tx = KeeperH + 6, tw = 1048 - tx - 20;
    var kn = Label(r, tab == 7 && S.ports[port].name == "Muscat" ? "Harbour master, dock patch" : k.name, tx, 12, tw, 72, T.fontSizeTitle - 8, T.ledgerInk, TextAnchor.UpperLeft, true); kn.horizontalOverflow = HorizontalWrapMode.Wrap; kn.verticalOverflow = VerticalWrapMode.Overflow; /* v0.6k: long keeper titles wrap inside panel */
    Label(r, line.mood, tx, 88, tw, 36, T.fontSizeSmall - 4, T.ledgerMuted);
    Label(r, $"“{line.text}”", tx, 126, tw, KeeperH - 140, T.fontSize + 4, T.ledgerInk, TextAnchor.UpperLeft);
    return true; }

  // ---- bank / lender / shipwright / tavern / palace ----
  void BankScreen(RectTransform led) {
    var bk = S.bank; LedgerHeader(led, $"Bank of {S.ports[port].name}");
    Label(led, $"Deposited: <color={T.gold}>{bankCoins:N0}</color> coins · {bk.interestPerMoon * 100:0.#}% interest each moon · safe from sinking and robbery", 26, 80, 980, 110, T.fontSize + 2, T.text);
    float fee = bankPort >= 0 && bankPort != port ? bk.otherPortWithdrawFee : 0;
    Label(led, fee > 0 ? $"Withdrawing here costs {fee * 100:0}% (you banked in {S.ports[bankPort].name})." : "No fee to withdraw here.", 26, 196, 980, 60, T.fontSizeSmall + 2, T.textMuted);
    void Dep(int n) { n = Mathf.Min(n, coins); if (n <= 0) return; coins -= n; bankCoins += n; bankPort = port; Say(bankCoins > 5000 ? "richPlayer" : "deposit"); Refresh(); }
    void Wd(int n) { n = Mathf.Min(n, bankCoins); if (n <= 0) return; int f = Mathf.CeilToInt(n * fee); bankCoins -= n; coins += n - f; Say(bankCoins == 0 ? "withdrawAll" : f > 0 ? "otherPortFee" : "greet"); Refresh(); }
    Btn(led, "Deposit 100", 26, 280, 480, 120, () => Dep(100), coins >= 1); Btn(led, "Deposit all", 526, 280, 480, 120, () => Dep(coins), coins >= 1);
    Btn(led, "Withdraw 100", 26, 420, 480, 120, () => Wd(100), bankCoins > 0); Btn(led, "Withdraw all", 526, 420, 480, 120, () => Wd(bankCoins), bankCoins > 0);
  }
  void LenderScreen(RectTransform led) {
    var ml = S.moneyLender; if (loanCap == 0) { loanCap = ml.firstLoanCap; loanRate = ml.startRatePerMoon; }
    LedgerHeader(led, "Money Lender");
    if (loanOwed > 0) {
      int movesLeft = loanDueWeek - week; bool late = movesLeft < 0;
      Label(led, $"You owe <color={T.gold}>{loanOwed:N0}</color> coins. " + (late ? $"<color={T.down}>{-movesLeft} weeks LATE</color>" : $"Due in {movesLeft} weeks (moon {loanDueWeek / S.weeksPerMoon + 1})."), 26, 80, 980, 110, T.fontSize + 2, T.text);
      Btn(led, $"Repay {Mathf.Min(coins, loanOwed):N0}", 26, 220, 980, 130, () => { int n = Mathf.Min(coins, loanOwed); coins -= n; loanOwed -= n;
        if (loanOwed == 0) { if (week <= loanDueWeek) { Say("repayOnTime"); if (ml.onTimeRaisesCap) loanCap = Mathf.Min(ml.maxLoanCap, loanCap * 2); if (ml.onTimeLowersRate) loanRate = Mathf.Max(0.05f, loanRate - 0.01f); } else Say("greet"); }
        Refresh(); }, coins > 0);
    } else {
      Label(led, $"Borrow up to <color={T.gold}>{loanCap:N0}</color> coins at {loanRate * 100:0}% a moon, due in {ml.termMoons} moons. Repay on time and he lends more, cheaper.", 26, 80, 980, 130, T.fontSize + 2, T.text);
      Btn(led, $"Borrow {loanCap:N0}", 26, 230, 480, 130, () => { coins += loanCap; loanOwed = loanCap; loanDueWeek = week + ml.termMoons * S.weeksPerMoon; Say("borrow"); Refresh(); });
      Btn(led, "Not today", 526, 230, 480, 130, () => { Say("refuse"); Refresh(); });
    }
    Label(led, "Late payers have their warehouse seized first, then a debt collector comes calling.", 26, 400, 980, 80, T.fontSizeSmall + 2, T.textMuted);
  }
  static readonly string[] UpKinds = { "Hull", "Sails", "Guns", "Hold" }; static readonly int[] UpCost = { 800, 2000, 5000 };
  void ShipwrightScreen(RectTransform led) {
    string mode = ShipwrightMode(S.ports[port].name);
    if (mode == "none") { HarbourScreen(led); return; }
    var sw = S.shipwright; string spec = sw.specialty(S.ports[port].name);
    LedgerHeader(led, mode == "repair_only" ? "Shipwright · repairs only" : "Shipwright");
    Label(led, (spec != null ? $"This yard's specialty: <color={T.gold}>{spec}</color> (20% off). " : "") + (RepairCost > 0 ? $"Hull {HullCur:0}/{HullMax:0}, sails {SailsCur:0}/{SailsMax:0}: damage stays until you repair it." : "Your hull and sails are sound."), 26, 76, 980, 100, T.fontSizeSmall + 4, T.text);
    float y = 190; bool gwd = S.ports[port].name == "Gwadar"; int rc = gwd ? Mathf.CeilToInt(RepairCost * gwadarRepairMult) : RepairCost;
    if (rc > 0) { Btn(led, gwd ? $"Repair all · {rc:N0} (-{(1 - gwadarRepairMult) * 100:0}%)" : $"Repair all · {rc:N0}", 26, y, 480, 100, () => { coins -= rc; Repair(); Say("upgrade"); msg = "Planks and canvas good as new."; Refresh(); }, coins >= rc);
      int half = Mathf.CeilToInt(rc * S.shipwright.adRepairDiscount); if (AdsUnlocked) Btn(led, $"Half price (ad) · {half:N0}", 526, y, 480, 100, () => StartCoroutine(ShowAd("halfPriceRepairOncePerPortVisit", () => { coins -= half; Repair(); msg = "Half-price repairs, courtesy of a sponsor."; })), coins >= half && AdBlockReason() == null); y += 124; }
    if (mode != "repair_only") {
      for (int k = 0; k < 4; k++) { int kk = k; int tier = shipTier[k]; bool max = tier >= sw.upgradeTiers;
        int cost = max ? 0 : Mathf.RoundToInt(UpCost[tier] * (spec != null && spec.ToLower() == UpKinds[k].ToLower() ? 0.8f : 1f));
        Label(led, $"{UpKinds[k]}  {new string('●', tier)}{new string('○', sw.upgradeTiers - tier)}", 26, y, 420, 110, T.fontSize + 6, T.text);
        Btn(led, max ? "Fully upgraded" : $"Upgrade · {cost:N0}", 470, y + 10, 530, 100, () => { coins -= cost; shipTier[kk]++; ApplyShipTiers(); Say("upgrade"); msg = $"{UpKinds[kk]} upgraded."; Refresh(); }, !max && coins >= cost);
        y += 124; }
      if (mode == "sells_small_hulls") { Label(led, "Small hulls (coming soon). Timber jobs keep the yard busy.", 26, y, 980, 60, T.fontSizeSmall, T.textMuted); y += 70; }
    } else { Label(led, "Gwadar's yard repairs only — no new hulls sold here.", 26, y, 980, 60, T.fontSizeSmall, T.textMuted); y += 70; } /* art_t4_fixes: room before Ask about work */
    QuestBtn(led, "shipwright", "Shipwright of " + S.ports[port].name, Mathf.Min(y + 40, 1320));
  }
  void ApplyShipTiers() { S.holdSize = baseHold + shipTier[3] * 5; S.combat.hasan.hull = baseHull + shipTier[0] * 15; S.combat.hasan.sails = baseSails + shipTier[1] * 10; S.combat.hasan.gunsPerSide = baseGuns + shipTier[2]; }
  int baseHold, baseGuns; float baseHull, baseSails; bool basesKept;
  void KeepBases() { if (basesKept) return; basesKept = true; baseHold = S.holdSize; baseHull = S.combat.hasan.hull; baseSails = S.combat.hasan.sails; baseGuns = (int)S.combat.hasan.gunsPerSide; }
  void TavernScreen(RectTransform led) { /* gamedev_egfix1: list scrolls in a masked viewport; Ask about work + Contract board pinned at the panel bottom, same spot in every port, clear of the ticker */
    const float pinH = 140f;
    var vp = Stretch(led, "TavVp", 0, 0, 0, pinH); vp.gameObject.AddComponent<RectMask2D>(); var vpi = vp.gameObject.AddComponent<Image>(); vpi.color = Color.clear;
    var lst = Px(vp, "TavList", 0, 0, 1032, 400);
    LedgerHeader(lst, "The Salt & Lantern tavern");
    Label(lst, "Sailors trade gossip for a cup of tea.", 26, 76, 980, 60, T.fontSizeSmall + 4, T.textMuted);
    float y = 150; /* tavernStory is computed once in OpenBuilding so a re-draw can never blank it */
    if (tavernStory != null) { var tl = Label(lst, $"<b>“{tavernStory}”</b>", 26, y, 980, 150, T.fontSize + 4, T.gold, TextAnchor.UpperLeft); Canvas.ForceUpdateCanvases(); float th = Mathf.Max(150, tl.preferredHeight + 10); tl.rectTransform.sizeDelta = new Vector2(tl.rectTransform.sizeDelta.x, th); y += th + 24; } foreach (var n in news.Take(5)) { y += Flow(Label(lst, "· " + n, 26, y, 980, 100, T.fontSizeSmall + 6, T.text, TextAnchor.UpperLeft), 100) + 6; }
    Btn(lst, "Buy a round (10 coins) for a rumour", 26, y + 10, 980, 120, () => { coins -= 10; news.Insert(0, RandomRumour()); Refresh(); }, coins >= 10); y += 140;
    if (AdsUnlocked) { Btn(lst, "Hear next week's talk early (ad)", 26, y, 980, 110, () => StartCoroutine(ShowAd("earlyRumor", () => { var pe = pendingEv.FirstOrDefault(); news.Insert(0, pe.e != null ? $"Early word: {Content((string)pe.e["id"]).GetValueOrDefault("headline", "") }".Replace("{port}", S.ports[pe.p].name).Replace("{good}", pe.g >= 0 ? S.goods[pe.g].name.ToLower() : "goods") : "Nothing new is brewing yet."); })), AdBlockReason() == null); y += 130; }
    lst.sizeDelta = new Vector2(1032, y + 10);
    var sr = vp.gameObject.AddComponent<ScrollRect>(); sr.viewport = vp; sr.content = lst; sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 40;
    Box(Pin(Px(led, "TavPinRule", 20, 0, 992, 3), 20, pinH - 4), T.ledgerRule);
    TryCompleteJobsOnEnterBuilding("tavern");
    bool board = IsBoardPort(port);
    var qb = QuestBtn(led, "tavern", "Innkeeper of " + S.ports[port].name, 0, 26, board ? 482 : 980); Pin(qb.GetComponent<RectTransform>(), 26, 16);
    var eb = EgBoardBtn(led, 0, 524, 482); if (eb) Pin(eb.GetComponent<RectTransform>(), 524, 16); /* gamedev_end1 */
  }
  static RectTransform Pin(RectTransform r, float x, float bottom) { r.anchorMin = r.anchorMax = Vector2.zero; r.pivot = Vector2.zero; r.anchoredPosition = new Vector2(x, bottom); return r; } /* gamedev_egfix1 */
  string RandomRumour() { var g = S.goods[Rnd.Range(0, S.goods.Length)]; var p = S.ports[Rnd.Range(0, S.ports.Length)];
    string[] f = { $"They say {p.name} is short of {g.name.ToLower()}…", $"A trader swore {g.name.ToLower()} will be worth more in {p.name} soon.", $"Pirates were seen near {p.name}, chasing fat holds." }; return f[Rnd.Range(0, f.Length)]; }

  void PalaceScreen(RectTransform led) {
    LedgerHeader(led, "Governor's Palace");
    Label(led, "The governor's banner flies above the court. Official escorts and favours are offered here when your standing allows.", 26, 80, 980, 120, T.fontSize + 4, T.text, TextAnchor.UpperLeft);
    Label(led, $"Sultan standing: {sultanStanding} (Muscat rep {rep[Muscat]:+0;-0;0})", 26, 220, 980, 50, T.fontSize, T.gold);
    QuestBtn(led, "palace", "Governor of " + S.ports[port].name, 300);
  }

  // ---- chart table (map) ----
  static readonly Dictionary<string, Vector2> ChartPos = new Dictionary<string, Vector2> { { "Gwadar", new Vector2(0.285f, 0.29f) }, { "Debal", new Vector2(0.2f, 0.56f) }, { "Muscat", new Vector2(0.64f, 0.27f) },
    { "Sur", new Vector2(0.74f, 0.4f) }, { "Sohar", new Vector2(0.79f, 0.6f) }, { "Kachchh", new Vector2(0.84f, 0.84f) } };
  int chartDest = -1;
  void ChartScreen(RectTransform led) {
    if (chartDest < 0 || chartDest == port) chartDest = (port + 1) % S.ports.Length;
    var tex = Resources.Load<Texture2D>("Art/chart"); float cw = 1016, chh = tex ? cw * tex.height / tex.width : 486;
    var fr = Px(led, "chart", 0, 0, cw, chh); var ri = fr.gameObject.AddComponent<RawImage>(); ri.texture = tex;
    Vector2 P(string n) { var v = ChartPos[n]; return new Vector2(v.x * cw, v.y * chh); }
    // dotted route from here to the chosen port
    var a = P(S.ports[port].name); var b = P(S.ports[chartDest].name); var mid = (a + b) / 2 + new Vector2(0, 60);
    for (int i = 1; i < 26; i++) { float t = i / 26f; var p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * mid + t * t * b; var d = Px(fr, "dot", p.x - 6, p.y - 6, 12, 12).gameObject.AddComponent<Image>(); d.sprite = Ring(0, 1); d.color = UITheme.C(T.gold); }
    foreach (var kv in ChartPos) { int pi = Array.FindIndex(S.ports, p => p.name == kv.Key); var p = P(kv.Key); bool known = pi >= 0;
      var m = Px(fr, "port " + kv.Key, p.x - 50, p.y - 50, 100, 100); var mi = m.gameObject.AddComponent<Image>(); mi.sprite = Ring(known ? 0.55f : 0.7f); mi.color = UITheme.C(pi == port ? T.marker : pi == chartDest ? T.gold : known ? T.parchment : T.textMuted);
      if (known && pi != port) { int k = pi; m.gameObject.AddComponent<Button>().onClick.AddListener(() => { chartDest = k; Refresh(); }); }
      if (!known) Label(fr, "uncharted", p.x - 80, p.y + 40, 160, 34, T.fontSizeSmall - 8, T.textMuted, TextAnchor.MiddleCenter); }
    float y = chh + 16; int dp = chartDest;
    Label(led, $"To {S.ports[dp].name}", 26, y, 700, 70, T.fontSizeTitle, T.parchment, TextAnchor.MiddleLeft, true);
    bool calmT = sails + 1 <= S.pirateFreeSails || sails + 1 < safeUntilSail; Label(led, port == GwadarIdx || dp == GwadarIdx ? (calmT ? "Safe coastal route" : "Coastal route, calm seas") : "Open water", 640, y + 10, 360, 56, T.fontSizeSmall, T.textMuted, TextAnchor.MiddleRight); y += 76;
    string danger = DangerLabel(AmbushChance(), sails + 1 <= S.pirateFreeSails || sails + 1 < safeUntilSail);
    Label(led, danger, 26, y, 980, 54, T.fontSize, T.text); y += 58;
    var bannedHere = Enumerable.Range(0, S.goods.Length).Where(g => Banned(g, dp)).Select(g => S.goods[g].name).ToList();
    Label(led, bannedHere.Count > 0 ? "Banned on arrival: " + string.Join(", ", bannedHere) : "No bans reported.", 26, y, 980, 54, T.fontSizeSmall + 2, T.textMuted); y += 56;
    Label(led, "Prices unknown until you dock.", 26, y, 980, 54, T.fontSizeSmall + 2, T.textMuted); y += 66;
    Btn(led, $"SET SAIL FOR {S.ports[dp].name.ToUpper()}", 26, y, 980, 150, () => StartCoroutine(Voyage(dp)), true, true);
  }
  public float AmbushChance() => AmbushChanceFor(chartDest >= 0 && chartDest != port ? chartDest : (port + 1) % S.ports.Length); /* v06_ambush_data roll, see GameV06 */

  // ---- difficulty ----
  void OptionsDialog() { StartCoroutine(AskD("Options · difficulty", $"Current: {S.combat.difficulties[difficulty].name}.\n\nRelaxed: strong aim help at the breach, gentler pirates.\nCaptain: light aim help (recommended).\nHard: no help, fiercer pirates.",
    ("Relaxed", () => { difficulty = 0; Save(); }), ("Captain", () => { difficulty = 1; Save(); }), ("Hard", () => { difficulty = 2; Save(); }))); }
}
public partial class Game {
  void MoonMoney() {
    if (bankCoins > 0) bankCoins += Mathf.FloorToInt(bankCoins * S.bank.interestPerMoon);
    if (loanOwed > 0) { loanOwed += Mathf.CeilToInt(loanOwed * loanRate);
      if (week > loanDueWeek) { var w = wh.FirstOrDefault(x => x.tier > 0);
        if (S.moneyLender.lateSeizesWarehouseFirst && w != null) { w.tier = 0; w.items.Clear(); news.Insert(0, "Rafiq's men seized your warehouse for the late loan."); }
        else if (S.moneyLender.lateSendsDebtCollector) { int take = Mathf.Min(coins, loanOwed); coins -= take; loanOwed -= take; news.Insert(0, $"A debt collector took {take} coins from your purse."); } } }
  }
}

public partial class Game { System.Collections.IEnumerator PulseChip(RectTransform r) { for (float t = 0; t < 3f && r; t += Time.unscaledDeltaTime) { float k = 1 + 0.04f * Mathf.Sin(t * 9); r.localScale = new Vector3(k, k, 1); yield return null; } if (r) r.localScale = Vector3.one; } }
