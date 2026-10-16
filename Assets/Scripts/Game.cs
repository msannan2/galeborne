using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Rnd = UnityEngine.Random;

[Serializable] public class Ban { public int good, port, start, end; }
[Serializable] public class Batch { public int good; public float qty; public int storedWeek; }
[Serializable] public class Warehouse { public int tier; public List<Batch> items = new List<Batch>(); }

public partial class Game : MonoBehaviour {
  public GameSettings S; public UITheme T;
  // ---- state ----
  public int coins, week, port, sails, tab = -1; public bool crate, tithe;
  public int[] hold; public int[] rep; public float[,] price, lastPrice, basePrice;
  public List<Ban> bans = new List<Ban>(); public Warehouse[] wh; public List<string> news = new List<string>();
  public string msg = ""; public int safeUntilSail; public bool crewInjured; public List<string> charms = new List<string>();
  // ---- ui ----
  Canvas canvas; RectTransform root, content, fx, topBar; Image backdrop; Text coinText; Transform sea3D; Camera cam;
  Dictionary<string, Sprite> spr = new Dictionary<string, Sprite>(); Sprite tri, dot;
  bool busy;

  int G(string id) => Array.FindIndex(S.goods, g => g.id == id);
  int Moon => week / S.weeksPerMoon; 
  string DateStr => $"Week {week % S.weeksPerMoon + 1} · Moon {Moon % S.moonsPerYear + 1} · Year {week / (S.weeksPerMoon * S.moonsPerYear) + 1}";
  int HoldUsed => hold.Sum();
  public bool Banned(int g, int p) => bans.Any(b => b.good == g && b.port == p && week >= b.start && week < b.end);
  public float Risk(int p) => Mathf.Max(S.customs.floor, S.customs.baseRisk - (crate || papersGiven ? S.customs.fakersCrate : 0) - (tithe ? S.customs.templeTithe : 0));
  bool CarriesBanned(int p) { for (int g = 0; g < hold.Length; g++) if (hold[g] > 0 && Banned(g, p)) return true; return false; }

  bool shotMode, onTitle;
  void Start() { Application.targetFrameRate = 30; 
#if UNITY_STANDALONE || UNITY_EDITOR
    Application.runInBackground = true; /* desktop only: a bare Xvfb has no window manager: without focus the player loop paused at the title screen */
#endif
    shotMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-gbshots") >= 0; { var ga = Environment.GetCommandLineArgs(); int fi = Array.IndexOf(ga, "-gbforce"); if (fi >= 0 && fi + 1 < ga.Length) gbForce = ga[fi + 1].ToLower(); if (gbForce.Contains("customs_caught")) ptForceCustoms = 1; if (gbForce.Contains("customs_pass")) ptForceCustoms = 0; if (gbForce != "") Debug.Log("GB FORCE " + gbForce); } Init(); BuildWorld(); BuildUI(); { var qa = Environment.GetCommandLineArgs(); int qi = Array.IndexOf(qa, "-gbquality"); int q0 = qi >= 0 && qi + 1 < qa.Length && int.TryParse(qa[qi + 1], out int qv) ? qv : PlayerPrefs.GetInt("gbQuality", DefaultQuality()); string m0 = msg; ApplyQuality(Mathf.Clamp(q0, 0, 2)); msg = m0; } /* k4: saved graphics level applied at boot */
    { var ga2 = Environment.GetCommandLineArgs(); int di = Array.IndexOf(ga2, "-gbdueltest"); if (di >= 0 && di + 1 < ga2.Length) { duelTest = true; duelTestDir = ga2[di + 1]; System.IO.Directory.CreateDirectory(duelTestDir); Init(); onTitle = false; StartCoroutine(Prologue()); return; }
      if (Array.IndexOf(ga2, "-gbsavetest") >= 0) { StartCoroutine(SaveTestIfAsked()); return; }
      if (gbForce.Contains("qa_run")) { StartCoroutine(QaRun()); return; } /* gamedev_qa1 */
      if (gbForce.Contains("endgame_test")) { StartCoroutine(EgSelfTest()); return; } /* gamedev_end1 */
      if (Array.IndexOf(ga2, "-gblandmarks") >= 0) { StartCoroutine(LandmarkShotsIfAsked()); return; }
      { int gi = Array.IndexOf(ga2, "-gbduel"); if (gi >= 0 && gi + 1 < ga2.Length) { string gm = ga2[gi + 1].ToLower(); if (gm == "repel" || gm == "board") { StartCoroutine(GbDuelIfAsked(gm)); return; } Debug.Log("GB DUEL bad mode " + gm + " (use repel|board)"); } } /* gamedev_t11_gbduel */ }
    if (shotMode) { Refresh(); StartCoroutine(ShotModeIfAsked()); } else { ShowTitle(); StartCoroutine(PlaythroughIfAsked()); } }

  /* debug: -gbcoins N (tests only) */
  public void GbCoins(string when) { var ga = Environment.GetCommandLineArgs(); int i = Array.IndexOf(ga, "-gbcoins"); if (i >= 0 && i + 1 < ga.Length && int.TryParse(ga[i + 1], out int n)) { coins = n; Debug.Log("GB COINS " + n + " (" + when + ")"); } }
  public void Init() {
    EgReset(); /* gamedev_end1 */
    S = GameSettings.Load(); T = UITheme.Load(); ApplyDesign(); Act1Init();
    int ng = S.goods.Length, np = S.ports.Length;
    coins = S.startCoins; GbCoins("new game"); hold = new int[ng]; rep = new int[np]; wh = new Warehouse[np];
    for (int p = 0; p < np; p++) wh[p] = new Warehouse();
    price = new float[np, ng]; lastPrice = new float[np, ng]; basePrice = new float[np, ng];
    for (int g = 0; g < ng; g++) {
      float b = Rnd.Range(S.goods[g].min, S.goods[g].max); // reroll within range each new game
      for (int p = 0; p < np; p++) {
        float m = PortMult(p, g);
        basePrice[p, g] = b * m * Rnd.Range(0.95f, 1.05f); price[p, g] = lastPrice[p, g] = basePrice[p, g]; } }
    for (int g = 0; g < ng; g++) if (S.goods[g].contraband) { int p = Rnd.Range(0, np); bans.Add(new Ban { good = g, port = p, start = 0, end = Rnd.Range(S.contraband.banMoonsMin, S.contraband.banMoonsMax + 1) * S.weeksPerMoon }); }
    KeepBases(); shipTier = new int[4]; ApplyShipTiers(); fame = 0; bankCoins = 0; bankPort = -1; loanOwed = 0; loanCap = 0; fightsFought = 0; duelsFought = 0; if (GwadarIdx >= 0) port = GwadarIdx;
    InitV06(); ScheduleBans(); msg = $"Dawn at {S.ports[port].name}. Hasan's dhow creaks against the pier. Tap the bazaar.";
  }

  void ScheduleBans() {
    if (DZ != null) { EnsureDesignBans(); return; } /* v0.6: bans come from the back-alley data and the opening */
    int np = S.ports.Length;
    for (int g = 0; g < S.goods.Length; g++) if (S.goods[g].contraband) {
      var active = bans.Where(b => b.good == g && b.end > week).ToList();
      int target = Rnd.Range(S.contraband.bannedPortsMin, S.contraband.bannedPortsMax + 1);
      if (active.Count(b => b.end > week + S.contraband.announceWeeksAhead) < S.contraband.bannedPortsMin || active.Count < target) {
        var free = Enumerable.Range(0, np).Where(p => !active.Any(b => b.port == p)).ToList();
        if (free.Count == 0) continue;
        int pp = free[Rnd.Range(0, free.Count)], st = week + S.contraband.announceWeeksAhead;
        bans.Add(new Ban { good = g, port = pp, start = st, end = st + Rnd.Range(S.contraband.banMoonsMin, S.contraband.banMoonsMax + 1) * S.weeksPerMoon });
        news.Insert(0, $"Rumor ({DateStr}): {S.ports[pp].name} will ban {S.goods[g].name} from next week."); } }
    bans.RemoveAll(b => b.end <= week);
  }

  void NewWeek() {
    int oldMoon = Moon; week++; EgNewWeek(); /* gamedev_end1 */
    for (int p = 0; p < S.ports.Length; p++) for (int g = 0; g < S.goods.Length; g++) {
      lastPrice[p, g] = price[p, g];
      float v = price[p, g] * (1 + Rnd.Range(-S.weeklyDriftMax, S.weeklyDriftMax));
      float bp = basePrice[p, g] * EvMult(p, g); /* event multiplier applied after drift */ v = Mathf.Lerp(v, bp, S.pullToBase);
      price[p, g] = Mathf.Clamp(v, bp * 0.5f, bp * 2f); }
    for (int p = 0; p < wh.Length; p++) if (wh[p].tier == 1) {
      if (coins >= S.warehouse.leasePerWeek) coins -= S.warehouse.leasePerWeek;
      else { wh[p].tier = 0; news.Insert(0, $"Your lease in {S.ports[p].name} lapsed; goods held until you pay again."); } }
    if (Moon != oldMoon) MoonMoney();
    if (Moon != oldMoon) foreach (var w in wh) foreach (var b in w.items)
      if ((week - b.storedWeek) / S.weeksPerMoon >= S.warehouse.decayAfterMoons) b.qty *= 1 - S.warehouse.decayPerMoon;
    TickMarketV06(Moon != oldMoon); ScheduleBans(); TickHarbourDebt();
  }

  // ---------------- trading ----------------
  public float[] avgCost; float[] AvgCost { get { if (avgCost == null || avgCost.Length != S.goods.Length) avgCost = new float[S.goods.Length]; return avgCost; } }
  public bool Buy(int g) {
    if (Banned(g, port) || coins < Mathf.CeilToInt(price[port, g]) || HoldUsed >= S.holdSize || StockOf(g) <= 0) return false; if (stock != null) stock[port, g]--;
    int paid = Mathf.CeilToInt(price[port, g]); AvgCost[g] = (AvgCost[g] * hold[g] + paid) / (hold[g] + 1); coins -= paid; hold[g]++; price[port, g] *= 1 + S.perUnitBuyRise; return true; }
  public int SellPrice(int g) => RugsBanSale(g) ? RugsBanPrice() : Mathf.FloorToInt(price[port, g] * (Banned(g, port) ? S.contraband.bannedSellMult : S.sellSpread));
  public bool Sell(int g) {
    if (hold[g] <= 0) return false; int v = SellPrice(g); coins += v; hold[g]--; JackpotCheck(g, v); EgFirstSale(g); /* gamedev_end1 */
    if (g == Gp && port == Muscat && !openingGpSold) { openingGpSold = true; msg = GpSoldLine; news.Insert(0, GpSoldLine); /* research_gp1 */ if (playthrough) PLog2("opening sale", "gunpowder sold in Muscat's alley — medium goal clears"); } /* v0.6h4: sale not confiscation */
    price[port, g] *= 1 - (Banned(g, port) ? S.contraband.bannedPerUnitSellDrop : S.perUnitSellDrop); return true; }

