using System.Linq;
using UnityEngine;

/// v0.6j: Kenney watercraft (player dhow) + distant KayKit townsfolk. Duel Hasan/raider stay procedural.
public static class FreeModels {
  const string KenneyTex = "Ship/Kenney/kenney_watercraft_colormap_galeborne";
  const string KenneyShip = "Ship/Kenney/hasan_dhow_kenney";
  const string FolkA = "Ship/Kenney/kaykit_townsfolk_a";
  const string FolkB = "Ship/Kenney/kaykit_townsfolk_b";
  const string TexA = "Ship/Kenney/kaykit_townsfolk_a_texture";
  const string TexB = "Ship/Kenney/kaykit_hasan_texture";

  public static Transform SpawnKenneyDhow(Transform parent, string name) {
    var prefab = Resources.Load<GameObject>(KenneyShip);
    if (prefab == null) { Debug.LogWarning("GB FreeModels: Kenney prefab missing at " + KenneyShip); return null; }
    var go = Object.Instantiate(prefab, parent);
    go.name = name;
    go.transform.localPosition = Vector3.zero;
    /* FBX: length on Y, height on Z → lay flat so length runs +Z (bow forward) */
    go.transform.localRotation = Quaternion.Euler(-90f, 180f, 0f);
    go.transform.localScale = Vector3.one * 0.55f;
    ApplyTex(go, KenneyTex, creamSail: true);
    foreach (var r in go.GetComponentsInChildren<Renderer>()) r.enabled = true;
    FixLateenSail(go); /* AFTER enable — hides Kenney lateen, spawns cream */
    Debug.Log("GB FreeModels: spawned Kenney dhow tris~" + go.GetComponentsInChildren<MeshFilter>().Length);
    return go.transform;
  }

  public static void ApplyTex(GameObject go, string texPath, bool creamSail = false) {
    var tex = Resources.Load<Texture2D>(texPath);
    if (tex == null) { Debug.LogWarning("GB FreeModels: texture missing " + texPath); return; }
    var lit = Shader.Find("Universal Render Pipeline/Lit");
    foreach (var r in go.GetComponentsInChildren<Renderer>()) {
      var mats = r.materials;
      for (int i = 0; i < mats.Length; i++) {
        var m = mats[i];
        if (lit != null && m.shader != lit) { var nm = new Material(lit); nm.name = m.name; mats[i] = nm; m = nm; }
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        bool sail = creamSail && (r.gameObject.name.ToLower().Contains("lateen") || r.gameObject.name.ToLower().Contains("sail"));
        var col = sail ? new Color(0.93f, 0.88f, 0.76f) : Color.white;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
      }
      r.materials = mats; r.enabled = true;
    }
  }

