using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Rnd = UnityEngine.Random;

/// v0.6h Act 1 pass: three goals, prologue choice (Honor/Fear), Yusuf and the seal, Farida's rugs run, jackpot spill.
/// Numbers live in design/v06_settings.json -> "act1". Art slots (swap the PNG, keep the name):
///   Resources/Art/Portraits/yusuf.png, farida.png, farida_rival.png (768x1024, teak-and-brass frame),
///   Resources/Art/alley_debal.jpg, alley_muscat.jpg, Resources/Art/coin_spill.png
public partial class Game {
  public int honor, fear; public bool yusufSeal; public int faridaState; /* 0 not offered, 1 ally (ran the rugs), 2 rival (turned in) */
  public bool openingGpSold; /* v0.6h4: true only after a real Muscat alley gunpowder sale (not confiscation) */
  float A1(string k, float d) => Design.F(DZ?.Path(DZ.settings, "act1." + k), d);
  string A1s(string k, string d) => Design.Path0(DZ, "act1." + k, d);
  public void Act1Init() { honor = (int)A1("honorStart", 0); fear = (int)A1("fearStart", 0); yusufSeal = false; faridaState = 0; openingGpSold = false; }
  int Rugs => G("rugs");

  // ---------- goals: short = current sale target, medium = next unlock, long = story ----------
  public (string, string, string) Goals3() {
    if (!InOpening && JournalUnlocked) return JournalGoalsSummary();
    string l = yusufSeal ? A1s("goalLong", "Find out what House Qarin is hoarding.") : A1s("goalLongBeforeYusuf", "Who burned your ship? The Qarin seal.");
    /* v0.6h4: medium clears on the Muscat gunpowder sale (openingGpSold), not at the tavern; confiscation keeps the gunpowder goal */
    bool midDone = openingGpSold || (fallbackFired && storyStep >= 7);
    string m = !midDone ? A1s("goalMediumOpening", "Get the gunpowder to Muscat.") : faridaState == 0 ? A1s("goalMediumAfter", "Run Farida's rugs.") : A1s("goalMediumAfterFarida", "Save for a bigger ship.");
    string s;
    bool needGpAgain = !openingGpSold && !fallbackFired && jackpotStartCoins > 0 && hold[Gp] == 0 && (storyStep >= 6 || !InOpening);
    if (needGpAgain) s = (InOpening && OpenFlag("openingDebalAlleyStocksGunpowder")) ? "Buy gunpowder in Debal" : "Buy gunpowder in Gwadar";
    else if (InOpening) s = new[] { "Buy dates and salt in Gwadar", "Sail to Debal", "Sell dates and salt in Debal", "Hear the talk at Debal's tavern",
        rumorHeard >= 2 && hold[Gp] == 0 ? "Buy copper in Gwadar" : "Buy gunpowder in Debal's alley", fallbackFired || hold[Gp] == 0 ? "Sail to Muscat with the copper" : "Sail to Muscat with the gunpowder",
        fallbackFired ? "Sell copper in Muscat (x2)" : "Sell gunpowder in Muscat's alley", "Ask Muscat's tavern about the seal" }[Mathf.Clamp(storyStep, 0, 7)];
    else if (hold[Rugs] > 0) s = "Sell rugs in Muscat's alley (x3)";
    else if (!yusufSeal) s = "Show Yusuf the seal in Debal";
    else if (faridaState == 0) s = "Meet Farida in Debal's alley";
    else s = HoldUsed == 0 ? "Buy where it's piled high" : "Sell where they're begging";
    return (s, m, l); }

