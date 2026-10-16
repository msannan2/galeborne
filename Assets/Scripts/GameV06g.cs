using Rnd = UnityEngine.Random;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// v0.6g: resume pause card, save fallback test, duel art test shots
public partial class Game {
  RectTransform pauseCard; float pauseTs = 1; bool wasPaused;
  void OnApplicationFocus(bool f) { if (f && wasPaused) { wasPaused = false; ShowPauseCard(); } }
  void NotePause(bool paused) { if (paused && Application.isMobilePlatform) wasPaused = true; }
  string VoyageDay() => $"Day {week * 7 + 1}";
  string VoyageName() => sailing ? $"{S.ports[port].name} to {S.ports[dest].name}" : $"In port at {S.ports[port].name}";
  /// parchment card in a teak frame with brass trim, over a dark blurred copy of the screen; one button
  public void ShowPauseCard() { if (pauseCard != null || onTitle || S == null) return; StartCoroutine(PauseCo()); }
  IEnumerator PauseCo() {
    yield return new WaitForEndOfFrame(); var shot = ScreenCapture.CaptureScreenshotAsTexture();
    int bw = Mathf.Max(8, shot.width / 24), bh = Mathf.Max(8, shot.height / 24); var rt = RenderTexture.GetTemporary(bw, bh); Graphics.Blit(shot, rt); var rt2 = RenderTexture.GetTemporary(bw / 2, bh / 2); Graphics.Blit(rt, rt2);
    var blur = new Texture2D(bw / 2, bh / 2, TextureFormat.RGB24, false); var prev = RenderTexture.active; RenderTexture.active = rt2; blur.ReadPixels(new Rect(0, 0, bw / 2, bh / 2), 0, 0); blur.Apply(); RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); RenderTexture.ReleaseTemporary(rt2); Destroy(shot);
    blur.filterMode = FilterMode.Bilinear; blur.wrapMode = TextureWrapMode.Clamp;
    pauseTs = Time.timeScale; Time.timeScale = 0;
    pauseCard = Stretch(root, "PauseCard", 0, 0, 0, 0); pauseCard.offsetMax = new Vector2(0, 400); pauseCard.offsetMin = new Vector2(0, -400);
    var bg = pauseCard.gameObject.AddComponent<RawImage>(); bg.texture = blur; bg.color = new Color(0.32f, 0.3f, 0.3f); /* dark blur behind */
    var holder = R(pauseCard, "c", 0.5f, 0.5f, 0.5f, 0.5f); holder.sizeDelta = new Vector2(820, 700);
    Img(holder, new Color(0.24f, 0.14f, 0.08f), 0); /* teak frame */ Img(holder, new Color(0.36f, 0.22f, 0.12f), 10); Img(holder, new Color(0.72f, 0.56f, 0.3f), 26); /* brass trim */ Img(holder, new Color(0.24f, 0.14f, 0.08f), 30);
    var parchR = Stretch(holder, "parch", 32, 32, 32, 32); Box(parchR, T.ledgerBg, T.frame); ParchFill(parchR); /* same parchment art as the Qarin letter panel */
    var inner = R(parchR, "in", 0.5f, 1, 0.5f, 1); inner.sizeDelta = new Vector2(756, 636); inner.pivot = new Vector2(0.5f, 1); inner.anchoredPosition = Vector2.zero;
    Label(inner, "Voyage paused", 0, 56, 756, 80, T.fontSizeTitle + 6, "#3B2414", TextAnchor.MiddleCenter, true);
    Box(Px(inner, "rule", 178, 150, 400, 3), "#9C7A3C");
    Label(inner, VoyageName(), 0, 178, 756, 60, T.fontSize + 4, "#3B2414", TextAnchor.MiddleCenter);
    Label(inner, VoyageDay(), 0, 238, 756, 56, T.fontSize, "#6B4A2C", TextAnchor.MiddleCenter);
    Label(inner, "Captain Hasan's log is safe. The wind will wait.", 40, 310, 676, 60, T.fontSizeSmall, "#6B4A2C", TextAnchor.MiddleCenter);
    Btn(inner, "Resume voyage", 128, 430, 500, 120, HidePauseCard, true, true);
  }
  /// Chief Art Designer's parchment stretched over the whole panel (no tiling); wide art for landscape panels, square art for tall ones
  void ParchFill(RectTransform r, float inset = 3) { Canvas.ForceUpdateCanvases(); var sz = r.rect.size; bool wide = sz.x > sz.y * 1.35f;
    var tex = Resources.Load<Texture2D>(wide ? "Art/ui_parch_wide" : "Art/ui_parch_tall"); if (!tex) return;
    var f = Stretch(r, "Parchment", inset, inset, inset, inset); f.SetAsFirstSibling(); var ri = f.gameObject.AddComponent<RawImage>(); ri.texture = tex; ri.raycastTarget = false; }
  /// grows a label to its wrapped text height; returns the height used
  float Flow(Text t, float minH) { Canvas.ForceUpdateCanvases(); float h = Mathf.Max(minH, t.preferredHeight + 6); t.rectTransform.sizeDelta = new Vector2(t.rectTransform.sizeDelta.x, h); return h; }
  RectTransform Img(RectTransform p, Color c, float inset) { var r = Stretch(p, "f", inset, inset, inset, inset); var im = r.gameObject.AddComponent<Image>(); im.color = c; return r; }
  public void HidePauseCard() { if (pauseCard == null) return; var ri = pauseCard.GetComponent<RawImage>(); if (ri && ri.texture) Destroy(ri.texture); Destroy(pauseCard.gameObject); pauseCard = null; Time.timeScale = pauseTs; lastInput = Time.unscaledTime; }

  /// -gbsavetest <dir>: two saves, corrupt the newest, load (expects the older); corrupt both, expect New Game only
  IEnumerator SaveTestIfAsked() {
    var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, "-gbsavetest"); if (i < 0) yield break; string dir = a[i + 1]; Directory.CreateDirectory(dir); var log = new StreamWriter(Path.Combine(dir, "savetest.txt"));
    void L(string s) { log.WriteLine(s); log.Flush(); Debug.Log("GB SAVETEST " + s); }
    if (!SaveDirIsolated) { L("REFUSED: run with -gbsavedir <dir> so the shared default saves are never touched"); log.Close(); Application.Quit(4); yield break; }
    L("save dir: " + SaveDir);
    yield return new WaitForSeconds(1f); foreach (var f in new[] { SavePath, SavePathB, VoyageBackup }) if (File.Exists(f)) File.Delete(f);
    onTitle = false; Init(); coins = 1111; Save(); L($"save 1: coins 1111 -> {Path.GetFileName(saveSeq % 2 == 1 ? SavePath : SavePathB)} seq {saveSeq}");
    coins = 2222; Save(); string newest = saveSeq % 2 == 1 ? SavePath : SavePathB; L($"save 2: coins 2222 -> {Path.GetFileName(newest)} seq {saveSeq}");
    File.WriteAllText(newest, "{\"seq\":99,\"hold\":[1,2"); L("corrupted newest slot " + Path.GetFileName(newest));
    coins = 0; saveSeq = 0; bool ok = Load(); L($"load: {ok}, from {lastLoadedFrom}, coins {coins} (expected True, 1111) => {(ok && coins == 1111 ? "PASS" : "FAIL")}");
    coins = 3333; SaveVoyageBackup(); L($"voyage backup written: {File.Exists(VoyageBackup)}");
    foreach (var f in new[] { SavePath, SavePathB, VoyageBackup }) if (File.Exists(f)) File.WriteAllText(f, "garbage");
    { /* journal persistence */ LoadJobsV06j(); journal.Clear(); oneShotUsed.Clear(); declinedByBuilding.Clear();
      var j1 = jobPool[0]; AcceptJob(j1); var os = jobPool.FirstOrDefault(x => x.oneShot); if (os != null) DeclineJob(os); declinedByBuilding["9:test"] = new List<string> { "x_declined" };
      int due = journal[0].dueWeek; Save(); journal.Clear(); oneShotUsed.Clear(); declinedByBuilding.Clear(); saveSeq = 0; bool ok2 = Load();
      bool pass = ok2 && journal.Count == 1 && journal[0].id == j1.id && journal[0].status == "active" && journal[0].dueWeek == due && journal[0].objective != null == (j1.objective != null)
        && (os == null || oneShotUsed.Contains(os.id)) && declinedByBuilding.ContainsKey("9:test") && (os == null || !OffersFor(os.building, 99).Any(x => x.id == os.id));
      L($"journal: {journal.Count} active [{string.Join(",", journal.Select(x => x.id + "@" + x.dueWeek))}], oneShot used {string.Join(",", oneShotUsed)}, declined keys {declinedByBuilding.Count}, msg '{msg}' => {(pass && !msg.Contains("Welcome back, Captain. Welcome") ? "PASS" : "FAIL")}"); }
    foreach (var f in new[] { SavePath, SavePathB, VoyageBackup }) if (File.Exists(f)) File.WriteAllText(f, "garbage");
    ok = Load(); L($"all slots broken: load {ok} (expected False) => {(!ok ? "PASS" : "FAIL")}");
    saveBroken = true; ShowTitle(); yield return null; yield return ShotCo(Path.Combine(dir, "title_both_broken.png")); yield return new WaitForSeconds(0.6f);
    L("title buttons: " + Labels()); log.Close(); Application.Quit(); }

  /// -gbdueltest <dir>: the prologue scene staged for art review, close shots, then quit
  public bool duelTest; public string duelTestDir;
  IEnumerator DuelTestShots() {
    yield return new WaitForSeconds(1.2f); var c = cam.transform; Func<string, IEnumerator> Sh = n => ShotCo(Path.Combine(duelTestDir, n));
    var h = ship.Find("PrologueDeck/Hasan"); var r = ship.Find("PrologueDeck/Raider");
    if (Dhow.HasanSail != null) { var hs = Dhow.HasanSail;       foreach (var (nm, a, b) in new[] { ("tack", 0f, 0f), ("peak", 1f, 0f), ("clew", 0f, 1f), ("foot mid", 0f, 0.5f), ("luff third", 0.33f, 0f), ("clew up third", 0.33f, 0.67f) }) { var w = ship.TransformPoint(Dhow.SailPoint(hs[0], hs[1], hs[2], a, b)); Debug.Log($"GB SAILPT {nm} local {Dhow.SailPoint(hs[0], hs[1], hs[2], a, b)} viewport {cam.WorldToViewportPoint(w)}"); } }
    yield return Sh("duel_wide.png");
    foreach (var mf in r.GetComponentsInChildren<MeshFilter>()) { var m = mf.sharedMesh; var sb = new System.Text.StringBuilder(); foreach (var v in m.vertices) sb.AppendLine($"v {v.x:F5} {v.y:F5} {v.z:F5}"); var tr = m.triangles; for (int k = 0; k < tr.Length; k += 3) sb.AppendLine($"f {tr[k] + 1} {tr[k + 1] + 1} {tr[k + 2] + 1}"); File.WriteAllText(Path.Combine(duelTestDir, "raider_" + mf.name + ".obj"), sb.ToString()); }
    var p0 = c.position; var q0 = c.rotation;
    var mid = (h.position + r.position) / 2 + Vector3.up * 1.4f; c.position = mid + (p0 - mid).normalized * 4.6f + Vector3.up * 0.3f; c.LookAt(mid + Vector3.up * 0.55f); yield return new WaitForSeconds(0.3f); yield return Sh("duel_close_both.png");
    foreach (var (t, n) in new[] { (h, "hasan"), (r, "raider") }) { var hp = t.TransformPoint(new Vector3(0, 1.6f, 0.03f)); var toCam = Vector3.Lerp((p0 - hp).normalized, t.TransformDirection(Vector3.forward), 0.45f).normalized; c.position = hp + toCam * 0.85f + Vector3.up * 0.04f; c.LookAt(hp); yield return new WaitForSeconds(0.3f); yield return Sh($"duel_face_{n}.png");
      var bp = t.TransformPoint(new Vector3(0, 1.05f, 0)); c.position = bp + Vector3.Lerp((p0 - bp).normalized, t.TransformDirection(Vector3.forward), 0.35f).normalized * 2.6f; c.LookAt(bp); yield return new WaitForSeconds(0.2f); yield return Sh($"duel_full_{n}.png"); }
    c.position = p0; c.rotation = q0; Application.Quit(); }
}
public partial class Game {
  /// v0.6g4 sail fire, emitted from Hasan's sail surface (Dhow.HasanSail, ship space): flames start at the clew (lower aft corner) and along the foot,
  /// licking a third of the way up the cloth; opaque sharp-edged tongues flickering orange to yellow; only the smoke above is soft.
  public static float DuelFov = 35f, DuelTilt = 8f; public static Vector3 DuelCamPos = new Vector3(-6.2f, 2.3f, -1.2f), DuelCamLook = new Vector3(0f, 1.05f, -1.2f); /* v0.6g6: g3 framing restored exactly */
  /// the dark spoked shape top-left of the duel was the gold sun emblem painted on Hasan's sail (ShipArt SAIL cell, centre u .7 v .36), gone near-black under the night tint.
  /// For the duel only, a cloth patch on the sail surface covers it, textured from a plain cream part of the same sail cell (same material, same night tint).
  IEnumerator MeasureFighters() { yield return null; yield return null; float lo = 1, hi = 0; foreach (var n in new[] { "PrologueDeck/Hasan", "PrologueDeck/Raider" }) { var t = ship.Find(n); if (!t) continue; foreach (var y in new[] { 0f, 1.83f }) { var vp = cam.WorldToViewportPoint(t.TransformPoint(new Vector3(0, y, 0))); lo = Mathf.Min(lo, vp.y); hi = Mathf.Max(hi, vp.y); } } /* soles to turban/crown top */
    Debug.Log($"GB FIGHTERS screen height {(hi - lo) * 100:0}% (bottom {lo * 100:0}% top {hi * 100:0}% of the screen from the bottom; strike panel covers the bottom ~32%)"); if (playthrough) PLog($"fighters measure {(hi - lo) * 100:0}% of screen height, feet at {lo * 100:0}% from bottom"); }
  void SunPatch(Transform parent) { var hs = Dhow.HasanSail; if (hs == null) return; var R = ShipArt.UV(ShipArt.SAIL); var mr0 = ship.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(x => x.sharedMaterial && x.sharedMaterial.name.Contains("ShipHasan")); if (!mr0) return;
    Vector3 belly = Vector3.Cross(hs[1] - hs[0], hs[2] - hs[0]).normalized; if (belly.x < 0) belly = -belly; var vs = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>(); int n = 14; float rad = 0.15f;
    for (int j = 0; j <= n; j++) for (int i = 0; i <= n; i++) { float du = (i / (float)n * 2 - 1) * rad, dv = (j / (float)n * 2 - 1) * rad; float U = 0.7f + du, Vv = 0.36f + dv; float a = Vv, b = U - Vv; /* sail UV: u = a + b, v = a */
      vs.Add(Dhow.SailPoint(hs[0], hs[1], hs[2], a, b) - belly * 0.03f); uvs.Add(new Vector2(R.xMin + R.width * (0.32f + du), R.yMin + R.height * (0.62f + dv))); }
    for (int j = 0; j < n; j++) for (int i = 0; i < n; i++) { int k = j * (n + 1) + i; tris.AddRange(new[] { k, k + n + 1, k + 1, k + 1, k + n + 1, k + n + 2, k, k + 1, k + n + 1, k + 1, k + n + 2, k + n + 1 }); }
    var m = new Mesh(); m.SetVertices(vs); m.SetUVs(0, uvs); m.SetTriangles(tris, 0); m.RecalculateNormals(); var g = new GameObject("SunPatch"); g.transform.SetParent(parent, false); g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = mr0.sharedMaterial; }
  IEnumerator FireSim2(Transform tr) {
    var smokeGo = new GameObject("SailSmoke"); smokeGo.transform.SetParent(tr, false); var smf = smokeGo.AddComponent<MeshFilter>(); smf.sharedMesh = new Mesh(); smokeGo.AddComponent<MeshRenderer>().sharedMaterial = Resources.Load<Material>("Mats/Smoke");
    var fmr = tr.GetComponent<MeshRenderer>(); var cut = Resources.Load<Material>("Mats/FireCut"); if (cut) fmr.sharedMaterial = cut;
    var glow = new GameObject("DeckFireGlow").AddComponent<Light>(); glow.type = LightType.Point; glow.color = new Color(1f, 0.5f, 0.15f); glow.range = 4.5f; glow.intensity = 1.3f; glow.shadows = LightShadows.None; glow.transform.SetParent(tr, false); glow.transform.localPosition = new Vector3(-0.6f, 1.45f, -1.2f); /* small orange light on the deck: the fighters catch the fire from below */
    var hs = Dhow.HasanSail ?? new[] { new Vector3(0.3f, 2.4f, 8.5f), new Vector3(0.3f, 13f, -5f), new Vector3(0.6f, 2.3f, -3.6f) };
    Vector3 belly = Vector3.Cross(hs[1] - hs[0], hs[2] - hs[0]).normalized; if (belly.x < 0) belly = -belly; /* camera looks from -x: flames sit just proud of the cloth on that face */
    Vector3 OnSail(float a, float b) => Dhow.SailPoint(hs[0], hs[1], hs[2], a, b) - belly * 0.07f;
    var sailGlow = new GameObject("SailFireGlow").AddComponent<Light>(); sailGlow.type = LightType.Point; sailGlow.color = new Color(1f, 0.48f, 0.12f); sailGlow.range = 6.5f; sailGlow.intensity = 3.2f; sailGlow.shadows = LightShadows.None; sailGlow.transform.SetParent(tr, false); sailGlow.transform.localPosition = OnSail(0.2f, 0.66f) - belly * 0.8f; /* the cloth round the flames reads as burning sail */
    const int NF = 260, NE = 40, NS = 40; int N = NF + NE;
    var A = new float[N]; var Bc = new float[N]; var rise = new float[N]; var age = new float[N + NS]; var life = new float[N + NS]; var size = new float[N + NS]; var seed = new float[N + NS]; var pos = new Vector3[N + NS]; var vel = new Vector3[N + NS];
    Mesh Mk(Mesh m, int n, out Vector3[] v, out Color[] c) { m.MarkDynamic(); v = new Vector3[n * 4]; c = new Color[n * 4]; var uv = new Vector2[n * 4]; var t = new int[n * 6];
      for (int i = 0; i < n; i++) { uv[i * 4] = new Vector2(0, 0); uv[i * 4 + 1] = new Vector2(1, 0); uv[i * 4 + 2] = new Vector2(1, 1); uv[i * 4 + 3] = new Vector2(0, 1); int b = i * 4; t[i * 6] = b; t[i * 6 + 1] = b + 2; t[i * 6 + 2] = b + 1; t[i * 6 + 3] = b; t[i * 6 + 4] = b + 3; t[i * 6 + 5] = b + 2; }
      m.vertices = v; m.uv = uv; m.colors = c; m.triangles = t; m.bounds = new Bounds(Vector3.zero, Vector3.one * 40); return m; }
    var fm = Mk(tr.GetComponent<MeshFilter>().sharedMesh, N, out var fv, out var fc); var sm = Mk(smf.sharedMesh, NS, out var sv, out var sc);
    for (int i = 0; i < N + NS; i++) { age[i] = Rnd.value * 2; life[i] = 0; seed[i] = Rnd.value * 100; }
    var orange = new Color(1f, 0.42f, 0.05f); var yellow = new Color(1f, 0.85f, 0.25f); var red = new Color(0.85f, 0.2f, 0.03f);
    while (tr) { float dt = Time.deltaTime, tt = Time.time; var cm = Camera.main ? Camera.main.transform : null; Vector3 r = cm ? tr.InverseTransformDirection(cm.right) : Vector3.right; Vector3 up = Vector3.up;
      glow.intensity = 1.0f + 0.6f * Mathf.PerlinNoise(tt * 7, 1.3f); sailGlow.intensity = 5f + 2f * Mathf.PerlinNoise(tt * 6, 4.1f);
      for (int i = 0; i < N + NS; i++) { age[i] += dt; bool ember = i >= NF && i < N, smoke = i >= N;
        if (age[i] >= life[i]) { age[i] = 0;
          float lee = 0.06f + Mathf.Pow(Rnd.value, 1.3f) * 0.3f; /* up the leech from the clew corner (where it leaves the stern-cabin roof), a third of the way, densest low */ float b = 1 - lee - Mathf.Pow(Rnd.value, 1.3f) * 0.22f * (1 - lee * 1.2f); /* v0.6g6: the burn band reaches further in from the leech so a strip of it climbs into the g3 frame's top-right */ float aMax = lee; float corner = 1 - (lee - 0.06f) / 0.3f;
          if (smoke) { float a0 = Rnd.Range(0f, aMax); pos[i] = OnSail(a0, b - a0) + up * Rnd.Range(0.9f, 1.6f) - belly * 0.2f; vel[i] = new Vector3(Rnd.Range(-0.1f, 0.1f), Rnd.Range(0.5f, 0.9f), Rnd.Range(-0.3f, 0.1f)); life[i] = Rnd.Range(2.5f, 4f); size[i] = Rnd.Range(0.45f, 0.8f); }
          else { A[i] = aMax; Bc[i] = b; rise[i] = 0; life[i] = ember ? Rnd.Range(1.2f, 2.4f) : Rnd.Range(0.35f, 0.75f); size[i] = ember ? Rnd.Range(0.025f, 0.04f) : Rnd.Range(0.12f, 0.38f) * (0.55f + 0.7f * corner); seed[i] = Rnd.value * 100;
            vel[i] = ember ? new Vector3(Rnd.Range(-0.3f, 0.1f), Rnd.Range(0.7f, 1.4f), Rnd.Range(-0.4f, 0.4f)) : Vector3.zero; pos[i] = OnSail(A[i], Bc[i]); } }
        float u = age[i] / Mathf.Max(0.01f, life[i]);
        if (smoke) { pos[i] += vel[i] * dt; var col = new Color(0.08f, 0.07f, 0.065f, 0.4f * Mathf.Sin(u * Mathf.PI)); float s2 = size[i] * (1 + u * 1.5f); int b4 = (i - N) * 4;
          sv[b4] = pos[i] - r * s2 - up * s2; sv[b4 + 1] = pos[i] + r * s2 - up * s2; sv[b4 + 2] = pos[i] + r * s2 + up * s2; sv[b4 + 3] = pos[i] - r * s2 + up * s2; sc[b4] = sc[b4 + 1] = sc[b4 + 2] = sc[b4 + 3] = col; continue; }
        Vector3 p; float w, h; Color c;
        if (ember) { pos[i] += vel[i] * dt; p = pos[i]; w = size[i]; h = size[i] * 1.6f; c = Color.Lerp(yellow, red, u); c.a = u < 0.85f ? 1 : 0; }
        else { /* a tongue crawls up the cloth from its root, a third of the way at most */ float climb = Mathf.Min(A[i] + u * 0.04f, 0.36f); p = OnSail(climb, Mathf.Max(0, Bc[i] - u * 0.04f)) + up * (u * 0.25f - 0.12f);
          float fl = Mathf.PerlinNoise(seed[i], tt * 9); float asp = 1.5f + 2.2f * Mathf.Repeat(seed[i] * 0.37f, 1f); w = size[i] * (1 - 0.55f * u) * (0.75f + 0.5f * fl); h = w * asp; /* varied tongues: squat to tall */ c = Color.Lerp(orange, yellow, Mathf.PerlinNoise(seed[i] + 7, tt * 11)); if (u > 0.7f) c = Color.Lerp(c, red, (u - 0.7f) / 0.3f); c.a = 1 - u * 0.5f; }
        int k = i * 4; var lean = r * (w * (Mathf.PerlinNoise(seed[i], tt * 2.5f) - 0.5f) * 1.6f); bool flip = ((int)seed[i] & 1) == 1; var L = flip ? r : -r; fv[k] = p + L * w; fv[k + 1] = p - L * w; fv[k + 2] = p - L * w * 0.7f + up * h * 2 + lean; fv[k + 3] = p + L * w * 0.7f + up * h * 2 + lean; /* mirrored, leaning, tapered: no two alike */ fc[k] = fc[k + 1] = fc[k + 2] = fc[k + 3] = c; }
      fm.vertices = fv; fm.colors = fc; sm.vertices = sv; sm.colors = sc; yield return null; } }
}
