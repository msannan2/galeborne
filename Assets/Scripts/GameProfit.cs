using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Rnd = UnityEngine.Random;

/* research_profit (Game Designer spec, Chief Art Designer art): the first-profitable-sale moment.
   Every sale transaction (sheet Sell N, alley Sell 1 / Sell all) is totalled against the hold's weighted average buy price (AvgCost).
   Profit: '+N profit' in gold over the market, 5-8 brass coins arc into the HUD counter while it ticks up over ~0.6 s, with a clink.
   First profit in a save: one-time toast + 1 fame, flag saved (firstProfitDone). Loss: '−N' muted red, no coins, no sound. */
public partial class Game {
  public const string ProfitHex = "#E0B85A", LossHex = "#A8584A", ProfitOutlineHex = "#3A2618", FirstProfitToast = "First profit! The trade winds favour you.";
  public const float LossAlpha = 0.85f, ProfitOutlinePx = 2f, CoinFlySec = 0.6f, CoinStagger = 0.05f, CounterTickSec = 0.6f;
  public bool firstProfitDone; /* saved */
  float saleRev, saleCost; int saleUnits; /* the open sale transaction */
  public int coinShown = -1; /* >= 0 while the HUD counter is ticking */
  public Text lastProfitText, lastToast; public int lastProfitN, lastCoinFly, clinkCount;
  GameObject gaspLayer; public float gaspStartT = -99, gaspEndT = -99, toastShownT = -99; /* Game Designer: gasp first, toast once it clears */
  int CoinsShown => coinShown >= 0 ? coinShown : coins;
  void ProfitBegin() { saleRev = saleCost = 0; saleUnits = 0; }
  void ProfitTrack(int g, int v) { saleRev += v; saleCost += AvgCost[g]; saleUnits++; }
  /// closes the open sale transaction: N = revenue - units x avg buy price (rounded), shows the profit/loss moment at 'at'
  void ProfitEnd(Vector3 at) {
    if (saleUnits == 0) return; int rev = Mathf.RoundToInt(saleRev); int n = Mathf.RoundToInt(saleRev - saleCost); saleRev = saleCost = 0; saleUnits = 0; lastProfitN = n;
    Debug.Log($"GB PROFIT sale rev {rev} profit {n}");
    if (n == 0) return; /* break-even: no popup */
    bool up = n > 0; var col = UITheme.C(up ? ProfitHex : LossHex); if (!up) col.a = LossAlpha;
    var t = Label(fx, up ? $"+{n:N0} profit" : $"−{-n:N0}", 0, 0, 520, 80, T.fontSizeTitle + 6, up ? ProfitHex : LossHex, TextAnchor.MiddleCenter, true);
    t.name = up ? "ProfitText" : "LossText"; t.color = col; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow;
    var o = t.gameObject.AddComponent<Outline>(); o.effectColor = UITheme.C(ProfitOutlineHex); o.effectDistance = new Vector2(ProfitOutlinePx, -ProfitOutlinePx);
    var o2 = t.gameObject.AddComponent<Outline>(); o2.effectColor = UITheme.C(ProfitOutlineHex); o2.effectDistance = new Vector2(-ProfitOutlinePx, ProfitOutlinePx);
    t.rectTransform.pivot = new Vector2(0.5f, 0.5f); t.rectTransform.position = at; t.rectTransform.anchoredPosition += new Vector2(0, 62); /* Chief Art Designer: centred just above the Sell price */ fx.SetAsLastSibling(); lastProfitText = t;
    if (Application.isPlaying) StartCoroutine(FloatText(t, 1.8f));
    if (!up) { lastCoinFly = 0; return; }
    int k = Mathf.Clamp(5 + n / 40, 5, 8); lastCoinFly = k; var spr = Resources.Load<Sprite>("Art/UI/profit_coin");
    for (int i = 0; i < k; i++) { var c = new GameObject("profit_coin_fly", typeof(RectTransform)).GetComponent<RectTransform>(); c.SetParent(fx, false); c.position = at; float s = Rnd.Range(24f, 32f); c.sizeDelta = new Vector2(s, s);
      var ci = c.gameObject.AddComponent<Image>(); ci.sprite = spr; ci.raycastTarget = false; ci.preserveAspect = true; if (!spr) ci.color = UITheme.C(ProfitHex);
      if (Application.isPlaying) StartCoroutine(FlyCoin(c, i * CoinStagger, i == 0)); }
    if (Application.isPlaying) StartCoroutine(TickCounter(coins - rev, coins));
    if (!firstProfitDone) { firstProfitDone = true; fame++; Debug.Log($"GB PROFIT first profit: +1 fame (fame {fame})"); ShowToast(FirstProfitToast); Save(); }
  }
  IEnumerator FloatText(Text t, float d) { var r = t.rectTransform; var p0 = r.anchoredPosition; var c0 = t.color;
    for (float x = 0; x < d && t; x += Time.deltaTime) { float k = x / d; r.anchoredPosition = p0 + new Vector2(0, 60 * k); t.color = new Color(c0.r, c0.g, c0.b, c0.a * (k < 0.7f ? 1 : 1 - (k - 0.7f) / 0.3f)); yield return null; }
    if (t) Destroy(t.gameObject); }
  IEnumerator FlyCoin(RectTransform c, float delay, bool clink) {
    yield return new WaitForSeconds(delay); if (!c) yield break; Vector3 p0 = c.position; var off = new Vector3(Rnd.Range(-80f, 80f), Rnd.Range(80f, 160f), 0) * root.localScale.x; float spin = Rnd.Range(-360f, 360f);
    for (float t = 0; t < CoinFlySec && c; t += Time.deltaTime) { float k = t / CoinFlySec; Vector3 p1 = coinText ? coinText.rectTransform.position : p0; c.position = Vector3.Lerp(p0, p1, k * k) + off * Mathf.Sin(k * Mathf.PI); c.localRotation = Quaternion.Euler(0, 0, spin * k); yield return null; }
    if (clink) PlayClink(); if (c) Destroy(c.gameObject); }
  IEnumerator TickCounter(int from, int to) {
    counterDrawn = coins; SetCounter(from);
    for (float t = 0; t < CounterTickSec; t += Time.deltaTime) { SetCounter(Mathf.RoundToInt(Mathf.Lerp(from, to, t / CounterTickSec))); yield return null; }
    SetCounter(coins); coinShown = -1; }
  int counterDrawn = -1;
  /// writes v into the HUD line in place (no full Refresh); a Refresh mid-tick redraws coinShown, which is the same v
  void SetCounter(int v) { string a = $"{counterDrawn:N0} coins", b = $"{v:N0} coins"; coinShown = v;
    if (coinText) { var s = coinText.text; int i = s.IndexOf(a); if (i >= 0 && (i == 0 || s[i - 1] == ' ')) coinText.text = s.Substring(0, i) + b + s.Substring(i + a.Length); } counterDrawn = v; }
  void ShowToast(string s) { if (Application.isPlaying) StartCoroutine(ToastAfterGasp(s)); else ToastNow(s); }
  IEnumerator ToastAfterGasp(string s) { yield return null; /* a gasp started by the same sale is already up */ while (gaspLayer) yield return null; ToastNow(s); }
  /// Chief Art Designer: a strip just under the HUD bar (above the merchant card), ~90% of the screen wide, one line, best-fit shrink
  void ToastNow(string s) {
    var p = new GameObject("FirstProfitToast", typeof(RectTransform)).GetComponent<RectTransform>(); p.SetParent(fx, false); p.anchorMin = new Vector2(0.05f, 1); p.anchorMax = new Vector2(0.95f, 1); p.pivot = new Vector2(0.5f, 1);
    float h = 64; if (topBar) { var c = new Vector3[4]; topBar.GetWorldCorners(c); var lp = fx.InverseTransformPoint(c[0]); p.anchoredPosition = new Vector2(0, lp.y - fx.rect.yMax - 6); h = Mathf.Max(48, topBar.rect.height); } else p.anchoredPosition = new Vector2(0, -90);
    p.sizeDelta = new Vector2(0, h); Box(p, T.panel, T.gold);
    var tr = new GameObject("T", typeof(RectTransform)).GetComponent<RectTransform>(); tr.SetParent(p, false); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(16, 4); tr.offsetMax = new Vector2(-16, -4);
    var t = Txt(tr, s, T.fontSize + 4, ProfitHex, TextAnchor.MiddleCenter, true); t.horizontalOverflow = HorizontalWrapMode.Wrap; t.resizeTextForBestFit = true; t.resizeTextMinSize = 12; t.resizeTextMaxSize = T.fontSize + 4; /* best fit shrinks rather than wrapping (height fits one line) */
    var o = t.gameObject.AddComponent<Outline>(); o.effectColor = UITheme.C(ProfitOutlineHex); o.effectDistance = new Vector2(ProfitOutlinePx, -ProfitOutlinePx);
    foreach (var g in p.GetComponentsInChildren<Graphic>()) g.raycastTarget = false; lastToast = t; toastShownT = Time.unscaledTime; fx.SetAsLastSibling(); Debug.Log($"GB PROFIT toast: {s} (gasp ended {gaspEndT:0.00}, toast {toastShownT:0.00})");
    if (Application.isPlaying) StartCoroutine(KillAfter(p.gameObject, 3.2f)); }
  IEnumerator KillAfter(GameObject g, float s) { yield return new WaitForSeconds(s); if (g) Destroy(g); }
  /* procedural coin clink (no SFX assets in the project, like the duel clang): two bright partials, fast decay */
  AudioClip clinkClip;
  void PlayClink() { clinkCount++;
    if (clinkClip == null) { int sr = 44100, n = (int)(sr * 0.22f); var d = new float[n];
      for (int i = 0; i < n; i++) { float t = i / (float)sr, e = Mathf.Exp(-t * 22f), e2 = Mathf.Exp(-t * 40f), t2 = Mathf.Max(0, t - 0.05f), e3 = t < 0.05f ? 0 : Mathf.Exp(-t2 * 28f);
        d[i] = 0.4f * e * (0.5f * Mathf.Sin(2 * Mathf.PI * 3150f * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * 5240f * t)) + 0.2f * e2 * Mathf.Sin(2 * Mathf.PI * 7900f * t) + 0.25f * e3 * Mathf.Sin(2 * Mathf.PI * 3620f * t2); }
      clinkClip = AudioClip.Create("gb_clink", n, 1, sr, false); clinkClip.SetData(d, 0); }
    if (clangSrc == null) clangSrc = gameObject.AddComponent<AudioSource>(); clangSrc.PlayOneShot(clinkClip, 0.7f); Debug.Log("GB PROFIT clink"); }
}