  // ---------- art slots ----------
  Texture2D ArtSlot(string path, string fallback = null) => Resources.Load<Texture2D>(path) ?? (fallback != null ? Resources.Load<Texture2D>(fallback) : null);
  /// a story card with a portrait slot on the right (AskCard + portrait, body narrowed so text never runs under it)
  IEnumerator PortraitCard(string title, string body, Texture2D face, params (string, Action)[] opts) {
    var co = AskCard(title, body, opts); bool more = co.MoveNext();
    if (dialog != null && face != null) { var inner = dialog.Find("in") as RectTransform; if (inner) { foreach (var t in inner.GetComponentsInChildren<Text>()) if (t.GetComponentInParent<Button>() == null && t.rectTransform.sizeDelta.y > 200) t.rectTransform.sizeDelta = new Vector2(700, t.rectTransform.sizeDelta.y);
        var fr = Px(inner, "Portrait", 752, 100, 222, 296); var teak = fr.gameObject.AddComponent<Image>(); teak.color = new Color(0.24f, 0.14f, 0.08f); teak.raycastTarget = false;
        var brass = Px(fr, "brass", 7, 7, 208, 282).gameObject.AddComponent<Image>(); brass.color = new Color(0.72f, 0.56f, 0.3f); brass.raycastTarget = false;
        var im = Px(fr, "img", 10, 10, 202, 276).gameObject.AddComponent<RawImage>(); im.texture = face; im.raycastTarget = false; /* teak frame, brass trim, 3:4 portrait */ } }
    while (more) { yield return co.Current; more = co.MoveNext(); } }

  // ---------- prologue choice ----------
  IEnumerator PrologueChoice() {
    yield return AskCard("The raider in the water", "The raider leaps overboard and thrashes in the black water, choking on smoke. A rope hangs at your feet.\n\nSpare him: +Honor (he may return).\nLet him drown: +Fear (pirates hesitate later).",
      ("Spare him", () => { honor += (int)A1("spareHonor", 1); msg = "You throw the rope. He climbs into the dark without a word."; }),
      ("Let him drown", () => { fear += (int)A1("drownFear", 1); msg = "You let the sea take him. Your crew goes quiet."; })); }

  // ---------- Yusuf and the seal (first Debal visit after the opening payout) ----------
  void YusufCheck() { if (yusufSeal || InOpening || !qarinLetter || port != Debal || sailing || fighting || dialog != null || onTitle) return; yusufSeal = true; StartCoroutine(YusufSeal()); }
  IEnumerator YusufSeal() {
    var face = ArtSlot("Art/Portraits/yusuf");
    yield return PortraitCard("Yusuf, harbour master of Debal", "You set the scorched seal on Yusuf's table.\n\nYUSUF: \"Put that away. Qarin's seal opens doors... and graves.\"", face, ("Go on", null));
    yield return PortraitCard("Yusuf, harbour master of Debal", "YUSUF: \"They're buying every warehouse from Gwadar to Muscat. Nobody knows why.\"\n\nNew story goal: " + A1s("goalLong", "Find out what House Qarin is hoarding."), face, ("I'll find out", null));
    msg = "Story goal: " + A1s("goalLong", "Find out what House Qarin is hoarding."); Save(); Refresh(); }

  // ---------- Farida's rugs run ----------
  /// opening Debal's back alley after the opening: Farida offers the rugs once; the choice is permanent
  public bool FaridaOffer() { if (InOpening || faridaState != 0 || port != Debal || dialog != null) return false; StartCoroutine(FaridaCard()); return true; }
  IEnumerator FaridaCard() {
    var face = FaridaPortrait();
    yield return PortraitCard("Farida of the back alley", "FARIDA: \"Rugs. Muscat's banned them, so Muscat pays triple. Interested?\"\nHASAN: \"And if customs finds them?\"\nFARIDA: \"Then they're very nice rugs you bought for your mother.\"", face,
      ("Run the rugs", () => { faridaState = 1; msg = $"Farida grins. \"Partners, then.\" Her papers cost {A1("faridaAllyPapersDiscount", 0.2f) * 100:0}% less from now on."; }),
      ("Turn her in", () => { faridaState = 2; int c = (int)A1("turnInCoins", 500); coins += c; rep[Muscat] += (int)A1("turnInSultanStanding", 10); msg = $"Customs pay you {c} coins and the Sultan's men remember your name. Farida won't forget either."; }));
    Save(); Refresh(); }
  /// Farida's alley list: rugs only once she's your ally (a rival won't sell to you)
  /// ally portrait until turned in; rival portrait from the moment FaridaState == 2
  Texture2D FaridaPortrait() => faridaState == 2
    ? ArtSlot("Art/Portraits/farida_rival", "Art/Portraits/farida")
    : ArtSlot("Art/Portraits/farida", "Keepers/k2_3");
  Texture2D AlleyBackdrop() => port == Debal ? ArtSlot("Art/alley_debal")
    : port == Muscat ? ArtSlot("Art/alley_muscat") : null; /* v0.6h4: Muscat alley painting */
  /// v0.6h2: Farida's rugs banned in Muscat sell at rugsMuscatBannedSellMult x the Debal buy price (no Muscat markup stacked on top);
  /// the unit-drop after each sale still applies via Muscat's price ratio. Gunpowder and other contraband keep price x contraband.bannedSellMult.
  bool RugsBanSale(int g) => g == Rugs && Rugs >= 0 && port == Muscat && Banned(g, port) && !InOpening;
  int RugsBanPrice() => Mathf.FloorToInt(price[Debal, Rugs] * A1("rugsBuyMult", 1f) * A1("rugsMuscatBannedSellMult", 3f) * Mathf.Min(1f, price[Muscat, Rugs] / Mathf.Max(0.01f, basePrice[Muscat, Rugs])));
  bool RugsHidden(int p) => p == Debal && !InOpening && faridaState != 1;
  public int FakersPrice => Mathf.CeilToInt(S.customs.fakersCratePrice * (faridaState == 1 ? 1 - A1("faridaAllyPapersDiscount", 0.2f) : 1));

