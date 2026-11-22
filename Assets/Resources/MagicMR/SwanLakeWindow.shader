Shader "MagicMR/SwanLakeWindow"
{
    Properties
    {
        _BaseMap("Lake", 2D)="white"{}
        _Opening("Opening", Range(0,1))=1
        _LandscapePhase("Wave phase", Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float _Opening, _LandscapePhase;
            CBUFFER_END
            struct Attr {float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Vary {float4 pos:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO};
            Vary Vert(Attr v){
                Vary o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=TransformObjectToHClip(v.vertex.xyz); o.uv=v.uv; return o;
            }
            float Hash(float2 p){
                float3 q=frac(p.xyx*float3(.1031,.1030,.0973));
                q+=dot(q,q.yzx+33.33);
                return frac((q.x+q.y)*q.z);
            }
            half4 Frag(Vary i):SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv=i.uv;
                float2 q=abs(uv*2-1);
                float edge=pow(pow(q.x,5)+pow(q.y,5),.2);
                float grain=Hash(floor(uv*float2(230,350)));
                float scallop=.021*sin(uv.y*92+uv.x*45)+.012*sin(uv.x*119-uv.y*59);
                float coverage=_Opening*(.98+scallop)-edge-.055*grain;
                clip(coverage);
                float2 sampleUV=uv;
                sampleUV.x+=.0015*sin(uv.y*150+_LandscapePhase*4)*(1-smoothstep(.22,.41,uv.y));
                half3 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,sampleUV).rgb;
                float glint=(1-smoothstep(0,.026,coverage))*smoothstep(.79,.99,grain);
                color+=half3(.34,.19,.08)*glint;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
