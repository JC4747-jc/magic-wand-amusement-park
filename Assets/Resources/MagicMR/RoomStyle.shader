Shader "MagicMR/RoomStyle"
{
    Properties {
        _BaseMap("Original albedo",2D)="white" {}
        _BaseColor("Renderer tint",Color)=(1,1,1,1)
        _RoomBaseTint("Original tint",Color)=(1,1,1,1)
        _RoomStyle("Watercolor 1 / cyber 2",Float)=1
    }
    SubShader {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Pass {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;half4 _RoomBaseTint;half4 _BaseColor;half _RoomStyle;
            CBUFFER_END
            struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;half3 n:TEXCOORD1;float2 uv:TEXCOORD2;float3 local:TEXCOORD3;UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A i){V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.n=TransformObjectToWorldNormal(i.n);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);o.local=i.p.xyz;return o;}
            half4 Frag(V i):SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n=normalize(i.n);Light l=GetMainLight();half diffuse=saturate(dot(n,l.direction));
                half3 albedo=SAMPLE_TEXTURE2D_BIAS(_BaseMap,sampler_BaseMap,i.uv,1.2).rgb*_RoomBaseTint.rgb*_BaseColor.rgb;
                half luminance=dot(albedo,half3(.2126,.7152,.0722));
                if(_RoomStyle<1.5){
                    // Object-space pigment variation remains still as the observer moves.
                    half grain=sin(i.local.x*31+i.local.z*17+sin(i.local.y*7))*sin(i.local.y*23+i.local.z*13);
                    half wash=.78+.17*diffuse+.018*grain;
                    half3 pastel=lerp(half3(.94,.91,.81),lerp(luminance.xxx,albedo,.6),.64);
                    return half4(pastel*wash,1);
                }
                half rim=pow(1-saturate(dot(n,GetWorldSpaceNormalizeViewDir(i.world))),3);
                half grid=1-smoothstep(.012,.035,abs(frac(i.local.y*24)-.5));
                half3 neon=lerp(half3(.05,.8,1),half3(.9,.1,.55),saturate(i.local.y*2));
                return half4(half3(.025,.035,.08)+albedo*(.12+.15*diffuse)+neon*(rim*.65+grid*.3),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
