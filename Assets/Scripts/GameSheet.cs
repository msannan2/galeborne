using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// Tap a market row -> quantity sheet with a slider, live prices and Buy / Sell.
public partial class Game {
  RectTransform sheet; int sheetGood; Slider qty; Text sTitle, sHeld, sBuy, sSell, sQty, sBuyPrev, sSellPrev; Button sBuyBtn, sSellBtn; Image sBuyArrow, sSellArrow;
  int BuyMax(int g) { if (Banned(g, port)) return 0; float p = price[port, g], c = coins; int n = 0, room = S.holdSize - HoldUsed;
    while (n < room && c >= Mathf.CeilToInt(p)) { c -= Mathf.CeilToInt(p); p *= 1 + S.perUnitBuyRise; n++; } return n; }
  int CostOf(int g, int q) { float p = price[port, g]; int t = 0; for (int i = 0; i < q; i++) { t += Mathf.CeilToInt(p); p *= 1 + S.perUnitBuyRise; } return t; }
  int RevenueOf(int g, int q) { if (RugsBanSale(g)) { int t0 = 0; float k = 1; for (int i = 0; i < q; i++) { t0 += Mathf.FloorToInt(RugsBanPrice() * k); k *= 1 - S.contraband.bannedPerUnitSellDrop; } return t0; } float p = price[port, g]; float m = Banned(g, port) ? S.contraband.bannedSellMult : S.sellSpread; int t = 0; for (int i = 0; i < q; i++) { t += Mathf.FloorToInt(p * m); p *= 1 - S.perUnitSellDrop; } return t; }

