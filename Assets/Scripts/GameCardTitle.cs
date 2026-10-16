using UnityEngine;
using UnityEngine.UI;

/* gamedev_title1: shared card title (Chief Art Designer spec). One line that fits => exactly the old geometry (title y40 h52, rule y100, body y132).
   Otherwise up to 2 lines with best-fit down to ~85% size, same left inset; the rule sits under the LAST line and the body starts 12 px below the rule.
   CardTitleFit() measures first so callers can size the panel; CardTitle() draws. Extra = how far the body moves down (0 for one line). */
public partial class Game {
  public struct TitleFit { public int size, lines; public float h, extra; }
  const float TitleMinFrac = 0.85f; /* gamedev_title1 */
  TitleFit CardTitleFit(string title, float w, int baseSize) {
    var gen = new TextGenerator();
    TextGenerationSettings St(int s) => new TextGenerationSettings { font = T.bold, fontSize = s, lineSpacing = 1, richText = true, scaleFactor = 1, textAnchor = TextAnchor.UpperLeft, generationExtents = new Vector2(w, 4000f), pivot = Vector2.zero, generateOutOfBounds = true, horizontalOverflow = HorizontalWrapMode.Wrap, verticalOverflow = VerticalWrapMode.Overflow, updateBounds = true, color = Color.white, fontStyle = FontStyle.Normal };
    gen.Populate(title ?? "", St(baseSize)); if (gen.lineCount <= 1) return new TitleFit { size = baseSize, lines = 1, h = 52, extra = 0 }; /* unchanged one-line title */
    int min = Mathf.CeilToInt(baseSize * TitleMinFrac), pick = min;
    for (int s = baseSize; s >= min; s--) { gen.Populate(title, St(s)); if (gen.lineCount <= 1) { return new TitleFit { size = s, lines = 1, h = 52, extra = 0 }; } if (gen.lineCount <= 2) { pick = s; break; } } /* largest size that fits in 2 lines (one line if shrinking gets it there) */
    var g2 = new TextGenerator(); g2.Populate(title, St(pick)); float h = Mathf.Max(52f, g2.GetPreferredHeight(title, St(pick)) + 6f);
    float rule = 40 + h + 8; return new TitleFit { size = pick, lines = Mathf.Min(2, g2.lineCount), h = h, extra = rule - 100f }; /* old: rule y100 = 40 + 52 + 8 */
  }
  /* draws title + rule at x, returns the fit; anchor MiddleLeft (Ask/AskCard) or MiddleCenter (contract card). ruleW defaults to w. */
  TitleFit CardTitle(Transform p, string title, float x, float w, int baseSize, string col, TextAnchor a, TitleFit f, float ruleW = -1, float ruleX = -1) {
    var t = Label(p, title, x, 40, w, f.h, f.size, col, a, true); if (f.lines > 1) { t.verticalOverflow = VerticalWrapMode.Truncate; t.lineSpacing = 0.95f; }
    Box(Px(p, "rule", ruleX < 0 ? x : ruleX, 100 + f.extra, ruleW < 0 ? w : ruleW, 2), T.brass); return f;
  }
}
