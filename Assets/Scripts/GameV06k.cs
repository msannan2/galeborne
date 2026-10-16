using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// v0.6k3: per-port shore landmarks (textured townMat, ~1/3 prior size) + town layout skins.
/// Landmarks ONLY in town + port-arrival views — never mid-ocean sailing or sea fight.
public partial class Game {
  Transform landmarksTown, landmarksSea, townLayoutsRoot;
  readonly Dictionary<string, GameObject> townMarks = new Dictionary<string, GameObject>();
  readonly Dictionary<string, GameObject> seaMarks = new Dictionary<string, GameObject>(); // port-arrival shore only
  readonly Dictionary<string, GameObject> townLayouts = new Dictionary<string, GameObject>();
  string landmarkTownShown = "";

  const bool LandmarksOn = true; /* k4art: on (town root + arrival only) */
  void BuildLandmarksV06k() {
    if (!LandmarksOn) { return; } /* k3: landmarks OFF everywhere; work stashed for k4 */
    if (town3D == null || townMat == null) return;
    if (landmarksTown != null) { ShowTownLandmark(); return; }
    landmarksTown = new GameObject("LandmarksTown").transform; landmarksTown.SetParent(town3D, false);
    landmarksSea = new GameObject("LandmarksSea").transform; landmarksSea.SetParent(sea3D, false);
    townLayoutsRoot = new GameObject("TownLayouts").transform; townLayoutsRoot.SetParent(town3D, false);

    // ~1/3 of k2 scale; ON THE SHORE at far terrace edge (reads in TownCamera at townS≈0–12)
    /* ~1/3 of k2; ON THE SHORE at far terrace edge (readable in TownCamera) */
    /* k4art: per-town spots (design/v06k4_town_layouts.md); scale ~1.8 x terrace bldg h (~5.2) / mesh h */
    /* k4art patch: roof level up the slope (y ~16) so GD rock bases fit underneath; z 52 clears the terraces */
    townMarks["Gwadar"] = BuildLandmarkMesh("LM_Gwadar_Town", landmarksTown, LandmarkGwadar, new Vector3(-8f, 16f, 56f), 0.70f);   // left cliff, on GD crag ledge (CragTopY 16, z 56)
    townMarks["Debal"]  = BuildLandmarkMesh("LM_Debal_Town",  landmarksTown, LandmarkDebal,  new Vector3(4.5f, 16f, 56f), 0.62f);  // top-right on GD crag, pennant in frame
    townMarks["Muscat"] = BuildLandmarkMesh("LM_Muscat_Town", landmarksTown, LandmarkMuscat, new Vector3(6f, 16f, 56f), 0.72f);    // right crag (x +6) on GD crag

    // Port-arrival shore silhouettes (same meshes) — parked on the coast, shown only for arrival framing
    seaMarks["Gwadar"] = BuildLandmarkMesh("LM_Gwadar_Arrive", landmarksSea, LandmarkGwadar, new Vector3(-24f, 1.2f, 78f), 0.95f); // left
    seaMarks["Debal"]  = BuildLandmarkMesh("LM_Debal_Arrive",  landmarksSea, LandmarkDebal,  new Vector3(14f, 1.2f, 82f), 0.9f);   // right, pennant in frame
    seaMarks["Muscat"] = BuildLandmarkMesh("LM_Muscat_Arrive", landmarksSea, LandmarkMuscat, new Vector3(18f, 1.5f, 80f), 0.95f);  // right crag
    /* k4art patch: arrival copies stand on a low headland, not in open water (matte crag/sand, no glow) */
    ShoreBase(seaMarks["Gwadar"], "#8A5A3C", "#6B4430"); ShoreBase(seaMarks["Debal"], "#C9A46A", "#A9844F"); ShoreBase(seaMarks["Muscat"], "#ECE6D8", "#B8AE9A");

    foreach (var tn in new[] { "Gwadar", "Debal", "Muscat" }) townLayouts[tn] = TownLayoutsK4.Build(tn, townLayoutsRoot, townMat, TU); // k4 layouts (Game Designer)

    foreach (var kv in seaMarks) kv.Value.SetActive(false);
    foreach (var kv in townMarks) kv.Value.SetActive(false);
    foreach (var kv in townLayouts) kv.Value.SetActive(false);
    // Hide generic skyline boxes that compete with port skins
    foreach (Transform c in town3D) {
      if (c.name == "Hills") c.gameObject.SetActive(false);
    }
    Debug.Log("GB v06k3 textured shore landmarks + layouts built");
    ShowTownLandmark();
  }

