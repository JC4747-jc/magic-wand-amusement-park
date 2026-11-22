Shader "MagicMR/Snowflake"
{
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass {
   Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A{float4 p:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
   V Vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.color=i.color;return o;}
   float Segment(float2 p,float2 a,float2 b){float2 d=b-a;return length(p-a-d*saturate(dot(p-a,d)/dot(d,d)));}
   half4 Frag(V i):SV_Target{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float2 p=i.uv*2-1;float radius=length(p);float angle=atan2(p.y,p.x);angle-=round(angle/1.04719755)*1.04719755;
    float2 q=float2(cos(angle),abs(sin(angle)))*radius;
    float d=min(Segment(q,float2(0,0),float2(.9,0)),min(Segment(q,float2(.34,0),float2(.53,.23)),Segment(q,float2(.60,0),float2(.79,.23))));
    half alpha=(1-smoothstep(.022,.05,d))*saturate((1-radius)*12)*i.color.a;clip(alpha-.01);return half4(i.color.rgb,alpha);
   }
   ENDHLSL
  }
 }
}
