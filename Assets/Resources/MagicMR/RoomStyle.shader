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
            float RoomPaperHash(float2 p) {
                float3 q=frac(float3(p.xyx)*.1031);
                q+=dot(q,q.yzx+33.33);
                return frac((q.x+q.y)*q.z);
            }
            float RoomPaperNoise(float2 p) {
                float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(RoomPaperHash(cell),RoomPaperHash(cell+float2(1,0)),f.x),
                            lerp(RoomPaperHash(cell+float2(0,1)),RoomPaperHash(cell+1),f.x),f.y);
            }
            half4 Frag(V i):SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n=normalize(i.n);Light l=GetMainLight();half diffuse=saturate(dot(n,l.direction));
                half3 albedo=SAMPLE_TEXTURE2D_BIAS(_BaseMap,sampler_BaseMap,i.uv,1.2).rgb*_RoomBaseTint.rgb*_BaseColor.rgb;
                half luminance=dot(albedo,half3(.2126,.7152,.0722));
                if(_RoomStyle<1.5){
                    // Preserve source pigment instead of turning every surface into pale clay.
                    // Layered washes, strokes and paper remain fixed in object space.
                    float2 paperUv=i.local.xy+float2(i.local.z*.37,i.local.z*.61);
                    half wash=RoomPaperNoise(paperUv*5.3);
                    half strokes=RoomPaperNoise(paperUv*float2(21,3.7)+float2(2.3,4.1));
                    float2 grainFootprint=fwidth(paperUv*120);
                    half grainVisible=1-smoothstep(.65,1.35,max(grainFootprint.x,grainFootprint.y));
                    half grain=(RoomPaperNoise(paperUv*120)-.5)*grainVisible;
                    half coverage=saturate(.86+.12*(wash-.5)+.06*(strokes-.5)+.025*(1-diffuse));
                    half3 pigment=lerp(luminance.xxx,albedo,.9);
                    half3 paper=half3(.97,.952,.914);
                    half shading=.82+.16*diffuse;
                    half granulation=1+.035*(strokes-.5)+.035*grain;
                    return half4(lerp(paper,pigment,coverage)*shading*granulation,1);
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
