using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
/// Minimal JSON reader (objects, arrays, numbers, strings, bools, null) for the v0.6 design data files.
public static class MiniJson {
  public static object Parse(string s) { int i = 0; return Val(s, ref i); }
  static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
  static object Val(string s, ref int i) {
    Ws(s, ref i); char c = s[i];
    if (c == '{') { var d = new Dictionary<string, object>(); i++; Ws(s, ref i); if (s[i] == '}') { i++; return d; }
      while (true) { Ws(s, ref i); string k = Str(s, ref i); Ws(s, ref i); i++; d[k] = Val(s, ref i); Ws(s, ref i); if (s[i] == ',') { i++; continue; } i++; return d; } }
    if (c == '[') { var l = new List<object>(); i++; Ws(s, ref i); if (s[i] == ']') { i++; return l; }
      while (true) { l.Add(Val(s, ref i)); Ws(s, ref i); if (s[i] == ',') { i++; continue; } i++; return l; } }
    if (c == '"') return Str(s, ref i);
    if (s.Substring(i).StartsWith("true")) { i += 4; return true; } if (s.Substring(i).StartsWith("false")) { i += 5; return false; } if (s.Substring(i).StartsWith("null")) { i += 4; return null; }
    int st = i; while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++; return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture); }
  static string Str(string s, ref int i) { var sb = new StringBuilder(); i++;
    while (s[i] != '"') { if (s[i] == '\\') { i++; char e = s[i]; if (e == 'n') sb.Append('\n'); else if (e == 't') sb.Append('\t'); else if (e == 'u') { sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; } else sb.Append(e); } else sb.Append(s[i]); i++; }
    i++; return sb.ToString(); }
}
/// The v0.6 data layer: v06_settings plus the market, ambush and content files; "$a.b" strings resolve against v06_settings at load.
public class Design {
  public Dictionary<string, object> settings, market, ambush, content;
  public static Design Load() {
    var d = new Design(); d.settings = Read("v06_settings"); d.market = (Dictionary<string, object>)d.Resolve(Read("v06_market_events_data")); d.ambush = (Dictionary<string, object>)d.Resolve(Read("v06_ambush_data")); d.content = Read("v06_events_content"); return d; }
  static Dictionary<string, object> Read(string n) { var ta = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Design/" + n); return ta ? (Dictionary<string, object>)MiniJson.Parse(ta.text) : new Dictionary<string, object>(); }
  public object Path(object root, string path) { object o = root; foreach (var p in path.Split('.')) { if (o is Dictionary<string, object> dd && dd.TryGetValue(p, out var v)) o = v; else return null; } return o; }
  object Resolve(object o) {
    if (o is string s && s.StartsWith("$")) return Path(settings, s.Substring(1));
    if (o is Dictionary<string, object> d) { foreach (var k in new List<string>(d.Keys)) d[k] = Resolve(d[k]); return d; }
    if (o is List<object> l) { for (int i = 0; i < l.Count; i++) l[i] = Resolve(l[i]); return l; }
    return o; }
  public static string Path0(Design d, string path, string def) => d?.Path(d.settings, path) as string ?? def;
  public static float F(object o, float def = 0) => o is double x ? (float)x : o is bool b ? (b ? 1 : 0) : def;
  public static List<string> SL(object o) { var r = new List<string>(); if (o is List<object> l) foreach (var x in l) r.Add(x as string); return r; }
  public static Dictionary<string, object> D(object o) => o as Dictionary<string, object> ?? new Dictionary<string, object>();
}
