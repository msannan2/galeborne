Shader "Galeborne/RangeRing" {
  /* gamedev_ring: naval range ring decal. Lies on the water: the vertex follows the same waves as Water.shader, normal alpha blend
     (no additive), ZWrite Off, ZTest LEqual + small depth offset so it sits on the sea but every hull in front of it hides it.
     Queue 2450 = after opaque ships/water, before transparent wakes/foam/fire. */
  Properties { _MainTex("Sprite", 2D) = "white" {} _Color("Tint", Color) = (1,1,1,1) _Speed("Speed", Float) = 0 _Offset("Offset", Vector) = (0,0,0,0) _Amp("Wave scale", Float) = 1 _Lift("Lift", Float) = 0.15 }
  SubShader {
    Tags { "Queue"="AlphaTest-0" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
    Pass {
      Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off Offset -1, -1
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
      CBUFFER_START(UnityPerMaterial) float4 _MainTex_ST; half4 _Color; float _Speed; float4 _Offset; float _Amp; float _Lift; CBUFFER_END
      struct A { float4 pos:POSITION; float2 uv:TEXCOORD0; };
      struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
      float wave(float2 p, float2 d, float f, float a, float s){ return a*sin(dot(p,d)*f + _Time.y*s); }
      V vert(A i){ V o; float3 wp = TransformObjectToWorld(i.pos.xyz);
        float2 p = wp.xz + _Offset.xy + float2(0, _Time.y*_Speed);
        float h = wave(p,normalize(float2(0.2,1)),0.18,0.55,1.1)+wave(p,normalize(float2(-0.7,0.6)),0.31,0.28,1.7)
                + wave(p,normalize(float2(0.9,0.3)),0.53,0.14,2.3)+wave(p,normalize(float2(-0.2,-1)),0.9,0.06,3.1);
        wp.y += h*_Amp + _Lift; o.pos = TransformWorldToHClip(wp); o.uv = i.uv; return o; }
      half4 frag(V i):SV_Target { half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv); return t * _Color; }
      ENDHLSL
    }
  }
}
