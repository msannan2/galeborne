using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// gamedev_blockade_warning: the blockade is always warned before it bites. Strings: Resources/Design/blockade_strings.json (design/blockade_strings.json),
/// rumours: v06_events_content rumors[type=blockade]. Icon: Resources/Art/UI/blockade_icon (design/blockade_icon_spec.md).
public partial class Game {
  public const string BlkAmber = "#F2A93B"; public const string AskOff = "~off~";
  Dictionary<string, object> blkStr;
  Dictionary<string, object> BlkS { get { if (blkStr == null) { var ta = Resources.Load<TextAsset>("Design/blockade_strings"); blkStr = ta ? Design.D(MiniJson.Parse(ta.text)) : new Dictionary<string, object>(); } return blkStr; } }
  string BlkStr(string path, string fb) { object o = BlkS; foreach (var k in path.Split('.')) { var d = o as Dictionary<string, object>; if (d == null || !d.ContainsKey(k)) return fb; o = d[k]; } return o as string ?? fb; }
  /// the design copy names Muscat/copper; template it for any blockaded port/good
  public string BlkFill(string s, int p, int g) { string gn = g >= 0 ? S.goods[g].name.ToLower() : "goods"; string pn = p >= 0 ? S.ports[p].name : "port";
    s = (s ?? "").Replace("{port}", pn).Replace("{good}", gn).Replace("Muscat", pn).Replace("Copper", gn.Length > 0 ? char.ToUpper(gn[0]) + gn.Substring(1) : gn).Replace("copper", gn); return s; }
  List<string> BlkRumourVariants() => DZ?.content == null ? new List<string>() : ((List<object>)DZ.content["rumors"]).Select(Design.D).Where(x => (string)x["type"] == "blockade").Select(x => (string)x["text"]).ToList();
  int blkTavPick;
  /// active blockades elsewhere, each with one rumour variant (only the wording varies)
  public List<string> BlkTavernLines() { var l = new List<string>(); if (blockGood == null) return l; var vs = BlkRumourVariants();
    for (int p = 0; p < blockGood.Length; p++) if (blockGood[p] >= 0 && p != port) l.Add(vs.Count > 0 ? BlkFill(vs[(blkTavPick + p) % vs.Count], p, blockGood[p]) : $"Soldiers are seizing {S.goods[blockGood[p]].name.ToLower()} at {S.ports[p].name}."); return l; }
  void BlkHearInTavern() { if (blockGood == null) return; blkTavPick++; var vs = BlkRumourVariants();
    for (int p = 0; p < blockGood.Length; p++) if (blockGood[p] >= 0 && p != port && !blockKnown[p]) { blockKnown[p] = true; blockHeard[p] = vs.Count > 0 ? BlkFill(vs[(blkTavPick + p) % vs.Count], p, blockGood[p]) : null; Debug.Log($"GB BLOCKADE heard rumour {S.ports[p].name} ({S.goods[blockGood[p]].id})"); } }
  /// Hail her: the lateen shouts the warning about a blockade the player hasn't heard of yet
  public bool BlkHailReveal() { if (blockGood == null) return false; for (int p = 0; p < blockGood.Length; p++) if (blockGood[p] >= 0 && !blockKnown[p]) {
      string w = BlkFill(BlkStr("hailWarning.bodyShort", "Soldiers search copper at Muscat"), p, blockGood[p]); blockKnown[p] = true; blockHeard[p] = w; msg = $"They call across the water: \"{w}.\""; Debug.Log($"GB BLOCKADE heard hail {S.ports[p].name}"); return true; } return false; }
  public int BlkFee(int p) { int g = blockGood[p]; return g < 0 ? 0 : Mathf.CeilToInt(0.30f * hold[g] * price[p, g]); }
  public string BlkRoutePreview(int p) => BlkFill(BlkStr("routePreview.text", "Blockade at Muscat: your copper will be seized."), p, blockGood[p]);
  public string BlkPlaque(int p) => BlkFill(BlkStr("seaMap.plaque", "Blockade: copper seized"), p, blockGood[p]);
  void BlkMapIcon(RectTransform map, int pi, Vector2 c) { float s = 44f; /* 44 px at 1080 wide (canvas reference width) */
    var r = Px(map, "blockade " + S.ports[pi].name, c.x + 46 - s * 0.6f, c.y - 46 - s * 0.4f, s, s); var spr = Resources.Load<Sprite>("Art/UI/blockade_icon");
    if (spr) { var im = r.gameObject.AddComponent<Image>(); im.sprite = spr; im.preserveAspect = true; } else { var im = r.gameObject.AddComponent<Image>(); im.color = UITheme.C("#C9A050"); }
    int k = pi; r.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (dialog == null) StartCoroutine(AskD(BlkPlaque(k), blockHeard[k] ?? BlkRoutePreview(k), ("Close", null))); }); }
  public bool blkFramed;
  /// blockade_polish: frame the arrival landmark behind the card exactly like Customs does (Game.Voyage atCustoms branch)
  void BlkHarbourFrame(int p, bool on) {
    if (on) { if (sea3D) sea3D.gameObject.SetActive(true); ShowSeaLandmark(p); if (backdrop) backdrop.color = Color.clear; if (dimmer) dimmer.gameObject.SetActive(false); if (town3D) town3D.gameObject.SetActive(false); Clear(content); TopBar(); if (pirateShip) pirateShip.gameObject.SetActive(false);
      string cn = S.ports[p].name; arrivalTarget = (seaMarks.TryGetValue(cn, out var clm) && clm ? clm.transform.position : new Vector3(0, 0, 80f)) + Vector3.up * CustomsLookUp; customsFrame = true; blkFramed = true; Debug.Log("GB BLOCKADE harbour backdrop " + cn); }
    else { customsFrame = false; blkFramed = false; ShowSeaLandmark(-1); if (sea3D) sea3D.gameObject.SetActive(false); } }
  /// arrival card: pay them off (~30% of the seized goods' value) or let them take it
  public IEnumerator BlockadeCard() { int p = port, g = blockGood[p]; if (g < 0 || hold[g] <= 0) { blockPending = -1; yield break; }
    int q = hold[g], fee = BlkFee(p); bool can = coins >= fee; string pn = S.ports[p].name;
    string title = BlkFill(BlkStr("arrivalCard.title", "Blockade at Muscat"), p, g), body = BlkFill(BlkStr("arrivalCard.body", "Soldiers board to seize your copper."), p, g);
    string pay = BlkStr("arrivalCard.options.pay", "Pay them off ({fee} coins)").Replace("{fee}", fee.ToString());
    if (!can) { pay = AskOff + pay + "\n" + BlkStr("arrivalCard.payDisabled", "Not enough coins"); }
    Debug.Log($"GB CARD show {title} fee={fee} coins={coins} {(can ? "" : "(pay greyed)")}");
    BlkHarbourFrame(p, true); /* blockade_polish: the arrival port's harbour behind the card, same framing as Customs (never the plain brown backdrop) */
    try {
    yield return Ask(title, body,
      (pay, () => { coins -= fee; msg = $"You pay the soldiers {fee} coins. Your {GoodCount(g, q)} stays aboard."; Debug.Log($"GB DOCK blockade bribe paid {fee} kept {q}x{S.goods[g].id} at {pn}"); }),
      (BlkStr("arrivalCard.options.surrender", "Let them take it"), () => { hold[g] = 0; msg = $"Soldiers on the quay seize your {GoodCount(g, q)}. {pn} is under a blockade on {S.goods[g].name}."; Debug.Log($"GB DOCK blockade seized {q}x{S.goods[g].id} at {pn}"); }));
    } finally { BlkHarbourFrame(p, false); }
    blockPending = -1; }
}
