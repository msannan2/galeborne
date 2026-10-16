Shader "Galeborne/Sky" {
  Properties { _Horizon("Horizon",Color)=(0.95,0.85,0.68,1) _Zenith("Zenith",Color)=(0.31,0.47,0.68,1) _Sun("Sun",Color)=(0.95,0.75,0.39,1) _SunDir("SunDir",Vector)=(0.3,0.35,0.88,0) }
  SubShader {
    Tags { "Queue"="Background" "RenderType"="Background" "RenderPipeline"="UniversalPipeline" }
    Cull Front ZWrite Off
    Pass {
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      CBUFFER_START(UnityPerMaterial) half4 _Horizon; half4 _Zenith; half4 _Sun; float4 _SunDir; CBUFFER_END
      struct A{float4 pos:POSITION;}; struct V{float4 pos:SV_POSITION; float3 d:TEXCOORD0;};
      V vert(A i){V o; float3 wp=TransformObjectToWorld(i.pos.xyz); o.d=wp-_WorldSpaceCameraPos; o.pos=TransformWorldToHClip(wp); return o;}
      half4 frag(V i):SV_Target{ float3 d=normalize(i.d); float t=saturate(d.y*2.2);
        half3 c=lerp(_Horizon.rgb,_Zenith.rgb,pow(t,0.7));
        float s=saturate(dot(d,normalize(_SunDir.xyz)));
        c+=_Sun.rgb*(smoothstep(0.9975,0.999,s)*1.2+pow(s,24)*0.35);
        return half4(c,1);}
      ENDHLSL
    }
  }
}
