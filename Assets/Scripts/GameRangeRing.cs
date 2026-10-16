using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/* gamedev_ring: naval range ring around the pirate (art/range-ring 51d0125, design/range_ring_spec.md).
   One range source: core.FireRange(H) (= the loaded shot's range, used by CanFire and the "Out of range for X (N m)" message)
   and core.FireDist(H,P) (= the distance CanFire compares). Ring radius = FireRange * K world units, so the player's ship
   crosses the ring exactly when the range part of the FIRE check flips. Flat on the sea under every hull, normal alpha blend. */
public partial class Game {
  public static float RingOutAlpha = RingAlphaArg(0.8f); /* spec 60%; raised to 80% per Game Designer: at 60% the dashes went faint on the lighter daytime water near Hasan (ring_shots a0.6 vs a0.8). -gbringalpha overrides */
  static float RingAlphaArg(float d) { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-gbringalpha"); return i >= 0 && i + 1 < a.Length && float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? Mathf.Clamp01(v) : d; }
  static readonly Color RingBrass = new Color(0xC8 / 255f, 0xA0 / 255f, 0x60 / 255f), RingPale = new Color(0xE6 / 255f, 0xCF / 255f, 0xA0 / 255f);
  const float RingSpritePx = 512f, RingStrokePx = 250f; /* outer edge of the stroke sits 250 px from the centre of the 512 px sprite */
  const float RingMarkerWorld = 5f;
  Transform rangeRing, rangeMarker; MeshRenderer rangeRingR, rangeMarkerR; Material rangeRingMat, rangeMarkerMat; Texture rangeTexDashed, rangeTexSolid, rangeTexPulse;
  RectTransform rangePlaque; Text rangePlaqueTxt; Image fireRangeBorder;
  public bool ringInRange; public float ringRadiusWorld, ringRadiusM, ringDistM; public int ringPlaqueN = -1, ringPulses; string ringTrend = "Closing"; float ringLastDist = -1, ringSpin;
  Material NewRingMat(Texture tex) { var sh = Resources.Load<Shader>("Shaders/RangeRing"); var m = new Material(sh); m.mainTexture = tex; m.renderQueue = 2450; return m; }
  static Mesh ringGrid; /* subdivided unit quad (XZ, centred) so the decal follows the waves */
  static Mesh RingGrid() { if (ringGrid) return ringGrid; int n = 48; var v = new Vector3[(n + 1) * (n + 1)]; var uv = new Vector2[v.Length]; var tri = new int[n * n * 6];
    for (int y = 0; y <= n; y++) for (int x = 0; x <= n; x++) { v[y * (n + 1) + x] = new Vector3(x / (float)n - 0.5f, 0, y / (float)n - 0.5f); uv[y * (n + 1) + x] = new Vector2(x / (float)n, y / (float)n); }
    int k = 0; for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { int a = y * (n + 1) + x; tri[k++] = a; tri[k++] = a + n + 1; tri[k++] = a + 1; tri[k++] = a + 1; tri[k++] = a + n + 1; tri[k++] = a + n + 2; }
    ringGrid = new Mesh { name = "RangeRingGrid", vertices = v, uv = uv, triangles = tri }; ringGrid.RecalculateBounds(); ringGrid.bounds = new Bounds(Vector3.zero, new Vector3(1, 4, 1)); return ringGrid; }
  Transform RingQuad(string name, Material m, out MeshRenderer mr) { var g = new GameObject(name); g.transform.SetParent(sea3D, false); g.AddComponent<MeshFilter>().sharedMesh = RingGrid();
    mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; return g.transform; }
  static Texture RingTex(string n) { var s = Resources.Load<Sprite>("Art/FX/" + n); if (s) return s.texture; var t = Resources.Load<Texture2D>("Art/FX/" + n); if (!t) Debug.LogWarning("GB RING missing Art/FX/" + n); return t; }

  void BuildRangeRing(RectTransform fireButton) {
    rangeTexDashed = RingTex("range_ring_dashed"); rangeTexSolid = RingTex("range_ring"); rangeTexPulse = RingTex("range_pulse");
    if (rangeRing == null) { rangeRingMat = NewRingMat(rangeTexDashed); rangeRing = RingQuad("RangeRing", rangeRingMat, out rangeRingR);
      rangeMarkerMat = NewRingMat(RingTex("range_marker")); rangeMarker = RingQuad("RangeMarker", rangeMarkerMat, out rangeMarkerR); }
    rangeRing.gameObject.SetActive(true); rangeMarker.gameObject.SetActive(true); ringInRange = false; ringLastDist = -1; ringTrend = "Closing"; ringPlaqueN = -1; ringPulses = 0;
    rangePlaque = Px(cui, "RangePlaque", 404, 116, 470, 76); var pi = rangePlaque.gameObject.AddComponent<Image>(); pi.sprite = Resources.Load<Sprite>("Art/FX/range_label_plaque"); pi.type = Image.Type.Sliced; pi.raycastTarget = false;
    rangePlaqueTxt = Txt(R(rangePlaque, "t", 0, 0, 1, 1), "", 32, "#3A2618", TextAnchor.MiddleCenter, true); rangePlaqueTxt.raycastTarget = false;
    if (arcWedges == null) { arcWedgeMat = NewRingMat(ArcWedgeTex(core.C.arcDeg)); arcWedges = RingQuad("ArcWedges", arcWedgeMat, out _); } else arcWedgeMat.mainTexture = ArcWedgeTex(core.C.arcDeg); arcWedges.gameObject.SetActive(true);
    fireRangeBorder = Px(fireButton, "rangeBorder", -8, -8, 266, 266).gameObject.AddComponent<Image>(); fireRangeBorder.sprite = Ring(0.93f); fireRangeBorder.color = RingBrass; fireRangeBorder.raycastTarget = false; fireRangeBorder.gameObject.SetActive(false);
  }
  void HideRangeRing() { if (arcWedges) arcWedges.gameObject.SetActive(false); if (rangeRing) rangeRing.gameObject.SetActive(false); if (rangeMarker) rangeMarker.gameObject.SetActive(false); }

  /// called every combat frame from UpdateCombatView (after the ships are placed)
  void UpdateRangeRing() {
    if (rangeRing == null || core == null) return; var H = core.H; var P = core.P;
    ringRadiusM = core.FireRange(H); ringDistM = core.FireDist(H, P); bool inR = core.InFireRange(H, P);
    ringRadiusWorld = ringRadiusM * K; float side = 2f * ringRadiusWorld * RingSpritePx / (2f * RingStrokePx);
    Vector3 c = W(P); c.y = 0;
    foreach (var m in new[] { rangeRingMat, rangeMarkerMat }) { m.SetFloat("_Speed", waterMat.GetFloat("_Speed")); m.SetVector("_Offset", waterMat.GetVector("_Offset")); if (waterMat.HasProperty("_Amp")) m.SetFloat("_Amp", waterMat.GetFloat("_Amp")); }
    if (inR && !ringInRange) { ringPulses++; StartCoroutine(RangePulse(c, side)); Debug.Log($"GB RING in range dist={ringDistM:0.0} range={ringRadiusM:0.0}"); }
    ringInRange = inR;
    if (!inR) ringSpin = (ringSpin + 6f * Time.deltaTime) % 360f;
    rangeRing.position = c; rangeRing.localScale = new Vector3(side, 1, side); rangeRing.rotation = Quaternion.Euler(0, inR ? 0 : ringSpin, 0);
    rangeRingMat.mainTexture = inR ? rangeTexSolid : rangeTexDashed; var col = inR ? RingPale : RingBrass; col.a = inR ? 1f : RingOutAlpha; rangeRingMat.SetColor("_Color", col);
    Vector3 toH = W(H) - W(P); toH.y = 0; Vector3 dir = toH.sqrMagnitude > 1e-4f ? toH.normalized : Vector3.forward;
    rangeMarker.position = c + dir * ringRadiusWorld; rangeMarker.rotation = Quaternion.LookRotation(dir, Vector3.up); /* sprite "up" (+V = +Z) points outward to Hasan */
    rangeMarker.localScale = new Vector3(RingMarkerWorld, 1, RingMarkerWorld); rangeMarkerMat.SetColor("_Color", Color.white);
    if (ringLastDist >= 0 && Mathf.Abs(ringDistM - ringLastDist) > 0.01f) ringTrend = ringDistM < ringLastDist ? "Closing" : "Opening"; ringLastDist = ringDistM;
    ringPlaqueN = Mathf.CeilToInt(ringDistM); /* ceil: an out-of-range distance never reads at or below the range figure */
    bool arc = core.InArc(H, P); /* gamedev_arc: the same arc check that gives 'Not in arc' */
    rangePlaqueTxt.text = inR ? (arc ? "In range" : "In range · Turn broadside") : $"Range {ringPlaqueN} m · {ringTrend}";
    fireRangeBorder.gameObject.SetActive(inR && arc);
    UpdateArcWedges();
  }
  IEnumerator RangePulse(Vector3 c, float side) {
    var m = NewRingMat(rangeTexPulse); var t = RingQuad("RangePulse", m, out _); t.position = c;
    for (float k = 0; k < 0.4f; k += Time.deltaTime) { float u = k / 0.4f; t.localScale = new Vector3(side, 1, side) * (1 + 0.25f * u); var col = RingPale; col.a = 1 - u; m.SetColor("_Color", col);
      m.SetFloat("_Speed", waterMat.GetFloat("_Speed")); m.SetVector("_Offset", waterMat.GetVector("_Offset")); yield return null; }
    Destroy(t.gameObject); Destroy(m); }

  /* gamedev_arc: two faint brass wedges off Hasan's port and starboard beams = the true firing arc (CombatCore.InArc: |bearing - 90| <= arcDeg),
     radius = the loaded shot's range. Same RangeRing shader, brass tint and opacity as the dashed range ring. */
  /* art_arc_restyle (Chief Art Designer): brass edge lines with a dark teak outline, flat brass fill, no gradient; the wedge the pirate is in
     is boosted only when range AND arc are valid (no green tint). Widths are screen px at 1080 wide and scale with Screen.width. */
  public const string ArcEdgeHex = "#C9A35A", ArcOutlineHex = "#3A2618";
  public const float ArcFillAlpha = 0.14f, ArcActiveEdgeAlpha = 1.0f, ArcActiveFillAlpha = 0.22f;
  public const float ArcEdgePx1080 = 4f, ArcOutlinePx1080 = 2f, ArcOutlineAlpha = 0.6f; public static float ArcEdgeAlpha => RingOutAlpha; /* Chief Art Designer: 2 px edges + 1 px outline at 540 wide; edge alpha = the ring opacity (0.8) */
  public const int ArcTexN = 512;
  public Transform arcWedges; Material arcWedgeMat; public Texture2D arcWedgeTex; public const float ArcTexFill = 0.94f; float arcWedgeDeg = -1;
  public float arcEdgeTexels = 4f, arcOutlineTexels = 1.5f; public int arcActiveSide = -1; /* -1 none, 0 starboard (+x, bearing 90), 1 port (bearing 270) */
  string arcKey = "";
  public Texture2D ArcWedgeTex(float arcDeg) {
    string key = $"{arcDeg}|{arcEdgeTexels}|{arcOutlineTexels}|{arcActiveSide}"; if (arcWedgeTex && key == arcKey) return arcWedgeTex; arcKey = key; arcWedgeDeg = arcDeg;
    int n = ArcTexN; var t = arcWedgeTex ? arcWedgeTex : new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "ArcWedges" }; var px = new Color32[n * n];
    Color32 Mk(Color c, float a) => new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), (byte)Mathf.Round(Mathf.Clamp01(a) * 255));
    Color br = UITheme.C(ArcEdgeHex), tk = UITheme.C(ArcOutlineHex); float E = arcEdgeTexels, O = arcOutlineTexels, half = n * 0.5f;
    for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float u = (x + 0.5f) / n * 2 - 1, v = (y + 0.5f) / n * 2 - 1, r = Mathf.Sqrt(u * u + v * v) / ArcTexFill;
      float b = Mathf.Abs(Mathf.Atan2(u, v) * Mathf.Rad2Deg); float off = Mathf.Abs(b - 90) - arcDeg; bool act = arcActiveSide == (u >= 0 ? 0 : 1);
      float rT = r * ArcTexFill * half; float edgeD = -off * Mathf.Deg2Rad * rT; /* texels inside the straight edge (<0 outside) */ float rimD = (ArcTexFill - r * ArcTexFill) * half; /* texels inside the rim */
      float d = Mathf.Min(edgeD, rimD); Color32 c = new Color32(0, 0, 0, 0);
      if (r > 0.06f) { if (d >= 0) c = d < E ? Mk(br, act ? ArcActiveEdgeAlpha : ArcEdgeAlpha) : d < E + O ? Mk(tk, ArcOutlineAlpha) : Mk(br, act ? ArcActiveFillAlpha : ArcFillAlpha);
        else if (d > -O && r <= 1f + O / (ArcTexFill * half) + 0.001f && off <= O / Mathf.Max(1f, rT) * Mathf.Rad2Deg) c = Mk(tk, ArcOutlineAlpha); }
      px[y * n + x] = c; }
    t.SetPixels32(px); t.Apply(); arcWedgeTex = t; return t; }
  public float ArcWedgeAlphaAt(float relDeg, float frac) { var t = arcWedgeTex; if (!t) return -1; return ArcWedgeColorAt(relDeg, frac).a; }
  public Color ArcWedgeColorAt(float relDeg, float frac) { var t = arcWedgeTex; float u = Mathf.Sin(relDeg * Mathf.Deg2Rad) * frac * ArcTexFill, v = Mathf.Cos(relDeg * Mathf.Deg2Rad) * frac * ArcTexFill;
    int x = Mathf.Clamp(Mathf.FloorToInt((u + 1) / 2 * t.width), 0, t.width - 1), y = Mathf.Clamp(Mathf.FloorToInt((v + 1) / 2 * t.height), 0, t.height - 1); return t.GetPixel(x, y); }
  /// rim edge-line texel on a side (0 starboard, 1 port): the middle of the brass line just inside the rim
  public Color ArcEdgeSample(int side) => ArcWedgeColorAt(side == 0 ? 90 : 270, 1f - (arcEdgeTexels * 0.5f) / (ArcTexFill * ArcTexN * 0.5f));
  public Color ArcFillSample(int side) => ArcWedgeColorAt(side == 0 ? 90 : 270, 0.5f);
  public float arcWedgeScreenPx;
  void UpdateArcWedges() { if (!arcWedges) return; var H = core.H; float side = 2f * core.FireRange(H) * K / ArcTexFill; Vector3 c = W(H); c.y = 0;
    arcWedges.position = c; arcWedges.rotation = Quaternion.Euler(0, H.heading, 0); arcWedges.localScale = new Vector3(side, 1, side);
    var P = core.P; int act = -1; if (core.InFireRange(H, P) && core.InArc(H, P)) { Vector3 lp = Quaternion.Inverse(arcWedges.rotation) * (W(P) - c); act = lp.x >= 0 ? 0 : 1; }
    float edge = arcEdgeTexels, ol = arcOutlineTexels;
    if (cam) { Vector3 a = cam.WorldToScreenPoint(c), bb = cam.WorldToScreenPoint(c + cam.transform.right * side * 0.5f); float sp = 2f * Vector2.Distance(a, bb); if (a.z > 0 && sp > 1f) { arcWedgeScreenPx = sp;
        float tpp = ArcTexN / sp, sc = Screen.width / 1080f; edge = Mathf.Clamp(Mathf.Round(ArcEdgePx1080 * sc * tpp * 2f) / 2f, 1f, 48f); ol = Mathf.Clamp(Mathf.Round(ArcOutlinePx1080 * sc * tpp * 2f) / 2f, 0.5f, 24f); } }
    arcEdgeTexels = edge; arcOutlineTexels = ol; arcActiveSide = act; arcWedgeMat.mainTexture = ArcWedgeTex(core.C.arcDeg);
    arcWedgeMat.SetColor("_Color", Color.white); arcWedgeMat.SetFloat("_Speed", waterMat.GetFloat("_Speed")); arcWedgeMat.SetVector("_Offset", waterMat.GetVector("_Offset")); if (waterMat.HasProperty("_Amp")) arcWedgeMat.SetFloat("_Amp", waterMat.GetFloat("_Amp")); }
}