  public void OpenSheet(int g) {
    CloseSheet(); sheetGood = g; var gd = S.goods[g];
    sheet = R(root, "Sheet", 0, 0, 1, 1);
    var dim = R(sheet, "dim", 0, 0, 1, 1); var di = dim.gameObject.AddComponent<Image>(); di.color = new Color(0, 0, 0, 0.45f); dim.gameObject.AddComponent<Button>().onClick.AddListener(CloseSheet);
    var pnl = Px(Bottom1920(sheet, "b"), "panel", 0, 960, 1080, 960); Box(pnl, T.ledgerBg, T.frame); pnl.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // eat taps
    var ic = Px(pnl, "icon", 36, 36, 130, 130).gameObject.AddComponent<Image>(); ic.sprite = Icons.For(gd.id, UITheme.C(T.ledgerInk));
    sTitle = Label(pnl, gd.name, 190, 36, 600, 76, T.fontSizeTitle + 10, T.ledgerInk, TextAnchor.MiddleLeft, true);
    sHeld = Label(pnl, "", 190, 104, 740, 80, T.fontSizeSmall, T.ledgerMuted);
    LBtn(pnl, "✕", 940, 36, 104, 104, CloseSheet, true);
    Box(Px(pnl, "rule", 36, 186, 1008, 3), T.ledgerRule);
    Label(pnl, "BUY", 60, 210, 300, 36, T.fontSizeSmall - 4, T.ledgerMuted); sBuy = Label(pnl, "", 60, 246, 300, 80, T.fontSizeTitle + 16, T.ledgerInk, TextAnchor.MiddleLeft, true);
    sBuyArrow = Arrow(pnl, 300, 274, 0, 28);
    Label(pnl, "SELL", 560, 210, 300, 36, T.fontSizeSmall - 4, T.ledgerMuted); sSell = Label(pnl, "", 560, 246, 300, 80, T.fontSizeTitle + 16, T.ledgerInk, TextAnchor.MiddleLeft, true);
    sSellArrow = Arrow(pnl, 800, 274, 0, 28); priceTexts[g] = sSell;
    // quantity slider
    sQty = Label(pnl, "", 36, 350, 1008, 60, T.fontSizeTitle, T.ledgerInk, TextAnchor.MiddleCenter, true);
    LBtn(pnl, "−", 36, 430, 130, 120, () => qty.value = Mathf.Max(qty.minValue, qty.value - 1), true);
    LBtn(pnl, "+", 914, 430, 130, 120, () => qty.value = Mathf.Min(qty.maxValue, qty.value + 1), true);
    var sr = Px(pnl, "Slider", 196, 450, 688, 80); qty = sr.gameObject.AddComponent<Slider>();
    var bg = Stretch(sr, "bg", 0, 30, 0, 30); Box(bg, T.ledgerRowAlt, T.ledgerRule);
    var fa = Stretch(sr, "Fill Area", 10, 30, 10, 30); var fill = Stretch(fa, "Fill", 0, 0, 0, 0); Box(fill, T.ledgerButton);
    var ha = Stretch(sr, "Handle Area", 30, 0, 30, 0); var h = R(ha, "Handle", 0, 0, 0, 1); h.sizeDelta = new Vector2(64, 0); var hi = h.gameObject.AddComponent<Image>(); hi.sprite = Ring(0, 1); hi.color = UITheme.C(T.ledgerButton);
    qty.fillRect = fill; qty.handleRect = h; qty.targetGraphic = hi; qty.wholeNumbers = true; qty.minValue = 1;
    qty.onValueChanged.AddListener(_ => UpdateSheet(false));
    sBuyPrev = Label(pnl, "", 36, 580, 500, 50, T.fontSizeSmall, T.ledgerMuted, TextAnchor.MiddleCenter);
    sSellPrev = Label(pnl, "", 544, 580, 500, 50, T.fontSizeSmall, T.ledgerMuted, TextAnchor.MiddleCenter);
    sBuyBtn = LBtn(pnl, "Buy", 36, 640, 500, 150, () => StartCoroutine(SheetTrade(true)), true);
    sSellBtn = LBtn(pnl, "Sell", 544, 640, 500, 150, () => StartCoroutine(SheetTrade(false)), true);
    Label(pnl, $"Each unit sold lowers the price {S.perUnitSellDrop * 100:0.#}%.", 36, 820, 1008, 50, T.fontSizeSmall - 4, T.ledgerMuted, TextAnchor.MiddleCenter);
    UpdateSheet(true);
  }
  void UpdateSheet(bool resetQty) {
    if (sheet == null) return; int g = sheetGood; bool ban = Banned(g, port); int bmax = BuyMax(g), smax = ban ? 0 : hold[g];
    int max = Mathf.Max(1, Mathf.Max(bmax, smax)); qty.maxValue = max; if (resetQty) qty.SetValueWithoutNotify(Mathf.Clamp(Mathf.Max(1, smax > 0 ? smax : Mathf.Min(5, bmax)), 1, max));
    int q = Mathf.RoundToInt(qty.value); float d = price[port, g] / lastPrice[port, g] - 1;
    sHeld.text = $"Held {hold[g]} · hold space {S.holdSize - HoldUsed} · {coins:N0} coins" + (ban ? $"  ·  <color={T.ledgerDown}>banned here</color>" : "");
    sBuy.text = ban ? "N/A" : $"{Mathf.CeilToInt(price[port, g]):N0}"; sSell.text = ban ? "N/A" : $"{SellPrice(g):N0}";
    bool carry = hold[g] > 0 && AvgCost[g] > 0; sSell.color = UITheme.C(!carry || ban ? T.ledgerInk : SellPrice(g) >= AvgCost[g] ? T.ledgerUp : T.ledgerDown);
    if (carry) sHeld.text = $"Bought at {AvgCost[g]:N0} avg · " + (SellPrice(g) >= AvgCost[g] ? $"<color={T.ledgerUp}>+{SellPrice(g) - AvgCost[g]:N0} each</color>" : $"<color={T.ledgerDown}>{SellPrice(g) - AvgCost[g]:N0} each</color>") + "\n" + sHeld.text;
    foreach (var a in new[] { sBuyArrow, sSellArrow }) { a.enabled = !ban; a.sprite = Mathf.Abs(d) < 0.005f ? null : Tri(); a.color = UITheme.C(Mathf.Abs(d) < 0.005f ? T.ledgerMuted : d > 0 ? T.ledgerUp : T.ledgerDown); a.rectTransform.localScale = new Vector3(1, d < 0 ? -1 : 1, 1); }
    sQty.text = $"Quantity  {q}";
    sBuyPrev.text = q <= bmax ? $"Cost {CostOf(g, q):N0}" : bmax == 0 ? (ban ? "Banned here" : "Can't afford / no room") : $"Max {bmax}";
    sSellPrev.text = q <= smax ? $"Earn ~{RevenueOf(g, q):N0}" : smax == 0 ? (ban ? "Sell in the Back Alley" : "None held") : $"Max {smax}";
    SetBtn(sBuyBtn, $"Buy {q}", q <= bmax && !busy); SetBtn(sSellBtn, $"Sell {q}", q <= smax && !busy);
  }
  void SetBtn(Button b, string label, bool en) { b.interactable = en; b.GetComponent<Image>().sprite = Panel(UITheme.C(en ? T.ledgerButton : T.ledgerRowAlt), UITheme.C(en ? T.frame : T.ledgerRule));
    var t = b.GetComponentInChildren<Text>(); t.text = label; t.color = UITheme.C(en ? T.ledgerButtonText : T.ledgerMuted); }
  IEnumerator SheetTrade(bool buy) {
    int g = sheetGood, q = Mathf.RoundToInt(qty.value); busy = true; int done = 0;
    for (int i = 0; i < q; i++) {
      if (buy) { if (!Buy(g)) break; }
      else { int v = SellPrice(g); if (!Sell(g)) break; SaleFx(sSell.rectTransform.position + new Vector3(0, 0, 0)); }
      done++; Refresh(); UpdateSheet(false); yield return new WaitForSeconds(buy ? 0.03f : 0.1f); }
    msg = buy ? $"Bought {done} {S.goods[g].name}." : $"Sold {done} {S.goods[g].name}. Next sells for {SellPrice(g)}.";
    busy = false; Refresh(); UpdateSheet(true);
  }
  public void CloseSheet() { if (sheet) Destroy0(sheet.gameObject); sheet = null; priceTexts.Remove(sheetGood); }
}
