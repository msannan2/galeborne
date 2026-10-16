using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Rnd = UnityEngine.Random;

public partial class Game {
  const float K = 0.45f; // metres -> world units
  public CombatCore core; public bool fighting, combatPaused; Transform pirateShip; RectTransform cui; Vector3 combatShipScale0;
  Text hudH, hudP, hudMsg, windTxt, fireTxt; Image reloadRing, rudderBar, windArrow; Button boardBtn; Image[] ammoBtns = new Image[4]; Text[] ammoTxt = new Text[4];
  RectTransform[] aimChips = new RectTransform[4]; bool firePressed, boardPressed; float camYaw; int pirateGood, pirateQty, pirateGood2, pirateQty2;
  float dragX0; bool dragging; Image fireBg; /* gamedev_rudder: see ReadSteer */
  static readonly string[] PartNames = { "Hull", "Sails", "Guns", "Crew" };

  IEnumerator PirateEncounter() {
    int cargo = HoldUsed, toll = Mathf.CeilToInt(cargo * S.combat.tollCargoPct);
    float windFactor = Rnd.value; float flee = Mathf.Clamp01(S.combat.encounterFleeBase + S.combat.fleeWindWeight * windFactor); // offered at the encounter; separate from a pirate's in-fight escape chance (fleeBase)
    int choice = 0;
    yield return Ask("Pirate dhow off the bow", $"A dark-sailed dhow runs up the black flag and turns to cut you off.\n\nFlee: {flee * 100:0}% with this wind.  Toll: {toll} of your {cargo} cargo.",
      ("Fight", () => choice = 0), ($"Flee {flee * 100:0}%", () => choice = 1), (cargo > 0 ? "Pay toll" : "Pay nothing", () => choice = 2));
    if (choice == 2) { PayCargo(toll); msg = toll > 0 ? $"You hand over {toll} cargo. The pirates sheer off." : "Your hold is empty; they jeer and sheer off."; yield break; }
    if (choice == 1 && Rnd.value < flee) { msg = "Hasan catches the gust and the pirate falls astern."; yield break; }
    if (choice == 1) msg = "The wind fails you. They close to fight!";
    yield return Fight();
  }
  void PayCargo(int n, bool spareGp = false, HashSet<int> keep = null) { int gp = G("gunpowder"); for (int i = 0; i < n && HoldUsed > 0; i++) { var held = LosableGoods() /* gamedev_t15_storysafe */ .Where(g => !(spareGp && g == gp) && (keep == null || !keep.Contains(g)) /* gamedev_t8 */).ToList(); if (held.Count == 0) break; hold[held[Rnd.Range(0, held.Count)]]--; } } /* the opening spares the gunpowder on a first-ambush loss */
  int AddCargo(int g, int q) { q = Mathf.Min(q, S.holdSize - HoldUsed); if (q > 0) hold[g] += q; return Mathf.Max(0, q); }

