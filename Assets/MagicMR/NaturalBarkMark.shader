Shader "MagicMR/NaturalBarkMark"
{
    Properties {_BaseColor("Tint",Color)=(1,1,1,1) _Breath("Virtual scar breath",Range(0,1))=0}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1"}
        Cull Off
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Offset -1,-1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _Breath;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 colour:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 colour:COLOR;float2 uv:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A input)
            {
                V output=(V)0;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(output.positionWS);output.normalWS=TransformObjectToWorldNormal(input.normalOS);output.colour=input.colour;output.uv=input.uv;return output;
            }
            half4 Frag(V input,FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal=normalize(input.normalWS)*IS_FRONT_VFACE(front,1,-1);Light light=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half grain=sin(input.uv.x*76+sin(input.uv.y*11)*.7)*.035;
                half3 albedo=input.colour.rgb*_BaseColor.rgb*(1+grain);
                half3 ambient=max(SampleSH(normal),half3(.18,.19,.20));
                half center=1-smoothstep(.12,.46,length(input.uv-.5));
                return half4(albedo*(ambient+light.color*saturate(dot(normal,light.direction))*.8*light.shadowAttenuation)+half3(.36,.10,.025)*center*_Breath,1);
            }
            ENDHLSL
        }
    }
}