  // ---------- jackpot spectacle ----------
  string lastJackpot; float jackpotT = -99;
  void JackpotCheck(int g, int v) { float cost = AvgCost[g]; if (cost <= 0 || v < A1("jackpotMult", 2.5f) * cost) return; float x = v / cost;
    lastJackpot = $"JACKPOT {S.goods[g].id} sold at {v} ({x:0.00}x buy {cost:0})"; if (Time.unscaledTime - jackpotT < 2f) return; jackpotT = Time.unscaledTime; StartCoroutine(JackpotFx(x)); }
  IEnumerator JackpotFx(float x) {
    var layer = Stretch(root, "JackpotFx", 0, 0, 0, 0); layer.SetAsLastSibling(); var cg = layer.gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false;
    var spill = ArtSlot("Art/coin_spill"); RectTransform sr = null; if (spill) { sr = Px(layer, "spill", 60, 620, 960, 520); var ri = sr.gameObject.AddComponent<RawImage>(); ri.texture = spill; ri.raycastTarget = false; } /* v0.6h4: lower, over ledger rows so Farida/merchant quote stays clear */
    var strip = Px(layer, "gaspStrip", 0, 850, 1080, 110); var si = strip.gameObject.AddComponent<Image>(); si.color = new Color(0.07f, 0.05f, 0.03f, 0.88f); si.raycastTarget = false;
    var line1 = Px(strip, "trimT", 0, 0, 1080, 4).gameObject.AddComponent<Image>(); line1.color = UITheme.C(T.brass); line1.raycastTarget = false; var line2 = Px(strip, "trimB", 0, 106, 1080, 4).gameObject.AddComponent<Image>(); line2.color = UITheme.C(T.brass); line2.raycastTarget = false;
    var gasp = Label(layer, $"<b>The merchant gasps!</b>  ×{x:0.0}", 0, 860, 1080, 90, T.fontSizeTitle + 6, T.gold, TextAnchor.MiddleCenter, true); gasp.raycastTarget = false; gasp.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
    float dur = A1("jackpotSpillSeconds", 1.4f);
    for (float t = 0; t < dur && layer; t += Time.unscaledDeltaTime) { float k = t / dur; /* coins drop onto the ledger with a small bounce, then fade */
      if (sr) { float drop = k < 0.25f ? 1 - k / 0.25f : 0; float bounce = k >= 0.25f && k < 0.45f ? Mathf.Sin((k - 0.25f) / 0.2f * Mathf.PI) * 0.06f : 0; sr.anchoredPosition = new Vector2(60, -620 + 200 * drop - 400 * bounce); sr.localScale = Vector3.one * (0.85f + 0.15f * Mathf.Min(1, k * 4)); }
      gasp.rectTransform.localScale = Vector3.one * (1 + 0.15f * Mathf.Sin(Mathf.Min(1, k * 3) * Mathf.PI)); cg.alpha = k < 0.75f ? 1 : 1 - (k - 0.75f) / 0.25f; yield return null; }
    if (layer) Destroy(layer.gameObject); }
}