  public IEnumerator Fight(bool scripted = false) {
    fighting = true; core = new CombatCore(S.combat, Rnd.Range(0, int.MaxValue), crewInjured, PirateTierNow, difficulty); ApplyDamageToFight(); fightsFought++; if (duelsFought < S.combat.noviceDuels) core.noviceZoneBonus = S.combat.noviceDuelBonus;
    core.OnBroadside += (a, b, shots, hits) => { Puffs(a == core.H ? ship : pirateShip, b == core.H ? ship : pirateShip, hits); if (a == core.H) hudMsg.text = hits > 0 ? $"Broadside! {hits} of {shots} shots hit their {PartNames[(int)a.aim].ToLower()}." : "Broadside falls short."; else hudMsg.text = hits > 0 ? $"The pirate hits you {hits} times!" : "Their broadside splashes wide."; };
    pirateGood = Rnd.Range(0, S.goods.Length); pirateQty = Rnd.Range(6, 13); pirateGood2 = Rnd.Range(0, S.goods.Length); pirateQty2 = Rnd.Range(0, 6);
    if (pirateShip == null) pirateShip = Dhow.Build(sea3D, T, true, "PirateDhow"); pirateShip.gameObject.SetActive(true);
    sea3D.gameObject.SetActive(true);
    /* v0.6k: backdrop must stay CLEAR so 3D sea shows through Overlay canvas; cam/strip cover parchment gaps */
    backdrop.sprite = null; backdrop.color = Color.clear; if (dimmer) dimmer.gameObject.SetActive(false);
    cam.clearFlags = CameraClearFlags.SolidColor;
    /* v0.6k3: skyHorizon is parchment beige — use zenith blue so no tan strip at top; j camera unchanged */
    cam.backgroundColor = UITheme.C(T.skyZenith);
    RenderSettings.fogColor = UITheme.C(T.skyZenith); RenderSettings.fogEndDistance = 260;
    { var skyR = sea3D ? sea3D.Find("SeaSky")?.GetComponent<Renderer>() : null;
      if (skyR && skyR.sharedMaterial) { var sm = skyR.material; if (sm.HasProperty("_Horizon")) sm.SetColor("_Horizon", UITheme.C(T.skyZenith)); if (sm.HasProperty("_Zenith")) sm.SetColor("_Zenith", UITheme.C(T.skyZenith)); } }
    if (sea3D) { var sky = sea3D.Find("Sphere"); /* sky dome from BuildWorld */ if (sky == null) foreach (Transform c in sea3D) if (c.name.Contains("Sky") || c.GetComponent<MeshFilter>()) { /* keep */ break; } }
    Clear(content); BuildCombatUI(); waterMat.SetFloat("_Speed", 0); EnsureKenneyDhowOnShip(); HideAllSeaLandmarks(); /* v0.6k3: no shore fort in fight */
    if (ship) { combatShipScale0 = ship.localScale; ship.localScale = combatShipScale0 * 1.45f; } /* v0.6k2: enlarge Hasan only; camera stays j high 3/4 */
    camYaw = CombatCore.Bearing(core.P.x - core.H.x, core.P.z - core.H.z);
    if (ambushForced) hudMsg.text = "Tutorial: tap CHAIN and aim at their SAILS to slow her; then GRAPE at the CREW, or BOARD when she is crippled.";
    Outcome o = Outcome.None; float fightT = 0; float capS = Design.F(DZ?.Path(DZ.settings, "ambush.fightCapSeconds"), 60f); bool capFog = ambushForced && InOpening && OpenFlag("openingAmbushNoDefeat");
    if (ambushForced) { var sc = S.combat; core.SetupStoryApproach(sc.storyStartRange > 0 ? sc.storyStartRange : 140); core.ApplyStoryBalance(); /* gamedev_balance: story fog fight only */ core.idleTollSeconds = sc.storyIdleTollSeconds > 0 ? sc.storyIdleTollSeconds : 30; capS = sc.storySafetyCapSeconds > 0 ? sc.storySafetyCapSeconds : 180; camYaw = CombatCore.Bearing(core.P.x - core.H.x, core.P.z - core.H.z);
      if (ShowSteerHint(true)) { steerHint.SetActive(true); steerHintSeen = true; Debug.Log("GB RUDDER steer hint shown"); } /* gamedev_rudder: once per save */
      Debug.Log($"GB AMBUSH start range={core.Range:0.0} idleToll={core.idleTollSeconds:0} cap={capS:0}"); } /* gamedev_ambush: Game Designer ruling (140 m start, 30 s idle toll, the 60 s cap no longer ends the story fight) */
    while (o == Outcome.None) {
      fightT += Time.deltaTime; if (capS > 0 && fightT > capS) { o = capFog ? Outcome.HasanStruck : Outcome.Escaped; if (playthrough) PLog2("fight cap", $"{capS:0} s reached: auto-resolved to " + (capFog ? "the pirate toll" : "the pirate breaking off")); break; } /* the unlosable opening fight never drags on */
      if (!combatPaused) {
        ReadSteer(); core.H.ammo = Mathf.Clamp(core.H.ammo, 0, 3);
        if (firePressed) { firePressed = false; if (!core.FireTap()) hudMsg.text = FireBlockReason(); /* gamedev_toll_fix: CombatCore.FireTap resets the idle clock on any in-range tap and on every shot */ }
        float dt = Mathf.Min(Time.deltaTime, 0.1f); o = core.Step(dt); if (o == Outcome.HasanStruck && core.idleTollFired) { if (!capFog) o = Outcome.Escaped; Debug.Log($"GB AMBUSH idle toll after {core.idleT:0.0} s idle (fight {core.t:0.0} s)"); if (playthrough) PLog2("fight idle", $"{core.idleTollSeconds:0} s without closing or firing"); }
        if (o == Outcome.None && boardPressed) { boardPressed = false; if (core.BoardAllowed(core.H, core.P)) o = Outcome.BoardReady; }
      }
      UpdateCombatView(); yield return null; }
    if (o == Outcome.Sunk) yield return SinkCinematic(core.sankInBreach); else yield return new WaitForSeconds(0.8f);
    StoreDamageFromFight(o); yield return Resolve(o);
    if (o == Outcome.Sunk || o == Outcome.Surrender || o == Outcome.TollPaid || ((o == Outcome.BoardReady || o == Outcome.HasanBoarded || o == Outcome.Disabled) && duelWon)) fame++;
    { var st = canvasRoot ? canvasRoot.Find("CombatSkyStrip") : null; if (st) Destroy0(st.gameObject); }
    HideRangeRing(); /* gamedev_ring */ Destroy0(cui.gameObject); cui = null; pirateShip.gameObject.SetActive(false); if (ship && combatShipScale0 != Vector3.zero) ship.localScale = combatShipScale0; waterMat.SetFloat("_Speed", 3); waterMat.SetVector("_Offset", Vector4.zero); backdrop.color = Color.clear;
    fighting = false; Refresh();
  }
  int crateBonus;
  /// short sinking cinematic: she rolls and settles while the camera eases round; a ship sunk through her glowing breach spills crates you sail through
  IEnumerator SinkCinematic(bool breach) {
    inCinematic = true; HideRangeRing(); /* gamedev_ring */ foreach (var b in shipBars) b.gameObject.SetActive(false); aimGlow.gameObject.SetActive(false); breachGlow.gameObject.SetActive(false); crateBonus = 0; var crates = new List<Transform>(); var mat = Resources.Load<Material>("Mats/ShipPirate");
    hudMsg.text = breach ? "Through the breach! She's going down — sail through the floating crates." : "She's going down!";
    Vector3 p0 = pirateShip.position; var r0 = pirateShip.rotation; Vector3 c0 = cam.transform.position;
    for (float t = 0; t < 3.2f; t += Time.deltaTime) { float k = t / 3.2f;
      pirateShip.position = p0 + Vector3.down * (k * k * 9f); pirateShip.rotation = r0 * Quaternion.Euler(-k * 25f, 0, k * 35f);
      cam.transform.position = Vector3.Lerp(c0, p0 + (c0 - p0).normalized * 30f + Vector3.up * 10f, Mathf.SmoothStep(0, 1, k)); cam.transform.LookAt(p0 + Vector3.up * 2);
      if (breach && crates.Count < 6 && t > 0.4f + crates.Count * 0.3f) { var g = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(g.GetComponent<Collider>()); g.GetComponent<Renderer>().sharedMaterial = mat;
        g.transform.localScale = Vector3.one * 1.1f; g.transform.position = p0 + new Vector3(Rnd.Range(-5f, 5f), 0.2f, Rnd.Range(-5f, 5f)); g.transform.rotation = Quaternion.Euler(0, Rnd.Range(0, 360f), 8); crates.Add(g.transform); }
      foreach (var cr in crates) cr.position += Vector3.up * Mathf.Sin(Time.time * 2 + cr.GetInstanceID()) * 0.004f;
      yield return null; }
    if (breach) { // Hasan sails through the debris: each crate drifts alongside and is hauled aboard
      for (float t = 0; t < 2.4f; t += Time.deltaTime) { ship.position += ship.forward * Time.deltaTime * 4f;
        foreach (var cr in crates) if (cr) { cr.position = Vector3.MoveTowards(cr.position, ship.position + Vector3.up * 0.5f, Time.deltaTime * 9f); if ((cr.position - ship.position).magnitude < 1.5f) { Destroy(cr.gameObject); crateBonus++; hudMsg.text = $"Crate hauled aboard! ({crateBonus})"; } }
        yield return null; }
      foreach (var cr in crates) if (cr) { Destroy(cr.gameObject); crateBonus++; } }
    pirateShip.rotation = r0; inCinematic = false; yield return new WaitForSeconds(0.3f); }
  string FireBlockReason() {
    var H = core.H; var P = core.P; if (H.reloadT > 0) return "Reloading…"; if (H.gunHp <= 0) return "Your guns are wrecked.";
    if (H.ammo == 3 && H.fireLeft <= 0) return "No fire shot left.";
    if (!core.InArc(H, P)) return "Not in arc: turn to bring them abeam (within 50°)."; return OutOfRangeMsg(); } /* gamedev_ring: same source as the ring */
  /// gamedev_ambush: both numbers, the distance exactly as the plaque shows it (ceil) and the loaded shot's reach
  public string OutOfRangeMsg() => $"Out of range: {Mathf.CeilToInt(core.FireDist(core.H, core.P))} m, {S.combat.ammo[core.H.ammo].name} reaches {core.FireRange(core.H):0} m";

