using System.Collections.Generic;
using UnityEngine;

/// Detailed low-poly dhows (one mesh + one 1024 atlas per ship, ~3-4k tris) after hasan_dhow_detail_sheet.jpg.
/// Hasan: carved figurehead, indigo/gold trim with brass studs, lattice stern cabin, lanterns, cargo, patched lateen sail, rigging, pennant.
/// Pirate: two masts, straight raked bow with a ram beak, darker wood, black-and-red patched sails, broken rail and scorch damage.
public class MeshB {
  public List<Vector3> v = new List<Vector3>(); public List<Vector2> uv = new List<Vector2>(); public List<int> t = new List<int>();
  public int Tris => t.Count / 3;
  public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Rect r, bool both = false) {
    int i = v.Count; v.AddRange(new[] { a, b, c, d }); uv.AddRange(new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) });
    t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 }); if (both) Quad(b, a, d, c, r); }
  public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, bool both = true) {
    int i = v.Count; v.AddRange(new[] { a, b, c }); uv.AddRange(new[] { ua, ub, uc }); t.AddRange(new[] { i, i + 1, i + 2 }); if (both) Tri(a, c, b, ua, uc, ub, false); }
  public void Box(Vector3 c, Vector3 s, Quaternion q, Rect r) {
    Vector3 P(float x, float y, float z) => c + q * Vector3.Scale(new Vector3(x, y, z), s * 0.5f);
    Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), r); Quad(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), r);
    Quad(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), r); Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), r);
    Quad(P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1), r); Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), r); }
  public void Box(Vector3 c, Vector3 s, Rect r) => Box(c, s, Quaternion.identity, r);
  public void Cyl(Vector3 a, Vector3 b, float ra, float rb, int sides, Rect r) {
    var ax = (b - a).normalized; var p1 = Vector3.Cross(ax, Mathf.Abs(ax.y) < 0.9f ? Vector3.up : Vector3.right).normalized; var p2 = Vector3.Cross(ax, p1);
    for (int i = 0; i < sides; i++) { float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides; var d0 = p1 * Mathf.Cos(a0) + p2 * Mathf.Sin(a0); var d1 = p1 * Mathf.Cos(a1) + p2 * Mathf.Sin(a1);
      float u0 = r.xMin + r.width * i / sides, u1 = r.xMin + r.width * (i + 1) / sides; int k = v.Count;
      v.AddRange(new[] { a + d0 * ra, a + d1 * ra, b + d1 * rb, b + d0 * rb }); uv.AddRange(new[] { new Vector2(u0, r.yMin), new Vector2(u1, r.yMin), new Vector2(u1, r.yMax), new Vector2(u0, r.yMax) });
      t.AddRange(new[] { k, k + 2, k + 1, k, k + 3, k + 2 }); } }
  public void Lathe(Vector3 c, float[] radii, float hgt, int sides, Rect r) { for (int j = 0; j < radii.Length - 1; j++) { float y0 = hgt * j / (radii.Length - 1), y1 = hgt * (j + 1) / (radii.Length - 1);
      var rr = new Rect(r.xMin, r.yMin + r.height * j / (radii.Length - 1), r.width, r.height / (radii.Length - 1)); Cyl(c + Vector3.up * y0, c + Vector3.up * y1, radii[j], radii[j + 1], sides, rr); } }
  public static Rect RopeUV; public void Rope(Vector3 a, Vector3 b) => Cyl(a, b, 0.035f, 0.035f, 3, RopeUV);
  public Mesh Build() { var m = new Mesh(); m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds(); return m; }
}

