using System;
using System.Collections.Generic;
using UnityEngine;

/// v0.6g duel fighters after art/duel_fighters_turnaround.jpg and the store banner (playstore_feature_1024x500.jpg):
/// smooth shared-vertex surfaces (no boxes): sculpted head (brow, sockets, nose, cheekbones, lips, chin, ears), eyes,
/// a beard shell shaped along the jaw, wrapped indigo turban with a tail / raider's hair, jointed hands with fingers round the grip.
public static class FighterArt {
  class PB { public List<Vector3> v = new List<Vector3>(); public List<Vector2> uv = new List<Vector2>(); public List<int> t = new List<int>();
    public Mesh Build() { var m = new Mesh(); if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; m.SetVertices(v); while (uv.Count < v.Count) uv.Add(Vector2.zero); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m; } }
  static Dictionary<string, PB> parts; static PB M(string n) { if (!parts.TryGetValue(n, out var b)) parts[n] = b = new PB(); return b; }
  /// (nu x nv) grid; closedU wraps u (shared seam = smooth); keep filters cells
  static void Grid(PB b, int nu, int nv, bool closedU, Func<float, float, Vector3> f, Func<float, float, bool> keep = null, bool flip = false) {
    int cu = closedU ? nu : nu + 1, i0 = b.v.Count, t0 = b.t.Count;
    while (b.uv.Count < b.v.Count) b.uv.Add(Vector2.zero); for (int j = 0; j <= nv; j++) for (int i = 0; i < cu; i++) { b.v.Add(f(i / (float)nu, j / (float)nv)); b.uv.Add(new Vector2(i / (float)nu, j / (float)nv)); }
    for (int j = 0; j < nv; j++) for (int i = 0; i < nu; i++) { if (keep != null && !keep((i + 0.5f) / nu, (j + 0.5f) / nv)) continue;
      int a = i0 + j * cu + i, bb = i0 + j * cu + (i + 1) % cu, c = a + cu, d = bb + cu;
      if (!flip) b.t.AddRange(new[] { a, c, bb, bb, c, d }); else b.t.AddRange(new[] { a, bb, c, bb, d, c }); }
    /* orient outward: triangles must face away from the grid's centroid (closed shapes and shells over the head alike) */
    var cen = Vector3.zero; for (int k = i0; k < b.v.Count; k++) cen += b.v[k]; cen /= Mathf.Max(1, b.v.Count - i0); float sgn = 0;
    for (int k = t0; k < b.t.Count; k += 3) { Vector3 p0 = b.v[b.t[k]], p1 = b.v[b.t[k + 1]], p2 = b.v[b.t[k + 2]]; sgn += Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), (p0 + p1 + p2) / 3 - cen); }
    if (sgn < 0) for (int k = t0; k < b.t.Count; k += 3) { int tmp = b.t[k + 1]; b.t[k + 1] = b.t[k + 2]; b.t[k + 2] = tmp; } }
  static void Ell(this PB b, Vector3 c, Vector3 r, Quaternion q, int n = 14) => Grid(b, n, n / 2 + 2, true, (u, v) => { float th = u * Mathf.PI * 2, ph = Mathf.Lerp(-Mathf.PI / 2, Mathf.PI / 2, v);
    return c + q * Vector3.Scale(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Sin(ph), Mathf.Cos(th) * Mathf.Cos(ph)), r); });
  /// tube along a polyline; rad(s, angle) radius; flat (rx/rz ratio) via sx
  static void Tube(PB b, Vector3[] P, Func<float, float> rad, int sides = 12, float sx = 1f, Vector3? side = null) {
    int n = P.Length; var fr = new Vector3[n]; var up = new Vector3[n];
    Vector3 T0 = (P[1] - P[0]).normalized, ref0 = side ?? (Mathf.Abs(T0.y) < 0.9f ? Vector3.up : Vector3.right); var N = Vector3.Cross(T0, ref0).normalized;
    for (int i = 0; i < n; i++) { var T = (P[Mathf.Min(i + 1, n - 1)] - P[Mathf.Max(i - 1, 0)]).normalized; N = Vector3.Cross(Vector3.Cross(T, N), T).normalized; fr[i] = N; up[i] = Vector3.Cross(T, N); }
    Grid(b, sides, n - 1, true, (u, v) => { int i = Mathf.Clamp(Mathf.RoundToInt(v * (n - 1)), 0, n - 1); float a = u * Mathf.PI * 2, r = rad(i / (float)(n - 1));
      return P[i] + (fr[i] * Mathf.Cos(a) * sx + up[i] * Mathf.Sin(a)) * r; });
    Ell(b, P[0], Vector3.one * rad(0) * 0.98f, Quaternion.identity, 8); Ell(b, P[n - 1], Vector3.one * rad(1) * 0.98f, Quaternion.identity, 8); }
  static Vector3[] Bez(Vector3 a, Vector3 c, Vector3 d, int n = 10) { var r = new Vector3[n]; for (int i = 0; i < n; i++) { float t = i / (float)(n - 1); r[i] = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * d; } return r; }
  /// lathe with elliptical sections, shared vertices; ang0..ang1 optional open arc (vest)
  static void Body(PB b, float y0, float y1, float[] rx, float[] rz, float zOff = 0, int sides = 24, float a0 = 0, float a1 = 360, float xOff = 0) {
    bool closed = a1 - a0 >= 359; int nv = (rx.Length - 1) * 3;
    Grid(b, sides, nv, closed, (u, v) => { float k = v * (rx.Length - 1); int i = Mathf.Min((int)k, rx.Length - 2); float f = Mathf.SmoothStep(0, 1, k - i);
      float RX = Mathf.Lerp(rx[i], rx[i + 1], f), RZ = Mathf.Lerp(rz[i], rz[i + 1], f), a = Mathf.Deg2Rad * Mathf.Lerp(a0, a1, u);
      return new Vector3(xOff + Mathf.Sin(a) * RX, Mathf.Lerp(y0, y1, v), zOff + Mathf.Cos(a) * RZ); }); }
  static float SS(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); }
  static float G(float x, float y, float x0, float y0, float sx, float sy) { float a = (x - x0) / sx, c = (y - y0) / sy; return Mathf.Exp(-a * a - c * c); }

  /// head surface point: th azimuth (0 = face, +z), ph elevation; returns local point (head centre at origin)
  static Vector3 Head(float th, float ph, bool raider) {
    var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Sin(ph), Mathf.Cos(th) * Mathf.Cos(ph));
    var p = Vector3.Scale(d, new Vector3(raider ? 0.087f : 0.082f, 0.122f, 0.104f)); if (p.y > 0.02f) p.x *= 1 - 0.1f * Mathf.Clamp01((p.y - 0.02f) / 0.1f); /* narrower temples, longer oval */
    if (p.y < 0) { float k = Mathf.Pow(-p.y / 0.118f, 1.6f); p.x *= 1 - (raider ? 0.24f : 0.3f) * k; p.z *= 1 - 0.12f * k * Mathf.Clamp01(-d.z); } /* jaw tapers to the chin, nape stays */
    if (p.y > 0.04f) p.z *= 1 + 0.04f * Mathf.Clamp01(-d.z); /* fuller skull at the back */
    float front = SS(0.25f, 0.85f, d.z), x = p.x, y = p.y, ax = Mathf.Abs(x);
    float dz = 0;
    dz += 0.03f * G(x, y, 0, -0.008f, 0.0095f, 0.028f) * Mathf.Clamp01(1 - (y - 0.0f) / 0.04f + 0.6f) + 0.012f * G(x, y, 0, -0.03f, 0.012f, 0.01f) + 0.008f * G(ax, y, 0.016f, -0.034f, 0.007f, 0.007f); dz -= 0.004f * G(ax, y, 0.013f, -0.004f, 0.006f, 0.02f); /* nose bridge, tip, wings */
    dz += (raider ? 0.013f : 0.01f) * G(ax, y, 0.03f, 0.036f, 0.026f, 0.009f); /* brow ridge */
    dz -= 0.013f * G(ax, y, 0.031f, 0.017f, 0.015f, 0.01f); /* eye sockets */
    dz += 0.007f * G(ax, y, 0.048f, -0.006f, 0.018f, 0.014f); /* cheekbones */
    dz += 0.006f * G(x, y, 0, -0.051f, 0.019f, 0.005f) + 0.005f * G(x, y, 0, -0.062f, 0.016f, 0.005f) - 0.002f * G(x, y, 0, -0.057f, 0.02f, 0.002f); /* lips, mouth line */
    dz += 0.011f * G(x, y, 0, -0.088f, 0.024f, 0.014f); /* chin */
    dz -= 0.004f * G(ax, y, 0.045f, -0.05f, 0.015f, 0.02f); /* under the cheekbone */
    p.z += dz * front; return p; }
  static Vector3 HeadN(float th, float ph, bool raider) { const float e = 0.01f; var a = Head(th + e, ph, raider) - Head(th - e, ph, raider); var c = Head(th, ph + e, raider) - Head(th, ph - e, raider); var n = Vector3.Cross(c * 100f, a * 100f); n = n.sqrMagnitude > 1e-20f ? n / n.magnitude : Head(th, ph, raider).normalized; /* tiny cross: Vector3.normalized would return zero */ var p = Head(th, ph, raider); if (Vector3.Dot(n, p) < 0) n = -n; return n; }

  public static Dictionary<string, Mesh> Build(bool raider) {
    parts = new Dictionary<string, PB>(); float w = raider ? 1.12f : 1f;
    string shirt = raider ? "F_Shirt" : "F_Kurta";
    // ---- legs and feet ----
    for (int sd = -1; sd <= 1; sd += 2) { float x = sd * 0.1f * w;
      if (raider) { M("F_Trousers").Add(b => Tube(b, Bez(new Vector3(x, 0.92f, 0), new Vector3(x * 1.25f, 0.62f, 0.02f), new Vector3(x, 0.4f, 0.02f), 10), s => 0.1f + 0.035f * Mathf.Sin(s * Mathf.PI) - 0.02f * s, 14));
        M("F_Boot").Add(b => Tube(b, new[] { new Vector3(x, 0.44f, 0.01f), new Vector3(x, 0.25f, 0.0f), new Vector3(x, 0.08f, 0.0f), new Vector3(x, 0.05f, 0.05f), new Vector3(x, 0.04f, 0.13f) }, s => s < 0.6f ? 0.065f - 0.012f * s : 0.05f, 12, 1.2f)); M("F_Boot").Add(b => Tube(b, new[] { new Vector3(x, 0.44f, 0.01f), new Vector3(x, 0.4f, 0.01f) }, s => 0.074f, 12)); }
      else { M("F_Kurta").Add(b => Tube(b, Bez(new Vector3(x, 0.8f, 0), new Vector3(x * 1.2f, 0.45f, 0.02f), new Vector3(x, 0.14f, 0.02f), 10), s => 0.09f + 0.03f * Mathf.Sin(s * Mathf.PI * 0.9f) - 0.035f * s * s, 14)); /* off-white shalwar, gathered at the ankle */
        M("F_Skin").Add(b => Tube(b, new[] { new Vector3(x, 0.14f, 0.01f), new Vector3(x, 0.07f, 0.01f) }, s => 0.036f, 10));
        M("F_Leather").Add(b => Tube(b, new[] { new Vector3(x, 0.05f, -0.04f), new Vector3(x, 0.045f, 0.05f), new Vector3(x, 0.035f, 0.13f) }, s => 0.045f - 0.012f * s, 12, 1.25f)); } }
    // ---- torso ----
    if (raider) { M("F_Shirt").Add(b => Body(b, 0.86f, 1.45f, new[] { 0.19f * w, 0.18f * w, 0.19f * w, 0.215f * w, 0.215f * w, 0.12f }, new[] { 0.13f, 0.125f, 0.135f, 0.15f, 0.13f, 0.09f }));
      M("F_Vest").Add(b => Body(b, 0.93f, 1.43f, new[] { 0.198f * w, 0.2f * w, 0.226f * w, 0.226f * w, 0.14f }, new[] { 0.14f, 0.143f, 0.162f, 0.142f, 0.1f }, 0, 26, 32, 328)); /* dark leather vest, open down the front so the shirt shows */
      M("F_Vest").Add(b => Body(b, 0.93f, 1.43f, new[] { 0.192f * w, 0.194f * w, 0.22f * w, 0.22f * w, 0.135f }, new[] { 0.135f, 0.138f, 0.157f, 0.137f, 0.095f }, 0, 26, 32, 328)); /* inner face: no see-through edge */
      M("Sash").Add(b => Body(b, 0.84f, 0.99f, new[] { 0.205f * w, 0.2f * w, 0.2f * w }, new[] { 0.145f, 0.14f, 0.14f }));
      M("Sash").Add(b => Tube(b, Bez(new Vector3(0.1f, 0.9f, 0.14f), new Vector3(0.15f, 0.72f, 0.16f), new Vector3(0.13f, 0.52f, 0.13f), 8), s => 0.04f - 0.01f * s, 8, 0.35f)); }
    else { M("F_Kurta").Add(b => Body(b, 0.58f, 1.45f, new[] { 0.25f, 0.23f, 0.205f, 0.19f, 0.2f, 0.205f, 0.21f, 0.12f }, new[] { 0.19f, 0.17f, 0.15f, 0.135f, 0.14f, 0.15f, 0.135f, 0.09f }, 0, 26)); /* knee-length kurta */
      M("F_Leather").Add(b => Body(b, 0.96f, 1.03f, new[] { 0.198f, 0.198f }, new[] { 0.143f, 0.143f }));
      M("F_Gold").Add(b => Ell(b, new Vector3(0, 0.995f, 0.145f), new Vector3(0.03f, 0.025f, 0.008f), Quaternion.identity, 10)); }
    // ---- arms and hands ----
    float sh = 1.4f, sx = 0.215f * w;
    for (int sd = -1; sd <= 1; sd += 2) { var s0 = new Vector3(sd * sx, sh, 0); var el = new Vector3(sd * (sx + 0.06f), sh - 0.29f, sd > 0 ? 0.1f : 0.0f); var hd = sd > 0 ? new Vector3(sd * (sx + 0.0f), sh - 0.42f, 0.3f) : new Vector3(sd * (sx + 0.05f), sh - 0.58f, 0.04f);
      M(shirt).Add(b => Tube(b, Bez(s0 + new Vector3(-sd * 0.07f, -0.035f, 0), s0 + new Vector3(sd * 0.035f, -0.07f, 0), el, 8), s => (0.066f - 0.012f * s) * w, 12)); /* shoulder cap buried in the torso: sloped shoulders, no ball */
      var wr = Vector3.Lerp(el, hd, 0.86f); bool sleeveLong = !raider; /* Hasan: full sleeves to the wrist; raider: sleeves rolled to the elbow */
      if (sleeveLong) M(shirt).Add(b => Tube(b, new[] { el, Vector3.Lerp(el, wr, 0.5f), wr }, s => 0.058f - 0.012f * s, 12));
      else { M(shirt).Add(b => Tube(b, new[] { el + (el - hd).normalized * 0.04f, Vector3.Lerp(el, hd, 0.12f) }, s => 0.068f, 12)); M("F_Skin").Add(b => Tube(b, new[] { el, Vector3.Lerp(el, wr, 0.5f), wr }, s => 0.05f - 0.014f * s, 12)); }
      Hand(sd, wr, hd, sd > 0, raider); }
    // ---- neck and head ----
    var hc = new Vector3(0, sh + 0.175f, 0.02f);
    M("F_Skin").Add(b => Tube(b, new[] { new Vector3(0, sh - 0.04f, 0.0f), new Vector3(0, sh + 0.06f, 0.01f), hc + new Vector3(0, -0.07f, -0.01f) }, s => raider ? 0.064f : 0.058f, 12));
    if (!raider) M("F_Kurta").Add(b => Tube(b, new[] { new Vector3(0, sh - 0.02f, 0), new Vector3(0, sh + 0.025f, 0.005f) }, s => 0.066f, 14)); /* collar */
    M("F_Face").Add(b => Grid(b, 48, 36, true, (u, v) => hc + Head(u * Mathf.PI * 2, Mathf.Lerp(-Mathf.PI / 2, Mathf.PI / 2, v), raider)));
    for (int sd = -1; sd <= 1; sd += 2) { var hcL = hc; int s = sd;
      M("F_Skin").Add(b => Ell(b, hcL + new Vector3(s * 0.08f, 0.005f, -0.005f), new Vector3(0.01f, 0.028f, 0.017f), Quaternion.Euler(0, s * 20, 0), 10)); /* ears */
      var ec = hc + new Vector3(sd * 0.03f, 0.016f, 0.083f);
      M("F_EyeW").Add(b => Ell(b, ec, new Vector3(0.0105f, 0.0042f, 0.006f), Quaternion.Euler(0, 0, s * -4), 12));
      M("F_Eye").Add(b => Ell(b, ec + new Vector3(-s * 0.001f, 0, 0.0052f), new Vector3(0.0042f, 0.0042f, 0.0016f), Quaternion.identity, 10));
      M("F_Face").Add(b => Tube(b, Bez(ec + new Vector3(-s * 0.012f, 0.0005f, -0.001f), ec + new Vector3(0, 0.0075f, 0.0062f), ec + new Vector3(s * 0.012f, 0.001f, -0.002f), 7), q => 0.0028f, 6, 1f)); /* upper lid */
      M("F_Hair").Add(b => Tube(b, Bez(ec + new Vector3(-s * 0.011f, 0.002f, 0.002f), ec + new Vector3(0, 0.0065f, 0.0078f), ec + new Vector3(s * 0.0115f, 0.0025f, 0.0005f), 7), q => 0.0011f, 5, 1f)); /* lash line */
      M(raider ? "F_Hair" : "F_Beard").Add(b => Tube(b, Bez(hcL + new Vector3(s * 0.011f, 0.036f, 0.104f), hcL + new Vector3(s * 0.029f, 0.042f, 0.106f), hcL + new Vector3(s * 0.05f, 0.036f, 0.094f), 6), q => 0.0042f - 0.0018f * q, 6, 1.8f)); /* eyebrows */ }
    // beard: a band from under the jaw up to a curved cheek line, offset tapering to the skin at both edges (no cell steps); lips left bare
    float bt = raider ? 0.0022f : 0.008f; /* raider: rough stubble, Hasan: short groomed beard */
    Func<float, float> BTop = th => { float a = Mathf.Abs(th); return a < 0.3f ? -0.37f + 0.1f * a : a < 0.9f ? Mathf.Lerp(-0.34f, -0.24f, (a - 0.3f) / 0.6f) : Mathf.Lerp(-0.24f, 0.08f, SS(0.9f, 1.5f, a)); };
    Func<float, float> BBot = th => Mathf.Lerp(-1.05f, -0.75f, SS(0.2f, 1.6f, Mathf.Abs(th)));
    M("F_Beard").Add(b => Grid(b, 72, 22, false, (u, v) => { float th = Mathf.Lerp(-1.62f, 1.62f, u), lo = BBot(th), hi = BTop(th), ph = Mathf.Lerp(lo, hi, v); var hp = Head(th, ph, raider);
      float edge = SS(0, 0.18f, v) * SS(1, 0.82f, v) * SS(1.62f, 1.35f, Mathf.Abs(th)); float lipsBare = 1 - 0.95f * (G(hp.x, hp.y, 0, -0.056f, 0.021f, 0.009f));
      float chin = raider ? 0 : 0.01f * G(hp.x, hp.y, 0, -0.1f, 0.03f, 0.02f);
      return hc + hp + HeadN(th, ph, raider) * (-0.0015f + (0.0035f + bt + chin) * edge * lipsBare); }));
    M("F_Beard").Add(b => Tube(b, Bez(hc + new Vector3(-0.034f, -0.052f, 0.088f), hc + new Vector3(0, -0.037f, 0.118f), hc + new Vector3(0.034f, -0.052f, 0.088f), 12), q => 0.0048f + 0.0035f * Mathf.Sin(q * Mathf.PI), 8, 1.5f)); /* moustache */
    if (raider) { /* dark hair: band from a hairline (forehead, over the ears, nape) to the crown */
      Func<float, float> HL = th => Mathf.Lerp(-0.55f, 0.58f, Mathf.Pow((1 + Mathf.Cos(th)) / 2, 1.3f));
      M("F_Hair").Add(b => Grid(b, 56, 24, true, (u, v) => { float th = u * Mathf.PI * 2, lo = HL(th), ph = Mathf.Lerp(lo, Mathf.PI / 2, v); float tuft = 0.014f * Mathf.PerlinNoise(th * 4 + 5, ph * 6);
        float edge = SS(0, 0.22f, v); return hc + Head(th, ph, true) + HeadN(th, ph, true) * (-0.001f + (0.017f + tuft) * edge); })); }
    else { /* wrapped indigo turban (store banner): rolled lower edge across the forehead, layered folds, tail over the shoulder */
      Func<float, float> TL = th => Mathf.Lerp(-0.3f, 0.42f, Mathf.Pow((1 + Mathf.Cos(th)) / 2, 1.1f));
      M("Turban").Add(b => Grid(b, 64, 30, true, (u, v) => { float th = u * Mathf.PI * 2, lo = TL(th), ph = Mathf.Lerp(lo, Mathf.PI / 2, v);
        float fold = 0.006f * Mathf.Abs(Mathf.Sin(10f * (ph - lo) + 1.5f * Mathf.Sin(th) + 0.9f * th)); float puff = 0.026f + 0.026f * Mathf.Clamp01(Mathf.Sin(ph + 0.3f) * 1.3f) * (0.75f + 0.25f * Mathf.Cos(th - 0.5f));
        float edge = SS(0, 0.09f, v); return hc + Head(th, ph, false) + HeadN(th, ph, false) * (-0.003f + (puff + fold + 0.003f) * edge); }));
      M("Turban").Add(b => Tube(b, new[] { hc + new Vector3(-0.06f, 0.0f, -0.09f), hc + new Vector3(-0.1f, -0.1f, -0.1f), hc + new Vector3(-0.15f, -0.22f, -0.08f), hc + new Vector3(-0.19f, -0.36f, -0.05f), hc + new Vector3(-0.2f, -0.52f, -0.04f) }, s => 0.04f - 0.012f * s, 10, 0.4f, Vector3.forward)); }
    var res = new Dictionary<string, Mesh>(); foreach (var kv in parts) res[kv.Key] = kv.Value.Build(); parts = null; return res; }

  static void Add(this PB b, Action<PB> a) => a(b);
  /// painted skin for the head grid (u = azimuth, v = elevation): warm cheeks and nose, shadowed sockets, darker lips, stubble shadow under the beard
  public static Texture2D FaceTex(bool raider) { int W = 256, H = 192; var t = new Texture2D(W, H, TextureFormat.RGB24, true); var px = new Color[W * H];
    Color skin = raider ? new Color(0.62f, 0.42f, 0.29f) : new Color(0.74f, 0.5f, 0.34f), warm = new Color(0.78f, 0.42f, 0.32f), sock = new Color(0.42f, 0.27f, 0.22f), lip = new Color(0.5f, 0.27f, 0.23f), stub = raider ? new Color(0.3f, 0.24f, 0.21f) : new Color(0.38f, 0.28f, 0.22f);
    for (int j = 0; j < H; j++) for (int i = 0; i < W; i++) { float th = i / (float)W * Mathf.PI * 2, ph = Mathf.Lerp(-Mathf.PI / 2, Mathf.PI / 2, j / (float)(H - 1)); var p = Head(th, ph, raider); float x = p.x, y = p.y, ax = Mathf.Abs(x), fr = SS(0.2f, 0.8f, Mathf.Cos(th));
      var c = skin * (0.94f + 0.08f * Mathf.PerlinNoise(i * 0.15f, j * 0.15f));
      c = Color.Lerp(c, warm, fr * (0.35f * G(ax, y, 0.045f, -0.02f, 0.025f, 0.02f) + 0.3f * G(x, y, 0, -0.028f, 0.012f, 0.012f)));
      c = Color.Lerp(c, sock, fr * 0.55f * G(ax, y, 0.03f, 0.016f, 0.017f, 0.011f));
      c = Color.Lerp(c, lip, fr * 0.7f * (G(x, y, 0, -0.051f, 0.018f, 0.005f) + G(x, y, 0, -0.062f, 0.015f, 0.005f)));
      c = Color.Lerp(c, stub, (raider ? 0.6f : 0.45f) * fr * Mathf.SmoothStep(0, 1, (-0.03f - y) / 0.03f) * (1 - 0.9f * G(x, y, 0, -0.056f, 0.02f, 0.01f)));
      c = Color.Lerp(c, skin * 1.08f, fr * 0.25f * G(x, y, 0, 0.07f, 0.04f, 0.03f)); /* lit forehead */
      c = Color.Lerp(c, skin * 0.75f, 0.4f * Mathf.Clamp01(-Mathf.Cos(th))); /* back of the head in shade */
      px[j * W + i] = c; }
    t.SetPixels(px); t.Apply(true); t.wrapMode = TextureWrapMode.Repeat; return t; }
  /// hand: palm + 4 two-segment fingers + thumb; grip curls round the hilt (sword along +z/up)
  static void Hand(int sd, Vector3 wr, Vector3 tip, bool grip, bool raider) {
    var dn = (tip - wr).normalized; var side = Vector3.Cross(dn, Vector3.forward).sqrMagnitude < 0.01f ? Vector3.right : Vector3.Cross(dn, grip ? Vector3.forward : Vector3.right * sd).normalized; var fw = Vector3.Cross(side, dn).normalized; float k = raider ? 1.08f : 1f;
    var palm = wr + dn * 0.045f * k; M("F_Skin").Ell(palm, new Vector3(0.042f, 0.05f, 0.019f) * k, Quaternion.LookRotation(fw, -dn), 12);
    for (int f = 0; f < 4; f++) { float o = (f - 1.5f) * 0.019f * k; var b0 = palm + dn * 0.045f * k + side * o; var len = (0.04f - Mathf.Abs(f - 1.3f) * 0.005f) * k;
      Vector3 b1, b2; if (grip) { b1 = b0 + dn * len * 0.6f + fw * len * 0.5f; b2 = b1 + fw * len * 0.5f - dn * len * 0.45f; } else { b1 = b0 + dn * len + fw * len * 0.15f; b2 = b1 + dn * len * 0.8f + fw * len * 0.3f; }
      var P = new[] { b0, b1, b2 }; M("F_Skin").Add(b => Tube(b, P, s => 0.0085f * k - 0.002f * s, 8)); }
    var t0 = palm + side * 0.03f * k * (grip ? -1 : -1) + fw * 0.01f; var t1 = t0 + side * -0.02f * k + dn * 0.03f + fw * 0.025f; var t2 = t1 + dn * 0.02f + fw * (grip ? 0.025f : 0.01f);
    M("F_Skin").Add(b => Tube(b, new[] { t0, t1, t2 }, s => 0.01f * k - 0.002f * s, 8));
    if (grip) { /* scimitar: hilt in the fist, guard, curved blade forward and up */
      var hilt = palm + dn * 0.07f * k + fw * 0.03f; var bd = (new Vector3(0.02f, 0.38f, 0.92f)).normalized;
      M("F_Leather").Add(b => Tube(b, new[] { hilt - bd * 0.07f, hilt + bd * 0.06f }, s => 0.013f, 8));
      M("F_Gold").Add(b => Ell(b, hilt + bd * 0.07f, new Vector3(0.05f, 0.012f, 0.012f), Quaternion.LookRotation(bd), 10)); M("F_Gold").Add(b => Ell(b, hilt - bd * 0.08f, Vector3.one * 0.016f, Quaternion.identity, 8));
      var bl = Bez(hilt + bd * 0.08f, hilt + bd * 0.42f, hilt + bd * 0.72f + Vector3.up * 0.12f, 12);
      M("F_Steel").Add(b => Tube(b, bl, s => 0.018f * (1 - s * 0.85f) + 0.002f, 6, 0.22f, Vector3.right)); } }
}