  // ---------------- 3D sea ----------------
  Transform ship, passShip; Material waterMat; /* v0.6i: passing ship during voyage */
  void BuildWorld() {
    cam = Camera.main; if (cam == null) { cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; }
    cam.transform.position = new Vector3(-18f, 10f, -30f); cam.transform.LookAt(new Vector3(0, 4.5f, 1f));
    cam.fieldOfView = 55; cam.farClipPlane = 900; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UITheme.C(T.skyHorizon);
    sea3D = new GameObject("Sea3D").transform;
    Vector3 sunDir = new Vector3(-0.75f, 0.32f, 0.58f).normalized;
    var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional; light.transform.parent = sea3D;
    light.transform.rotation = Quaternion.LookRotation(-sunDir); light.color = new Color(1f, 0.88f, 0.68f); light.intensity = 1.2f; light.shadows = LightShadows.None;
    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.40f, 0.46f, 0.58f); // cool shade side, warm sun side
    RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = UITheme.C(T.skyHorizon);
    RenderSettings.fogStartDistance = 35; RenderSettings.fogEndDistance = 210; // water melts into the horizon colour
    // sky
    var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy0(sky.GetComponent<Collider>()); sky.name = "SeaSky"; sky.transform.parent = sea3D; sky.transform.localScale = Vector3.one * 1500;
    var skyMat = Resources.Load<Material>("Mats/Sky"); skyMat.SetColor("_Horizon", UITheme.C(T.skyHorizon)); skyMat.SetColor("_Zenith", UITheme.C(T.skyZenith));
    skyMat.SetColor("_Sun", UITheme.C(T.sun)); skyMat.SetVector("_SunDir", sunDir); sky.GetComponent<Renderer>().sharedMaterial = skyMat;
    // water grid
    var w = new GameObject("Water"); w.transform.parent = sea3D; w.AddComponent<MeshFilter>().sharedMesh = Grid(110, 4f);
    waterMat = Resources.Load<Material>("Mats/Water"); waterMat.SetColor("_Shallow", UITheme.C(T.seaShallow)); waterMat.SetColor("_Deep", UITheme.C(T.seaDeep));
    waterMat.SetColor("_Sun", UITheme.C(T.sun)); waterMat.SetVector("_SunDir", sunDir); w.AddComponent<MeshRenderer>().sharedMaterial = waterMat;
    w.transform.position = new Vector3(-220, 0, -120);
    // dhow — v0.6j Kenney watercraft (CC0) with Galeborne colormap; procedural fallback
    ship = new GameObject("Dhow").transform; ship.parent = sea3D;
    var kenney = FreeModels.SpawnKenneyDhow(ship, "HasanDhow");
    if (kenney == null) { var hd = Dhow.Build(ship, T, false, "HasanDhow"); hasanTris = Dhow.LastTris; Debug.LogWarning("GB: Kenney dhow failed — using procedural"); }
    else hasanTris = kenney.GetComponentsInChildren<MeshFilter>().Sum(m => m.sharedMesh ? m.sharedMesh.triangles.Length / 3 : 0);
    // distant islands + v0.6i coastline ridge so land is visible while sailing
    for (int i = 0; i < 5; i++) {
      var isl = Prim(PrimitiveType.Sphere, sea3D, new Vector3(Rnd.Range(-200f, 200f), -2, Rnd.Range(140f, 260f)), new Vector3(Rnd.Range(30, 70), Rnd.Range(8, 18), Rnd.Range(20, 40)), "Mats/Island", Quaternion.identity); isl.name = "Island"; }
    for (int i = 0; i < 6; i++) { float z = 70 + i * 28; var ridge = Prim(PrimitiveType.Sphere, sea3D, new Vector3(-55 - i * 8, -1.5f, z), new Vector3(40 + i * 4, 10, 22), "Mats/Island", Quaternion.identity); ridge.name = "Island"; }
    passShip = Dhow.Build(sea3D, T, false, "Passer"); passShip.gameObject.SetActive(false);
    sea3D.gameObject.SetActive(false);
  }
  static void Destroy0(UnityEngine.Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }
  GameObject Prim(PrimitiveType t, Transform par, Vector3 pos, Vector3 sc, string mat, Quaternion r) {
    var g = GameObject.CreatePrimitive(t); Destroy0(g.GetComponent<Collider>()); g.transform.parent = par; g.transform.localPosition = pos; g.transform.localScale = sc; g.transform.localRotation = r;
    g.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>(mat); return g; }
  static Mesh Grid(int n, float s) {
    var m = new Mesh(); m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; var v = new Vector3[(n + 1) * (n + 1)]; var tr = new int[n * n * 6];
    for (int z = 0, i = 0; z <= n; z++) for (int x = 0; x <= n; x++) v[i++] = new Vector3(x * s, 0, z * s);
    for (int z = 0, t = 0; z < n; z++) for (int x = 0; x < n; x++) { int a = z * (n + 1) + x; tr[t++] = a; tr[t++] = a + n + 1; tr[t++] = a + 1; tr[t++] = a + 1; tr[t++] = a + n + 1; tr[t++] = a + n + 2; }
    m.vertices = v; m.triangles = tr; m.bounds = new Bounds(new Vector3(n * s / 2, 0, n * s / 2), new Vector3(n * s, 20, n * s)); return m; }
  static Mesh HullMesh() {
    int N = 14; float L = 9f; var verts = new List<Vector3>(); var tris = new List<int>();
    for (int i = 0; i <= N; i++) { float t = i / (float)N; float z = -L * 0.42f + t * L;
      float w = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Lerp(0.18f, 1f, t)), 0.7f) * 1.35f; float sheer = 1.0f + Mathf.Pow(t, 3) * 1.1f + (1 - t) * 0.3f;
      verts.Add(new Vector3(-w, sheer, z)); verts.Add(new Vector3(-w * 0.75f, 0.1f, z)); verts.Add(new Vector3(0, -0.55f, z)); verts.Add(new Vector3(w * 0.75f, 0.1f, z)); verts.Add(new Vector3(w, sheer, z)); }
    for (int i = 0; i < N; i++) for (int k = 0; k < 4; k++) { int a = i * 5 + k, b = a + 5; tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 }); }
    for (int i = 0; i < N; i++) { int a = i * 5, b = a + 5; tris.AddRange(new[] { a, a + 4, b, b, a + 4, b + 4 }); } // deck
    tris.AddRange(new[] { 0, 1, 4, 1, 2, 3, 1, 3, 4 }); // transom
    // flat-shade
    var fv = new List<Vector3>(); var ft = new List<int>(); for (int i = 0; i < tris.Count; i++) { fv.Add(verts[tris[i]]); ft.Add(i); }
    var m = new Mesh(); m.SetVertices(fv); m.SetTriangles(ft, 0); m.RecalculateNormals(); return m; }
  static Mesh Lateen(Vector3 a, Vector3 b, Vector3 c) {
    var m = new Mesh(); m.vertices = new[] { a, b, c, a, c, b }; m.triangles = new[] { 0, 1, 2, 3, 4, 5 }; m.RecalculateNormals(); return m; }

  float sailT; int hasanTris;
  void Update() {
    EgUpdate(); /* gamedev_end1 */
    if (Input.GetKeyDown(KeyCode.Escape)) HandleBack(); /* final modal_queue_back: general Back handler (replaces the Credits-only Escape line) */
    if (prologuePending && dialog == null && !fighting) { prologuePending = false; StartCoroutine(Prologue()); }
    if (Input.anyKeyDown || Input.GetMouseButtonDown(0)) lastInput = Time.unscaledTime;
    if (InOpening && !onTitle && !sailing && !fighting && tab < 0 && dialog == null && Time.unscaledTime - lastInput > 45f) { lastInput = Time.unscaledTime; Nudge(); }
    if (!onTitle && !sailing && !fighting && tab < 0 && S != null) TownInput();
    if (town3D != null && town3D.gameObject.activeInHierarchy) TownAnimate();
    if (sea3D != null && sea3D.gameObject.activeSelf && ship != null && !fighting) {
      float t = Time.time;
      ship.position = new Vector3(0, 0.15f + Mathf.Sin(t * 1.3f) * 0.18f, 0);
      ship.rotation = Quaternion.Euler(Mathf.Sin(t * 0.9f) * 2.5f, -8f, Mathf.Sin(t * 1.1f) * 4f);
      /* v0.6i: closer follow so Hasan's dhow reads large on screen */
      float near = sailing ? 1f : 0f;
      cam.transform.position = Vector3.Lerp(new Vector3(-18f, 10f, -30f), new Vector3(-9f, 6.2f, -14f), near) + new Vector3(Mathf.Sin(t * 0.2f) * 0.6f, Mathf.Sin(t * 0.5f) * 0.15f, 0);
      cam.transform.LookAt(new Vector3(0, sailing ? 3.2f : 4.5f, 1f));
      if (arrivalPanT0 >= 0 && Time.time > arrivalPanT0 + 0.15f && (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))) /* gwadar_arrival_hold: only a fresh tap skips (not the held/same-frame tap that closed a card) */ { arrivalPanT0 = -1; Debug.Log("GB ARRIVAL pan skipped by tap"); } /* k4merge2: tap skips, camera returns at once */
      if (arrivalPanT0 >= 0) { float u = (Time.time - arrivalPanT0) / ArrivalPanDur; if (u >= 1f) arrivalPanT0 = -1; else { float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(u / 0.12f)) * (1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01((u - 0.9f) / 0.1f))) /* gwadar_arrival_hold: ease in ~0.35 s, hold, ease out last 0.3 s */; var look = SafeRot.Look(arrivalTarget - cam.transform.position, cam.transform.rotation); /* gamedev_t8 */ cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, look, k); } } /* k4merge2: arrival pan toward shore landmark, eased out and back */
      if (customsFrame) { var cl = SafeRot.Look(arrivalTarget - cam.transform.position, cam.transform.rotation); /* gamedev_t8 */ cam.transform.rotation = cl; } /* gamedev_t7: customs card framed on the arrival landmark */ if (passShip != null && passShip.gameObject.activeInHierarchy) { passShip.position += new Vector3(Time.deltaTime * 4.5f, 0, Time.deltaTime * 1.2f); if (passShip.position.x > 80) passShip.gameObject.SetActive(false); } }
  }

  // ---------------- UI construction ----------------
  Sprite Panel(Color fill, Color border) {
    string key = fill + "|" + border; if (spr.TryGetValue(key, out var s)) return s;
    if (!string.IsNullOrEmpty(T.panelSprite)) { var custom = Resources.Load<Sprite>(T.panelSprite); if (custom) return spr[key] = custom; }
    int n = 16, bp = T.borderPx; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false); tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Clamp;
    for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) tex.SetPixel(x, y, (x < bp || y < bp || x >= n - bp || y >= n - bp) ? border : fill);
    tex.Apply(); return spr[key] = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(bp + 1, bp + 1, bp + 1, bp + 1)); }
  Sprite Tri() { if (tri) return tri; int n = 32; var tex = new Texture2D(n, n); for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) tex.SetPixel(x, y, Mathf.Abs(x - n / 2f + .5f) <= (n - 1 - y) / 2f ? Color.white : Color.clear); tex.Apply(); return tri = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f)); }
  Sprite Dot() { if (dot) return dot; int n = 32; var tex = new Texture2D(n, n); for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) tex.SetPixel(x, y, new Vector2(x - 15.5f, y - 15.5f).magnitude < 15 ? Color.white : Color.clear); tex.Apply(); return dot = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f)); }

  static RectTransform R(Transform p, string n, float x0, float y0, float x1, float y1) {
    var g = new GameObject(n, typeof(RectTransform)); var r = (RectTransform)g.transform; r.SetParent(p, false);
    r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero; return r; }
  static Transform contentRoot; public const float TopBarH = 64f, TopGapK4 = 84f; /* k4merge: screens laid out from y172 now start at 88 (bar 12..76 + 12) */
  static RectTransform Px(Transform p, string n, float x, float yTop, float w, float h) { // pixel rect from parent's top-left
    if (p != null && p == contentRoot && yTop >= 150 && yTop < 1600) yTop -= TopGapK4; /* k4merge: one shared top offset under the 64px bar */
    var r = R(p, n, 0, 1, 0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -yTop); r.sizeDelta = new Vector2(w, h); return r; }
  bool parch; /* while drawing on parchment, dark-theme fills and light inks map to their ledger equivalents */
  string PF(string c) => !parch ? c : c == T.rowShade || c == T.panel || c == T.panelAlt ? T.ledgerRowAlt : c == T.ink ? T.ledgerRule : c == T.brass ? T.ledgerRule : c;
  string PI(string c) => !parch ? c : c == T.text || c == T.parchment ? T.ledgerInk : c == T.textMuted ? T.ledgerMuted : c == T.gold ? "#8A5A12" : c == T.up ? T.ledgerUp : c == T.down ? T.ledgerDown : c;
  Image Box(RectTransform r, string fill, string border = null) { fill = PF(fill); border = border == null ? null : PF(border);
    /* v0.6i: dark teak menu panels use the 9-slice teak frame (~110px borders), cream text on top */
    if (!parch && fill == T.panel) { var teak = Resources.Load<Sprite>("Art/ui_teak_panel"); if (teak) { var ti = r.gameObject.AddComponent<Image>(); ti.sprite = teak; ti.type = Image.Type.Sliced; ti.color = Color.white; return ti; } }
    var i = r.gameObject.AddComponent<Image>(); i.sprite = Panel(UITheme.C(fill), UITheme.C(border ?? fill)); i.type = Image.Type.Sliced; return i; }
  Text Txt(RectTransform r, string s, int size, string col, TextAnchor a = TextAnchor.MiddleLeft, bool bold = false) {
    var t = r.gameObject.AddComponent<Text>(); t.font = bold ? T.bold : T.regular; t.fontSize = size; t.color = UITheme.C(PI(col)); t.alignment = a; t.text = s;
    t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.supportRichText = true; return t; }
  Button Btn(Transform p, string label, float x, float y, float w, float h, Action on, bool enabled = true, bool accent = false) {
    bool pp = parch; parch = false; try { return Btn0(p, label, x, y, w, h, on, enabled, accent); } finally { parch = pp; } }
  Button Btn0(Transform p, string label, float x, float y, float w, float h, Action on, bool enabled, bool accent) {
    var r = Px(p, "Btn " + label, x, y, w, h); Box(r, enabled ? (accent ? T.panelAlt : T.button) : T.panel, enabled ? (accent ? T.gold : T.brass) : T.disabled);
    var b = r.gameObject.AddComponent<Button>(); b.interactable = enabled; b.onClick.AddListener(() => { if (!busy || label == "Continue") on(); });
    Txt(R(r, "L", 0, 0, 1, 1), label, T.fontSizeSmall, enabled ? (accent ? T.gold : T.buttonText) : T.disabled, TextAnchor.MiddleCenter); return b; }
  Text Label(Transform p, string s, float x, float y, float w, float h, int size, string col, TextAnchor a = TextAnchor.MiddleLeft, bool bold = false) => Txt(Px(p, "T", x, y, w, h), s, size, col, a, bold);

  RectTransform canvasRoot, dimmer; Rect lastSafe; Vector2 lastScreen;
  void BuildUI() {
    var cg = new GameObject("Canvas"); canvas = cg.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    var sc = cg.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1080, 2340); sc.matchWidthOrHeight = 0f; scaler = sc;
    cg.AddComponent<GraphicRaycaster>();
    if (FindFirstObjectByType<EventSystem>() == null) { var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>(); }
    canvasRoot = (RectTransform)cg.transform;
    var bd = R(canvasRoot, "Backdrop", 0, 0, 1, 1); backdrop = bd.gameObject.AddComponent<Image>(); backdrop.preserveAspect = false; // edge to edge, ignores safe area
    var f = bd.gameObject.AddComponent<AspectRatioFitter>(); f.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; f.aspectRatio = 1080f / 1920f;
    dimmer = R(canvasRoot, "Dim", 0, 0, 1, 1); dimmer.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.06f, 0.04f, 0.55f); dimmer.GetComponent<Image>().raycastTarget = false;
    root = R(canvasRoot, "SafeArea", 0, 0, 1, 1); ApplySafeArea();
    content = R(root, "Content", 0.5f, 0, 0.5f, 1); content.sizeDelta = new Vector2(1080, 0); contentRoot = content; fx = R(root, "FX", 0, 0, 1, 1);
  }
  /// Screen.safeArea, or a simulated camera-hole/gesture inset passed as -gbsafe top,bottom (pixels).
  Rect SafeRect() {
    var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-gbsafe");
    if (i >= 0 && i + 1 < args.Length) { var p = args[i + 1].Split(','); float top = float.Parse(p[0]), bot = float.Parse(p[1]); return new Rect(0, bot, Screen.width, Screen.height - top - bot); }
    return Screen.safeArea; }
  CanvasScaler scaler;
  // wide screens (foldables, tablets, 16:9): fit 1920 tall by height and centre a 1080 column; tall phones: 1080 wide by width
  void FitScaler() { if (scaler == null) return; bool wide = Screen.height < Screen.width * (2340f / 1080f) - 1;
    float hh = Mathf.Clamp(Screen.height / (float)Screen.width * 1080f, 1920f, 2340f);
    scaler.referenceResolution = new Vector2(1080, wide ? hh : 2340); scaler.matchWidthOrHeight = wide ? 1f : 0f; }
  void ApplySafeArea() { FitScaler();
    var sa = SafeRect(); if (sa == lastSafe && lastScreen == new Vector2(Screen.width, Screen.height)) return; lastSafe = sa; lastScreen = new Vector2(Screen.width, Screen.height);
    root.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height); root.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height); root.offsetMin = root.offsetMax = Vector2.zero; }
  void LateUpdate() { if (root) ApplySafeArea(); if (fighting) { if (dimmer) dimmer.gameObject.SetActive(false); if (backdrop) { backdrop.sprite = null; backdrop.color = Color.clear; } cam.backgroundColor = UITheme.C(T.skyZenith); RenderSettings.fogColor = UITheme.C(T.skyZenith); } else if (dimmer) dimmer.gameObject.SetActive(backdrop.color.a > 0.01f); }
  /// container anchored to the bottom of the safe area with a fixed 1920 design height, so bottom controls stay in thumb reach on tall phones
  RectTransform Bottom1920(Transform p, string n) { var r = R(p, n, 0, 0, 1, 0); r.pivot = new Vector2(0.5f, 0); r.sizeDelta = new Vector2(0, 1920); return r; }
  static RectTransform Stretch(Transform p, string n, float left, float top, float right, float bottom) { var r = R(p, n, 0, 0, 1, 1); r.offsetMin = new Vector2(left, bottom); r.offsetMax = new Vector2(-right, -top); return r; }

  void Clear(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) Destroy0(t.GetChild(i).gameObject); }

  void TopBar() { /* k4: exactly one line "{town} · {coins} coins · Hull {hull}% · Moon {moon}" (+ debt while active) */
    topBar = Px(content, "Top", 16, 12, 1048, 64); Box(topBar, T.panel, T.brass);
    bool overdue = harbourDebt > 0 && harbourDebtDueWeek >= 0 && week > harbourDebtDueWeek;
    string debt = harbourDebt <= 0 ? "" : $" · Debt {harbourDebt}" + (harbourDebtDueWeek < 0 ? "" : overdue ? " OVERDUE" : $" due moon {harbourDebtDueWeek / Mathf.Max(1, S.weeksPerMoon) + 1}");
    string town = sailing ? $"At sea → {S.ports[dest].name}" : tab == 4 ? S.ports[port].name : ""; /* in town the town title shows the name */
    string rest = $" · {coins:N0} coins · Hull {(HullMax > 0 ? HullCur / HullMax * 100f : 100f):0}% · Moon {Moon % S.moonsPerYear + 1}";
    int over = town.Length + rest.Length + debt.Length - 43; /* ~43 chars fit at 540px wide in the inset rect: shorten the town name first, coins/hull stay whole */
    if (over > 0 && sailing) { town = $"→ {S.ports[dest].name}"; over = town.Length + rest.Length + debt.Length - 43; } /* sea: keep the destination, drop "At sea" first */
    if (over > 0 && town.Length > 4) town = town.Substring(0, Mathf.Max(3, town.Length - over - 1)) + "…";
    string debtTxt = debt.Length == 0 ? "" : $"<color={(overdue ? T.down : "#E8A33A")}>{debt}</color>"; /* amber open, red overdue */
    coinText = Label(topBar, (town.Length > 0 ? town : rest.Substring(3)) + (town.Length > 0 ? rest : "") + debtTxt, 76, 6, 952, 52, /* 64px left inset reserved for the 56px Hasan medallion (merge) */ T.fontSizeSmall, T.parchment, TextAnchor.MiddleCenter);
    { var rs = Resources.Load<Sprite>("Art/UI/rank_" + Mathf.Clamp(eg.rank, 0, 2)); var mt = rs ? null : Resources.Load<Texture2D>("Art/ui_hasan_medallion"); /* gamedev_badge1: rank badge sprite, old medallion only if it fails to load */
      if (rs || mt) { var mr = Px(topBar, "Medallion", 10, 4, 56, 56); Graphic mi; if (rs) { var im = mr.gameObject.AddComponent<Image>(); im.sprite = rs; im.preserveAspect = true; mi = im; } else { var ri = mr.gameObject.AddComponent<RawImage>(); ri.texture = mt; mi = ri; } mi.raycastTarget = true; /* gamedev_badge1: same 56 px slot */ /* k4merge: Hasan medallion, opens Journal */
      var mb = mr.gameObject.AddComponent<Button>(); mb.targetGraphic = mi; var cb = mb.colors; cb.normalColor = Color.white; cb.highlightedColor = Color.white; cb.selectedColor = Color.white; cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); mb.colors = cb; mb.onClick.AddListener(() => { if (!fighting && dialog == null) OpenJournal(); }); } }
    coinText.supportRichText = true; coinText.horizontalOverflow = HorizontalWrapMode.Wrap; coinText.resizeTextForBestFit = true; coinText.resizeTextMinSize = 14; coinText.resizeTextMaxSize = T.fontSizeSmall; /* shrinks instead of wrapping */
  }
  void Banner() {
    var tex = Resources.Load<Texture2D>("Art/" + S.ports[port].art); var r = Px(content, "Banner", 16, 172, 1048, 280); Box(r, T.ink, T.brass);
    var ri = Px(r, "img", 3, 3, 1042, 274).gameObject.AddComponent<RawImage>(); ri.texture = tex;
    if (tex) { float h = (274f / 1042f) * tex.width / tex.height; ri.uvRect = new Rect(0, 0.62f - h / 2, 1, h); }
    var cap = Px(r, "cap", 0, 206, 1048, 74); cap.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.06f, 0.04f, 0.6f);
    Txt(Px(cap, "t", 24, 0, 1000, 74), S.ports[port].name, T.fontSizeTitle, T.parchment, TextAnchor.MiddleLeft, true);
  }

  // tab: -1 = port screen (temporary building stack; replaced by a 3D town in v0.6)
  static readonly string[] Tabs = { "Market", "Back Alley", "Warehouse", "Temple & Chandler", "Set Sail" };
  public void Refresh() {
    Clear(content); if (sailing) { TopBar(); return; } StoryTick(); if (tab == 4 && !onTitle) { ChartFull(); return; }
    var tex = (tab == 1 ? AlleyBackdrop() : null) ?? Resources.Load<Texture2D>("Art/" + S.ports[port].art); /* v0.6h: Debal back alley art */
    if (tex) backdrop.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f)); backdrop.color = Color.white; dimmer.gameObject.SetActive(true);
    if (dimmer) { var di0 = dimmer.GetComponent<Image>(); if (di0) di0.color = new Color(0.08f, 0.06f, 0.04f, tab == 1 && AlleyBackdrop() != null ? 0.28f : 0.55f); } /* v0.6h3: lighter dim over Debal alley painting */
    TopBar();
    if (tab < 0) { TownScreen(content); var mlT = R(content, "Msg", 0, 0, 1, 0); mlT.pivot = new Vector2(.5f, 0); mlT.offsetMin = new Vector2(16, 16); mlT.offsetMax = new Vector2(-16, 112); Box(mlT, T.panel, T.brass); Txt(Stretch(mlT, "m", 22, 4, 22, 4), msg, T.fontSizeSmall - 2, T.parchment); return; }
    if (town3D) town3D.gameObject.SetActive(false); bool kp = KeeperHeader(tab); if (!kp) Banner();
    var main = Stretch(content, "Main", 16, kp ? 172 + KeeperH + 16 : 468, 16, 128);
    {
      /* v0.6h3: Debal alley painting sits behind the parchment panel (visible as a frame + soft show-through) */
      var alleyPaint = tab == 1 ? AlleyBackdrop() : null;
      if (alleyPaint != null) { var abg = Stretch(main, "AlleyBg", 0, 0, 0, 0); abg.SetAsFirstSibling(); var ari = abg.gameObject.AddComponent<RawImage>(); ari.texture = alleyPaint; ari.raycastTarget = false; }
      var led = Stretch(main, "Panel", alleyPaint != null ? 20 : 0, 96, alleyPaint != null ? 20 : 0, alleyPaint != null ? 12 : 0); var ledImg = Box(led, T.ledgerBg, T.frame); if (alleyPaint != null) { var lc = ledImg.color; lc.a = 0.80f; ledImg.color = lc; } /* v0.6h4: ~80% opaque so alley painting shows through */ parch = tab != 0; /* cream parchment for every keeper screen */
      Btn(main, "‹  Town", 0, 0, 260, 84, () => { tab = -1; Refresh(); });
      bool parchHdr = parch; parch = false; /* art_t6_fixes: tab header sits on the town backdrop, not the parchment - keep T.parchment light (parch mapped it to dark ledger ink on every tab but Market) */
      Label(main, tab == 7 && (S.ports[port].name == "Muscat" || ShipwrightMode(S.ports[port].name) == "none") ? "Harbour Master" : TabNames[tab], 290, 0, 740, 84, /* researcher_harbour_tab: same rule as GameTown.cs L426 */ T.fontSizeTitle - 4, T.parchment, TextAnchor.MiddleLeft, true);
      parch = parchHdr; /* art_t6_fixes */
      try { switch (tab) { case 0: Market(led); break; case 1: Alley(led); break; case 2: Ware(led); break; case 3: Temple(led); break; case 4: ChartScreen(led); break; case 5: BankScreen(led); break; case 6: LenderScreen(led); break; case 7: ShipwrightScreen(led); break; case 8: ParchFill(led); TavernScreen(led); break; case 9: PalaceScreen(led); break; } } finally { parch = false; }
    }
    var ml = R(content, "Msg", 0, 0, 1, 0); ml.pivot = new Vector2(.5f, 0); ml.offsetMin = new Vector2(16, 16); ml.offsetMax = new Vector2(-16, 112); Box(ml, T.panel, T.brass);
    Txt(Stretch(ml, "m", 22, 4, 22, 4), msg, T.fontSizeSmall - 2, T.parchment);
  }
  void PortScreen(RectTransform main) {
    float y = 0; string[] hint = { "Buy and sell goods", "Contraband buyers, bans and rumours", "Store goods between voyages", "Tithes and the Faker's Crate", "" };
    for (int i = 0; i < 4; i++) { int k = i; var b = Btn(main, "", 0, y, 1048, 150, () => { tab = k; Refresh(); });
      Label(b.transform, Tabs[i], 40, 18, 900, 60, T.fontSizeTitle, T.parchment, TextAnchor.MiddleLeft, true); Label(b.transform, hint[i], 40, 80, 900, 50, T.fontSizeSmall - 2, T.textMuted); y += 166; }
    var sb = Btn(main, "", 0, y + 10, 1048, 260, () => { tab = 4; Refresh(); }, true, true);
    Label(sb.transform, "SET SAIL", 0, 50, 1048, 110, T.fontSizeTitle + 30, T.gold, TextAnchor.MiddleCenter, true);
    Label(sb.transform, "One week at sea to the next port", 0, 160, 1048, 50, T.fontSizeSmall, T.textMuted, TextAnchor.MiddleCenter);
  }

  Dictionary<int, Text> priceTexts = new Dictionary<int, Text>();
  void LedgerHeader(RectTransform led, string title, params (string, float)[] cols) {
    Label(led, title, 26, 12, 900, 52, T.fontSizeTitle - 6, T.parchment, TextAnchor.MiddleLeft, true);
    var line = Px(led, "rule", 22, 66, 988, 2); Box(line, T.brass);
    foreach (var c in cols) Label(led, c.Item1, c.Item2, 72, 200, 36, T.fontSizeSmall - 4, T.textMuted);
  }
  float marketScroll = 1f; ScrollRect marketSR;
  Button LBtn(Transform p, string label, float x, float y, float w, float h, Action on, bool en) {
    var r = Px(p, "LBtn " + label, x, y, w, h); Box(r, en ? T.ledgerButton : T.ledgerRowAlt, en ? T.frame : T.ledgerRule);
    var b = r.gameObject.AddComponent<Button>(); b.interactable = en; b.onClick.AddListener(() => { if (!busy) on(); });
    Txt(R(r, "L", 0, 0, 1, 1), label, T.fontSize, en ? T.ledgerButtonText : T.ledgerMuted, TextAnchor.MiddleCenter, true); return b; }
  Image Arrow(Transform p, float x, float y, float d, float size = 22) {
    var ic = Px(p, "arrow", x, y, size, size).gameObject.AddComponent<Image>();
    if (Mathf.Abs(d) < 0.005f) { ic.rectTransform.sizeDelta = new Vector2(size, 4); ic.rectTransform.anchoredPosition += new Vector2(0, -size / 2 + 2); ic.color = UITheme.C(T.ledgerMuted); return ic; }
    ic.sprite = Tri(); ic.color = UITheme.C(d > 0 ? T.ledgerUp : T.ledgerDown); if (d < 0) ic.rectTransform.localScale = new Vector3(1, -1, 1); return ic; }
  void Market(RectTransform led) {
    Label(led, $"{S.ports[port].name} Market", 30, 14, 700, 76, T.fontSizeTitle + 10, T.ledgerInk, TextAnchor.MiddleLeft, true);
    Label(led, DateStr, 640, 30, 360, 44, T.fontSizeSmall - 2, T.ledgerMuted, TextAnchor.MiddleRight);
    Box(Px(led, "rule", 20, 96, 992, 3), T.ledgerRule);
    var vp = Stretch(led, "Viewport", 8, 104, 8, 8); vp.anchorMin = new Vector2(0, 1); vp.offsetMin = new Vector2(8, -1184); /* art_t6_fixes: list ends at y 1184, 16 px above the fixed Ask about work button (y 1200) */ vp.gameObject.AddComponent<RectMask2D>();
    var mg = MarketGoods(port); float rh = 150; var list = Px(vp, "Rows", 0, 0, 1032, rh * mg.Count);
    marketSR = led.gameObject.AddComponent<ScrollRect>(); marketSR.viewport = vp; marketSR.content = list; marketSR.horizontal = false; marketSR.movementType = ScrollRect.MovementType.Clamped;
    marketSR.scrollSensitivity = 40; marketSR.verticalNormalizedPosition = marketScroll; marketSR.onValueChanged.AddListener(v => marketScroll = v.y);
    priceTexts.Clear();
    for (int mi = 0; mi < mg.Count; mi++) { int g = mg[mi];
      int gg = g; float y = mi * rh; var row = Px(list, "row " + S.goods[g].id, 0, y, 1032, rh); { var hit = mi % 2 == 1 ? Box(row, T.ledgerRowAlt) : row.gameObject.AddComponent<Image>(); if (mi % 2 == 0) hit.color = Color.clear; var rb = row.gameObject.AddComponent<Button>(); rb.transition = Selectable.Transition.None; rb.onClick.AddListener(() => OpenSheet(gg)); }
      Box(Px(row, "rule", 12, rh - 2, 992, 2), T.ledgerRule);
      bool ban = Banned(g, port); var gd = S.goods[g];
      var icon = Px(row, "icon", 14, 18, 110, 110).gameObject.AddComponent<Image>(); icon.sprite = Icons.For(gd.id, UITheme.C(T.ledgerInk)); icon.preserveAspect = true;
      { var nl = Label(row, gd.name, 136, 22, 250, 60, T.fontSizeTitle, T.ledgerInk, TextAnchor.MiddleLeft, true); nl.horizontalOverflow = HorizontalWrapMode.Overflow; if (gd.name.Length > 8) nl.fontSize = T.fontSizeTitle - 8; }
      Label(row, (foreignPort == port && foreignGood == g ? "<color=" + T.ledgerUp + ">foreign ship</color>  " : EvMult(port, g) > 1.01f ? "<color=" + T.ledgerDown + ">in demand</color>  " : EvMult(port, g) < 0.99f ? "<color=" + T.ledgerUp + ">glut</color>  " : "") + $"held {hold[g]} · stock {StockOf(g)}", 136, 80, 280, 40, T.fontSizeSmall - 4, T.ledgerMuted);
      if (ban) {
        var st = Px(row, "N/A stamp", 470, 30, 300, 86); st.localRotation = Quaternion.Euler(0, 0, 6);
        var sb = st.gameObject.AddComponent<Image>(); sb.sprite = Panel(new Color(0, 0, 0, 0), UITheme.C(T.stamp)); sb.type = Image.Type.Sliced; sb.pixelsPerUnitMultiplier = 0.25f;
        Txt(R(st, "t", 0, 0, 1, 1), "N/A", T.fontSizeTitle + 12, T.stamp, TextAnchor.MiddleCenter, true);
      } else {
        float d = price[port, g] / lastPrice[port, g] - 1;
        Label(row, "BUY", 420, 20, 160, 30, T.fontSizeSmall - 6, T.ledgerMuted);
        Label(row, $"{Mathf.CeilToInt(price[port, g]):N0}", 420, 50, 140, 64, T.fontSizeTitle + 2, T.ledgerInk, TextAnchor.MiddleLeft, true);
        Arrow(row, 560, 72, d);
        Label(row, "SELL", 650, 20, 160, 30, T.fontSizeSmall - 6, T.ledgerMuted);
        priceTexts[g] = Label(row, $"{SellPrice(g):N0}", 650, 50, 140, 64, T.fontSizeTitle + 2, T.ledgerInk, TextAnchor.MiddleLeft, true);
        Arrow(row, 790, 72, d);
      }
      Label(row, "›", 960, 20, 60, 110, T.fontSizeTitle + 24, T.ledgerMuted, TextAnchor.MiddleCenter, true);
    }
    /* v0.6j: market guild jobs — button sits below the scroll list on the ledger */
    QuestBtn(led, "market", "Guild factor of " + S.ports[port].name, 1200);
  }
  void DoSell(int g, RectTransform from) {
    int before = Mathf.RoundToInt(price[port, g]); int v = SellPrice(g);
    if (!Sell(g)) return; msg = $"Sold {GoodCount(g, 1)} for {v}. Next one sells for {SellPrice(g)}.";
    Vector3 at = priceTexts.TryGetValue(g, out var pt) && pt ? pt.rectTransform.position : from.position;
    Refresh(); SaleFx(at);
  }
  IEnumerator SellAll(int g) {
    busy = true; while (hold[g] > 0 && !Banned(g, port) && tab == 0 && !sailing) { DoSell(g, content); yield return new WaitForSeconds(0.12f); }
    busy = false; Refresh();
  }
  void SaleFx(Vector3 at) {
    fx.SetAsLastSibling();
    // subtle red down-arrow beside the live price
    var a = new GameObject("arrow", typeof(RectTransform)).GetComponent<RectTransform>(); a.SetParent(fx, false); a.position = at; a.anchoredPosition += new Vector2(-34, -6);
    a.sizeDelta = new Vector2(16, 13); var ai = a.gameObject.AddComponent<Image>(); ai.sprite = Tri(); ai.color = UITheme.C(T.down); a.localScale = new Vector3(1, -1, 1);
    if (Application.isPlaying) StartCoroutine(Fade(ai, 0.9f, new Vector2(0, -10)));
    // short coin spill toward the purse
    for (int i = 0; i < 5; i++) {
      var c = new GameObject("coin", typeof(RectTransform)).GetComponent<RectTransform>(); c.SetParent(fx, false); c.position = at; c.sizeDelta = new Vector2(14, 14);
      var ci = c.gameObject.AddComponent<Image>(); ci.sprite = Dot(); ci.color = UITheme.C(T.gold);
      if (Application.isPlaying) StartCoroutine(Coin(c, ci, i * 0.04f)); }
  }
  IEnumerator Fade(Image i, float d, Vector2 drift) { var r = i.rectTransform; var p0 = r.anchoredPosition; for (float t = 0; t < d; t += Time.deltaTime) { i.color = new Color(i.color.r, i.color.g, i.color.b, 0.85f * (1 - t / d)); r.anchoredPosition = p0 + drift * (t / d); yield return null; } Destroy(i.gameObject); }
  IEnumerator Coin(RectTransform c, Image i, float delay) {
    yield return new WaitForSeconds(delay); Vector3 p0 = c.position, p1 = coinText ? coinText.rectTransform.position : p0; var off = new Vector3(Rnd.Range(-60f, 60f), Rnd.Range(40f, 120f), 0) * root.localScale.x;
    for (float t = 0; t < 0.55f; t += Time.deltaTime) { float k = t / 0.55f; c.position = Vector3.Lerp(p0, p1, k * k) + off * Mathf.Sin(k * Mathf.PI); yield return null; }
    Destroy(c.gameObject); }

  void Alley(RectTransform led) {
    LedgerHeader(led, "The Back Alley", ("GOOD", 30), ("PRICE", 300), ("HELD", 600));
    float y = 112; bool any = false;
    if (faridaState == 1 && port == Debal) { Label(led, $"Farida's ally prices −20%  ·  Faker's Crate {FakersPrice}", 26, y, 980, 40, T.fontSizeSmall, "#8A5A12"); y = 152; } /* v0.6h4 */
    if (FaridaOffer()) { Label(led, "Farida waves you over...", 26, 112, 980, 70, T.fontSize + 8, T.textMuted); return; }
    foreach (var id in AlleySells(port)) { int g = G(id); if (g < 0 || (g == Rugs && RugsHidden(port))) continue; any = true; int gg = g;
      int fullPr = Mathf.CeilToInt(price[port, g]); bool allyCut = faridaState == 1 && port == Debal; int pr = allyCut ? Mathf.CeilToInt(fullPr * (1f - A1("faridaAllyPapersDiscount", 0.2f))) : fullPr;
      var row = Px(led, "buyrow " + id, 14, y, 1004, 96); Box(row, T.rowShade);
      Label(row, S.goods[g].name, 16, 0, 280, 96, T.fontSize + 8, T.text);
      Label(row, allyCut ? $"{pr}  <size=26><color=#8A5A12>−20%</color></size>" : pr.ToString(), 286, 0, 260, 96, T.fontSize + 8, T.gold, TextAnchor.MiddleLeft, true);
      Label(row, hold[g].ToString(), 600, 0, 80, 96, T.fontSize + 8, T.text);
      Btn(row, "Buy 1", 700, 8, 140, 80, () => AlleyBuy(gg, 1), coins >= pr && HoldUsed < S.holdSize); Btn(row, "Buy 5", 850, 8, 140, 80, () => AlleyBuy(gg, 5), coins >= pr && HoldUsed < S.holdSize); y += 104; }
    for (int g = 0; g < S.goods.Length; g++) if (Banned(g, port)) { any = true; int gg = g;
      var row = Px(led, "row", 14, y, 1004, 96); Box(row, T.rowShade);
      Label(row, S.goods[g].name, 16, 0, 280, 96, T.fontSize + 8, T.text); priceTexts[g] = Label(row, SellPrice(g).ToString(), 286, 0, 200, 96, T.fontSize + 8, T.gold, TextAnchor.MiddleLeft, true);
      Label(row, hold[g].ToString(), 600, 0, 80, 96, T.fontSize + 8, T.text);
      Btn(row, "Sell 1", 700, 8, 140, 80, () => DoSellAlley(gg, row), hold[g] > 0); Btn(row, "Sell all", 850, 8, 140, 80, () => { int got = 0, v = 0; while (hold[gg] > 0) { int pv = SellPrice(gg); if (!Sell(gg)) break; got++; v += pv; } if (got > 0) { msg = $"A quiet buyer takes {GoodCount(gg, got)} for {v:N0} coins." /* gamedev_t9 */; var at = priceTexts[gg].rectTransform.position; Refresh(); SaleFx(at); } }, hold[g] > 0); y += 104; }
    if (!any) { Label(led, $"Nothing is banned in {S.ports[port].name} this week. The alley is quiet.", 26, y + 8, 980, 110, T.fontSize + 8, T.textMuted, TextAnchor.UpperLeft); y += 126; } /* k4art: top-anchored, 8px under headings — 2-line wrap no longer rides up into GOOD/PRICE/HELD */
    y += 20; Label(led, "Current bans", 26, y, 600, 60, T.fontSize + 8, T.parchment, TextAnchor.MiddleLeft, true); y += 66;
    foreach (var b in bans) { string when = b.end >= int.MaxValue / 4 ? "banned for good" : week < b.start ? "from next week" : $"until moon {b.end / S.weeksPerMoon + 1}";
      Label(led, $"·  {S.goods[b.good].name} banned in {S.ports[b.port].name}, {when}  (sells ×{S.contraband.bannedSellMult:0.#} there)", 26, y, 980, 60, T.fontSizeSmall + 6, T.text, TextAnchor.UpperLeft); y += 4 + Flow(led.GetChild(led.childCount - 1).GetComponentInChildren<Text>(), 60); }
    y += 16; Label(led, "Harbour talk", 26, y, 600, 60, T.fontSize + 8, T.parchment, TextAnchor.MiddleLeft, true); y += 66;
    foreach (var n in news.Take(4)) { y += Flow(Label(led, n, 26, y, 980, 64, T.fontSizeSmall + 4, T.textMuted, TextAnchor.UpperLeft), 96) + 8; }
    QuestBtn(led, "alley", port == Muscat ? MuscatFence : faridaState == 2 ? "Farida (rival)" : "Farida of the back alley", y + 20); /* research_gp3 */
  }
  void AlleyBuy(int g, int n) {
    bool allyCut = faridaState == 1 && port == Debal; float full = price[port, g];
    if (allyCut) price[port, g] = full * (1f - A1("faridaAllyPapersDiscount", 0.2f));
    int got = 0; for (int i = 0; i < n; i++) if (Buy(g)) got++;
    if (allyCut) { price[port, g] = full * Mathf.Pow(1 + S.perUnitBuyRise, got); if (got > 0) { if (playthrough) PLog2("papers", "PAPERS -20%"); Debug.Log("GB PAPERS -20%"); } }
    if (got == 0) return;
    if (InOpening && jackpotStartCoins < 0 && g == G("gunpowder")) { jackpotStartCoins = coins + got * Mathf.CeilToInt(allyCut ? full * (1f - A1("faridaAllyPapersDiscount", 0.2f)) : full); news.Insert(0, "Yusuf: \"Muscat's purse is deep, Captain. Just don't let customs see what's in yours.\""); }
    if (!papersGiven && g == G("gunpowder") && firstRun) { papersGiven = true; keeperTrig = "papers"; msg = $"Farida slips you forged papers with the {S.goods[g].name.ToLower()}: Muscat customs will wave you through.";  /* research_gp3: papers make CustomsWaved true (risk 0), so say so at buy */ }
    else msg = allyCut ? $"Bought {GoodCount(g, got)} in the alley (−20%)." : $"Bought {GoodCount(g, got)} in the alley."; Refresh(); }
  void DoSellAlley(int g, RectTransform row) { int v = SellPrice(g); if (!Sell(g)) return; if (port == Muscat && g == Gp) keeperTrig = "sold"; /* research_gp4: Salim thanks you */ msg = $"A quiet buyer pays {v} for {GoodCount(g, 1)}."; var at = priceTexts[g].rectTransform.position; Refresh(); SaleFx(at); }

  void Ware(RectTransform led) {
    var w = wh[port]; int slots = w.tier == 2 ? S.warehouse.buySlots : w.tier == 1 ? S.warehouse.leaseSlots : 0; float used = w.items.Sum(b => b.qty);
    LedgerHeader(led, $"Warehouse · {S.ports[port].name}");
    Label(led, w.tier == 0 ? "You hold no warehouse here." : w.tier == 1 ? $"Leased · {S.warehouse.leasePerWeek} coins a week · {Mathf.FloorToInt(used)}/{slots} slots" : $"Owned · {Mathf.FloorToInt(used)}/{slots} slots", 26, 80, 980, 64, T.fontSize + 8, T.text);
    if (w.tier == 0) Btn(led, $"Lease ({S.warehouse.leasePerWeek}/wk, {S.warehouse.leaseSlots} slots)", 26, 156, 480, 110, () => { w.tier = 1; msg = "Lease signed. Rent is due each week you sail."; Refresh(); });
    if (w.tier < 2) Btn(led, $"Buy ({S.warehouse.buyPrice:N0}, {S.warehouse.buySlots} slots)", 526, 156, 480, 110, () => { coins -= S.warehouse.buyPrice; w.tier = 2; msg = "The warehouse deed is yours."; Refresh(); }, coins >= S.warehouse.buyPrice);
    Label(led, $"Goods stored {S.warehouse.decayAfterMoons}+ moons lose {S.warehouse.decayPerMoon * 100:0}% of their value each moon.", 26, 280, 980, 60, T.fontSizeSmall + 4, T.textMuted);
    float y = 360; Label(led, "IN HOLD", 30, y, 300, 48, T.fontSizeSmall + 2, T.textMuted); y += 56;
    for (int g = 0; g < S.goods.Length; g++) if (hold[g] > 0) { int gg = g;
      Label(led, $"{S.goods[g].name} ×{hold[g]}", 30, y, 420, 96, T.fontSize + 8, T.text);
      Btn(led, "Store 1", 580, y + 6, 200, 84, () => { Store(gg, 1); Refresh(); }, w.tier > 0 && used < slots);
      Btn(led, "Store all", 800, y + 6, 200, 84, () => { Store(gg, hold[gg]); Refresh(); }, w.tier > 0 && used < slots); y += 104; }
    y += 10; Label(led, "IN STORE", 30, y, 300, 48, T.fontSizeSmall + 2, T.textMuted); y += 56;
    foreach (var b in w.items.ToList()) { var bb = b; int age = (week - b.storedWeek) / S.weeksPerMoon;
      Label(led, $"{S.goods[b.good].name} ×{Mathf.FloorToInt(b.qty)}  <size=28><color={T.textMuted}>stored {age} moon(s)</color></size>", 30, y, 600, 96, T.fontSize + 8, T.text);
      Btn(led, "Take all", 800, y + 6, 200, 84, () => { int q = Mathf.Min(Mathf.FloorToInt(bb.qty), S.holdSize - HoldUsed); hold[bb.good] += q; bb.qty -= q; if (bb.qty < 1) w.items.Remove(bb); Refresh(); }, w.tier > 0 && HoldUsed < S.holdSize); y += 104; }
  }
  void Store(int g, int q) { var w = wh[port]; int slots = w.tier == 2 ? S.warehouse.buySlots : S.warehouse.leaseSlots; q = Mathf.Min(q, slots - Mathf.CeilToInt(w.items.Sum(b => b.qty))); if (q <= 0) return;
    hold[g] -= q; w.items.Add(new Batch { good = g, qty = q, storedWeek = week }); msg = $"Stored {GoodCount(g, q)}."; }

  void Temple(RectTransform led) {
    LedgerHeader(led, "Temple & Chandler");
    Label(led, $"Customs base risk {S.customs.baseRisk:0}%. Faker's Crate −{S.customs.fakersCrate:0}, temple tithe −{S.customs.templeTithe:0}, never below {S.customs.floor:0}%.", 26, 84, 980, 150, T.fontSize + 8, T.text);
    Label(led, $"Your risk if inspected now: {Risk(port):0}%", 26, 250, 980, 80, T.fontSizeTitle + 4, T.gold, TextAnchor.MiddleLeft, true);
    if (!TitheHidden) Btn(led, tithe ? "Tithe paid for next landing" : $"Pay temple tithe ({S.customs.titheCost})", 26, 360, 980, 130, () => { coins -= S.customs.titheCost; tithe = true; msg = "The priest blesses your cargo. Customs eyes will wander at your next landing."; Refresh(); }, !tithe && coins >= S.customs.titheCost);
    { int fp = FakersPrice; bool allyDisc = faridaState == 1 && !crate; string crateLbl = crate ? "Faker's Crate aboard" : allyDisc ? $"Buy the Faker's Crate ({fp})  −20%" : $"Buy the Faker's Crate ({fp})";
      Btn(led, crateLbl, 26, 520, 980, 130, () => { int paid = FakersPrice; coins -= paid; crate = true; if (faridaState == 1) { msg = "A crate with a false bottom. Farida's papers: −20%."; if (playthrough) PLog2("papers", "PAPERS -20%"); else Debug.Log("GB PAPERS -20%"); } else msg = "A crate with a false bottom. It will serve every voyage."; Refresh(); }, !crate && coins >= S.customs.fakersCratePrice); }
    Label(led, $"If caught: contraband seized, a fine of {S.customs.finePct * 100:0}% of its value, and −{S.customs.repLoss} standing in that port.", 26, 690, 980, 140, T.fontSizeSmall + 6, T.textMuted);
    TryCompleteJobsOnEnterBuilding("temple"); QuestBtn(led, "temple", "Priest of " + S.ports[port].name, 850);
  }

  void SailList(RectTransform led) {
    LedgerHeader(led, "Set Sail · one week at sea");
    float y = 90;
    for (int p = 0; p < S.ports.Length; p++) if (p != port) { int pp = p;
      var row = Px(led, "dest", 14, y, 1004, 200); Box(row, T.rowShade, T.brass);
      Label(row, S.ports[p].name, 22, 10, 600, 52, T.fontSizeTitle - 6, T.parchment, TextAnchor.MiddleLeft, true);
      var bannedHere = Enumerable.Range(0, S.goods.Length).Where(g => Banned(g, p) || bans.Any(b => b.good == g && b.port == p && b.start == week + 1)).Select(g => S.goods[g].name).ToList();
      Label(row, bannedHere.Count > 0 ? "Banned on arrival: " + string.Join(", ", bannedHere) : "No bans reported.", 22, 66, 700, 40, T.fontSizeSmall, T.textMuted);
      bool risky = Enumerable.Range(0, S.goods.Length).Any(g => hold[g] > 0 && (Banned(g, p) || bans.Any(b => b.good == g && b.port == p && b.start <= week + 1 && b.end > week + 1)));
      Label(row, risky ? $"Customs risk on docking: {Risk(p):0}%" : "Your hold is clean for this port.", 22, 110, 700, 44, T.fontSize, risky ? T.down : T.up);
      Btn(row, "Sail", 780, 60, 200, 80, () => StartCoroutine(Voyage(pp)), true, true); y += 216; }
  }

  // ---------------- voyage ----------------
  bool sailing, dockedSave; int dest; RectTransform dialog;
  IEnumerator Voyage(int to) {
    if (sailing) yield break; if (!CanLeavePort(out var hullWhy)) { msg = hullWhy; sailing = false; Refresh(); yield break; } if (town3D) town3D.gameObject.SetActive(false); RenderSettings.ambientLight = new Color(0.40f, 0.46f, 0.58f); RenderSettings.fogColor = UITheme.C(T.skyHorizon); RenderSettings.fogEndDistance = 210; SaveVoyageBackup(); sailing = true; dest = to; sails++; backdrop.color = Color.clear; sea3D.gameObject.SetActive(true); BuildLandmarksV06k(); HideAllSeaLandmarks(); EnsureKenneyDhowOnShip(); Refresh(); /* v0.6k3: no landmarks mid-ocean */
    float dur = Rnd.Range(S.sailSecondsMin, S.sailSecondsMax) / ((S.debugFast || Array.IndexOf(Environment.GetCommandLineArgs(), "-fast") >= 0) ? S.debugFastMultiplier : 1) / Mathf.Max(0.35f, LimpSailMult);
    bool free = sails <= S.pirateFreeSails || sails < safeUntilSail; int firstAmb = OpenInt("firstAmbushOnSailing", 3); bool storyFog = InOpening && !storyAmbushDone && to == Muscat && hold[G("gunpowder")] > 0 && sails + 1 >= firstAmb; /* fog on voyage 3, after two calm voyages */
    float ap = free ? 0 : AmbushChanceFor(to); bool fAmb = gbForce.Contains("ambush"), fSq = gbForce.Contains("squall") && !storyFog; bool fMer = gbForce.Contains("merchant") && !storyFog; if (fMer) fAmb = false; bool ambush = storyFog || fAmb || (!fSq && !fMer && Rnd.value < ap); bool encountered = ambush || fSq || fMer || Rnd.value < S.encounterChance; /* -gbforce ambush|squall|customs_caught for tests */ RectTransform fill = null;
    if (storyFog && sails < firstAmb) { /* Debal->Muscat in the opening is two legs: a calm one to Ras al Hadd, then the fog. The fog lands on real sailing 3 */
      var f0 = VoyageBar(to); for (float t = 0; t < dur * 0.5f && !skipVoyage; t += Time.deltaTime * (ff ? 2f : 1f)) { f0.sizeDelta = new Vector2(1000 * Mathf.Clamp01(t / (dur * 0.5f)), 12); yield return null; }
      if (playthrough) PLog2("calm leg", $"sailing {sails} ends calm at Ras al Hadd"); yield return Ask("Ras al Hadd", "A calm passage. You put in at Ras al Hadd for water and set out again before dawn, bound for Muscat.", ("Sail on", null)); sails++; skipVoyage = false; }
    lastVoyageAmbushChance = ap; ambushForced = storyFog; foreignPort = -1;
    ff = false;
    skipVoyage = false;
    /* v0.6i: 1–2 short decision events per voyage (horizon sail / storm front / wreckage), plus a passer on the beam */
    arrivalTapEarly = false; /* gamedev_t7 */ bool arrivalShown = false; int decisionsLeft = Rnd.Range(1, 3); float nextDec = dur * Rnd.Range(0.18f, 0.32f); bool passerOut = false; horizonDone = false; /* gamedev_t10_lateen */ voyageKindsUsed = 0; /* gamedev_t13_nodupe */
    for (float t = 0; t < dur; t += Time.deltaTime * (ff ? 2f : 1f)) { if (skipVoyage) t = dur;
      if (fill == null) fill = VoyageBar(to);
      fill.sizeDelta = new Vector2(1000 * Mathf.Clamp01(t / dur), 12);
      /* gamedev_t11_passer: no free-running passer spawn here any more; it only appears with the Sail on the horizon card (VoyageDecision kind 0) */
      if (decisionsLeft > 0 && t >= nextDec && dialog == null && !skipVoyage) { decisionsLeft--; nextDec = t + dur * Rnd.Range(0.28f, 0.4f); yield return VoyageDecision(); fill = null; backdrop.color = Color.clear; }
      if (encountered && t > dur * 0.45f) { encountered = false; PasserHide("ambush"); /* gamedev_t11_passer */ yield return Encounter(ambush, storyFog); fill = null; backdrop.color = Color.clear; }
      if (!arrivalShown && t > dur * 0.8f && dialog == null && !fighting) { arrivalShown = true; PasserHide("arrival"); /* gamedev_t11_passer */ ShowSeaLandmark(to); Debug.Log("GB ARRIVAL landmark " + S.ports[to].name); } // k4merge: arrival landmark ShowSeaLandmark(portIndex) /* k4merge: arrival landmark, last 20% only */
      if (arrivalShown && dialog == null && !fighting && FreshTap()) { if (arrivalLmUpT < 0) arrivalLmUpT = Time.time; arrivalTapEarly = true; } /* gamedev_t7: tap on the in-voyage arrival view = skip the hold */
      yield return null; }
    PasserHide("voyage_end"); /* gamedev_t11_passer */
    if (!arrivalShown && !fighting) { arrivalShown = true; if (sea3D) sea3D.gameObject.SetActive(true); ShowSeaLandmark(to); Debug.Log("GB ARRIVAL landmark " + S.ports[to].name + " (late: card/fight covered the window)"); } // k4merge: arrival landmark ShowSeaLandmark(portIndex)
    if (arrivalShown && !fighting) yield return ArrivalHold(S.ports[to].name, week); /* gamedev_t7: shared hold (voyage + intro) */
    NewWeek(); port = to; adsThisVisit = 0; bool atCustoms = CarriesBanned(port); /* ambush_customs_t6 */ customsPending = atCustoms ? port : -1; dockedSave = true; Save(); /* gamedev_customs_save: the port change is on disk with this leg's costs before the customs card, so a quit there Continues at the arrival port (customs replayed), never back at the departure port with the toll spent */
    if (atCustoms) { PasserHide("customs"); /* gamedev_t11_passer */ if (sea3D) sea3D.gameObject.SetActive(true); ShowSeaLandmark(port); backdrop.color = Color.clear; if (pirateShip) pirateShip.gameObject.SetActive(false); /* gamedev_t7 */ string cn = S.ports[port].name; arrivalTarget = (seaMarks.TryGetValue(cn, out var clm) && clm ? clm.transform.position : new Vector3(0, 0, 80f)) + Vector3.up * CustomsLookUp; customsFrame = true; /* gamedev_t7 */ yield return Customs(); customsPending = -1; /* gamedev_customs_save */ customsFrame = false; /* gamedev_t7 */ } /* ambush_customs_t6: customs card over the port arrival view (+ shared ModalDim from Ask), not blank sand */
    ShowSeaLandmark(-1); sea3D.gameObject.SetActive(false); /* ambush_customs_t6 */
    if (!atCustoms) /* ambush_customs_t6 */ msg = $"Docked at {S.ports[port].name}. " + (news.Count > 0 ? news[0] : ""); OnDockV06(); TryCompleteJobsOnDock(); EgOnDock(); /* gamedev_end1 */ FallbackCheck(); dockedSave = true; Save(); /* gamedev_continue: the arrival is on disk before any arrival card, so a quit on the gift/contract cards still Continues at this port */ if (blockPending == port) yield return BlockadeCard(); /* gamedev_blockade_warning: replaces the seizure card; saved before the card, replayed on Continue */ if (giftPending > 0) { giftPending = 0; yield return Ask("A gift from Yusuf", "\"Take it, Captain. A man with an empty purse makes poor trades. Pay me back in stories, not silver.\"", ("Thank you, Yusuf", null)); }
    tithe = false; sailing = false; dockedSave = false; crewInjured = false; tab = -1; Refresh(); Save();
  }
  bool ff, skipVoyage; Text ffTxt; bool horizonDone; /* gamedev_t10_lateen */
  /* gamedev_t11_passer: the lateen is visible ONLY during the Sail on the horizon moment */
  void PasserShow(string reason) { if (!passShip) return; passShip.position = new Vector3(-40f, 0.2f, 18f); passShip.rotation = Quaternion.Euler(0, 110, 0); passShip.gameObject.SetActive(true); Debug.Log("GB PASSER show reason=" + reason); }
  void PasserHide(string reason) { if (!passShip || !passShip.gameObject.activeSelf) return; passShip.gameObject.SetActive(false); Debug.Log("GB PASSER hide reason=" + reason); }
  RectTransform VoyageBar(int to) {
    var bar = Px(Bottom1920(content, "vb"), "voyage", 16, 1740, 1048, 160); Box(bar, T.panel, T.brass);
    Label(bar, $"Sailing to {S.ports[to].name}…", 24, 14, 740, 60, T.fontSize, T.parchment);
    var fb = Btn(bar, "", 790, 12, 238, 84, () => { ff = !ff; UpdateFF(); }, true, true); ffTxt = fb.GetComponentInChildren<Text>(); UpdateFF();
    if (AdsUnlocked) Btn(bar, "Skip ▶ ad", 540, 12, 238, 84, () => StartCoroutine(ShowAd("skipVoyage", () => skipVoyage = true)), AdBlockReason() == null);
    var trackBg = Px(bar, "track", 24, 120, 1000, 12); Box(trackBg, T.ink, T.brass); var fill = Px(trackBg, "fill", 0, 0, 0, 12); Box(fill, T.gold); return fill; }
  void UpdateFF() { if (ffTxt) { ffTxt.text = ff ? "▶▶ 2× on" : "▶▶ 2×"; ffTxt.color = UITheme.C(ff ? T.gold : T.buttonText); } }
  /* final modal_queue_back: Ask() = FORCED (Back ignored). AskD() = DISMISSABLE (Back closes it through its own done path, finally cleanup runs). */
  IEnumerator Ask(string title, string body, params (string, Action)[] opts) => AskCore(false, title, body, opts);
  IEnumerator AskD(string title, string body, params (string, Action)[] opts) => AskCore(true, title, body, opts);
  public float askGuardSec; /* gamedev_board: set before a boarding card - its buttons ignore taps for this long after it opens */
  IEnumerator AskCore(bool dismissable, string title, string body, (string, Action)[] opts) { float askGuard = askGuardSec, askOpenT = Time.unscaledTime; askGuardSec = 0;
    if (title != "Sail on the horizon") PasserHide("card:" + title); /* gamedev_t11_passer: any other Ask card hides the lateen */
    while (CardBusy) yield return null; /* final modal_queue_back: queue - wait for any open card (Ask/AskCard/Credits/QuestMenu); checked before this card exists, so it never waits on itself */
    /* v0.6k3: panel height fits title+body+buttons (no half-empty storm/voyage card) */
    float bodyH = CardBodyH(body, 640f); /* art_t10_fixes: body x 130–770 */ /* art_t5_fixes: measured at the x 100–800 body width */ /* final3: measured at the inset width (780 = 840 − 2×30) */
    var tf = CardTitleFit(title, 640, T.fontSizeTitle); /* gamedev_title1 */
    float y = 132 + tf.extra + bodyH + 20; /* gamedev_title1: + tf.extra */ /* art_t10_fixes: title/rule moved down, body starts at 132 */ /* final: body starts below the corner brackets (~100 px); 20 px body→buttons */
    bool wide = opts.Length >= 4; /* art_t4_fixes: 4-button rows (Options) clear the bottom + side brackets */
    bool askSub = opts.Any(o => o.Item1.StartsWith(AskOff) && o.Item1.Contains("\n")); /* blockade_polish: greyed option reason sits on a small line under its button */
    float panelH = y + 84 + 110 + (askSub ? 44 : 0); /* art_t11_fixes: 4+ button rows (Options) also 110 px above the card bottom, clear of the bottom brackets */ /* art_t8_brackets: 1-, 2- and 3-button cards (Sail on the horizon, Storm front, Bait/toll result, difficulty) all clear the bottom brackets; 4+ (Options) unchanged */ /* art_t6_fixes: 3-button cards (difficulty) clear the bottom brackets; 1–2 and 4+ button cards unchanged */ /* tight to buttons — no half-empty card */
    float top = Mathf.Max(180f, (1920f - panelH) * 0.38f);
    var myDim = ModalDim("AskDim"); modalDepth++; /* final ask_modal: own full-screen dim on root, above Content (whole HUD: SET SAIL, Journal, Options, top bar) and just below this card */
    var my = Px(root, "Dialog", 90, top, 900, panelH); dialog = my; Box(my, T.panel, T.gold); bool done = false; var be = PushBack(title, () => my != null, dismissable ? (Action)(() => done = true) : null); /* k4r2: each Ask closes its OWN panel (k3.1 fix restored) */
    CardTitle(my, title, 130, 640, T.fontSizeTitle, T.parchment, TextAnchor.MiddleLeft, tf); /* gamedev_title1: was Label 130,40,640,52 + rule 130,100 (unchanged for one-line titles) */ /* art_t10_fixes: off the top rail, between the corner brackets */ /* art_t5_fixes: clear of the top-left bracket */ /* art_t10_fixes: rule under title, between brackets */ /* final2: title starts 100 px in, clear of the ~95 px corner brackets */
    Label(my, body, 130, 132 + tf.extra /* gamedev_title1 */, 640, bodyH, /* art_t10_fixes */ /* art_t5_fixes: x 100–800, clear of both side bracket arms */ T.fontSize, T.text, TextAnchor.UpperLeft); /* final3: 30 px inset each side (x 60–840), clear of the side bracket arms */
    y = 132 + tf.extra + bodyH + 20; /* gamedev_title1 */ float bx = wide ? 120f : 100f /* art_t5_fixes */, w = ((wide ? 660f : 700f) - (opts.Length - 1) * 16) / Mathf.Max(1, opts.Length); /* art_t4_fixes */ /* final2: button row inset to x 95–805 so the end buttons clear the bottom corner brackets */
    Debug.Log("GB CARD show " + title); /* gamedev_t9 */
    askOpenT = Time.unscaledTime; /* gamedev_board: guard starts when the card is on screen */
    for (int i = 0; i < opts.Length; i++) { var o = opts[i]; bool oen = !o.Item1.StartsWith(AskOff); string olb = oen ? o.Item1 : o.Item1.Substring(AskOff.Length); /* gamedev_blockade_warning: "~off~" prefix = greyed option */ string osub = null; if (!oen && olb.Contains("\n")) { int nl = olb.IndexOf('\n'); osub = olb.Substring(nl + 1); olb = olb.Substring(0, nl); var st = Label(my, osub, bx + i * (w + 16), y + 88, w, 40, T.fontSizeSmall - 6, T.textMuted, TextAnchor.UpperCenter); st.gameObject.name = "OptionReason"; } /* blockade_polish */ var ob = Btn(my, olb, bx + i * (w + 16), y, w, 84, () => { if (Time.unscaledTime - askOpenT < askGuard) { Debug.Log("GB CARD tap ignored (card just opened) " + title); return; } Debug.Log("GB CARD choose " + title + " " + olb); /* gamedev_t9 */ o.Item2?.Invoke(); done = true; }, oen, i == 0 && oen); FitOptionLabel(ob); if (!oen) GreyOption(ob); /* blockade_polish: greyed option label readable above flat art */ }
    try { while (!done) yield return null; }
    finally { backStack.Remove(be); if (my) Destroy0(my.gameObject); if (myDim) Destroy0(myDim.gameObject); if (dialog == my) dialog = null; modalDepth = Mathf.Max(0, modalDepth - 1); } /* final ask_modal: card + its own dim go together on every close path (incl. coroutine stopped) */
  }
  /// blockade_polish (Game Tester): a greyed card option drew the 9-slice teak frame (~110 px borders) squeezed into an 84 px button, which smeared over its label.
  /// Greyed options get a flat dim panel instead, and the label is re-parented last so it always renders above the button art, in readable muted ink.
  public const string GreyLabelHex = "#D9C08A", GreyPlateHex = "#6B5A40", GreyOutlineHex = "#6B5A40"; public const float GreyPlateAlpha = 1f, GreyLabelAlpha = 0.6f; /* Chief Art Designer r5: dim plate #6B5A40, no outline, parchment label at 60% */ /* Chief Art Designer r4: parchment plate 85% with a 1 px teak outline, teak label 75% on top, no grey overlay */
  public const float OptionLabelPad = 14f; /* gamedev_final_strings: card option labels shrink to fit inside the plate with padding */
  void FitOptionLabel(Button b) { var lt = b.transform.Find("L") as RectTransform; var tx = lt ? lt.GetComponent<Text>() : null; if (!tx) return; lt.offsetMin = new Vector2(OptionLabelPad, 4); lt.offsetMax = new Vector2(-OptionLabelPad, -4);
    tx.horizontalOverflow = HorizontalWrapMode.Overflow; tx.resizeTextForBestFit = false; float avail = ((RectTransform)b.transform).rect.width - 2 * OptionLabelPad; /* one line, shrunk until it fits (best fit would wrap it into two tiny lines) */
    while (tx.preferredWidth > avail && tx.fontSize > 14) tx.fontSize--; }
  void GreyOption(Button b) { var im = b.GetComponent<Image>(); if (im) { im.sprite = Panel(UITheme.C(GreyPlateHex), UITheme.C(GreyOutlineHex)); im.type = Image.Type.Sliced; im.pixelsPerUnitMultiplier = Mathf.Max(1, T.borderPx) / 2f; /* outline = 2 canvas units = 1 px at 540 wide */ im.color = new Color(1, 1, 1, GreyPlateAlpha); }
    { var cb = b.colors; cb.disabledColor = Color.white; b.colors = cb; } if (im) im.canvasRenderer.SetColor(Color.white); /* gamedev_final_strings: the disabled tint was already cross-faded onto the plate before the transition was switched off (Game Tester: #63553F) */
    b.transition = Selectable.Transition.None; /* no disabled colour-tint overlay */
    var lt = b.transform.Find("L"); if (lt) { lt.SetAsLastSibling(); var tx = lt.GetComponent<Text>(); if (tx) { var lc = UITheme.C(GreyLabelHex); lc.a = GreyLabelAlpha; tx.color = lc; tx.raycastTarget = false; } } b.gameObject.name = "Btn " + (lt ? lt.GetComponent<Text>()?.text : "") ; }
  int modalDepth; /* final ask_modal: >0 while any Ask/AskCard card is up; HUD taps are ignored */
  bool ModalUp => modalDepth > 0;
  /* final modal_queue_back: one card at a time - Ask/AskCard queue behind any open card (Journal excluded: it is a screen, not a card) */
  bool CardBusy => modalDepth > 0 || creditsCard != null || (dialog != null && dialog.name == "QuestMenu");
  /* final modal_queue_back: one dim colour for Ask, AskCard and Credits (= creditsDim's original 0.05,0.03,0.02,0.6) */
  static readonly Color ModalDimColor = new Color(0.05f, 0.03f, 0.02f, 0.6f);
  RectTransform ModalDim(string n) { var d = R(root, n, 0, 0, 1, 1); Vector2 a0 = root.anchorMin, sz = root.anchorMax - root.anchorMin; if (sz.x > 0.01f && sz.y > 0.01f) { d.anchorMin = new Vector2(-a0.x / sz.x, -a0.y / sz.y); d.anchorMax = new Vector2((1f - a0.x) / sz.x, (1f - a0.y) / sz.y); d.offsetMin = d.offsetMax = Vector2.zero; } /* modal_dim_fullscreen: stays under root (card order unchanged) but anchors stretched past the -gbsafe / safe-area inset to the full screen */ var im = d.gameObject.AddComponent<Image>(); im.color = ModalDimColor; im.raycastTarget = true; return d; }
  /* final modal_queue_back: Back (Android) / Escape registry. close == null => FORCED (Back ignored). */
  class BackEntry { public string name; public Func<bool> alive; public Action close; }
  readonly List<BackEntry> backStack = new List<BackEntry>(); int lastBackFrame = -1;
  BackEntry PushBack(string n, Func<bool> alive, Action close) { var e = new BackEntry { name = n, alive = alive, close = close }; backStack.Add(e); return e; }
  void HandleBack() {
    if (lastBackFrame == Time.frameCount) return; lastBackFrame = Time.frameCount; /* once per frame */
    backStack.RemoveAll(e => e.alive == null || !e.alive());
    if (backStack.Count == 0 && !onTitle && !fighting && !sailing && dialog == null && tab >= 0) { /* gamedev_back1: no card open -> Back closes the open town tab (shipwright, market, etc.) or the sea map (tab 4), same as its "‹  Town" button */
      if (tab == 4 && chartDest >= 0 && chartDest != port) { Debug.Log("Back pressed, clearing route"); chartDest = -1; Refresh(); return; } /* gamedev_back2: first Back on the sea map clears only a selected route */
      Debug.Log("Back pressed, closing " + (tab == 4 ? "sea map" : "tab " + tab)); /* gamedev_back1 */
      if (tab == 4) { chartDest = -1; chartCentered = false; } tab = -1; Refresh(); return; } /* gamedev_back1 */
    if (backStack.Count == 0) { Debug.Log("Back pressed, ignored (" + (onTitle ? "title screen" : fighting ? "combat" : (sailing || tab == 4) ? "sea map" : "town, nothing open" /* back_market_t6: chart (tab 4) logs as sea map; behaviour unchanged */) + ")"); return; }
    var top = backStack[backStack.Count - 1];
    if (top.close == null) { Debug.Log("Back pressed, ignored (" + top.name + ", forced)"); return; }
    Debug.Log("Back pressed, closing " + top.name); top.close();
  }
  bool customsFrame; const float CustomsLookUp = 12f; /* gamedev_t7: aim above the landmark so it sits below the centred card */ float arrivalPanT0 = -1f; Vector3 arrivalTarget; const float ArrivalPanDur = 3.0f; /* gwadar_arrival_hold: every port holds ~3 s after the voyage, independent of the in-voyage window (was 1.0 s → late-path ports e.g. Gwadar showed <0.7 s) */ /* k4merge2: arrival pan */
  public float lastVoyageAmbushChance;
  /// v0.6i soft mid-voyage choices (not ambushes): sail on horizon, storm front, wreckage
  int voyageKindsUsed; /* gamedev_t13_nodupe: bitmask of decision kinds already drawn this voyage */
  IEnumerator VoyageDecision() {
    var freeKinds = Enumerable.Range(0, 3).Where(k => (voyageKindsUsed & (1 << k)) == 0).ToList(); /* gamedev_t13_nodupe */
    if (freeKinds.Count == 0) { Debug.Log("GB VOYAGE decision kind=none (all used)"); yield break; }
    int kind = freeKinds[Rnd.Range(0, freeKinds.Count)]; if (gbForce.Contains("storm") && freeKinds.Contains(1)) kind = 1; /* gamedev_t16_stormcard */ /* -gbforce storm (test only) */ voyageKindsUsed |= 1 << kind;
    Debug.Log("GB VOYAGE decision kind=" + kind + " (" + (kind == 0 ? "horizon" : kind == 1 ? "storm" : "wreckage") + ")"); /* gamedev_t13_nodupe */
    if (kind == 0) {
      bool close = false; PasserShow("horizon"); /* gamedev_t11_passer */
      yield return Ask("Sail on the horizon", "A lateen shows against the haze, holding a parallel course. She flies no colours you know.",
        ("Hail her", () => { close = true; msg = "They wave a friendly pennant and sheer off."; if (BlkHailReveal()) close = false; /* gamedev_blockade_warning: Hail warning outcome */ }),
        ("Hold your course", () => msg = "You keep the wind and leave her astern."));
      horizonDone = true; PasserHide("horizon_closed"); /* gamedev_t11_passer */ /* gamedev_t10_lateen: the lateen leaves with the card (Hail or Hold), not behind the next card */
      if (close && Rnd.value < 0.35f) { int g = Rnd.Range(0, S.goods.Length); if (!S.goods[g].contraband) { int q = Mathf.Min(2, S.holdSize - HoldUsed); if (q > 0) { hold[g] += q; msg = $"They toss you {GoodCount(g, q)} for goodwill."; } } }
    } else if (kind == 1) {
      bool reef = false; int stLost = 0, stG = 0; /* gamedev_t16_stormcard */
      yield return Ask("Storm front", "A black wall of rain marches across the Gulf. The mate waits on your word.",
        ("Reef sails", () => { reef = true; msg = "You take in canvas and ride it out."; }),
        ("Press on", () => { if (HoldUsed > 0 && Rnd.value < 0.4f) { var held = LosableGoods(); /* gamedev_t15_storysafe */ int g = held.Count > 0 ? held[Rnd.Range(0, held.Count)] : 0; int lost = held.Count > 0 ? Mathf.Min(hold[g], 1) : 0; hold[g] -= lost; stLost = lost; stG = g; /* gamedev_t16_stormcard */ msg = lost > 0 ? $"A wave takes {GoodCount(g, lost)}." /* gamedev_t9 */ : "You punch through the squall."; } else msg = "You punch through the squall."; }));
      if (reef) { /* costs a little time but Voyage bar already handles FF */ }
      yield return Ask("Storm front", stLost > 0 ? $"Lost: {GoodCount(stG, stLost)}." : "You ride it out.", ("Continue", null)); /* gamedev_t16_stormcard */
    } else {
      int g = Rnd.Range(0, S.goods.Length), q = Mathf.Min(Rnd.Range(1, 4), S.holdSize - HoldUsed);
      yield return Ask("Wreckage", q > 0 ? $"Casks and splintered planks drift on the swell. You could fish up {GoodCount(g, q)}." : "Casks drift by, but your hold is full.",
        ("Salvage", () => { if (q > 0) { hold[g] += q; msg = $"Salvaged {GoodCount(g, q)}."; } else msg = "Nothing to take."; }),
        ("Sail on", () => msg = "You leave the wreckage to the gulls."));
    }
    Refresh();
  }
  IEnumerator Encounter(bool pirate, bool forcedFog) {
    var wgt = S.weights; float tot = wgt.pirate + wgt.wreck + wgt.merchant + wgt.squall + wgt.rumor, r = Rnd.value * tot;
    if (!pirate && gbForce.Contains("merchant")) { r = wgt.pirate + wgt.wreck + wgt.merchant * 0.5f; Debug.Log("GB FORCE merchant: event forced"); } else if (!pirate) r = gbForce.Contains("squall") ? wgt.pirate + wgt.wreck + wgt.merchant + wgt.squall * 0.5f : wgt.pirate + Rnd.value * (tot - wgt.pirate); else r = 0; /* the ambush roll (AmbushChanceFor) decides pirates; other events share the rest */
    int any = Enumerable.Range(0, hold.Length).FirstOrDefault(g => hold[g] > 0);
    if ((r -= wgt.pirate) < 0) {
      yield return AmbushWithResult(forcedFog ? "fog" : PickAmbushType()); /* designer_ambush_customs */
    } else if ((r -= wgt.wreck) < 0) {
      int g = Rnd.Range(0, S.goods.Length), q = Mathf.Min(Rnd.Range(2, 6), S.holdSize - HoldUsed);
      yield return Ask("Wreckage adrift", q > 0 ? $"Splintered timbers and {q} sealed bales of {S.goods[g].name}." : "Splintered timbers, but your hold is full.",
        ("Haul it aboard", () => { hold[g] += q; msg = q > 0 ? $"Salvaged {GoodCount(g, q)}." : "Nothing to take."; }));
    } else if ((r -= wgt.merchant) < 0) {
      int g = Rnd.Range(0, S.goods.Length); if (S.goods[g].contraband) g = 0; int unit = Mathf.Max(1, Mathf.RoundToInt(basePrice[dest, g] * 0.7f)), q = Mathf.Min(5, S.holdSize - HoldUsed);
      q = Mathf.Min(q, Mathf.FloorToInt(coins * seaMerchantMaxFrac / unit)); /* offer capped at design frac of coins */
      Debug.Log($"GB MERCHANT coins {coins} unit {unit} cap {seaMerchantMaxFrac:0.##} -> q {q} cost {unit * Mathf.Max(q,0)} " + (q >= 1 ? "OFFER SHOWN" : "OFFER HIDDEN (smallest lot unaffordable)"));
      if (q >= 1) yield return Ask("A merchant hails you", $"A fat baghlah offers {GoodCount(g, q)} at {unit} each, cheaper than any quay.",
        ($"Buy ({unit * q})", () => { if (coins >= unit * q) { coins -= unit * q; hold[g] += q; msg = $"Bought {GoodCount(g, q)} at sea."; } else msg = "You can't afford it."; }), ("Decline", () => msg = "You wave the merchant on."));
    } else if ((r -= wgt.squall) < 0) {
      int lost = 0; if (HoldUsed > 0 && !(InOpening && OpenFlag("openingSquallNoCargoLoss"))) { var held = LosableGoods(); /* gamedev_t15_storysafe */ if (held.Count > 0) { int g = held[Rnd.Range(0, held.Count)]; lost = Mathf.Min(hold[g], Rnd.Range(1, 4)); hold[g] -= lost; any = g; } }
      yield return Ask("Squall", lost > 0 ? $"A black squall heels the dhow over. {GoodCount(any, lost)} wash overboard." : HoldUsed > 0 ? "A black squall heels the dhow over. Your crew lash the cargo down and nothing is lost." : "A black squall heels the dhow over, but your empty hold rides it out.", ("Continue", () => msg = "The squall passes."));
    } else {
      int p = Rnd.Range(0, S.ports.Length); var want = S.ports[p].want; int g = G(want[Rnd.Range(0, want.Length)]);
      price[p, g] *= S.rumorSpike; yield return Ask("Rumor on the wind", $"A fisherman swears {S.ports[p].name} is starved for {S.goods[g].name}. Prices there have jumped.", ("Noted", () => msg = $"Rumor: {S.goods[g].name} is dear in {S.ports[p].name}."));
    }
    Refresh(); }
  public string CustomsWaveReason(int port) => papersGiven ? "papers" : ((InOpening && OpenFlag("firstRunCustomsAlwaysPass")) || sails < OpenInt("customsRiskFromVoyage", 4)) ? "early" : null; /* port: papersGiven is global today, kept for future per-port papers */ /* designer_ambush_customs */
  public bool CustomsWaved(int port) => CustomsWaveReason(port) != null; /* designer_ambush_customs */
  string LostList(int[] before) { var parts = new System.Collections.Generic.List<string>(); for (int g = 0; g < hold.Length; g++) if (before[g] > hold[g]) parts.Add(GoodCount(g, before[g] - hold[g])) /* gamedev_t9 */; return parts.Count > 0 ? string.Join(", ", parts) : "nothing"; } /* designer_ambush_customs */
  /* gamedev_t7: one arrival hold for every arrival (voyage end + intro "Limp into Gwadar"). Fresh tap skips: polled here every frame (not only in Update's sea block), and a tap made while the landmark was already up before the hold starts also counts. */
  bool arrivalTapEarly; float arrivalLmUpT = -1f; /* gamedev_t7 */
  bool FreshTap() => Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began); /* gamedev_t7 */
  IEnumerator ArrivalHold(string an, int wk) { /* gamedev_t7 */
    arrivalTarget = (seaMarks.TryGetValue(an, out var lmg) && lmg ? lmg.transform.position : new Vector3(0, 0, 80f)) + Vector3.up * 5f; float t0 = Time.time; arrivalPanT0 = t0;
    Debug.Log("GB ARRIVAL hold start " + an + " t=" + t0.ToString("F2"));
    bool skipped = arrivalTapEarly; arrivalTapEarly = false; bool shot = false; yield return null;
    while (!skipped && Time.time < t0 + ArrivalPanDur) {
      if (arrivalPanT0 < 0 && Time.time < t0 + ArrivalPanDur - 0.05f) skipped = true; /* Update's own tap check fired */
      if (FreshTap() && dialog == null) skipped = true;
      if (!skipped && !shot && playthrough && Time.time >= t0 + ArrivalPanDur * 0.5f) { shot = true; yield return ShotCo(System.IO.Path.Combine(ptDir, $"arrival_{an.ToLower()}_{wk}.png")); continue; }
      yield return null; }
    arrivalPanT0 = -1; arrivalLmUpT = -1f;
    if (skipped) Debug.Log("GB ARRIVAL skipped t=" + (Time.time - t0).ToString("F2") + " " + an);
    Debug.Log("GB ARRIVAL hold end " + an + " t=" + Time.time.ToString("F2")); } /* gamedev_t7 */
  IEnumerator AmbushWithResult(string type) { /* designer_ambush_customs */
    var before = (int[])hold.Clone(); lastAmbushOutcome = null; yield return AmbushEncounter(type); /* designer_ambush_customs */
    string res = lastAmbushOutcome == "bait" ? $"Your cargo goes over the side. The pirates swing toward the floating crates, and you slip away.\n\nLost: {LostList(before)}." /* designer_ambush_customs */
      : lastAmbushOutcome == "toll" ? $"You hand over part of your hold. The pirate captain grins and waves you on.\n\nPaid: {LostList(before)}." /* designer_ambush_customs */
      : lastAmbushOutcome == "run" ? "Every sail up. You pull away before they can close." /* designer_ambush_customs */
      : lastAmbushOutcome == "bluff" ? "They hesitate at your guns and decide you aren't worth the trouble." : null; /* designer_ambush_customs */
    if (res != null) yield return Ask("Clear of the pirates", res, ("Continue", null)); /* designer_ambush_customs */
  } /* designer_ambush_customs */
  IEnumerator Customs() {
    float risk = Risk(port); int val = 0; for (int g = 0; g < hold.Length; g++) if (hold[g] > 0 && Banned(g, port)) val += Mathf.RoundToInt(hold[g] * basePrice[port, g]);
    bool dump = false;
    yield return Ask($"Customs at {S.ports[port].name}", $"Inspectors wait on the quay. You carry goods banned here, worth about {val} coins.\n\n" + (CustomsWaveReason(port) == "papers" ? $"<b><color={T.up}>Customs waved you through.</color></b>" : CustomsWaveReason(port) == "early" ? $"<b><color={T.up}>Search risk: none. The inspectors wave you through.</color></b>" : $"Risk of being searched and caught: <b><color={T.down}>{risk:0}%</color></b>") + (crate ? "\nFaker's Crate aboard." : "") + (tithe ? "\nTithe paid." : ""), /* designer_ambush_customs */
      ("Dock", null), ("Dump it", () => dump = true));
    if (dump) { var dSg = InOpening ? StoryGoods() : new HashSet<int>(); /* gamedev_t16_dumpsafe */ int dDumped = 0, dKept = 0; for (int g = 0; g < hold.Length; g++) if (Banned(g, port) && hold[g] > 0) { if (dSg.Contains(g)) dKept += hold[g]; else { dDumped += hold[g]; hold[g] = 0; } } msg = dKept == 0 ? "You dumped the contraband before the pilot boat came alongside." : dDumped == 0 ? "You keep Farida's kegs below; nothing else to dump." : "You dumped the contraband but kept Farida's kegs below."; /* gamedev_t18_dumptext */ yield break; }
    bool safeRun = CustomsWaved(port); if (safeRun) risk = 0; /* designer_ambush_customs */
    if (gbForce.Contains("customs_caught")) safeRun = false; /* -gbforce customs_caught beats the first-run pass */ bool caught = !safeRun && (ptForceCustoms >= 0 ? ptForceCustoms == 1 : Rnd.value * 100 < risk); if (playthrough) PLog2("customs", $"risk {risk:0}% → " + (caught ? "CAUGHT" : "passed") + (safeRun ? " (first run / before voyage 4: always clears)" : ptForceCustoms >= 0 ? " (roll forced by branch)" : ""));
    if (caught) { int sVal = 0, sQty = 0; foreach (int g in LosableGoods()) if (Banned(g, port)) { sVal += Mathf.RoundToInt(hold[g] * basePrice[port, g]); sQty += hold[g]; hold[g] = 0; } /* gamedev_t16_seizesafe */ /* gamedev_t17_customs */ int fine = InOpening ? 0 : Mathf.CeilToInt(sVal * S.customs.finePct); if (Design.F(DZ?.Path(DZ.settings, "customs.fineCappedAtCoins"), 1f) > 0) fine = Mathf.Clamp(fine, 0, Mathf.Max(0, coins)); /* never fined into debt */ if (sQty == 0 && fine <= 0) { msg = "The officer waves you on."; /* gamedev_t17_customs */ yield return Ask($"Customs at {S.ports[port].name}", msg, ("Continue", null)); OnJobCustomsPass(); yield break; }
      coins -= fine; if (!InOpening) rep[port] -= S.customs.repLoss;
      msg = sQty == 0 ? $"Nothing seized. Fined {fine} coins, standing in {S.ports[port].name} −{S.customs.repLoss}." /* gamedev_t17_customs */ : $"Caught! Contraband seized, fined {fine} coins, standing in {S.ports[port].name} −{S.customs.repLoss}."; if (sQty == 0) yield return Ask($"Customs at {S.ports[port].name}", msg, ("Continue", null)); OnJobCustomsCaught(); }
    else { msg = "The inspector waves you through. Your back-alley buyers are waiting."; OnJobCustomsPass(); }
  }

  // ---------------- screenshot mode (Linux player: -gbshots <dir>) ----------------
  IEnumerator ShotModeIfAsked() {
    var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-gbshots"); if (i < 0 || i + 1 >= args.Length) yield break; string dir = args[i + 1];
    int ti = Array.IndexOf(args, "-gbtag"); string tag = ti >= 0 ? args[ti + 1] + "_" : "";
    System.IO.Directory.CreateDirectory(dir);
    Func<string, IEnumerator> Shot = n => ShotCo(System.IO.Path.Combine(dir, tag + n));
    yield return new WaitForSeconds(1f);
    ShowTitle(); yield return Shot("01_title.png"); onTitle = false;
    for (int k = 0; k < 4; k++) Buy(G("cotton")); for (int k = 0; k < 3; k++) Buy(G("indigo"));
    lastPrice[port, G("pepper")] *= 1.08f; lastPrice[port, G("salt")] *= 0.93f; lastPrice[port, G("tea")] *= 0.9f;
    if (!S.goods.Select((g, k) => k).Any(k => Banned(k, port))) bans.Add(new Ban { good = G("gunpowder"), port = port, start = 0, end = 8 });
    tab = -1; Refresh(); townS = townSMin; yield return null; yield return Shot("02_town_quay.png");
    townS = 20; yield return null; yield return Shot("02b_town_middle.png"); townS = townSMax; yield return null; yield return Shot("02c_town_palace.png"); townS = 4;
    foreach (var (tb, nm) in new[] { (5, "bank"), (6, "money_lender"), (7, "shipwright"), (8, "tavern") }) { OpenBuilding(tb); yield return null; yield return Shot($"02k_{nm}.png"); }
    OpenBuilding(4); yield return null; yield return Shot("02m_chart_map.png");
    tab = -1; Refresh(); for (int bi = 0; bi < bldgs.Count; bi++) { townClose = bi; yield return null; yield return null; yield return Shot($"02t_{bi}_{bldgs[bi].name.Replace(" ", "_").Replace("'", "").ToLower()}.png"); } townClose = -1;
    tab = 0; Refresh(); yield return Shot("03_market.png");
    OpenSheet(G("cotton")); yield return null; qty.value = 3; yield return Shot("04_slider_sheet.png");
    StartCoroutine(SheetTrade(false)); yield return new WaitForSeconds(0.17f); yield return Shot("05_slider_selling.png"); yield return new WaitForSeconds(1.2f); CloseSheet();
    marketScroll = 0; Refresh(); yield return null; yield return Shot("06_market_banned_NA.png"); marketScroll = 1;
    tab = 1; Refresh(); yield return Shot("07_back_alley.png");
    tab = 2; Refresh(); yield return Shot("08_warehouse.png");
    tab = 3; Refresh(); yield return Shot("09_temple.png");
    tab = 4; Refresh(); yield return Shot("10_set_sail.png");
    S.encounterChance = 0; S.sailSecondsMin = S.sailSecondsMax = 9999; var voy = StartCoroutine(Voyage((port + 1) % S.ports.Length)); yield return new WaitForSeconds(2.5f); ff = true; UpdateFF();
    ff = true; UpdateFF(); yield return null;
    yield return Shot("11_sailing.png");
    fighting = true; cam.transform.position = new Vector3(19f, 9f, 17f); cam.transform.LookAt(new Vector3(0, 4.2f, 0)); yield return null;
    yield return Shot("12_hasan_dhow_closeup.png"); fighting = false;
    var enc = StartCoroutine(PirateEncounter()); yield return new WaitForSeconds(0.4f); yield return Shot("13_encounter_card.png");
    StopCoroutine(enc); if (dialog) Destroy(dialog.gameObject); StopCoroutine(voy);
    StartCoroutine(Fight()); yield return new WaitForSeconds(0.5f); combatPaused = true;
    core.H.x = core.H.z = 0; core.H.heading = 10; core.P.x = 70; core.P.z = 30; core.P.heading = 200; core.H.aim = Part.Sails; core.H.ammo = 1; core.windTo = 290;
    core.P.sails = 31; core.P.hull = 58; core.H.hull = 84; core.H.reloadT = 1.4f; camYaw = CombatCore.Bearing(70, 30);
    yield return new WaitForSeconds(0.6f); hudMsg.text = "Broadside! 3 of 4 shots hit their sails."; core.Fire(core.H, core.P); core.H.reloadT = 1.4f; yield return new WaitForSeconds(0.25f);
    yield return Shot("14_combat_aiming.png");
    core.P.hull = 30; core.breachT0 = core.t; core.H.aim = Part.Hull; core.H.ammo = 0; yield return new WaitForSeconds(0.3f); yield return Shot("14b_hull_breach_glowing.png");
    { var sink = StartCoroutine(SinkCinematic(true)); yield return new WaitForSeconds(1.6f); yield return Shot("14c_sinking.png"); yield return new WaitForSeconds(2.2f); yield return Shot("14d_floating_crates.png"); yield return new WaitForSeconds(2.2f); StopCoroutine(sink); inCinematic = false; core.P.hull = 58; core.breachT0 = -1; }
    core.P.x = 14; core.P.z = 9; core.P.heading = 30; core.P.sails = 8; camYaw = CombatCore.Bearing(14, 9); yield return new WaitForSeconds(0.4f); yield return Shot("15_board_ready_pirate_dhow.png");
    duelFreeze = 0.47f; StartCoroutine(Duel(true)); yield return new WaitForSeconds(0.4f); yield return Shot("16_boarding_duel.png");
    Debug.Log("GB TRIS hasan/pirate: " + hasanTris + " / " + Dhow.LastTris);
    Application.Quit();
  }
  IEnumerator ShotCo(string path) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(path); if (Time.timeScale == 0) yield return new WaitForSecondsRealtime(0.5f); else yield return new WaitForSeconds(0.5f); }
}
