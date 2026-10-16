Shader "Galeborne/Water" {
  Properties {
    _Shallow("Shallow", Color) = (0.18,0.71,0.67,1)
    _Deep("Deep", Color) = (0.12,0.16,0.4,1)
    _Sun("Sun", Color) = (0.95,0.75,0.39,1)
    _SunDir("SunDir", Vector) = (0.3,0.35,0.88,0)
    _Speed("Flow speed", Float) = 3
    _Offset("World offset", Vector) = (0,0,0,0)
    _Amp("Wave scale", Float) = 1
  }
  SubShader {
    Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
    Pass {
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_fog
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      CBUFFER_START(UnityPerMaterial)
        half4 _Shallow; half4 _Deep; half4 _Sun; float4 _SunDir; float _Speed; float4 _Offset; float _Amp;
      CBUFFER_END
      struct A { float4 pos:POSITION; };
      struct V { float4 pos:SV_POSITION; float3 wp:TEXCOORD0; float3 n:TEXCOORD1; float h:TEXCOORD2; float fog:TEXCOORD3; };
      float wave(float2 p, float2 d, float f, float a, float s, out float2 g){
        float ph = dot(p,d)*f + _Time.y*s; g = d*f*a*cos(ph); return a*sin(ph); }
      V vert(A i){
        V o; float3 wp = TransformObjectToWorld(i.pos.xyz);
        float2 p = wp.xz + _Offset.xy + float2(0, _Time.y*_Speed);
        float2 g1,g2,g3,g4;
        float h = wave(p,normalize(float2(0.2,1)),0.18,0.55,1.1,g1)+wave(p,normalize(float2(-0.7,0.6)),0.31,0.28,1.7,g2)
                + wave(p,normalize(float2(0.9,0.3)),0.53,0.14,2.3,g3)+wave(p,normalize(float2(-0.2,-1)),0.9,0.06,3.1,g4);
        h *= _Amp; float2 g = (g1+g2+g3+g4) * _Amp; wp.y += h;
        o.n = normalize(float3(-g.x,1,-g.y)); o.wp = wp; o.h = h;
        o.pos = TransformWorldToHClip(wp); o.fog = ComputeFogFactor(o.pos.z); return o; }
      half4 frag(V i):SV_Target{
        float3 v = normalize(_WorldSpaceCameraPos - i.wp);
        float dist = length(_WorldSpaceCameraPos.xz - i.wp.xz);
        float deep = saturate((dist-8.0)/55.0); deep = deep*deep*(3-2*deep);
        half3 c = lerp(_Shallow.rgb, _Deep.rgb, deep);
        c *= 0.85 + i.h*0.25;
        float fres = pow(1-saturate(dot(i.n,v)),4);
        c = lerp(c, half3(0.85,0.82,0.75), fres*0.35);
        float3 l = normalize(_SunDir.xyz); float3 hv = normalize(l+v);
        c += _Sun.rgb * pow(saturate(dot(i.n,hv)),300)*0.8;
        c = MixFog(c, i.fog); return half4(c,1); }
      ENDHLSL
    }
  }
}
