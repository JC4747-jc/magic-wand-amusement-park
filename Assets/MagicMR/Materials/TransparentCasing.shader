Shader "MagicMR/TransparentCasing"
{
 Properties { _BaseColor("Tint",Color)=(.3,.8,1,.08) _EdgeAlpha("Edge opacity",Range(0,1))=.48 }
 SubShader {
 Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
 Pass {
 Tags { "LightMode"="UniversalForward" }
 Blend SrcAlpha OneMinusSrcAlpha
 ZWrite Off
 Cull Back
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 half4 _BaseColor; half _EdgeAlpha;
 CBUFFER_END
 struct A {float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
 V Vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(a.p.xyz);o.world=TransformObjectToWorld(a.p.xyz);o.normal=TransformObjectToWorldNormal(a.n);return o;}
 half4 Frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);half rim=pow(1-saturate(abs(dot(normalize(i.normal),GetWorldSpaceNormalizeViewDir(i.world)))),3);return half4(_BaseColor.rgb,lerp(_BaseColor.a,_EdgeAlpha,rim));}
 ENDHLSL
 }
 }
}
