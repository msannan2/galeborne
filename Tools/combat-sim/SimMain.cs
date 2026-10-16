using System; using System.Collections.Generic; using System.Reflection; using System.Linq; using System.Text;
static class J { // tiny JSON reader for the standalone sim
  public static object Parse(string s){int i=0;return V(s,ref i);}
  static void W(string s,ref int i){while(i<s.Length&&char.IsWhiteSpace(s[i]))i++;}
  static object V(string s,ref int i){W(s,ref i);char c=s[i];
    if(c=='{'){i++;var d=new Dictionary<string,object>();W(s,ref i);if(s[i]=='}'){i++;return d;}
      while(true){W(s,ref i);var k=(string)V(s,ref i);W(s,ref i);i++;d[k]=V(s,ref i);W(s,ref i);if(s[i++]=='}')return d;}}
    if(c=='['){i++;var l=new List<object>();W(s,ref i);if(s[i]==']'){i++;return l;}while(true){l.Add(V(s,ref i));W(s,ref i);if(s[i++]==']')return l;}}
    if(c=='"'){var b=new StringBuilder();i++;while(s[i]!='"'){if(s[i]=='\\')i++;b.Append(s[i++]);}i++;return b.ToString();}
    if(s.Substring(i).StartsWith("true")){i+=4;return true;} if(s.Substring(i).StartsWith("false")){i+=5;return false;}
    int st=i;while(i<s.Length&&"+-0123456789.eE".IndexOf(s[i])>=0)i++;return double.Parse(s.Substring(st,i-st),System.Globalization.CultureInfo.InvariantCulture);}
  public static object To(Type t,object v){
    if(t==typeof(float))return Convert.ToSingle(v);if(t==typeof(int))return Convert.ToInt32(v);if(t==typeof(bool))return (bool)v;if(t==typeof(string))return (string)v;
    if(t.IsArray){var l=(List<object>)v;var a=Array.CreateInstance(t.GetElementType(),l.Count);for(int k=0;k<l.Count;k++)a.SetValue(To(t.GetElementType(),l[k]),k);return a;}
    var o=Activator.CreateInstance(t);var d=(Dictionary<string,object>)v;foreach(var f in t.GetFields())if(d.ContainsKey(f.Name))f.SetValue(o,To(f.FieldType,d[f.Name]));return o;}
}
class SimMain{ static void Main(string[] a){
  var root=(Dictionary<string,object>)J.Parse(System.IO.File.ReadAllText(a[0]));var C=(CombatCfg)J.To(typeof(CombatCfg),root["combat"]);
  Console.WriteLine(CombatSim.Run(C,int.Parse(a.Length>1?a[1]:"1000")));}}
