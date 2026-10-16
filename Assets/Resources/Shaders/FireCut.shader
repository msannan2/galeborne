Shader "Galeborne/FireCut" {
  Properties { _BaseMap ("Tex", 2D) = "white" {} _Cut ("Cutoff", Float) = 0.45 }
  SubShader { Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
    Pass { ZWrite On Cull Off
      CGPROGRAM
      #pragma vertex v
      #pragma fragment f
      #include "UnityCG.cginc"
      sampler2D _BaseMap; float _Cut;
      struct a { float4 p : POSITION; float2 uv : TEXCOORD0; fixed4 c : COLOR; };
      struct o { float4 p : SV_POSITION; float2 uv : TEXCOORD0; fixed4 c : COLOR; };
      o v(a i) { o r; r.p = UnityObjectToClipPos(i.p); r.uv = i.uv; r.c = i.c; return r; }
      fixed4 f(o i) : SV_Target { float m = tex2D(_BaseMap, i.uv).a * i.c.a; clip(m - _Cut); return fixed4(i.c.rgb, 1); }
      ENDCG } } }
