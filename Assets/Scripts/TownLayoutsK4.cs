using System;
using UnityEngine;

/// k4 town layouts (Game Designer). Static, primitive-only (MeshB boxes/cyls on the town atlas, no external assets).
/// Spec: design/v06k4_town_layouts.md. Avoids landmark footprints from art/landmarks_k4/GUIDE.md
/// (Gwadar (-8,16.2,55), Debal (6,16.2,54), Muscat (-4,16.5,56): nothing here above y 14 within |dx|<6 of those, z>46)
/// and keeps the centre stair (|x|<2) clear. Terrace grid: RowY(r)=3.2r, RowZ(r)=6+9r.
public static class TownLayoutsK4 {
  // Same order as Game.TCell (GameTown.cs) — kept as ints so this file needs no access to the private enum.
  const int Stone = 0, Plaster = 1, Roof = 2, AwnRed = 3, AwnTeal = 4, Gold = 5, Rock = 6, Wood = 7, Rug = 8, Dark = 9,
    Marble = 10, Paving = 11, Green = 13, Teak = 14, Cloth = 15, Whitewash = 16, DarkTeak = 17, Ochre = 18, Planks = 19, Indigo = 22, Rope = 23, Sand = 24;
  static float RowY(int r) => r * 3.2f; static float RowZ(int r) => 6 + r * 9f;

  /// Shared generic-town objects hidden while a k4 layout is shown (quay, terrace floors and keeper buildings stay).
  public const string SharedBackName = "SharedBackK4";
  static readonly string[] SharedHide = { SharedBackName, "Props" };
  /// Rides on each layout object: when the layout is active, the shared back blocks / side rock walls / market stalls are off.
  public class SharedHider : MonoBehaviour {
    void OnEnable() { Set(false); var g = FindObjectOfType<Game>(); if (g != null) g.SendMessage("ApplyKeeperSkinK4", name.Replace("LayoutK4_", ""), SendMessageOptions.DontRequireReceiver); } /* keeper skins (keeper_skins.diff); no-op until merged */
    void OnDisable() => Set(true);
    void Set(bool on) { var town = transform.parent != null ? transform.parent.parent : null; if (town == null) return;
      foreach (var n in SharedHide) { var c = town.Find(n); if (c != null) c.gameObject.SetActive(on); } }
  }

  public static GameObject Build(string town, Transform parent, Material mat, Func<int, Rect> tu) {
    var mb = new MeshB();
    if (town == "Debal") Debal(mb, tu); else if (town == "Muscat") Muscat(mb, tu); else Gwadar(mb, tu);
    LandmarkCrag(mb, tu, town);
    var g = new GameObject("LayoutK4_" + town); g.transform.SetParent(parent, false);
    g.AddComponent<MeshFilter>().sharedMesh = mb.Build();
    g.AddComponent<MeshRenderer>().sharedMaterial = mat;
    g.AddComponent<SharedHider>();
    Debug.Log($"GB k4 layout {town} tris {mb.Tris}");
    return g;
  }

