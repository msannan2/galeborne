using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/* gamedev_ring: naval range ring around the pirate (art/range-ring 51d0125, design/range_ring_spec.md).
   One range source: core.FireRange(H) (= the loaded shot's range, used by CanFire and the "Out of range for X (N m)" message)
   and core.FireDist(H,P) (= the distance CanFire compares). Ring radius = FireRange * K world units, so the player's ship
   crosses the ring exactly when the range part of the FIRE check flips. Flat on the sea under every hull, normal alpha blend. */
public partial class Game {
  public static float RingOutAlpha = RingAlphaArg(0.6f); /* spec: brass at 60% out of range; -gbringalpha overrides for the daytime-sea readability check */
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
    fireRangeBorder = Px(fireButton, "rangeBorder", -8, -8, 266, 266).gameObject.AddComponent<Image>(); fireRangeBorder.sprite = Ring(0.93f); fireRangeBorder.color = RingBrass; fireRangeBorder.raycastTarget = false; fireRangeBorder.gameObject.SetActive(false);
  }
  void HideRangeRing() { if (rangeRing) rangeRing.gameObject.SetActive(false); if (rangeMarker) rangeMarker.gameObject.SetActive(false); }

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
    rangePlaqueTxt.text = inR ? "In range" : $"Range {ringPlaqueN} m · {ringTrend}";
    fireRangeBorder.gameObject.SetActive(inR);
  }
  IEnumerator RangePulse(Vector3 c, float side) {
    var m = NewRingMat(rangeTexPulse); var t = RingQuad("RangePulse", m, out _); t.position = c;
    for (float k = 0; k < 0.4f; k += Time.deltaTime) { float u = k / 0.4f; t.localScale = new Vector3(side, 1, side) * (1 + 0.25f * u); var col = RingPale; col.a = 1 - u; m.SetColor("_Color", col);
      m.SetFloat("_Speed", waterMat.GetFloat("_Speed")); m.SetVector("_Offset", waterMat.GetVector("_Offset")); yield return null; }
    Destroy(t.gameObject); Destroy(m); }
}