  /// small distant background townsfolk only — never near the camera / quay close-ups
  public static void SpawnDistantTownsfolk(Transform townRoot) {
    if (townRoot == null) return;
    var a = Resources.Load<GameObject>(FolkA);
    var b = Resources.Load<GameObject>(FolkB);
    if (a == null && b == null) return;
    // far back / side streets: |z| large or high on hills
    var spots = new[] {
      new Vector3(-18f, 0.02f, 42f), new Vector3(16f, 0.02f, 48f), new Vector3(-12f, 0.5f, 55f),
      new Vector3(10f, 0.02f, 60f), new Vector3(-22f, 1.2f, 38f), new Vector3(20f, 0.8f, 52f),
    };
    for (int i = 0; i < spots.Length; i++) {
      var prefab = (i % 2 == 0 ? a : b) ?? a ?? b;
      var go = Object.Instantiate(prefab, townRoot);
      go.name = "DistantFolk" + i;
      go.transform.position = spots[i];
      go.transform.rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360), 0);
      go.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.9f, 1.05f);
      ApplyTex(go, i % 2 == 0 ? TexA : TexB);
      // strip any leftover weapon-ish children by name
      foreach (var t in go.GetComponentsInChildren<Transform>().ToArray()) {
        var n = t.name.ToLower();
        if (n.Contains("staff") || n.Contains("crossbow") || n.Contains("axe") || n.Contains("wand") || n.Contains("hat"))
          Object.Destroy(t.gameObject);
      }
    }
  }

  /// v0.6k3: replace Kenney lateen mesh in place (keep transform so it faces camera) with cream ShipArt.SAIL
  public static void FixLateenSail(GameObject go) {
    if (go == null) return;
    foreach (var t in go.GetComponentsInChildren<Transform>(true)) {
      if (t.name == "LateenBack" || t.name == "SailAnchor") Object.DestroyImmediate(t.gameObject);
    }
    MeshFilter sailMf = null;
    foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true)) {
      var n = mf.gameObject.name.ToLower();
      if (n.Contains("lateen_sail") || (n.Contains("sail") && !n.Contains("yard") && !n.Contains("cream"))) { sailMf = mf; break; }
    }
    // If we already swapped once, find CreamLateen or renamed
    if (sailMf == null) {
      foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        if (mf.gameObject.name == "CreamLateen" || mf.sharedMesh != null && mf.sharedMesh.name.Contains("CreamLateenMesh")) return;
      Debug.LogWarning("GB FixLateenSail: no lateen_sail found"); return;
    }
    // Build double-sided lateen covering original bounds (same local frame → same facing)
    Bounds lb = sailMf.sharedMesh != null ? sailMf.sharedMesh.bounds : new Bounds(Vector3.zero, new Vector3(1, 3, 5));
    Vector3 a, b, c;
    a = lb.center + new Vector3(0, lb.extents.y, 0);
    b = lb.center + new Vector3(0, -lb.extents.y * 0.6f, lb.extents.z);
    c = lb.center + new Vector3(0, -lb.extents.y * 0.4f, -lb.extents.z);
    if (sailMf.sharedMesh != null && sailMf.sharedMesh.isReadable) {
      try {
        var vs = sailMf.sharedMesh.vertices;
        if (vs != null && vs.Length >= 3) {
          // PCA-free: extreme corners of AABB of verts, then force a triangle with area
          Vector3 min = vs[0], max = vs[0];
          foreach (var v in vs) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
          // Lateen along the longest horizontal axis of the AABB
          var size = max - min;
          if (size.z >= size.x) {
            a = new Vector3((min.x + max.x) * 0.5f, max.y, (min.z + max.z) * 0.5f);
            b = new Vector3(min.x, min.y, max.z);
            c = new Vector3(max.x, min.y, min.z);
          } else {
            a = new Vector3((min.x + max.x) * 0.5f, max.y, (min.z + max.z) * 0.5f);
            b = new Vector3(max.x, min.y, max.z);
            c = new Vector3(min.x, min.y, min.z);
          }
          // Ensure non-degenerate: if area tiny, fall back to bounds
          if (Vector3.Cross(b - a, c - a).sqrMagnitude < 0.01f) {
            a = lb.center + Vector3.up * lb.extents.y;
            b = lb.center + new Vector3(lb.extents.x, -lb.extents.y, lb.extents.z);
            c = lb.center + new Vector3(-lb.extents.x, -lb.extents.y, -lb.extents.z);
          }
        }
      } catch { }
    }
    var mb = new MeshB();
    var sailUV = ShipArt.UV(ShipArt.SAIL);
    var nrm = Vector3.Cross(b - a, c - a).normalized;
    if (nrm.sqrMagnitude < 1e-6f) nrm = Vector3.right;
    float thick = 0.06f;
    int N = 8;
    // Two outward shells (±normal) so Lit/cull-back always shows cream — no black backface
    for (int side = -1; side <= 1; side += 2) {
      Vector3 off = nrm * (thick * side);
      Vector3 P(float u, float v) { var p = a + (b - a) * u + (c - a) * v; return p + nrm * (Mathf.Sin(Mathf.PI * Mathf.Clamp01(u + v)) * 0.2f) + off; }
      Vector2 UVp(float u, float v) => new Vector2(sailUV.xMin + sailUV.width * u, sailUV.yMin + sailUV.height * v);
      void T3(float ua, float va, float ub, float vb, float uc, float vc) {
        if (side > 0) mb.Tri(P(ua, va), P(ub, vb), P(uc, vc), UVp(ua, va), UVp(ub, vb), UVp(uc, vc), false);
        else mb.Tri(P(ua, va), P(uc, vc), P(ub, vb), UVp(ua, va), UVp(uc, vc), UVp(ub, vb), false); }
      for (int i = 0; i < N; i++) for (int j = 0; j < N - i; j++) {
        float u0 = i / (float)N, v0 = j / (float)N, u1 = (i + 1) / (float)N, v1 = (j + 1) / (float)N;
        T3(u0, v0, u1, v0, u0, v1);
        if (i + j < N - 1) T3(u1, v0, u1, v1, u0, v1);
      }
    }
    var mesh = mb.Build(); mesh.name = "CreamLateenMesh";
    sailMf.sharedMesh = mesh;
    sailMf.gameObject.name = "CreamLateen";
    /* Runtime cream atlas (ShipHasan asset texture can be missing/stripped in player → pink checkers) */
    ShipArt.Paint(false, out var albedo, out _);
    var atlas = new Texture2D(ShipArt.N, ShipArt.N, TextureFormat.RGBA32, false);
    atlas.name = "CreamSailAtlas"; atlas.SetPixels(albedo); atlas.Apply(false, true);
    var lit = Shader.Find("Universal Render Pipeline/Lit");
    var mat = lit != null ? new Material(lit) : new Material(Shader.Find("Sprites/Default"));
    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", atlas);
    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", atlas);
    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
    if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
    if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
    if (mat.HasProperty("_EmissionColor")) {
      mat.EnableKeyword("_EMISSION");
      mat.SetColor("_EmissionColor", new Color(0.35f, 0.32f, 0.26f));
    }
    var r = sailMf.GetComponent<Renderer>();
    if (r != null) { r.sharedMaterial = mat; r.materials = new[] { mat }; r.enabled = true; }
    else { r = sailMf.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; }
    Debug.Log("GB FixLateenSail: in-place cream lateen tris=" + mb.Tris + " bounds=" + lb.size);
  }
}