  IEnumerator Resolve(Outcome o) {
    var c = S.combat; string pg = S.goods[pirateGood].name;
    switch (o) {
      case Outcome.Sunk: { EgFightWon("sunk"); /* gamedev_end1 */ int q = AddCargo(pirateGood, Mathf.RoundToInt(pirateQty * c.sinkCargoPct)); int coinsWon = Rnd.Range((int)c.sinkCoinsMin, (int)c.sinkCoinsMax + 1); coins += coinsWon;
        yield return Ask("She goes down", $"The pirate dhow settles by the stern. You fish {q} {pg} and {coinsWon} coins from the wreckage." + (crateBonus > 0 ? $"\n\n{crateBonus} floating crates hauled aboard: +{AddCargo(pirateGood2, crateBonus)} {S.goods[pirateGood2].name} and {crateBonus * 25} coins." : ""), ("Continue", null)); if (crateBonus > 0) coins += crateBonus * 25; crateBonus = 0; break; }
      case Outcome.Surrender:
      case Outcome.Disabled: { bool board = false;
        yield return Ask("Colours struck", (o == Outcome.Surrender ? "Her crew is broken and hauls down the flag." : "Crippled and outnumbered, the pirate hauls down her flag.") +
          "\n\nSurrender: their hold and the ship.\nBoard: also the captain, his strongbox and a charm — but you must win the duel.", ("Accept surrender", null), ("Board", () => board = true));
        if (board) { yield return BoardFlow(); break; } EgFightWon("surrender"); /* gamedev_end1 */
        int q = AddCargo(pirateGood, pirateQty) + AddCargo(pirateGood2, pirateQty2); coins += (int)c.prizeShipCoins; string extra = "";
        if (Rnd.value < c.surrenderCrewJoinChance) { crewInjured = false; extra = " A few of their sailors sign on with Hasan."; }
        yield return Ask("Spoils", $"They hand over their hold ({q} goods) and you sell the ship as a prize for {c.prizeShipCoins:0} coins.{extra}", ("Continue", null)); break; }
      case Outcome.HasanStruck: yield return Lose("Hasan's crew can fight no more and you strike your colours."); break;
      case Outcome.BoardReady: yield return BoardFlow(); break;
      case Outcome.TollPaid: { EgFightWon("toll"); /* gamedev_end1 */ int q = AddCargo(pirateGood, Mathf.CeilToInt(pirateQty * c.tollLootFrac)); int pay = Mathf.RoundToInt(c.prizeShipCoins * c.tollLootFrac); coins += pay;
        yield return Ask("They pay a toll", $"Beaten and with dusk coming, the pirate captain strikes his flag and offers a toll to be let go: {pay} coins and {q} {pg}.", ("Accept", null)); break; }
      case Outcome.Escaped: yield return Ask("She escapes", "The pirate claws away out of range. She will remember you.", ("Continue", null)); break;
      case Outcome.HasanBoarded: { yield return Duel(false);
        if (duelWon) EgFightWon("boarders repelled"); /* gamedev_end1 */
        if (duelWon) yield return Ask("Boarders repelled", "Hasan's crew throw the boarders back. The pirate cuts loose and flees.", ("Continue", null)); else yield return Lose("The pirates swarm aboard."); break; }
      case Outcome.HasanSunk: yield return Lose("Your dhow is holed and wallowing. A passing fisher tows you on."); break;
      default: yield return Ask("Night falls", "Both ships lose each other in the dark.", ("Continue", null)); break;
    }
  }
  IEnumerator BoardFlow() {
    var c = S.combat; yield return Duel(true);
    if (!duelWon) { yield return Lose("Their captain's blade drives you back to your own deck."); yield break; }
    EgFightWon("ship taken"); /* gamedev_end1 */ if (ambushForced) boardedStoryPirate = true;
    int q = AddCargo(pirateGood, pirateQty) + AddCargo(pirateGood2, pirateQty2); int box = Rnd.Range((int)c.strongboxMin, (int)c.strongboxMax + 1); coins += (int)c.prizeShipCoins + box;
    var ch = c.charms[Rnd.Range(0, c.charms.Length)]; charms.Add(ch); string charm = $" The captain's strongbox holds {box} coins and {ch}.";
    yield return Ask("Ship taken", $"The pirate dhow is yours. You take {q} goods and sell her as a prize for {c.prizeShipCoins:0} coins.{charm}\n\nTheir captain kneels before Hasan.",
      ($"Ransom ({c.ransom:0})", () => { coins += (int)c.ransom; msg = "The captain's family pays the ransom."; }),
      ("Recruit", () => { msg = "The pirate captain joins your crew, grumbling."; charms.Add("Turncoat's Oath"); }),
      ($"Bounty ({c.bounty:0})", () => { coins += (int)c.bounty; rep[dest] += 5; msg = "You hand him to the harbour master for the bounty."; }));
  }
  IEnumerator Lose(string why) {
    if (InOpening && OpenFlag("openingAmbushNoDefeat")) { int toll = Mathf.Min(coins, Mathf.Max(20, Mathf.RoundToInt(coins * 0.1f))); coins -= toll; storyAmbushDone = true; if (playthrough) PLog2("pirate toll", $"no defeat in the opening: toll {toll} coins, cargo kept");
      yield return Ask("The pirates take a toll", $"The pirates have you pinned, but their captain only wants silver. They take {toll} coins and sheer off into the fog. Your cargo is untouched.", ("Continue", null)); msg = $"Paid a {toll}-coin toll. The hold is intact."; yield break; }
    var c = S.combat; int lost = Mathf.CeilToInt(HoldUsed * c.loseCargoPct); PayCargo(lost, spareGunpowder); crewInjured = true; safeUntilSail = sails + c.postLossSafeSails;
    yield return Ask("Defeat", $"{why} You lose {lost} cargo and {c.loseCrewInjuredPct * 100:0}% of your crew are injured (slower reloads until you reach port).\n\nNo pirate will trouble you for the next {c.postLossSafeSails} voyages.", ("Continue", null));
    msg = "You limp on, bruised but alive.";
    if (sailing && AdsUnlocked && AdBlockReason() == null) yield return Ask("Tow to port?", "A fishing crew offers a tow to the next port, straight away.", ("Watch ad: tow me", () => StartCoroutine(ShowAd("instantTowToPort", () => skipVoyage = true))), ("Limp on", null)); }

