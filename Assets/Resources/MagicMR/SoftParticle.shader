Shader "MagicMR/SoftParticle"
{
 Properties { _BaseColor("Tint",Color)=(1,1,1,1) _Softness("Radial falloff",Range(0,1))=1 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor; half _Softness;
   CBUFFER_END
   struct A { float4 p:POSITION; half4 c:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct V { float4 p:SV_POSITION; half4 c:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
   V Vert(A i) { V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.p=TransformObjectToHClip(i.p.xyz); o.c=i.c*_BaseColor; o.uv=i.uv; return o; }
   half4 Frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float2 p=i.uv*2-1;
    half falloff=saturate(1-dot(p,p));
    return half4(i.c.rgb,i.c.a*lerp(1,falloff*falloff,_Softness));
   }
   ENDHLSL
  }
 }
}