  /// k4: irregular rock base under each town landmark (CAD lowers landmarks to y≈16): Gwadar left cliff (-8,·,56),
  /// Debal top-right (4.5,·,56), Muscat right crag (6,·,56). Cluster of noisy boulders (24x14 rings each, ~670 tris) in
  /// several rock tones, rising from y≈4 to a flattened ledge at y 16 for the landmark's plinth. Seeded → identical every run.
  public const float CragTopY = 16f;
  static void LandmarkCrag(MeshB mb, Func<int, Rect> tu, string town) {
    Vector3 lm = town == "Debal" ? new Vector3(4.5f, CragTopY, 56f) : town == "Muscat" ? new Vector3(6f, CragTopY, 56f) : new Vector3(-8f, CragTopY, 56f);
    var r = new System.Random(town.GetHashCode() & 0xffff);
    int[] tones = town == "Debal" ? new[] { Stone, Sand, Ochre, Rock } : town == "Muscat" ? new[] { Rock, Dark, Rock, Stone } : new[] { Rock, Ochre, Sand, Rock };
    float spread = town == "Gwadar" ? 9f : 6.5f, lean = town == "Gwadar" ? -4f : 2.5f; // Gwadar cliff spreads LEFT, others lean right/back
    // core mass: big boulder whose flattened top is the ledge (top exactly CragTopY)
    Boulder(mb, tu, new Vector3(lm.x, CragTopY - 5.5f, lm.z), new Vector3(spread * 0.95f, 5.5f, 6.5f), r, tones[0], CragTopY);
    for (int i = 0; i < 9; i++) { float a = (float)r.NextDouble() * Mathf.PI * 2f, d = 0.45f + (float)r.NextDouble() * 0.6f;
      var c = new Vector3(lm.x + lean * (float)r.NextDouble() + Mathf.Cos(a) * spread * d, 4f + (float)r.NextDouble() * 8f, lm.z + Mathf.Sin(a) * 5f * d);
      float s = 2.2f + (float)r.NextDouble() * 3.2f;
      Boulder(mb, tu, c, new Vector3(s * (1f + (float)r.NextDouble() * 0.6f), s * (0.8f + (float)r.NextDouble() * 0.7f), s), r, tones[(i + 1) % tones.Length], CragTopY - 0.3f); }
    for (int i = 0; i < 14; i++) { var c = new Vector3(lm.x + ((float)r.NextDouble() - 0.5f) * spread * 2.2f, CragTopY - 9f + (float)r.NextDouble() * 3f, lm.z - 5f - (float)r.NextDouble() * 3f);
      float s = 0.6f + (float)r.NextDouble() * 1.4f; Boulder(mb, tu, c, new Vector3(s, s * 0.7f, s), r, tones[i % tones.Length], 99f); } // scree at the foot
  }
  /// Noisy lat-long rock: per-vertex radial jitter + 3 lumps, squashed and clipped flat at maxY.
  static void Boulder(MeshB mb, Func<int, Rect> tu, Vector3 c, Vector3 rad, System.Random r, int cell, float maxY) {
    const int SEG = 24, RING = 14; var uv = tu(cell); var P = new Vector3[RING + 1, SEG + 1];
    var lumps = new Vector3[3]; for (int k = 0; k < 3; k++) lumps[k] = new Vector3((float)r.NextDouble() - 0.5f, (float)r.NextDouble() - 0.3f, (float)r.NextDouble() - 0.5f).normalized;
    float ph = (float)r.NextDouble() * 10f;
    for (int j = 0; j <= RING; j++) for (int i = 0; i <= SEG; i++) {
      float th = Mathf.PI * j / RING, fi = Mathf.PI * 2f * (i % SEG) / SEG; var n = new Vector3(Mathf.Sin(th) * Mathf.Cos(fi), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(fi));
      float k = 1f + 0.18f * (Mathf.PerlinNoise(n.x * 2.3f + ph, n.z * 2.3f + n.y * 1.7f) - 0.5f) * 2f + 0.06f * (Mathf.PerlinNoise(n.x * 7f + ph, n.y * 7f) - 0.5f) * 2f;
      foreach (var l in lumps) k += 0.12f * Mathf.Max(0, Vector3.Dot(n, l) - 0.6f) * 2.5f;
      var p = c + Vector3.Scale(n * k, rad); if (p.y > maxY) p.y = maxY + (p.y - maxY) * 0.04f; // flat ledge, slight irregularity
      P[j, i] = p; }
    for (int j = 0; j < RING; j++) for (int i = 0; i < SEG; i++) {
      Rect q = new Rect(uv.xMin + uv.width * i / SEG, uv.yMin + uv.height * j / RING, uv.width / SEG, uv.height / RING);
      mb.Quad(P[j + 1, i], P[j + 1, i + 1], P[j, i + 1], P[j, i], q, true); } // two-sided: winding-proof
  }

  static void House(MeshB mb, Func<int, Rect> tu, Vector3 basePos, Vector3 size, int wall, int roofCell, int door, float rotY = 0) {
    var q = Quaternion.Euler(0, rotY, 0);
    mb.Box(basePos + Vector3.up * size.y * 0.5f, size, q, tu(wall));
    mb.Box(basePos + Vector3.up * (size.y + 0.12f), new Vector3(size.x + 0.3f, 0.24f, size.z + 0.3f), q, tu(roofCell)); // flat parapet roof
    mb.Box(basePos + q * new Vector3(0, 0.9f, -size.z * 0.5f - 0.05f), new Vector3(0.9f, 1.8f, 0.12f), q, tu(door));
  }

