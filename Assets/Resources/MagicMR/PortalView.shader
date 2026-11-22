Shader "MagicMR/PortalView"
{
 Properties {_BaseColor("Tint",Color)=(1,.78,.18,1) _PortalId("Portal stencil",Int)=21}
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Geometry+20"}
  Pass {
   ZWrite Off ZTest Always Cull Back
   Stencil {Ref [_PortalId] Comp Equal Pass Keep}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor;float4 _ClipPlane;
   CBUFFER_END
   struct A{float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;half shade:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
   V Vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.shade=.45+.55*saturate(dot(TransformObjectToWorldNormal(i.n),normalize(float3(-.4,.8,-.5))));return o;}
   half4 Frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);clip(dot(float4(i.world,1),_ClipPlane));return half4(_BaseColor.rgb*i.shade,1);}
   ENDHLSL
  }
 }
}