  // ---------------- input ----------------
  public float qaRudder = float.NaN;
  /* gamedev_rudder (Game Designer ruling): the rudder stays where the finger leaves it; released near centre it snaps to exactly 0. qaPtr* lets qa_run drive the real drag path. */
  public const float RudderSnapBand = 0.12f; public bool steerHintSeen; GameObject steerHint; float dragR0;
  public bool qaPtrOn, qaPtrDown; public Vector2 qaPtrPos;
  public static float RudderFromDrag(float r0, float dx, float w) => Mathf.Clamp(r0 + dx / (w * 0.22f), -1, 1);
  public static float RudderOnRelease(float r) => Mathf.Abs(r) < RudderSnapBand ? 0f : r;
  public bool ShowSteerHint(bool story) => story && !steerHintSeen;
  public bool SteerHintVisible => steerHint && steerHint.activeSelf;
  bool PointerOverUI(Vector2 mp) {
    var es = UnityEngine.EventSystems.EventSystem.current; if (es == null) return false;
    var pe = new UnityEngine.EventSystems.PointerEventData(es) { position = mp }; var hits = new List<UnityEngine.EventSystems.RaycastResult>(); es.RaycastAll(pe, hits); lastSteerHit = hits.Count > 0 ? hits[0].gameObject.name : ""; foreach (var h in hits) if (h.gameObject.GetComponentInParent<Selectable>() != null) { lastSteerHit = "button:" + h.gameObject.name; return true; } return false; } /* gamedev_rudder: only real controls block a steer drag; full-screen overlays/labels used to make the whole lower half a dead zone */
  public string lastSteerHit = "";
  public bool SteerStartsAt(Vector2 mp) => mp.y < Screen.height * 0.5f && !PointerOverUI(mp);
  void ReadSteer() {
    if (playthrough) { core.H.rudder = ptRudder; return; }
    if (!float.IsNaN(qaRudder)) { core.H.rudder = qaRudder; return; } /* gamedev_ambush: qa_run hand-style steering hook */
    bool down = qaPtrOn ? qaPtrDown : Input.GetMouseButton(0); Vector2 mp = qaPtrOn ? qaPtrPos : (Vector2)Input.mousePosition;
    if (down && !dragging && !steerPressed) { steerPressed = true; if (SteerStartsAt(mp)) { dragging = true; dragX0 = mp.x; dragR0 = core.H.rudder; if (steerHint && steerHint.activeSelf) steerHint.SetActive(false); } }
    if (!down) { if (dragging) core.H.rudder = RudderOnRelease(core.H.rudder); dragging = false; steerPressed = false; }
    if (dragging) core.H.rudder = RudderFromDrag(dragR0, mp.x - dragX0, Screen.width);
  }
  bool steerPressed;