public static class Dhow {
  static Rect U(RectInt r) => ShipArt.UV(r);
  static Rect Sub(RectInt r, float u0, float v0, float u1, float v1) { var q = U(r); return new Rect(q.xMin + q.width * u0, q.yMin + q.height * v0, q.width * (u1 - u0), q.height * (v1 - v0)); }
  public static int LastTris;
  // hull half-width and sheer height along the hull (t: 0 stern .. 1 bow)
  static float Half(float t, bool pirate) => Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Lerp(0.22f, 1f, t)), pirate ? 0.5f : 0.65f) * (pirate ? 1.5f : 1.8f);
  static float Sheer(float t, bool pirate) => pirate ? 1.2f + t * 0.7f + (1 - t) * 0.25f : 1.15f + Mathf.Pow(t, 2.6f) * 1.6f + (1 - t) * 0.55f;
  const float L = 11f; static float Z(float t) => -L * 0.45f + t * L;

  public static Transform Build(Transform parent, UITheme T, bool pirate, string name) {
    var mb = new MeshB(); var rnd = new System.Random(pirate ? 7 : 3);
    int NS = 20; int G = 6; // sections, girth points
    // ---- hull loft (planks) ----
    var ring = new List<Vector3[]>();
    for (int i = 0; i <= NS; i++) { float t = i / (float)NS, w = Half(t, pirate), sh = Sheer(t, pirate), z = Z(t); if (t > 0.96f) w *= 0.12f;
      var pts = new Vector3[G + 1]; for (int j = 0; j <= G; j++) { float a = j / (float)G; float ang = Mathf.Lerp(-1, 1, a) * Mathf.PI / 2; // -90 (port) .. 90 (starboard)
        float y = Mathf.Lerp(-0.75f + (pirate ? 0.1f : 0), sh, Mathf.Pow(Mathf.Abs(Mathf.Sin(ang)), 0.8f)); float x = Mathf.Sin(ang) * w * Mathf.Lerp(0.55f, 1f, Mathf.Abs(Mathf.Sin(ang)));
        pts[j] = new Vector3(x, Mathf.Abs(ang) < 0.01f ? -0.75f : y, z); }
      ring.Add(pts); }
    var hull = U(ShipArt.HULL);
    for (int i = 0; i < NS; i++) for (int j = 0; j < G; j++) { var r = new Rect(hull.xMin + hull.width * i / NS, hull.yMin + hull.height * j / G, hull.width / NS, hull.height / G);
      mb.Quad(ring[i][j], ring[i + 1][j], ring[i + 1][j + 1], ring[i][j + 1], r, true); }
    // transom
    mb.Quad(ring[0][0], ring[0][G / 2], ring[0][G / 2], ring[0][G], U(ShipArt.HULL), true); for (int j = 0; j < G; j++) mb.Tri(ring[0][j], ring[0][j + 1], new Vector3(0, ring[0][j].y * 0.3f, ring[0][0].z), Vector2.zero, Vector2.zero, Vector2.zero);
    // deck
    for (int i = 0; i < NS; i++) { float y0 = Sheer(i / (float)NS, pirate) - 0.35f, y1 = Sheer((i + 1) / (float)NS, pirate) - 0.35f; float w0 = Half(i / (float)NS, pirate) * 0.95f, w1 = Half((i + 1) / (float)NS, pirate) * 0.95f;
      mb.Quad(new Vector3(-w0, y0, Z(i / (float)NS)), new Vector3(w0, y0, Z(i / (float)NS)), new Vector3(w1, y1, Z((i + 1) / (float)NS)), new Vector3(-w1, y1, Z((i + 1) / (float)NS)), Sub(ShipArt.DECK, 0, i / (float)NS, 1, (i + 1) / (float)NS), true); }
    // trim band (indigo/gold or dark red) + top rail, with brass studs; pirate has broken rail sections
    for (int side = -1; side <= 1; side += 2) for (int i = 0; i < NS; i++) {
      float t0 = i / (float)NS, t1 = (i + 1) / (float)NS; bool broken = pirate && (i == 7 || i == 8 || i == 14);
      Vector3 a0 = new Vector3(side * Half(t0, pirate) * 1.01f, Sheer(t0, pirate) - 0.55f, Z(t0)), a1 = new Vector3(side * Half(t1, pirate) * 1.01f, Sheer(t1, pirate) - 0.55f, Z(t1));
      Vector3 b0 = a0 + Vector3.up * 0.5f, b1 = a1 + Vector3.up * 0.5f; var tr = Sub(ShipArt.TRIM, t0, 0, t1, 1);
      if (side > 0) mb.Quad(a0, a1, b1, b0, tr); else mb.Quad(a1, a0, b0, b1, tr);
      if (!broken) { mb.Box((b0 + b1) / 2 + Vector3.up * 0.08f, new Vector3(0.14f, 0.12f, (b1 - b0).magnitude + 0.02f), SafeRot.Look(b1 - b0, Quaternion.identity) /* gamedev_t8: zero-length spar segments at build time (12 warnings per launch) */, U(ShipArt.MAST));
        if (i % 2 == 0) mb.Box(b0 + Vector3.up * 0.3f, new Vector3(0.09f, 0.45f, 0.09f), U(ShipArt.MAST)); }
      if (i % 1 == 0 && !(broken && rnd.Next(2) == 0)) mb.Box(Vector3.Lerp(a0, b0, 0.5f) + new Vector3(side * 0.03f, 0, 0), new Vector3(0.13f, 0.13f, 0.13f), U(ShipArt.BRASS)); }
    if (!pirate) {
      // stem post curling up into a carved figurehead
      Vector3 prev = new Vector3(0, Sheer(1, false) - 0.4f, Z(1)); for (int k = 1; k <= 5; k++) { float a = k / 5f; var p = new Vector3(0, Sheer(1, false) - 0.4f + a * 1.6f, Z(1) + Mathf.Sin(a * 1.4f) * 1.1f);
        mb.Cyl(prev, p, 0.26f - a * 0.06f, 0.24f - a * 0.06f, 6, U(ShipArt.FIGURE)); prev = p; }
      mb.Box(prev + new Vector3(0, 0.15f, 0.12f), new Vector3(0.38f, 0.5f, 0.45f), Quaternion.Euler(-20, 0, 0), U(ShipArt.FIGURE)); // carved head
      mb.Cyl(prev + new Vector3(-0.22f, 0.2f, -0.1f), prev + new Vector3(0.22f, 0.2f, -0.1f), 0.16f, 0.16f, 6, U(ShipArt.BRASS)); // brass scroll
      mb.Box(new Vector3(0, Sheer(0.97f, false) - 0.2f, Z(0.97f)), new Vector3(0.3f, 0.12f, 1.4f), U(ShipArt.BRASS));
      // lattice stern cabin with indigo roof, rail and lanterns
      Vector3 cc = new Vector3(0, Sheer(0.12f, false) + 0.55f, Z(0.14f)); Vector3 cs = new Vector3(2.7f, 1.6f, 2.6f);
      mb.Box(cc, cs, U(ShipArt.CABIN));
      for (int k = -1; k <= 1; k++) { mb.Quad(cc + new Vector3(k * 0.8f - 0.3f, -0.45f, cs.z / 2 + 0.02f), cc + new Vector3(k * 0.8f + 0.3f, -0.45f, cs.z / 2 + 0.02f), cc + new Vector3(k * 0.8f + 0.3f, 0.5f, cs.z / 2 + 0.02f), cc + new Vector3(k * 0.8f - 0.3f, 0.5f, cs.z / 2 + 0.02f), U(ShipArt.LATTICE)); }
      for (int sd = -1; sd <= 1; sd += 2) for (int k = -1; k <= 1; k += 2) { var c0 = cc + new Vector3(sd * (cs.x / 2 + 0.02f), -0.4f, k * 0.6f - 0.3f); var c1 = c0 + new Vector3(0, 0, 0.6f);
        if (sd > 0) mb.Quad(c1, c0, c0 + Vector3.up * 0.9f, c1 + Vector3.up * 0.9f, U(ShipArt.LATTICE)); else mb.Quad(c0, c1, c1 + Vector3.up * 0.9f, c0 + Vector3.up * 0.9f, U(ShipArt.LATTICE)); }
      mb.Box(cc + Vector3.up * (cs.y / 2 + 0.1f), new Vector3(cs.x + 0.4f, 0.2f, cs.z + 0.4f), U(ShipArt.TRIM));
      for (int k = 0; k < 8; k++) { float a = k / 8f * Mathf.PI * 2; var rp = cc + Vector3.up * (cs.y / 2 + 0.45f) + new Vector3(Mathf.Clamp(Mathf.Cos(a) * 2, -1, 1) * (cs.x / 2 + 0.1f), 0, Mathf.Clamp(Mathf.Sin(a) * 2, -1, 1) * (cs.z / 2 + 0.1f)); mb.Box(rp, new Vector3(0.1f, 0.5f, 0.1f), U(ShipArt.MAST)); }
      mb.Box(cc + Vector3.up * (cs.y / 2 + 0.7f) + new Vector3(0, 0, cs.z / 2 + 0.1f), new Vector3(cs.x + 0.2f, 0.08f, 0.08f), U(ShipArt.MAST));
      Lantern(mb, cc + new Vector3(cs.x / 2 + 0.05f, cs.y / 2 + 0.2f, -cs.z / 2)); Lantern(mb, cc + new Vector3(-cs.x / 2 - 0.05f, cs.y / 2 + 0.2f, -cs.z / 2));
      Lantern(mb, new Vector3(0.9f, Sheer(0.8f, false) + 0.1f, Z(0.8f)));
    } else {
      // straight raked bow with a ram beak; low aft platform; scorch/hole patch
      mb.Cyl(new Vector3(0, Sheer(1, true) - 0.6f, Z(1)), new Vector3(0, Sheer(1, true) + 1.2f, Z(1) + 0.9f), 0.24f, 0.18f, 6, U(ShipArt.MAST));
      mb.Cyl(new Vector3(0, 0.4f, Z(0.97f)), new Vector3(0, 0.7f, Z(1) + 2.6f), 0.3f, 0.04f, 5, U(ShipArt.BRASS)); // ram beak
      mb.Box(new Vector3(0, Sheer(0.08f, true) + 0.35f, Z(0.1f)), new Vector3(2.6f, 0.3f, 2.2f), U(ShipArt.DECK));
      mb.Quad(new Vector3(Half(0.45f, true) * 1.03f, 0.6f, Z(0.4f)), new Vector3(Half(0.5f, true) * 1.03f, 0.6f, Z(0.5f)), new Vector3(Half(0.5f, true) * 1.03f, 1.1f, Z(0.5f)), new Vector3(Half(0.45f, true) * 1.03f, 1.1f, Z(0.4f)), U(ShipArt.DARK)); // shot hole
      Lantern(mb, new Vector3(0, Sheer(0.1f, true) + 0.7f, Z(0.0f) + 0.2f));
    }
    // cargo: crates, sacks, jars
    float dy(float t) => Sheer(t, pirate) - 0.35f;
    var cr = U(ShipArt.CRATE); mb.Box(new Vector3(-0.5f, dy(0.42f) + 0.3f, Z(0.42f)), new Vector3(0.8f, 0.6f, 0.8f), cr); mb.Box(new Vector3(0.45f, dy(0.45f) + 0.3f, Z(0.45f)), new Vector3(0.75f, 0.6f, 0.9f), cr);
    mb.Box(new Vector3(-0.4f, dy(0.42f) + 0.85f, Z(0.43f)), new Vector3(0.6f, 0.5f, 0.6f), Quaternion.Euler(0, 15, 0), cr); mb.Box(new Vector3(0.2f, dy(0.62f) + 0.3f, Z(0.62f)), new Vector3(0.9f, 0.6f, 0.7f), Quaternion.Euler(0, -10, 0), cr);
    if (!pirate) mb.Box(new Vector3(-0.6f, dy(0.6f) + 0.3f, Z(0.6f)), new Vector3(0.6f, 0.6f, 0.6f), cr);
    float[] sackR = { 0.30f, 0.34f, 0.30f, 0.20f, 0.09f, 0.13f }; /* tied burlap bag, not a ball */ var sk = U(ShipArt.SACK);
    for (int k = 0; k < (pirate ? 2 : 4); k++) mb.Lathe(new Vector3(-0.9f + k * 0.55f, dy(0.3f) + 0.0f, Z(0.3f) + (k % 2) * 0.4f), sackR, 0.8f, 7, sk);
    float[] jarR = { 0.15f, 0.32f, 0.36f, 0.22f, 0.12f, 0.16f }; var jr = U(ShipArt.JAR);
    for (int k = 0; k < (pirate ? 1 : 3); k++) mb.Lathe(new Vector3(1.0f - k * 0.1f, dy(0.3f), Z(0.25f) + k * 0.6f), jarR, 0.9f, 8, jr);
    // masts, yards, sails, rigging
    MeshB.RopeUV = U(ShipArt.ROPE); var mast = U(ShipArt.MAST);
    var masts = pirate ? new[] { (z: Z(0.48f), h: 9.0f, rake: 6f, yardLen: 11f, sail: 1f), (z: Z(0.8f), h: 6.5f, rake: 12f, yardLen: 7.5f, sail: 0.7f) }
                       : new[] { (z: Z(0.55f), h: 10.5f, rake: 8f, yardLen: 13.5f, sail: 1f), (z: Z(0.18f) - 1.0f, h: 4.5f, rake: 0f, yardLen: 0f, sail: 0f) };
    int mi = 0;
    foreach (var m in masts) {
      var foot = new Vector3(0, dy((m.z + L * 0.45f) / L), m.z); var top = foot + Quaternion.Euler(m.rake, 0, 0) * Vector3.up * m.h;
      mb.Cyl(foot, top, 0.2f * m.h / 10 + 0.05f, 0.1f, 8, mast);
      if (m.yardLen > 0) {
        var yA = new Vector3(0.3f, foot.y + 1.0f, m.z + m.yardLen * 0.55f); var yB = new Vector3(0.3f, top.y + 1.2f * m.sail, m.z - m.yardLen * 0.45f);
        if (pirate && mi == 1) yB += new Vector3(0.5f, -1.2f, 0); // damaged, sagging fore yard
        mb.Cyl(yA, yB, 0.1f, 0.06f, 6, mast);
        var clew = new Vector3(0.6f, foot.y + 0.9f, m.z - m.yardLen * 0.35f); Sail(mb, yA, yB, clew, pirate, mi);
        mb.Rope(top, new Vector3(0, dy(0.98f) + 0.2f, Z(0.99f))); mb.Rope(top, new Vector3(0, dy(0.02f) + 0.3f, Z(0.02f)));
        for (int sd = -1; sd <= 1; sd += 2) { mb.Rope(top + Vector3.down * 0.4f, new Vector3(sd * Half(0.5f, pirate), Sheer(0.5f, pirate), m.z - 0.8f)); mb.Rope(top + Vector3.down * 0.4f, new Vector3(sd * Half(0.5f, pirate), Sheer(0.5f, pirate), m.z + 0.8f)); }
        mb.Rope(clew, new Vector3(1.1f, dy(0.25f) + 0.2f, Z(0.25f))); mb.Rope((yA + yB) / 2, top);
      }
      // pennant at masthead
      var pr = U(ShipArt.PENNANT); var pt = (m.yardLen > 0 ? top + Vector3.up * 1.4f : top + Vector3.up * 0.3f);
      mb.Cyl(top, pt, 0.05f, 0.04f, 4, mast); mb.Tri(pt, pt + new Vector3(0, -0.6f, 0), pt + new Vector3(0.2f, -0.3f, -1.8f), new Vector2(pr.xMin, pr.yMax), new Vector2(pr.xMin, pr.yMin), new Vector2(pr.xMax, (pr.yMin + pr.yMax) / 2));
      mi++; }
    // anchor rope down the bow
    mb.Rope(new Vector3(0.5f, Sheer(0.9f, pirate), Z(0.9f)), new Vector3(0.8f, -0.4f, Z(1) + 1.2f));
    var go = new GameObject(name); go.transform.SetParent(parent, false);
    go.AddComponent<MeshFilter>().sharedMesh = mb.Build(); go.AddComponent<MeshRenderer>().sharedMaterial = Resources.Load<Material>(pirate ? "Mats/ShipPirate" : "Mats/ShipHasan");
    LastTris = mb.Tris; Wake(go.transform);
    return go.transform;
  }
  static void Lantern(MeshB mb, Vector3 p) { mb.Box(p + Vector3.up * 0.2f, new Vector3(0.25f, 0.32f, 0.25f), U(ShipArt.GLASS)); mb.Cyl(p + Vector3.up * 0.36f, p + Vector3.up * 0.6f, 0.2f, 0.02f, 4, U(ShipArt.BRASS)); mb.Box(p, new Vector3(0.3f, 0.06f, 0.3f), U(ShipArt.BRASS)); }
  // lateen sail as a billowed, subdivided triangle; the pirate's has a torn trailing edge
  public static Vector3[] HasanSail; /* tack, peak, clew of Hasan's main sail (ship space): the duel fire emits from this surface */
  public static Vector3 SailPoint(Vector3 tack, Vector3 peak, Vector3 clew, float a, float b) { Vector3 belly = Vector3.Cross(peak - tack, clew - tack).normalized; if (belly.x < 0) belly = -belly; var p = tack + (peak - tack) * a + (clew - tack) * b; float bil = Mathf.Sin(Mathf.PI * Mathf.Clamp01(a + b)) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(b / Mathf.Max(0.001f, 1 - a + 0.001f))) * 0.9f; return p + belly * bil; }
  static void Sail(MeshB mb, Vector3 tack, Vector3 peak, Vector3 clew, bool pirate, int idx) { if (!pirate && idx == 0) HasanSail = new[] { tack, peak, clew };
    var r = U(ShipArt.SAIL); int n = 7; Vector3 belly = Vector3.Cross(peak - tack, clew - tack).normalized; if (belly.x < 0) belly = -belly;
    Vector3 P(float a, float b) { var p = tack + (peak - tack) * a + (clew - tack) * b; float bil = Mathf.Sin(Mathf.PI * Mathf.Clamp01(a + b)) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(b / Mathf.Max(0.001f, 1 - a + 0.001f))) * 0.9f; return p + belly * bil; }
    Vector2 Q(float a, float b) => new Vector2(r.xMin + r.width * (a + b), r.yMin + r.height * a); // u along yard+clew, v along yard (matches painter's diagonal)
    for (int i = 0; i < n; i++) for (int j = 0; j < n - i; j++) {
      float a0 = i / (float)n, a1 = (i + 1) / (float)n, b0 = j / (float)n, b1 = (j + 1) / (float)n;
      bool torn = pirate && (i + j == n - 1) && ((i * 7 + j * 3 + idx) % 3 == 0); if (torn) continue;
      mb.Tri(P(a0, b0), P(a1, b0), P(a0, b1), Q(a0, b0), Q(a1, b0), Q(a0, b1));
      if (i + j < n - 1) mb.Tri(P(a1, b0), P(a1, b1), P(a0, b1), Q(a1, b0), Q(a1, b1), Q(a0, b1)); }
  }
  // foam wake: a widening strip astern plus a bow wave
  static void Wake(Transform ship) {
    var mat = Resources.Load<Material>("Mats/Foam"); if (mat == null) return;
    var m = new Mesh(); var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>(); int segs = 12;
    for (int i = 0; i <= segs; i++) { float a = i / (float)segs, z = -L * 0.45f - a * 26f, w = 1.4f + a * 6f; v.Add(new Vector3(-w, 0.25f, z)); v.Add(new Vector3(w, 0.25f, z)); uv.Add(new Vector2(0, a)); uv.Add(new Vector2(1, a)); }
    for (int i = 0; i < segs; i++) { int k = i * 2; t.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 }); }
    int b = v.Count; float bz = L * 0.5f; // bow wave: two flared quads along the sides
    for (int sd = -1; sd <= 1; sd += 2) { int k = v.Count; v.Add(new Vector3(sd * 0.3f, 0.3f, bz)); v.Add(new Vector3(sd * 1.6f, 0.3f, bz - 3f)); v.Add(new Vector3(sd * 3.8f, 0.25f, bz - 9f)); v.Add(new Vector3(sd * 1.9f, 0.3f, bz - 9f));
      uv.AddRange(new[] { new Vector2(0.5f, 0), new Vector2(1, 0.2f), new Vector2(1, 1), new Vector2(0, 1) }); t.AddRange(new[] { k, k + 1, k + 3, k + 1, k + 2, k + 3 }); }
    m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateBounds();
    var g = new GameObject("Wake"); g.transform.SetParent(ship, false); g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = mat; }
}

/* gamedev_t8: LookRotation guard - a zero-length direction logs "Look rotation viewing vector is zero" and returns identity; callers pass a fallback instead */
public static class SafeRot { /* gamedev_t8 */
  public static Quaternion Look(Vector3 f, Quaternion fallback) => f.sqrMagnitude > 1e-10f ? Quaternion.LookRotation(f) : fallback; /* gamedev_t8 */
  public static Quaternion Look(Vector3 f, Vector3 up, Quaternion fallback) => f.sqrMagnitude > 1e-10f && Vector3.Cross(f, up).sqrMagnitude > 1e-12f ? Quaternion.LookRotation(f, up) : Look(f, fallback); /* gamedev_t8 */
} /* gamedev_t8 */
