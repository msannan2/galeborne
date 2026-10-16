Shader "Galeborne/Foam" {
  Properties { _MainTex("Foam", 2D) = "white" {} _Speed("Scroll", Float) = 0.6 _Alpha("Alpha", Float) = 0.85 }
  SubShader {
    Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
    Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
    Pass {
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
      CBUFFER_START(UnityPerMaterial) float4 _MainTex_ST; float _Speed; float _Alpha; CBUFFER_END
      struct A{float4 pos:POSITION; float2 uv:TEXCOORD0;}; struct V{float4 pos:SV_POSITION; float2 uv:TEXCOORD0;};
      V vert(A i){V o; o.pos=TransformObjectToHClip(i.pos.xyz); o.uv=i.uv; return o;}
      half4 frag(V i):SV_Target{ float2 uv=float2(i.uv.x*2, i.uv.y*3 + _Time.y*_Speed);
        half f = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a; half f2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv*0.5+0.3).a;
        half edge = 1 - abs(i.uv.x*2-1); half fade = 1 - i.uv.y;
        return half4(1,1,1, saturate(max(f, f2*0.8) * fade * (0.4 + edge) * _Alpha)); }
      ENDHLSL
    }
  }
}