  // ---------------- combat HUD ----------------
  void BuildCombatUI() {
    cui = R(root, "CombatUI", 0, 0, 1, 1); fx.SetAsLastSibling();
    /* v0.6k2: no sky strip / flat blue band — j framing; clear overlay only */
    if (backdrop) { backdrop.sprite = null; backdrop.color = Color.clear; }
    if (dimmer) dimmer.gameObject.SetActive(false);
    { var st = canvasRoot ? canvasRoot.Find("CombatSkyStrip") : null; if (st) Destroy0(st.gameObject); }
    shipBars = new[] { ShipBarPanel("Hasan"), ShipBarPanel("Pirate") };
    aimGlow = new GameObject("aimGlow", typeof(RectTransform)).GetComponent<RectTransform>(); aimGlow.SetParent(cui, false); aimGlow.anchorMin = aimGlow.anchorMax = Vector2.zero; aimGlow.sizeDelta = new Vector2(150, 150);
    var ag = aimGlow.gameObject.AddComponent<Image>(); ag.sprite = Ring(0.55f); ag.color = UITheme.C(T.marker); ag.raycastTarget = false;
    breachGlow = new GameObject("breach", typeof(RectTransform)).GetComponent<RectTransform>(); breachGlow.SetParent(cui, false); breachGlow.anchorMin = breachGlow.anchorMax = Vector2.zero; breachGlow.sizeDelta = new Vector2(130, 92);
    var bgI = breachGlow.gameObject.AddComponent<Image>(); bgI.sprite = BreachSprite(); bgI.raycastTarget = false; breachSparks.Clear();
    for (int i = 0; i < 7; i++) { var sp = Px(breachGlow, "spark", 60, 40, 9, 9).gameObject.AddComponent<Image>(); sp.sprite = Ring(0, 1); sp.color = new Color(1, 0.8f, 0.3f); sp.raycastTarget = false; breachSparks.Add(sp.rectTransform); }
    var tag = Px(breachGlow, "x2", 96, -14, 50, 32); var tagI = tag.gameObject.AddComponent<Image>(); tagI.color = new Color(0.1f, 0.06f, 0.04f, 0.85f); tagI.raycastTarget = false;
    Txt(R(tag, "t", 0, 0, 1, 1), "×2", T.fontSizeSmall - 8, T.gold, TextAnchor.MiddleCenter, true).raycastTarget = false;
    var wbox = Px(cui, "Wind", 48, 8, 340, 100); Box(wbox, T.ink, T.brass); /* thin border — teak 9-slice would clip text */
    windArrow = Px(wbox, "arrow", 20, 30, 32, 32).gameObject.AddComponent<Image>(); windArrow.sprite = Tri(); windArrow.color = UITheme.C(T.parchment);
    windTxt = Label(wbox, "", 60, 14, 260, 72, T.fontSizeSmall - 2, T.parchment);
    windTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
    var mb = Px(cui, "Msg", 404, 8, 652, 100); Box(mb, T.ink, T.brass); hudMsg = Label(mb, "Drag the lower screen to steer. Tap the enemy's parts to aim.", 16, 8, 648, 84, T.fontSizeSmall - 2, T.parchment);
    var cb = Bottom1920(cui, "cb");
    // ammo tray (right edge)
    var tray = R(cui, "tray", 1, 0, 1, 0); tray.pivot = new Vector2(1, 0); tray.sizeDelta = new Vector2(160, 1920); // hugs the screen's right edge (foldables: clear of the ships)
    bool wide = Screen.height < Screen.width * 1.9f;
    for (int i = 0; i < 4; i++) { int k = i; var r = Px(tray, "Ammo " + i, 16, (wide ? 300 : 760) + i * 128, 128, 116); ammoBtns[i] = Box(r, T.button, T.brass);
      r.gameObject.AddComponent<Button>().onClick.AddListener(() => core.H.ammo = k); ammoTxt[i] = Txt(R(r, "t", 0, 0, 1, 1), S.combat.ammo[i].name, T.fontSizeSmall - 2, T.buttonText, TextAnchor.MiddleCenter, true); }
    // aim chips: one row in thumb reach at the bottom, clear of the ships (>=48dp tall)
    Label(cb, "AIM AT", 24, 1580, 300, 28, T.fontSizeSmall - 4, T.textMuted);
    for (int i = 0; i < 4; i++) { int k = i; var r = Px(cb, "Aim " + PartNames[i], 24 + i * 190, 1608, 174, 100); Box(r, T.ink, T.brass); /* v0.6k2: Aim lowered; j camera unchanged */
      r.gameObject.AddComponent<Button>().onClick.AddListener(() => core.H.aim = (Part)k); Txt(R(r, "t", 0, 0, 1, 1), PartNames[i], T.fontSize, T.text, TextAnchor.MiddleCenter, true); aimChips[i] = r; }
    // bottom controls
    var sail = Btn(cb, "Full / Half sail", 24, 1740, 300, 120, () => core.H.fullSail = !core.H.fullSail);
    var rb = Px(cb, "Rudder", 350, 1784, 380, 28); Box(rb, T.panel, T.brass).raycastTarget = false; /* gamedev_rudder: the bar is not a dead zone for the steering drag */ rudderBar = Px(rb, "fill", 186, 2, 8, 24).gameObject.AddComponent<Image>(); rudderBar.color = UITheme.C(T.gold);
    Label(cb, "← drag to steer →", 350, 1820, 380, 40, T.fontSizeSmall - 4, T.textMuted, TextAnchor.MiddleCenter).raycastTarget = false;
    { var hb = Px(cb, "SteerHint", 190, 1150, 700, 110); var hi = Box(hb, T.ink, T.gold); hi.raycastTarget = false; var ht = Txt(R(hb, "t", 0, 0, 1, 1), "Drag to steer toward her.", T.fontSize, T.gold, TextAnchor.MiddleCenter, true); ht.raycastTarget = false; steerHint = hb.gameObject; steerHint.SetActive(false); } /* gamedev_rudder: one-time story-fight hint */
    var fr = Px(cb, "Fire", 800, 1600, 250, 250); var fb = fr.gameObject.AddComponent<Image>(); fb.sprite = Ring(0, 1); fb.color = UITheme.C(T.button); fireBg = fb;
    reloadRing = Px(fr, "ring", 0, 0, 250, 250).gameObject.AddComponent<Image>(); reloadRing.sprite = Ring(0.86f); reloadRing.type = Image.Type.Filled; reloadRing.fillMethod = Image.FillMethod.Radial360; reloadRing.color = UITheme.C(T.gold);
    fireTxt = Txt(R(fr, "t", 0, 0, 1, 1), "FIRE", T.fontSizeTitle, T.parchment, TextAnchor.MiddleCenter, true); fr.gameObject.AddComponent<Button>().onClick.AddListener(() => firePressed = true);
    boardBtn = Btn(cb, "BOARD!", 330, 1236, 420, 130, () => boardPressed = true, true, true);
    BuildRangeRing(fr); /* gamedev_ring */
  }
  RectTransform[] shipBars; RectTransform aimGlow;
  static readonly string[] BarCols = { "#C0503A", "#E8DCC0", "#C9A44C", "#2BB0A8" }; // hull, sails, guns, crew
  static Sprite[] barIcons = new Sprite[4];
  /// tiny pictograms for the ship bars: hull (boat), sails (lateen), guns (cannonball), crew (sailor)
  static Sprite BarIcon(int k) { if (barIcons[k] != null) return barIcons[k]; int n = 48; var tx = new Texture2D(n, n, TextureFormat.RGBA32, false); var px = new Color[n * n];
    for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float u = (x + 0.5f) / n * 2 - 1, v = (y + 0.5f) / n * 2 - 1; bool on = false;
      if (k == 0) on = v < 0.05f && v > -0.55f && Mathf.Abs(u) < 0.95f - (0.05f - v) * 0.9f || (Mathf.Abs(u) < 0.06f && v >= 0.05f && v < 0.8f);
      else if (k == 1) on = v > -0.8f && v < 0.85f && u > -0.6f && u < 0.7f && (u + 0.6f) < (0.85f - v) * 0.85f + 0.15f && u > -0.6f + (v + 0.8f) * 0.05f;
      else if (k == 2) on = u * u + v * v < 0.5f || (Mathf.Abs(u - 0.45f) < 0.12f && v > 0.45f && v < 0.85f);
      else on = (u * u + (v - 0.5f) * (v - 0.5f) < 0.09f) || (v < 0.1f && v > -0.9f && Mathf.Abs(u) < 0.5f - (0.1f - v) * 0.0f && Mathf.Abs(u) < 0.25f + (0.1f - v) * 0.35f);
      px[y * n + x] = on ? Color.white : Color.clear; }
    tx.SetPixels(px); tx.Apply(); return barIcons[k] = Sprite.Create(tx, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f)); }
  RectTransform breachGlow; readonly List<RectTransform> breachSparks = new List<RectTransform>(); static Sprite breachSpr;
  /// splintered waterline hole: jagged dark rim and splinters around an orange glowing interior
  static Sprite BreachSprite() { if (breachSpr) return breachSpr; int w = 130, h = 92; var tx = new Texture2D(w, h, TextureFormat.RGBA32, false); var px = new Color[w * h];
    for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { float u = (x - w / 2f) / (w / 2f), v = (y - h / 2f) / (h / 2f); float r = Mathf.Sqrt(u * u + v * v * 1.3f), a = Mathf.Atan2(v, u);
      float jag = 0.5f + 0.08f * Mathf.Sin(a * 7) + 0.06f * Mathf.Sin(a * 13 + 1) + 0.05f * Mathf.Sin(a * 23 + 2);
      float spl = 0.72f + 0.25f * Mathf.Pow(Mathf.Abs(Mathf.Sin(a * 5 + 0.7f)), 8);
      Color c = Color.clear;
      if (r < jag) { float k = 1 - r / jag; c = Color.Lerp(new Color(0.55f, 0.12f, 0.02f), new Color(1f, 0.82f, 0.35f), k * k * 1.2f); c.a = 1; }
      else if (r < jag + 0.08f) c = new Color(0.12f, 0.07f, 0.04f, 1);
      else if (r < spl && Mathf.Abs(Mathf.Sin(a * 5 + 0.7f)) > 0.86f) c = new Color(0.33f, 0.22f, 0.14f, 1);
      px[y * w + x] = c; }
    tx.SetPixels(px); tx.Apply(); return breachSpr = Sprite.Create(tx, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f)); }
  RectTransform ShipBarPanel(string n) {
    var p = new GameObject("bars " + n, typeof(RectTransform)).GetComponent<RectTransform>(); p.SetParent(cui, false); p.anchorMin = p.anchorMax = Vector2.zero; p.pivot = new Vector2(0.5f, 0); p.sizeDelta = new Vector2(250, 150);
    var bg = p.gameObject.AddComponent<Image>(); bg.color = new Color(0.08f, 0.06f, 0.05f, 0.7f); bg.raycastTarget = false;
    var nm = Txt(Px(p, "name", 10, 2, 230, 34), n == "Hasan" ? "Hasan" : "Pirate", T.fontSizeSmall - 6, T.parchment, TextAnchor.MiddleLeft, true); nm.raycastTarget = false;
    string[] lab = { "H", "S", "G", "C" };
    for (int i = 0; i < 4; i++) { var ic = Px(p, "l" + i, 12, 38 + i * 27, 24, 24).gameObject.AddComponent<Image>(); ic.sprite = BarIcon(i); ic.color = UITheme.C(BarCols[i]); ic.raycastTarget = false;
      var tr = Px(p, "track" + i, 40, 42 + i * 27, 196, 16); var ti = tr.gameObject.AddComponent<Image>(); ti.color = new Color(0, 0, 0, 0.6f); ti.raycastTarget = false;
      var f = Px(tr, "fill", 0, 0, 196, 16); var fi = f.gameObject.AddComponent<Image>(); fi.color = UITheme.C(BarCols[i]); fi.raycastTarget = false; }
    return p; }
  void PlaceBars(RectTransform p, Vector3 world, CShip s) {
    var sp = cam.WorldToScreenPoint(world); p.gameObject.SetActive(sp.z > 0); if (sp.z <= 0) return;
    RectTransformUtility.ScreenPointToLocalPointInRectangle(cui, sp, null, out var lp); var pos = lp - cui.rect.min;
    pos.x = Mathf.Clamp(pos.x, 130, cui.rect.width - 130 - 160); pos.y = Mathf.Clamp(pos.y, 420, cui.rect.height - 220);
    /* v0.6k2: keep Hasan/Pirate plates from stacking on top of each other */
    if (p == shipBars[0]) pos.x = Mathf.Min(pos.x, cui.rect.width * 0.42f);
    if (p == shipBars[1]) pos.x = Mathf.Max(pos.x, cui.rect.width * 0.58f);
    if (Mathf.Abs(shipBars[0].anchoredPosition.y - shipBars[1].anchoredPosition.y) < 160f && p == shipBars[1])
      pos.y = Mathf.Clamp(shipBars[0].anchoredPosition.y + 170f, 420, cui.rect.height - 220);
    p.anchoredPosition = pos;
    float[] v = { Mathf.Max(0, s.hull) / s.hull0, s.sails / s.sails0, s.Guns(S.combat) / (float)s.st.gunsPerSide, s.crew / s.crew0 };
    for (int i = 0; i < 4; i++) { var f = (RectTransform)p.Find("track" + i + "/fill"); f.sizeDelta = new Vector2(196 * Mathf.Clamp01(v[i]), 16); }
    p.Find("name").GetComponent<Text>().text = (s == core.H ? "Hasan" : "Pirate") + (s.burnT > 0 ? $"  <color={T.down}>FIRE</color>" : ""); }
  string Bars(string name, CShip s) =>
    $"<b><size={T.fontSize}>{name}</size></b>\nHull {Mathf.CeilToInt(Mathf.Max(0, s.hull))}/{s.hull0:0}   Sails {Mathf.CeilToInt(s.sails)}/{s.sails0:0}\nGuns {s.Guns(S.combat)}/{s.st.gunsPerSide:0}   Crew {Mathf.CeilToInt(s.crew)}/{s.crew0:0}" + (s.burnT > 0 ? $"\n<color={T.down}>ON FIRE</color>" : "");
  Vector3 W(CShip s) => new Vector3((s.x - core.H.x) * K, 0, (s.z - core.H.z) * K);
  bool inCinematic;
  void UpdateCombatView() {
    if (inCinematic) return;
    var H = core.H; var P = core.P; float t = Time.time;
    ship.position = W(H) + Vector3.up * (0.15f + Mathf.Sin(t * 1.3f) * 0.15f); ship.rotation = Quaternion.Euler(Mathf.Sin(t * 0.9f) * 2, H.heading, -H.rudder * 6 + Mathf.Sin(t) * 2);
    pirateShip.position = W(P) + Vector3.up * (0.15f + Mathf.Sin(t * 1.2f + 1) * 0.15f); pirateShip.rotation = Quaternion.Euler(Mathf.Sin(t * 0.8f) * 2, P.heading, -P.rudder * 6 + (P.hull <= 0 ? 25 : 0));
    waterMat.SetVector("_Offset", new Vector4(H.x * K, H.z * K, 0, 0));
    float rng = core.Range; float want = CombatCore.Bearing(P.x - H.x, P.z - H.z); camYaw = CombatCore.AngLerp(camYaw, want, Mathf.Min(1, Time.deltaTime * 1.5f));
    var dir = Quaternion.Euler(0, camYaw, 0) * Vector3.forward; var mid = (ship.position + pirateShip.position) / 2; float d = Mathf.Clamp(rng * K, 12, 90);
    cam.transform.position = ship.position - dir * (d * 0.55f + 16) + Vector3.up * (d * 0.75f + 15); cam.transform.LookAt(mid + Vector3.up * 2); /* v0.6k2: exact j high three-quarter */
    { bool open = core.BreachOpen && P.hull > 0; breachGlow.gameObject.SetActive(open);
      if (open) { float sideSign = Mathf.Sign(Vector3.Dot(ship.position - pirateShip.position, pirateShip.right)); var sp = cam.WorldToScreenPoint(pirateShip.position + pirateShip.right * sideSign * 1.7f + Vector3.up * 0.4f);
        if (sp.z > 0) { RectTransformUtility.ScreenPointToLocalPointInRectangle(cui, sp, null, out var lp); breachGlow.anchoredPosition = lp - cui.rect.min; float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 9); breachGlow.GetComponent<Image>().color = new Color(1f, 0.85f + 0.15f * k, 0.75f + 0.25f * k, 1);
          for (int i = 0; i < breachSparks.Count; i++) { float ph = (Time.time * 1.6f + i * 0.37f) % 1f; float ang = (i * 53 + Mathf.Floor(Time.time * 1.6f + i * 0.37f) * 97) % 360 * Mathf.Deg2Rad; var spk = breachSparks[i];
            spk.anchoredPosition = new Vector2(65 + Mathf.Cos(ang) * ph * 70, -46 + Mathf.Abs(Mathf.Sin(ang)) * ph * 60 - ph * ph * 30) - new Vector2(4.5f, -4.5f); spk.GetComponent<Image>().color = new Color(1, 0.75f + 0.25f * (1 - ph), 0.3f, 1 - ph); } } } }
    UpdateRangeRing(); /* gamedev_ring */
    PlaceBars(shipBars[0], ship.position + Vector3.up * 13f, H); PlaceBars(shipBars[1], pirateShip.position + Vector3.up * 13f, P);
    { Vector3 off = H.aim == Part.Sails ? new Vector3(0, 7, 0) : H.aim == Part.Crew ? new Vector3(0, 2.6f, 0) : H.aim == Part.Guns ? new Vector3(0, 1.8f, 0) + pirateShip.right * 1.6f : new Vector3(0, 0.6f, 0);
      var sp = cam.WorldToScreenPoint(pirateShip.position + off); bool vis = sp.z > 0; aimGlow.gameObject.SetActive(vis);
      if (vis) { RectTransformUtility.ScreenPointToLocalPointInRectangle(cui, sp, null, out var lp); aimGlow.anchoredPosition = lp + cui.rect.size * 0f - cui.rect.min; float pulse = 1 + 0.15f * Mathf.Sin(Time.time * 6); aimGlow.localScale = Vector3.one * pulse; var gc = UITheme.C(T.marker); gc.a = 0.55f + 0.35f * Mathf.Sin(Time.time * 6); aimGlow.GetComponent<Image>().color = gc; } }
    float rel = CombatCore.Diff(camYaw, core.windTo); windArrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -rel);
    bool up = core.Upwindness(H) > core.Upwindness(P);
    windTxt.text = $"Wind ×{core.WindMult(H.heading):0.0}\n" + (up ? $"<color={T.gold}>Upwind +{S.combat.upwindAccBonus * 100:0}%</color>" : "Downwind");
    for (int i = 0; i < 4; i++) { bool sel = H.ammo == i; ammoBtns[i].sprite = Panel(UITheme.C(sel ? T.panelAlt : T.button), UITheme.C(sel ? T.gold : T.brass));
      ammoTxt[i].text = S.combat.ammo[i].name + (i == 3 ? $" ×{H.fireLeft}" : "") + $"\n<size={T.fontSizeSmall - 8}>{S.combat.ammo[i].range:0} m</size>"; ammoTxt[i].color = UITheme.C(sel ? T.gold : T.buttonText); }
    reloadRing.fillAmount = 1 - Mathf.Clamp01(H.reloadT / Mathf.Max(0.01f, H.st.reload)); bool can = core.CanFire(H, P);
    fireTxt.text = H.reloadT > 0 ? $"{H.reloadT:0.0}s" : can ? "FIRE" : "FIRE"; fireTxt.color = UITheme.C(can ? T.gold : T.textMuted); if (fireBg) { var fc = UITheme.C(T.button); fireBg.color = can ? fc : new Color(fc.r * 0.55f, fc.g * 0.55f, fc.b * 0.55f, 0.75f); } /* gamedev_arc: FIRE dim unless range AND arc (and loaded) */
    rudderBar.rectTransform.anchoredPosition = new Vector2(186 + H.rudder * 180, -2);
    boardBtn.gameObject.SetActive(core.BoardAllowed(H, P));
    for (int i = 0; i < 4; i++) { var r = aimChips[i]; bool sel = (int)H.aim == i;
      r.GetComponent<Image>().sprite = Panel(UITheme.C(sel ? T.panelAlt : T.panel), UITheme.C(sel ? T.marker : T.brass));
      var tx = r.GetComponentInChildren<Text>(); tx.color = UITheme.C(sel ? T.marker : T.text); tx.text = (sel ? "◆ " : "") + PartNames[i]; }
  }
  void Puffs(Transform from, Transform to, int hits) {
    if (from == null || to == null || !Application.isPlaying) return;
    for (int i = 0; i < 3 + hits; i++) { var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(g.GetComponent<Collider>()); g.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>("Mats/Sail");
      bool atTarget = i >= 3; var basePos = atTarget ? to.position : from.position; g.transform.position = basePos + new Vector3(Rnd.Range(-2f, 2f), 1.5f + Rnd.Range(0, 1.5f), Rnd.Range(-3f, 3f));
      StartCoroutine(Puff(g.transform, atTarget ? 1.6f : 2.4f)); } }
  IEnumerator Puff(Transform g, float s) { for (float t = 0; t < 0.9f; t += Time.deltaTime) { g.localScale = Vector3.one * s * (0.4f + t); g.position += Vector3.up * Time.deltaTime; yield return null; } Destroy(g.gameObject); }

  // ---------------- boarding duel (timing) ----------------
  public bool duelWon; public int duelsFought; public float duelFreeze = -1; // shot mode: freeze ring at this fraction
  Dictionary<int, Sprite> rings = new Dictionary<int, Sprite>();
  Sprite Ring(float inner, float outer = 1f) { int key = Mathf.RoundToInt(inner * 1000) * 10000 + Mathf.RoundToInt(outer * 1000); if (rings.TryGetValue(key, out var s)) return s;
    int n = 256; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false); var px = new Color[n * n];
    for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float r = new Vector2(x - n / 2f + .5f, y - n / 2f + .5f).magnitude / (n / 2f); px[y * n + x] = r <= outer && r >= inner ? Color.white : Color.clear; }
    tex.SetPixels(px); tex.Apply(); return rings[key] = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f)); }
  /* gamedev_t10_duel: short procedural metal clang (no SFX assets in the project) */
  AudioClip clangClip; AudioSource clangSrc;
  void PlayClang() {
    if (clangClip == null) { int sr = 44100, n = (int)(sr * 0.45f); var d = new float[n]; var r = new System.Random(7);
      for (int i = 0; i < n; i++) { float t = i / (float)sr, env = Mathf.Exp(-t * 9f), hitEnv = Mathf.Exp(-t * 60f);
        d[i] = 0.5f * env * (0.45f * Mathf.Sin(2 * Mathf.PI * 1180f * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * 2730f * t) + 0.2f * Mathf.Sin(2 * Mathf.PI * 4310f * t) + 0.12f * Mathf.Sin(2 * Mathf.PI * 6150f * t)) + 0.35f * hitEnv * (float)(r.NextDouble() * 2 - 1); }
      clangClip = AudioClip.Create("gb_clang", n, 1, sr, false); clangClip.SetData(d, 0); }
    if (clangSrc == null) clangSrc = gameObject.AddComponent<AudioSource>();
    clangSrc.PlayOneShot(clangClip, 0.8f); Debug.Log("GB DUEL clang");
  }
  const float DuelMinWindowSec = 0.5f; /* gamedev_duel1: minimum STRIKE window in repel/board duels */
  IEnumerator Duel(bool attacking) {
    var c = S.combat; float zone = core.DuelZone(), zoneBase = zone; /* gamedev_duel1: zoneBase = un-widened zone */ int need = c.duelRounds / 2 + 1, won = 0, lost = 0;
    var pnl = Px(root, "Duel", 30, 1540, 1020, 720) /* art_t12_cards: 60 px taller, grown upward (bottom stays at 2260) */; Box(pnl, T.panel, T.gold);
    Label(pnl, attacking ? "Boarding: blade to blade" : "Repel boarders!", 130, 40, 760, 52, /* art_t12_cards: off the top rail, between the corner brackets */ T.fontSizeTitle - 4, T.parchment, TextAnchor.MiddleLeft, true);
    Box(Px(pnl, "rule", 130, 100, 760, 2), T.brass); /* art_t12_cards: rule under title */ var score = Label(pnl, "", 130, 110, 760, 44, /* art_t12_cards: was 24,70 */ T.fontSize, T.text);
    Label(pnl, $"Tap STRIKE as the ring crosses the gold band. Gold zone {zone * 100:0}%" + (core.P.crew < core.H.crew ? " (crew advantage)" : ""), 130, 156, 760, 70, /* art_t12_cards: was 24,114,970 */ T.fontSizeSmall - 4, T.textMuted);
    var arena = Px(pnl, "arena", 110, 236, 370, 370) /* art_t13_ring: disc x110-480 y236-606, 110+ px inside the bottom-left bracket and bottom edge (card h720), clear of the help line (ends 226) */ /* art_t12_cards: +60 */;
    var bg = Px(arena, "bg", 0, 0, 370, 370) /* art_t13_ring */.gameObject.AddComponent<Image>(); bg.sprite = Ring(0, 1); bg.color = UITheme.C(T.ink);
    var goldR = Px(arena, "gold", 0, 0, 370, 370) /* art_t13_ring */.gameObject.AddComponent<Image>(); goldR.sprite = Ring((0.5f - zone / 2) / (0.5f + zone / 2), 1); goldR.color = UITheme.C(T.gold) * new Color(1, 1, 1, 0.85f);
    goldR.rectTransform.pivot = new Vector2(.5f, .5f); goldR.rectTransform.anchoredPosition = new Vector2(185, -185); /* art_t13_ring: scale about the disc centre (Px pivot is top-left) */ goldR.rectTransform.localScale = Vector3.one * (0.5f + zone / 2);
    var closing = Px(arena, "closing", 0, 0, 370, 370) /* art_t13_ring */.gameObject.AddComponent<Image>(); closing.sprite = Ring(0.95f, 1); closing.color = UITheme.C(T.parchment); closing.rectTransform.pivot = new Vector2(.5f, .5f); closing.rectTransform.anchoredPosition = new Vector2(185, -185); /* art_t13_ring: closing ring shrinks to the disc centre */
    var res = Label(pnl, "", 490, 280, /* art_t12_cards: +60 */ 500, 80, T.fontSizeTitle - 4, T.parchment, TextAnchor.MiddleCenter, true);
    bool tapped = false; Btn(pnl, "STRIKE", 490, 390, /* art_t12_cards: +60 */ 500, 190, () => tapped = true, true, true);
    bool paced = duelsFought > 0; /* gamedev_t13_duelpace: repel/board only, prologue unchanged */
    for (int round = 0; won < need && lost < need; round++) {
      score.text = $"Round {round + 1}   You {won} – {lost} Pirate"; tapped = false; res.text = ""; float hitAt = -1;
      float ringSec = (paced && round == 0) ? Mathf.Max(c.ringSeconds, 2.0f) : c.ringSeconds; /* gamedev_t13_duelpace: gentler round 1 only; json value untouched */
      if (paced) { zone = Mathf.Max(zoneBase, DuelMinWindowSec / ringSec); goldR.sprite = Ring((0.5f - zone / 2) / (0.5f + zone / 2), 1); goldR.rectTransform.localScale = Vector3.one * (0.5f + zone / 2); } /* gamedev_duel1: repel/board rounds get at least a 0.5 s tap window (was zone*ringSec = 0.25*1.4 = 0.35 s in later rounds); gold ring redrawn to match */
      if (paced) { closing.rectTransform.localScale = Vector3.one; if (round == 0) { res.text = "En garde!"; yield return new WaitForSeconds(1.0f); } else yield return new WaitForSeconds(0.6f); res.text = ""; tapped = false; Debug.Log($"GB DUEL pace round={round + 1} ringSec={ringSec:0.0} lead={(round == 0 ? 1.0f : 0.6f):0.0}"); } /* gamedev_t13_duelpace: early taps during the beat are discarded */
      for (float t = 0; t < 1f; t += Time.deltaTime / ringSec) {
        if (duelFreeze >= 0) t = duelFreeze;
        closing.rectTransform.localScale = Vector3.one * Mathf.Max(0.02f, 1 - t);
        if (gbDuelAuto && t >= 0.5f) tapped = true; /* gamedev_t11_gbduel: -gbduelshots auto-taps mid-band */
        if (tapped) { hitAt = t; break; } yield return null; }
      bool hit = hitAt >= 0.5f - zone / 2 && hitAt <= 0.5f + zone / 2;
      bool parried = false; /* gamedev_t10_duel */
      bool forcedParry = !attacking && hit && GbParry; /* gamedev_t11_gbduel */
      if (!attacking && hit && (forcedParry || Rnd.value < c.pirateDuelHit * 0.5f)) { hit = false; parried = true; } // a defending crew is pressed harder (Jeff t10: kept, but shown as a visible parry)
      if (!hit && duelsFought == 0) { hit = true; parried = false; hitAt = 0.5f; } // prologue duel: Hasan's crew rallies, it can't be lost
      if (parried) PlayClang(); /* gamedev_t10_duel */
      Debug.Log($"GB DUEL round={round + 1} tap={hitAt:0.000} band=[{0.5f - zone / 2:0.000},{0.5f + zone / 2:0.000}] hit={hit} parried={parried} window={zone * ringSec:0.000}s fights={duelsFought}" + (forcedParry ? " forced" : "")); /* gamedev_duel1: effective window (s) + duels fought so far */ /* gamedev_t10_duel: ring outer radius = 1-tap; gold ring spans the same [lo,hi] radii */
      if (hit) won++; else lost++; res.text = hit ? "Hit!" : parried ? "Good strike, but the pirate parried!" /* gamedev_t10_duel */ : hitAt < 0 ? "Too slow — parried" : hitAt < 0.5f - zone / 2 ? "Too early — parried" : "Too late — parried";
      score.text = $"You {won} – {lost} Pirate"; if (gbDuelShotDir != null) { yield return new WaitForSeconds(0.2f); yield return ShotCo(System.IO.Path.Combine(gbDuelShotDir, $"gbduel_{(attacking ? "board" : "repel")}_r{round + 1}_{(hit ? "hit" : parried ? "parried" : "miss")}.png")); } yield return new WaitForSeconds(0.9f); }
    duelWon = won >= need; duelsFought++; Destroy0(pnl.gameObject);
  }

  /* gamedev_t11_gbduel: -gbduel repel|board opens the real Duel() at launch; -gbparry 1 forces the defending parry; -gbduelshots DIR auto-taps and screenshots */
  bool gbDuelAuto; string gbDuelShotDir;
  static bool GbParry { get { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-gbparry"); return i >= 0 && i + 1 < a.Length && a[i + 1] == "1"; } }
  IEnumerator GbDuelIfAsked(string mode) {
    var a = System.Environment.GetCommandLineArgs(); int si = System.Array.IndexOf(a, "-gbduelshots"); if (si >= 0 && si + 1 < a.Length) { gbDuelShotDir = a[si + 1]; gbDuelAuto = true; System.IO.Directory.CreateDirectory(gbDuelShotDir); }
    bool attacking = mode == "board"; onTitle = false; if (duelsFought < 1) duelsFought = 1;
    core = new CombatCore(S.combat, Rnd.Range(0, int.MaxValue), crewInjured, PirateTierNow, difficulty);
    Debug.Log($"GB DUEL start mode={mode} attacking={attacking} duelsFought={duelsFought} parryForced={GbParry} zone={core.DuelZone():0.000}");
    yield return new WaitForSeconds(0.5f); yield return Duel(attacking);
    Debug.Log($"GB DUEL end mode={mode} won={duelWon}"); core = null; fighting = false;
    if (gbDuelShotDir != null) { yield return new WaitForSeconds(0.5f); Application.Quit(); yield break; }
    ShowTitle(); }
}
