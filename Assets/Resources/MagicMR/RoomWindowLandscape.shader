Shader "MagicMR/RoomWindowLandscape"
{
    Properties { _LandscapePhase("Landscape phase",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _LandscapePhase;
            CBUFFER_END
            struct Attributes {
                float4 positionOS:POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;float2 kind:TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings {
                float4 positionCS:SV_POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;float kind:TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input){
                Varyings output;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.color=input.color;output.uv=input.uv;output.kind=input.kind.x;return output;
            }
            half Cloud(float2 p,float2 center,float2 scale){
                float2 q=(p-center)/scale;
                float d=min(length(q),min(length(q-float2(.64,-.04)),length(q+float2(.57,.16))));
                return 1-smoothstep(.60,1.08,d);
            }
            half4 Frag(Varyings input):SV_Target{
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p=input.uv;
                // Geometry is confined to the original aperture; no stencil, screen copy or camera feed.
                clip(min(min(p.x,1-p.x),min(p.y,1-p.y))+.0001);
                if(input.kind<.5){
                    half3 horizon=half3(.73,.79,.68),zenith=half3(.17,.40,.58);
                    half3 color=lerp(horizon,zenith,smoothstep(.22,1,p.y));
                    float2 sun=(p-float2(.74,.80))*float2(1,1.6125);
                    float radius=length(sun);
                    color=lerp(color,half3(1,.84,.48),(1-smoothstep(.055,.068,radius))*.93);
                    color+=half3(.07,.035,.005)*exp(-radius*radius*35);
                    float drift=sin(_LandscapePhase)*.036;
                    half clouds=max(Cloud(p,float2(.23+drift,.86),float2(.13,.034)),
                                    Cloud(p,float2(.65-drift*.7,.94),float2(.09,.026)));
                    return half4(lerp(color,half3(.91,.94,.90),clouds*.73),1);
                }
                if(input.kind>1.5){
                    half3 color=lerp(half3(.055,.20,.23),half3(.23,.43,.43),saturate(p.y/.372));
                    float wave=p.y*185+sin(p.x*24+_LandscapePhase*2)*.7+_LandscapePhase*6;
                    float edge=max(fwidth(wave)*.6,.025);
                    half rippleLine=smoothstep(.90-edge,.96+edge,sin(wave));
                    half reflection=exp(-pow((p.x-.74)/(.045+.10*(1-p.y/.372)),2));
                    color+=rippleLine*(half3(.045,.065,.055)+half3(.25,.18,.055)*reflection);
                    return half4(color,1);
                }
                return half4(input.color.rgb,1);
            }
            ENDHLSL
        }
    }
}
