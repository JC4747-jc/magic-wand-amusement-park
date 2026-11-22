Shader "MagicMR/PortalMask"
{
 Properties { _PortalId("Portal stencil",Int)=21 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10"}
  Pass {
   ColorMask 0 ZWrite Off Cull Off
   Stencil { Ref [_PortalId] Comp Always Pass Replace }
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A{float4 p:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   V Vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;return o;}
   half4 Frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float2 p=i.uv*2-1;clip(1-dot(p,p));return 0;}
   ENDHLSL
  }
 }
}