  GameObject BuildLandmarkMesh(string name, Transform parent, System.Action<MeshB> build, Vector3 pos, float scale) {
    var mb = new MeshB(); build(mb);
    var g = new GameObject(name); g.transform.SetParent(parent, false);
    g.transform.localPosition = pos; g.transform.localScale = Vector3.one * scale;
    g.AddComponent<MeshFilter>().sharedMesh = mb.Build();
    var lmMat = Resources.Load<Material>("LandmarksK4/M_LM_TownAtlas_k4"); /* k4art: landmark atlas (same TCell layout) */
    g.AddComponent<MeshRenderer>().sharedMaterial = lmMat != null ? lmMat : townMat;
    return g;
  }

  /// k4art patch: low headland under an arrival copy, tinted to the town's own rock (same hexes as GameTownArt.K4Skins / GD crags). Matte, no emission.
  void ShoreBase(GameObject lm, string topHex, string cragHex) {
    Rect w = TU((int)TCell.Whitewash); var src = lm.GetComponent<MeshRenderer>().sharedMaterial;
    void Part(string n, MeshB mb, string hex) { var m = new Material(src); ColorUtility.TryParseHtmlString(hex, out var c); if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.08f);
      var g = new GameObject(n); g.transform.SetParent(lm.transform, false); g.AddComponent<MeshFilter>().sharedMesh = mb.Build(); g.AddComponent<MeshRenderer>().sharedMaterial = m; }
    var crag = new MeshB(); crag.Cyl(new Vector3(2f, -3f, 2f), new Vector3(2f, 0.4f, 2f), 15f, 11.5f, 12, w);                 // wide low crag at the waterline
    crag.Box(new Vector3(-6f, -0.4f, 7f), new Vector3(7f, 2.6f, 6f), Quaternion.Euler(0, 20f, 4f), w);                          // shoulder of the headland
    var shelf = new MeshB(); shelf.Cyl(new Vector3(2f, 0.4f, 2f), new Vector3(2f, 0.9f, 2f), 11.5f, 10.5f, 12, w);             // shore shelf
    shelf.Box(new Vector3(-9f, 0.1f, -4f), new Vector3(6f, 1.2f, 5f), Quaternion.Euler(0, 25f, 0), w);                         // beach spit toward the ship
    Part("ShoreCrag", crag, cragHex); Part("ShoreShelf", shelf, topHex);
  }

  void ShowTownLandmark() {
    if (townMarks.Count == 0) return;
    string here = S != null && port >= 0 && port < S.ports.Length ? S.ports[port].name : "Gwadar";
    foreach (var kv in townMarks) kv.Value.SetActive(kv.Key == here);
    if (seaMarks != null) foreach (var kv in seaMarks) if (kv.Value) kv.Value.SetActive(false); /* k4art patch: one landmark per town, no arrival copy in town */
    foreach (var kv in townLayouts) kv.Value.SetActive(kv.Key == here);
    landmarkTownShown = here;
    ApplyTownPaletteV06k3(here);
    if (town3D != null) foreach (Transform c in town3D) {
      var n = c.name;
      if (n == "Temple" || n == "Governor's Palace" || n == "Hills" || n == "Skyline" || n.StartsWith("Tower") || n.StartsWith("Wall"))
        c.gameObject.SetActive(false);
    }
    // Dock framing: nudge camera focus toward shore landmark
    townS = Mathf.Clamp(townS, 0f, 14f);
    Debug.Log("GB v06k3 ShowTownLandmark → " + here);
  }

  /// Port-arrival only. Pass -1 / fighting / mid-ocean → all off.
  void ShowSeaLandmark(int destPort) {
    if (seaMarks.Count == 0) return;
    if (fighting || destPort < 0) {
      foreach (var kv in seaMarks) kv.Value.SetActive(false);
      return;
    }
    string dest = destPort < S.ports.Length ? S.ports[destPort].name : "";
    foreach (var kv in seaMarks) kv.Value.SetActive(kv.Key == dest);
  }

  void HideAllSeaLandmarks() {
    if (seaMarks == null) return;
    foreach (var kv in seaMarks) if (kv.Value) kv.Value.SetActive(false);
  }

  void ApplyTownPaletteV06k3(string town) {
    if (townMat == null) return;
    // Subtle tint on shared terrace atlas: mud brick / sandstone / whitewash lean
    Color tint = town == "Debal" ? new Color(0.95f, 0.88f, 0.78f)   // sandstone warm
      : town == "Muscat" ? new Color(0.98f, 0.98f, 1.02f)            // whitewash cool
      : new Color(0.92f, 0.84f, 0.70f);                               // Gwadar mud brick
    if (townMat.HasProperty("_BaseColor")) townMat.SetColor("_BaseColor", tint);
    if (town == "Muscat") {
      RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.50f);
      RenderSettings.fogColor = new Color(0.75f, 0.78f, 0.85f);
    } else if (town == "Debal") {
      RenderSettings.ambientLight = new Color(0.38f, 0.34f, 0.28f);
      RenderSettings.fogColor = new Color(0.82f, 0.74f, 0.62f);
    } else {
      RenderSettings.ambientLight = new Color(0.36f, 0.32f, 0.26f);
      RenderSettings.fogColor = new Color(0.80f, 0.72f, 0.58f);
    }
  }

  // ---- textured MeshB landmarks (town atlas cells) ----
  void LandmarkGwadar(MeshB mb) { // mud brick / ochre / plaster
    Rect rock = TU((int)TCell.Rock), ochre = TU((int)TCell.Ochre), plaster = TU((int)TCell.Plaster);
    Rect white = TU((int)TCell.Whitewash), wood = TU((int)TCell.Wood), cloth = TU((int)TCell.Cloth);
    Rect gold = TU((int)TCell.Gold), dark = TU((int)TCell.Dark), sand = TU((int)TCell.Sand);
    mb.Box(new Vector3(0, 2.5f, 3f), new Vector3(10f, 6f, 7f), rock);
    mb.Box(new Vector3(-1f, 5f, 2f), new Vector3(7f, 3.2f, 5f), ochre);
    mb.Cyl(new Vector3(0.5f, 7f, 2f), new Vector3(0.5f, 12.5f, 2f), 1.55f, 1.4f, 10, ochre);
    mb.Cyl(new Vector3(0.5f, 12.5f, 2f), new Vector3(0.5f, 13.5f, 2f), 1.6f, 0.3f, 10, ochre);
    mb.Box(new Vector3(0.5f, 13.85f, 2f), new Vector3(0.22f, 0.7f, 0.22f), gold);
    mb.Box(new Vector3(-4.2f, 3.2f, 0.5f), new Vector3(4.0f, 3.2f, 3.5f), plaster);
    mb.Cyl(new Vector3(-4.2f, 4.8f, 0.5f), new Vector3(-4.2f, 6.8f, 0.5f), 1.45f, 0.2f, 12, white);
    mb.Box(new Vector3(-4.2f, 2.0f, -1.2f), new Vector3(1.2f, 2.0f, 0.25f), dark);
    for (int i = 0; i < 4; i++) {
      float x = -2.5f + i * 1.35f;
      mb.Cyl(new Vector3(x, 0f, -2.2f), new Vector3(x, 2.6f, -2.2f), 0.07f, 0.07f, 5, wood);
      mb.Cyl(new Vector3(x, 0f, -3.5f), new Vector3(x, 2.6f, -3.5f), 0.07f, 0.07f, 5, wood);
      mb.Cyl(new Vector3(x, 2.5f, -2.2f), new Vector3(x, 2.5f, -3.5f), 0.05f, 0.05f, 4, wood);
      mb.Box(new Vector3(x, 1.3f, -2.85f), new Vector3(0.85f, 1.9f, 0.08f), cloth);
    }
  }

  void LandmarkDebal(MeshB mb) { // sandstone + teak + RED pennant
    Rect stone = TU((int)TCell.Stone), marble = TU((int)TCell.Marble), gold = TU((int)TCell.Gold);
    Rect teak = TU((int)TCell.Teak), roof = TU((int)TCell.Roof), dark = TU((int)TCell.Dark);
    Rect red = TU((int)TCell.AwnRed), ochre = TU((int)TCell.Ochre);
    mb.Box(new Vector3(0, 1.8f, 2.5f), new Vector3(5.5f, 3.5f, 5.5f), stone);
    float y = 3.5f;
    for (int i = 0; i < 10; i++) {
      float s = 4.6f - i * 0.36f; float h = 1.4f;
      mb.Box(new Vector3(0, y + h * 0.5f, 2.5f), new Vector3(s, h, s), i % 2 == 0 ? marble : ochre);
      y += h * 0.86f;
    }
    mb.Box(new Vector3(0, y + 1.1f, 2.5f), new Vector3(0.22f, 2.6f, 0.22f), gold);
    mb.Box(new Vector3(0.95f, y + 2.2f, 2.5f), new Vector3(1.9f, 0.9f, 0.1f), red); // RED pennant — keep in frame
    mb.Box(new Vector3(1.5f, y + 1.7f, 2.5f), new Vector3(0.85f, 0.6f, 0.08f), red);
    mb.Box(new Vector3(6.8f, 2.6f, 1f), new Vector3(5.5f, 5.2f, 4.2f), teak);
    mb.Box(new Vector3(6.8f, 5.4f, 1f), new Vector3(6.2f, 0.25f, 4.8f), teak);
    for (int i = -2; i <= 2; i++) {
      mb.Cyl(new Vector3(6.8f + i * 1.15f, 5.4f, 3.2f), new Vector3(6.8f + i * 1.15f, 6.7f, 3.2f), 0.09f, 0.09f, 5, teak);
      mb.Cyl(new Vector3(6.8f + i * 1.15f, 5.4f, -1.2f), new Vector3(6.8f + i * 1.15f, 6.7f, -1.2f), 0.09f, 0.09f, 5, teak);
    }
    mb.Box(new Vector3(6.8f, 6.6f, 1f), new Vector3(6.0f, 0.12f, 4.6f), teak);
    mb.Box(new Vector3(6.8f, 7.7f, 1f), new Vector3(5.8f, 1.4f, 4.4f), roof);
    mb.Box(new Vector3(6.8f, 1.6f, -1.0f), new Vector3(1.5f, 2.4f, 0.25f), dark);
  }

  void LandmarkMuscat(MeshB mb) { // whitewash fort + dark rock — boxes only (no giant Lathe)
    Rect rock = TU((int)TCell.Rock), white = TU((int)TCell.Whitewash), dark = TU((int)TCell.Dark);
    Rect plaster = TU((int)TCell.Plaster), stone = TU((int)TCell.Stone);
    mb.Box(new Vector3(0, 2.5f, 4f), new Vector3(9f, 6f, 7f), rock);
    mb.Box(new Vector3(-1f, 5.5f, 3.5f), new Vector3(6.5f, 4.5f, 5f), rock);
    mb.Box(new Vector3(1f, 7.5f, 4f), new Vector3(4f, 3f, 3.5f), rock);
    mb.Cyl(new Vector3(0, 5.5f, 3.5f), new Vector3(0, 15.2f, 3.5f), 2.9f, 2.75f, 14, plaster); /* k4art: keep starts inside the rock (was 9.5 → 0.5–3 m air gap under the keep) */
    for (int i = 0; i < 12; i++) {
      float a = i * Mathf.PI * 2f / 12f;
      mb.Box(new Vector3(Mathf.Cos(a) * 2.7f, 15.65f, 3.5f + Mathf.Sin(a) * 2.7f), new Vector3(0.7f, 1.05f, 0.5f), white); /* k4art: merlons overlap the keep top (no seam) */
    }
    mb.Box(new Vector3(0, 2.8f, 0.2f), new Vector3(6.8f, 5.5f, 3.6f), plaster);
    mb.Box(new Vector3(-3.1f, 4f, 0.2f), new Vector3(1.7f, 4f, 1.7f), plaster);
    mb.Box(new Vector3(3.1f, 4f, 0.2f), new Vector3(1.7f, 4f, 1.7f), plaster);
    for (int i = -2; i <= 2; i++) mb.Box(new Vector3(i * 1.15f, 5.8f, -1.5f), new Vector3(0.65f, 0.9f, 0.4f), white);
    mb.Box(new Vector3(0, 2.3f, -1.55f), new Vector3(2.2f, 3.4f, 0.4f), dark);
    for (int k = 0; k < 5; k++) mb.Box(new Vector3(0, 0.15f + k * 0.35f, -2.6f + k * 0.4f), new Vector3(2.8f, 0.35f, 0.4f), stone);
  }

  // ---- per-port layout skins (small quay clusters — landmarks carry the skyline; no giant flat slabs)
  GameObject BuildLayoutGwadar(Transform parent) {
    var mb = new MeshB();
    Rect rock = TU((int)TCell.Rock), ochre = TU((int)TCell.Ochre), sand = TU((int)TCell.Sand);
    Rect wood = TU((int)TCell.Wood), cloth = TU((int)TCell.Cloth), plaster = TU((int)TCell.Plaster);
    // squat mud-brick houses left of dock stair
    mb.Box(new Vector3(-9f, 1.6f, 6f), new Vector3(3.2f, 3.2f, 3.0f), ochre);
    mb.Box(new Vector3(-9f, 3.4f, 6f), new Vector3(3.4f, 0.25f, 3.2f), sand);
    mb.Box(new Vector3(-6.2f, 1.4f, 8.5f), new Vector3(2.4f, 2.8f, 2.6f), plaster);
    mb.Cyl(new Vector3(-6.2f, 2.8f, 8.5f), new Vector3(-6.2f, 4.0f, 8.5f), 1.0f, 0.15f, 10, plaster);
    // net racks on quay
    for (int i = 0; i < 4; i++) {
      float x = 3.5f + i * 1.15f;
      mb.Cyl(new Vector3(x, 0f, -1.2f), new Vector3(x, 2.2f, -1.2f), 0.05f, 0.05f, 4, wood);
      mb.Cyl(new Vector3(x, 0f, -2.4f), new Vector3(x, 2.2f, -2.4f), 0.05f, 0.05f, 4, wood);
      mb.Box(new Vector3(x, 1.1f, -1.8f), new Vector3(0.7f, 1.6f, 0.05f), cloth);
    }
    mb.Box(new Vector3(-12f, 2.5f, 14f), new Vector3(4f, 5f, 6f), rock); // low cliff only
    return FinishLayout("Layout_Gwadar", parent, mb);
  }
  GameObject BuildLayoutDebal(Transform parent) {
    var mb = new MeshB();
    Rect teak = TU((int)TCell.Teak), stone = TU((int)TCell.Stone), roof = TU((int)TCell.Roof), ochre = TU((int)TCell.Ochre);
    // dense teak/brick flanking dock — human-scale, not skyscraper slabs
    mb.Box(new Vector3(-8f, 2.2f, 7f), new Vector3(3.0f, 4.4f, 3.2f), teak);
    mb.Box(new Vector3(-8f, 4.6f, 7f), new Vector3(3.2f, 0.2f, 3.4f), teak);
    mb.Box(new Vector3(-5.2f, 2.0f, 9f), new Vector3(2.6f, 4.0f, 2.8f), stone);
    mb.Box(new Vector3(-5.2f, 4.4f, 9f), new Vector3(2.8f, 1.0f, 3.0f), roof);
    mb.Box(new Vector3(8.5f, 2.0f, 6.5f), new Vector3(3.4f, 4.0f, 3.5f), teak);
    mb.Box(new Vector3(8.5f, 4.2f, 6.5f), new Vector3(3.6f, 0.18f, 3.7f), teak);
    mb.Box(new Vector3(8.5f, 5.0f, 6.5f), new Vector3(3.5f, 1.1f, 3.6f), roof);
    mb.Box(new Vector3(7.2f, 1.8f, 3.5f), new Vector3(2.2f, 3.2f, 2.4f), ochre); // customs near dock
    return FinishLayout("Layout_Debal", parent, mb);
  }
  GameObject BuildLayoutMuscat(Transform parent) {
    var mb = new MeshB();
    Rect white = TU((int)TCell.Whitewash), plaster = TU((int)TCell.Plaster), rock = TU((int)TCell.Rock);
    // whitewashed low walls + gatehouse feel near quay; dark crag behind landmark only
    mb.Box(new Vector3(-8f, 1.5f, 7f), new Vector3(4.0f, 3.0f, 3.5f), plaster);
    mb.Box(new Vector3(-8f, 3.2f, 7f), new Vector3(4.2f, 0.35f, 3.7f), white);
    for (int i = -2; i <= 2; i++) mb.Box(new Vector3(-8f + i * 0.85f, 3.7f, 5.4f), new Vector3(0.55f, 0.7f, 0.4f), white);
    mb.Box(new Vector3(8f, 1.6f, 8f), new Vector3(3.5f, 3.2f, 3.2f), white);
    mb.Box(new Vector3(0f, 1.2f, 5f), new Vector3(2.5f, 2.4f, 2.0f), plaster);
    mb.Box(new Vector3(0f, 1.0f, 3.6f), new Vector3(1.4f, 2.2f, 0.35f), rock); // dark gate slit
    // one dark mountain mass far behind (not a wall of slabs)
    /* v0.6k3: flat mountain slabs removed (CAD: no flat boxes); generic Hills lathe stays */
    return FinishLayout("Layout_Muscat", parent, mb);
  }
  GameObject FinishLayout(string n, Transform parent, MeshB mb) {
    var g = new GameObject(n); g.transform.SetParent(parent, false);
    g.AddComponent<MeshFilter>().sharedMesh = mb.Build();
    g.AddComponent<MeshRenderer>().sharedMaterial = townMat;
    return g;
  }

  IEnumerator LandmarkShotsIfAsked() {
    var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-gblandmarks"); if (i < 0 || i + 1 >= a.Length) yield break;
    string dir = a[i + 1]; System.IO.Directory.CreateDirectory(dir);
    if (town3D == null) BuildTown(); BuildLandmarksV06k();
    onTitle = false; tab = -1;
    foreach (var name in new[] { "Gwadar", "Debal", "Muscat" }) {
      int pi = PortIdx(name); if (pi < 0) continue;
      port = pi; ShowTownLandmark(); HideAllSeaLandmarks();
      if (town3D) town3D.gameObject.SetActive(true); if (sea3D) sea3D.gameObject.SetActive(false);
      backdrop.color = Color.clear; if (dimmer) dimmer.gameObject.SetActive(false);
      Clear(content); TopBar();
      // Normal TownCamera dock framing (townS mid so shore landmark is at far edge)
      townS = name == "Debal" ? 6f : name == "Muscat" ? 8f : 5f;
      TownCamera();
      /* k4art: real TownCamera framing (no shot-only LookAt) */
      yield return null; yield return null;
      if (townMarks.ContainsKey(name)) { var lm = townMarks[name]; var bb = lm.GetComponent<Renderer>().bounds; Debug.Log($"GB k4art town {name} active={lm.activeInHierarchy} vpTop={cam.WorldToViewportPoint(bb.center + Vector3.up * bb.extents.y)} vpBase={cam.WorldToViewportPoint(bb.center - Vector3.up * bb.extents.y)} wpos={lm.transform.position}"); }
      yield return ShotCo(System.IO.Path.Combine(dir, $"v06k4art_{name.ToLower()}_town.png"));

      // Port-arrival (shore landmark on coast — NOT open-ocean mid-voyage)
      if (town3D) town3D.gameObject.SetActive(false); sea3D.gameObject.SetActive(true);
      HideSeaIslands(true); ShowSeaLandmark(pi); EnsureKenneyDhowOnShip(); FreeModels.FixLateenSail(ship.gameObject);
      ship.position = new Vector3(-3f, 0.15f, 40f); ship.rotation = Quaternion.Euler(0, 10f, 0);
      Vector3 shore = (seaMarks.ContainsKey(name) ? seaMarks[name].transform.position : new Vector3(0, 0, 80f)) + Vector3.up * 6f;
      cam.transform.position = new Vector3(shore.x * 0.2f - 8f, 5.5f, shore.z - 32f);
      cam.transform.LookAt(shore);
      Clear(content); TopBar();
      Label(content, $"Approaching {name}", 40, 160, 1000, 60, T.fontSizeTitle, T.parchment, TextAnchor.MiddleCenter, true);
      yield return null; yield return null;
      if (seaMarks.ContainsKey(name)) { /* k4art: park arrival mark on the coast the live sea camera actually frames */
        var fw = cam.transform.forward; fw.y = 0; fw.Normalize(); var rt = cam.transform.right; rt.y = 0; rt.Normalize();
        float side = name == "Gwadar" ? -9f : name == "Debal" ? 7f : 9f;
        var lp = cam.transform.position + fw * 75f + rt * side; lp.y = 1.2f; seaMarks[name].transform.position = lp;
        seaMarks[name].transform.rotation = Quaternion.LookRotation(-fw); }
      if (name == "Gwadar") foreach (var rr in ship.GetComponentsInChildren<Renderer>(true)) Debug.Log($"GB k4art shipR {rr.gameObject.name} mat={rr.sharedMaterial?.name} tex={rr.sharedMaterial?.mainTexture?.name}");
      yield return null;
      yield return ShotCo(System.IO.Path.Combine(dir, $"v06k4art_{name.ToLower()}_sea.png"));
      ShowSeaLandmark(-1);
    }
    // Clean sailing shot — open water, Kenney dhow, NO landmarks / islands / decision card
    HideAllSeaLandmarks(); HideSeaIslands(true);
    if (town3D) town3D.gameObject.SetActive(false); sea3D.gameObject.SetActive(true);
    EnsureKenneyDhowOnShip(); FreeModels.FixLateenSail(ship.gameObject);
    ship.position = Vector3.zero; ship.rotation = Quaternion.Euler(2f, 25f, -3f);
    cam.transform.position = new Vector3(-10f, 5f, -16f); cam.transform.LookAt(new Vector3(2f, 2.5f, 8f));
    waterMat.SetFloat("_Speed", 3); Clear(content); TopBar();
    Label(content, "At sea", 40, 160, 1000, 50, T.fontSizeTitle, T.parchment, TextAnchor.MiddleCenter, true);
    yield return null; yield return null;
    yield return ShotCo(System.IO.Path.Combine(dir, "v06k4art_sailing.png"));
    HideSeaIslands(false);

    // Storm / voyage decision card — panel sized to text (Ask fit); no wait for tap
    Clear(content); TopBar();
    {
      string body = "A black wall of rain marches across the Gulf. The mate waits on your word.";
      float bodyH = CardBodyH(body); /* k4art: measured */
      float y = 92 + bodyH + 20;
      float panelH = y + 84 + 20;
      float top = Mathf.Max(180f, (1920f - panelH) * 0.38f);
      dialog = Px(root, "Dialog", 90, top, 900, panelH); Box(dialog, T.panel, T.gold);
      Label(dialog, "Storm front", 30, 16, 840, 52, T.fontSizeTitle, T.parchment, TextAnchor.MiddleLeft, true);
      Box(Px(dialog, "rule", 30, 72, 840, 2), T.brass);
      Label(dialog, body, 30, 92, 840, bodyH, T.fontSize, T.text, TextAnchor.UpperLeft); /* k4art: +8px pad (mirrors Ask) */
      float w = (840f - 16) / 2f;
      Btn(dialog, "Run before it", 30, y, w, 84, null, true, true);
      Btn(dialog, "Reef and ride", 30 + w + 16, y, w, 84, null, true, false);
    }
    yield return null; yield return null;
    yield return ShotCo(System.IO.Path.Combine(dir, "v06k4art_storm.png"));
    if (dialog) { Destroy0(dialog.gameObject); dialog = null; }
    StartCoroutine(Ask("Taken by pirates", "The raiders strip your hold. You lose 12 cargo and 30% of your crew are injured (slower reloads until you reach port).\n\nNo pirate will trouble you for the next 2 voyages. The mate says the wind is with you now.", ("Limp on", (System.Action)null), ("Curse the sea", (System.Action)null)));
    yield return null; yield return null;
    yield return ShotCo(System.IO.Path.Combine(dir, "v06k4art_storm_long.png"));
    if (dialog) { Destroy0(dialog.gameObject); dialog = null; }

    // Sea fight — j camera + k2 Hasan scale, skyZenith (no parchment strip), NO shore fort
    HideAllSeaLandmarks(); HideSeaIslands(true);
    if (town3D) town3D.gameObject.SetActive(false); sea3D.gameObject.SetActive(true);
    EnsureKenneyDhowOnShip(); FreeModels.FixLateenSail(ship.gameObject);
    ship.position = Vector3.zero; ship.rotation = Quaternion.Euler(0, 20f, 0);
    if (pirateShip == null) pirateShip = Dhow.Build(sea3D, T, true, "PirateDhow");
    pirateShip.gameObject.SetActive(true);
    pirateShip.position = new Vector3(14f, 0.15f, 18f); pirateShip.rotation = Quaternion.Euler(0, 200f, 0);
    var mid = (ship.position + pirateShip.position) / 2f;
    float d = 40f;
    cam.transform.position = ship.position - Quaternion.Euler(0, 25f, 0) * Vector3.forward * (d * 0.55f + 16) + Vector3.up * (d * 0.75f + 15);
    cam.transform.LookAt(mid + Vector3.up * 2f);
    cam.backgroundColor = UITheme.C(T.skyZenith); RenderSettings.fogColor = UITheme.C(T.skyZenith);
    { var skyR = sea3D.Find("SeaSky")?.GetComponent<Renderer>();
      if (skyR && skyR.sharedMaterial) { var sm = skyR.material; if (sm.HasProperty("_Horizon")) sm.SetColor("_Horizon", UITheme.C(T.skyZenith)); if (sm.HasProperty("_Zenith")) sm.SetColor("_Zenith", UITheme.C(T.skyZenith)); } }
    var sc0 = ship.localScale; ship.localScale = sc0 * 1.45f; /* k2 Hasan scale */
    Clear(content); TopBar();
    Label(content, "Pirate dhow!", 40, 160, 1000, 50, T.fontSizeTitle, T.parchment, TextAnchor.MiddleCenter, true);
    yield return null; yield return null;
    yield return ShotCo(System.IO.Path.Combine(dir, "v06k4art_fight.png"));
    ship.localScale = sc0; pirateShip.gameObject.SetActive(false); HideSeaIslands(false);
    Application.Quit();
  }

  /// k4art: measured body height for event cards (wrap at 840 px) so text never runs under the buttons
  float CardBodyH(string body) {
    var st = new TextGenerationSettings { font = T.regular, fontSize = T.fontSize, lineSpacing = 1, richText = true, scaleFactor = 1,
      textAnchor = TextAnchor.UpperLeft, generationExtents = new Vector2(840f, 4000f), pivot = Vector2.zero, generateOutOfBounds = true,
      horizontalOverflow = HorizontalWrapMode.Wrap, verticalOverflow = VerticalWrapMode.Overflow, updateBounds = true, color = Color.white };
    float ph = T.regular != null ? new TextGenerator().GetPreferredHeight(body ?? "", st) : 0f;
    int est = Mathf.Max(1, 1 + (body ?? "").Length / 44 + (body ?? "").Split('\n').Length - 1);
    return Mathf.Clamp(Mathf.Max(ph + 8f, est * (T.fontSize + 6)), 52f, 1200f);
  }

  void HideSeaIslands(bool hide) {
    if (sea3D == null) return;
    foreach (Transform c in sea3D) {
      var mr = c.GetComponent<MeshRenderer>();
      bool island = c.name == "Sphere" || c.name.StartsWith("Sphere") || c.name.Contains("Island");
      if (!island && mr != null && mr.sharedMaterial != null) {
        var mn = mr.sharedMaterial.name;
        island = mn.Contains("Island") || mn.Contains("island");
      }
      if (island) c.gameObject.SetActive(!hide);
    }
  }


  void EnsureKenneyDhowOnShip() {
    if (ship == null) return;
    var existing = ship.Find("HasanDhow");
    if (existing != null) {
      foreach (Transform c in ship) if (c != existing && (c.name.Contains("Dhow") || c.name.Contains("Hasan"))) Object.Destroy(c.gameObject);
      FreeModels.FixLateenSail(existing.gameObject);
      return;
    }
    foreach (Transform c in ship) Object.Destroy(c.gameObject);
    var k = FreeModels.SpawnKenneyDhow(ship, "HasanDhow");
    if (k == null) { Debug.LogWarning("GB v06k3: Kenney missing"); Dhow.Build(ship, T, false, "HasanDhow"); }
    else Debug.Log("GB v06k3: Kenney dhow OK");
  }
}