  // ---------------- Gwadar: low, flat, wide crescent; cliff + watchtower LEFT; small dome mosque centre-back ----------------
  static void Gwadar(MeshB mb, Func<int, Rect> tu) {
    // sandy crescent beach + quay lip (wide, flat)
    for (int i = 0; i < 11; i++) { float a = Mathf.Lerp(-60, 60, i / 10f) * Mathf.Deg2Rad; var p = new Vector3(Mathf.Sin(a) * 24f, -0.2f, 8f - Mathf.Cos(a) * 10f);
      mb.Box(p, new Vector3(5.2f, 0.4f, 6f), Quaternion.Euler(0, a * Mathf.Rad2Deg, 0), tu(Sand));
      mb.Box(p + new Vector3(0, 0.25f, -2.8f), new Vector3(5.2f, 0.5f, 0.6f), Quaternion.Euler(0, a * Mathf.Rad2Deg, 0), tu(Stone)); }
    // ONE level of low mud-brick houses, spread with gaps, following the crescent (left→right: harbour master, shipwright, market, tavern, mosque)
    float[] xs = { -15f, -10.5f, -5f, 5f, 10.5f, 15.5f };
    for (int i = 0; i < xs.Length; i++) { float x = xs[i]; float z = 9f + Mathf.Abs(x) * -0.25f + 4f;
      House(mb, tu, new Vector3(x, 0, z), new Vector3(3.4f, 2.4f + (i % 2) * 0.4f, 3.0f), Ochre, Sand, Indigo, x * 0.8f); }
    // small dome mosque centre-back (low; well below landmark)
    mb.Box(new Vector3(3.5f, 1.6f, 20f), new Vector3(4.2f, 3.2f, 4.2f), tu(Plaster));
    mb.Cyl(new Vector3(3.5f, 3.2f, 20f), new Vector3(3.5f, 4.6f, 20f), 1.9f, 1.4f, 12, tu(Whitewash));
    mb.Cyl(new Vector3(3.5f, 4.6f, 20f), new Vector3(3.5f, 5.6f, 20f), 1.4f, 0.15f, 12, tu(Whitewash));
    mb.Cyl(new Vector3(6.2f, 0f, 21.5f), new Vector3(6.2f, 6.2f, 21.5f), 0.45f, 0.35f, 8, tu(Plaster)); // thin minaret
    // LEFT cliff + squat mud-brick watchtower on it (in-town; the GUIDE landmark stays far back at z55)
    mb.Box(new Vector3(-21f, 3f, 12f), new Vector3(7f, 6f, 12f), Quaternion.Euler(0, 10, 0), tu(Rock));
    mb.Box(new Vector3(-22f, 6.5f, 14f), new Vector3(5f, 2f, 8f), Quaternion.Euler(0, 16, 4), tu(Rock));
    mb.Cyl(new Vector3(-20.5f, 7.4f, 12f), new Vector3(-20.5f, 11f, 12f), 1.5f, 1.3f, 10, tu(Ochre));
    for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3; mb.Box(new Vector3(-20.5f + Mathf.Cos(a) * 1.25f, 11.3f, 12f + Mathf.Sin(a) * 1.25f), new Vector3(0.5f, 0.6f, 0.5f), tu(Ochre)); }
    // props: beached boats, net racks (dark teak, grey nets, 1.3x tall), fish baskets
    for (int i = 0; i < 3; i++) { var c = new Vector3(-6f + i * 5.5f, 0.35f, 3.5f - (i == 1 ? 0.8f : 0));
      mb.Box(c, new Vector3(1.1f, 0.6f, 3.6f), Quaternion.Euler(0, 20 - i * 15, 8), tu(Wood)); }
    for (int i = 0; i < 4; i++) { float x = -13f + i * 1.3f;
      mb.Cyl(new Vector3(x, 0, 6.5f), new Vector3(x, 2.9f, 6.5f), 0.06f, 0.06f, 4, tu(DarkTeak));
      mb.Box(new Vector3(x + 0.65f, 1.6f, 6.5f), new Vector3(1.2f, 2.1f, 0.05f), tu(Rope)); }
    for (int i = 0; i < 6; i++) mb.Cyl(new Vector3(8f + i * 0.9f, 0f, 5.5f), new Vector3(8f + i * 0.9f, 0.5f, 5.5f), 0.32f, 0.38f, 7, tu(Wood));
  }

  // ---------------- Debal: tall, 3 stepped dense terraces behind a long wooden pier; teak customs house ON the pier ----------------
  static void Debal(MeshB mb, Func<int, Rect> tu) {
    // long wooden pier out to sea
    mb.Box(new Vector3(7f, 0.3f, -6f), new Vector3(3.2f, 0.3f, 20f), tu(Planks));
    for (int i = 0; i < 8; i++) { float z = -15f + i * 2.6f; mb.Cyl(new Vector3(5.5f, -2f, z), new Vector3(5.5f, 0.3f, z), 0.15f, 0.15f, 5, tu(DarkTeak)); mb.Cyl(new Vector3(8.5f, -2f, z), new Vector3(8.5f, 0.3f, z), 0.15f, 0.15f, 5, tu(DarkTeak)); }
    // teak-balconied customs house on the pier
    mb.Box(new Vector3(7f, 2.3f, -10f), new Vector3(3f, 4f, 3.6f), tu(Teak));
    mb.Box(new Vector3(7f, 3.2f, -12.1f), new Vector3(3.4f, 0.15f, 0.9f), tu(DarkTeak)); // balcony
    for (int i = -1; i <= 1; i++) mb.Cyl(new Vector3(7f + i * 1.4f, 3.2f, -12.5f), new Vector3(7f + i * 1.4f, 4f, -12.5f), 0.05f, 0.05f, 4, tu(DarkTeak));
    mb.Box(new Vector3(7f, 4.6f, -10f), new Vector3(3.6f, 0.6f, 4.2f), tu(Roof));
    // 3 stacked terrace levels, tightly packed, narrow lanes; heights climb with level
    for (int lv = 0; lv < 3; lv++) {
      float y = RowY(lv) * 1.6f, z = RowZ(lv) + 1f; // steeper than base grid → reads tall
      mb.Box(new Vector3(0, y - 0.6f, z + 2f), new Vector3(40f, 1.2f, 7f), tu(Stone)); // terrace retaining wall
      for (int k = 0; k < 10; k++) { float x = -18f + k * 4f; if (Mathf.Abs(x) < 2.2f) continue; // keep centre stair
        if (lv == 2 && x > 0 && x < 12) continue; // leave top-right open under the Debal spire/pennant
        float h = 3.4f + lv * 0.8f + ((k * 7 + lv) % 3) * 0.9f;
        House(mb, tu, new Vector3(x, y, z + (k % 2) * 0.8f), new Vector3(3.4f, h, 3.2f), k % 3 == 0 ? Ochre : Stone, Stone, Dark);
        if (k % 2 == 0) mb.Box(new Vector3(x, y + 1.9f, z - 1.9f), new Vector3(2.6f, 0.12f, 1.1f), Quaternion.Euler(-14, 0, 0), tu(k % 4 == 0 ? Gold : AwnTeal)); } // saffron/teal awnings (red reserved for pennant)
    }
    // market stalls + rug stalls + crates on the quay
    for (int i = 0; i < 4; i++) { float x = -14f + i * 3.2f; mb.Box(new Vector3(x, 0.5f, 3f), new Vector3(2.4f, 1f, 1.4f), tu(Wood)); mb.Box(new Vector3(x, 2.2f, 3f), new Vector3(2.8f, 0.1f, 1.8f), tu(Gold)); mb.Box(new Vector3(x, 1.2f, 3.75f), new Vector3(1.8f, 1.4f, 0.05f), tu(Rug)); }
    for (int i = 0; i < 5; i++) mb.Box(new Vector3(3.5f + (i % 3) * 0.9f, 0.4f + (i / 3) * 0.8f, 1.5f), new Vector3(0.8f, 0.8f, 0.8f), tu(Wood));
  }

  // ---------------- Muscat: horseshoe cove, dark crags both sides, 2 whitewash levels, white fort on RIGHT crag + gatehouse ----------------
  static void Muscat(MeshB mb, Func<int, Rect> tu) {
    // horseshoe of dark rock: arms reach toward the sea on both sides
    for (int s = -1; s <= 1; s += 2) for (int i = 0; i < 5; i++) {
      float z = -10f + i * 6f, x = s * (21f - i * 0.6f), h = 9f + i * 2f + (s > 0 ? 3f : 0);
      mb.Box(new Vector3(x, h * 0.5f - 1f, z), new Vector3(7f, h, 7f), Quaternion.Euler(0, i * 13 * s, s * 5), tu(Rock)); }
    // back of the cove: low pale-limestone (#B8AE9A via Muscat skin) ridge, top y≈8.5, so the fort on its right-crag base (y16, z56) shows in full;
    // horseshoe kept by the tall side arms. Two offset slabs + a dip on the right under the fort.
    mb.Box(new Vector3(-9f, 3.2f, 32f), new Vector3(26f, 10.5f, 7f), Quaternion.Euler(0, 4, 0), tu(Marble));
    mb.Box(new Vector3(12f, 1.8f, 33f), new Vector3(18f, 7.5f, 7f), Quaternion.Euler(0, -5, 0), tu(Marble));
    // 2 levels of whitewashed flat-roof blocks hugging the cove curve
    for (int lv = 0; lv < 2; lv++) for (int k = 0; k < 9; k++) {
      float a = Mathf.Lerp(-70, 70, k / 8f) * Mathf.Deg2Rad, r = 14f + lv * 4f;
      var p = new Vector3(Mathf.Sin(a) * r, lv * 3.6f, 10f + (1 - Mathf.Cos(a)) * -6f + lv * 6f);
      if (Mathf.Abs(p.x) < 2.2f) continue;
      House(mb, tu, p, new Vector3(3.6f, 3.2f, 3.2f), Whitewash, Whitewash, k % 3 == 0 ? Gold : DarkTeak, a * Mathf.Rad2Deg); }
    // round white crenellated fort on the RIGHT crag
    var f = new Vector3(19f, 14f, 6f);
    mb.Cyl(f, f + Vector3.up * 5f, 3f, 2.8f, 14, tu(Whitewash));
    for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6; mb.Box(f + new Vector3(Mathf.Cos(a) * 2.7f, 5.4f, Mathf.Sin(a) * 2.7f), new Vector3(0.7f, 0.8f, 0.5f), tu(Whitewash)); }
    mb.Box(f + new Vector3(-3.5f, -2f, 1f), new Vector3(4f, 3f, 3f), tu(Whitewash)); // curtain wall down the crag
    // gatehouse below the fort leading to the palace
    var gh = new Vector3(13f, 0, 16f);
    mb.Box(gh + Vector3.up * 2.5f, new Vector3(6f, 5f, 3f), tu(Whitewash));
    mb.Box(gh + new Vector3(0, 1.4f, -1.55f), new Vector3(1.8f, 2.8f, 0.15f), tu(DarkTeak));
    for (int i = -2; i <= 2; i++) mb.Box(gh + new Vector3(i * 1.2f, 5.35f, -1.2f), new Vector3(0.6f, 0.7f, 0.4f), tu(Whitewash));
    mb.Box(gh + new Vector3(0, 3.6f, -1.6f), new Vector3(2.2f, 0.25f, 0.1f), tu(Gold)); // brass band
    // customs tables at the dock, palms, guards (posts)
    for (int i = 0; i < 3; i++) mb.Box(new Vector3(-6f + i * 3f, 0.45f, 2f), new Vector3(2f, 0.9f, 1f), tu(Teak));
    foreach (var px in new[] { -9f, 5f, 10f }) { mb.Cyl(new Vector3(px, 0, 5f), new Vector3(px + 0.4f, 4.5f, 5f), 0.15f, 0.12f, 6, tu(Wood));
      for (int i = 0; i < 5; i++) { float a = i * Mathf.PI * 2 / 5; mb.Box(new Vector3(px + 0.4f + Mathf.Cos(a) * 0.9f, 4.4f, 5f + Mathf.Sin(a) * 0.9f), new Vector3(1.8f, 0.08f, 0.5f), Quaternion.Euler(0, -a * Mathf.Rad2Deg, -18), tu(Green)); } }
    for (int i = -1; i <= 1; i += 2) mb.Cyl(gh + new Vector3(i * 1.6f, 0, -2.2f), gh + new Vector3(i * 1.6f, 1.8f, -2.2f), 0.22f, 0.2f, 6, tu(Indigo));
  }
}
