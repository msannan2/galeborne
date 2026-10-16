Shader "Galeborne/FireAlpha" {
  Properties { _BaseMap ("Tex", 2D) = "white" {} _Boost ("Boost", Float) = 1.7 }
  SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
    Pass { Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
      CGPROGRAM
      #pragma vertex v
      #pragma fragment f
      #include "UnityCG.cginc"
      sampler2D _BaseMap; float _Boost;
      struct a { float4 p : POSITION; float2 uv : TEXCOORD0; fixed4 c : COLOR; };
      struct o { float4 p : SV_POSITION; float2 uv : TEXCOORD0; fixed4 c : COLOR; };
      o v(a i) { o r; r.p = UnityObjectToClipPos(i.p); r.uv = i.uv; r.c = i.c; return r; }
      fixed4 f(o i) : SV_Target { float m = tex2D(_BaseMap, i.uv).a; fixed4 c = i.c; c.rgb *= _Boost; c.a *= m; return c; }
      ENDCG } } }
