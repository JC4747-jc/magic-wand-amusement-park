Shader "MagicMR/WishGarden"
{
    Properties {_Bloom("Petal opening",Range(0,1))=1}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Bloom;
            CBUFFER_END
            struct Attributes {float3 positionOS:POSITION;float3 normalOS:NORMAL;float3 closed:TEXCOORD1;float3 closedNormal:TEXCOORD2;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            Varyings Vert(Attributes input){Varyings o=(Varyings)0;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionWS=TransformObjectToWorld(lerp(input.closed,input.positionOS,saturate(_Bloom)));o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(normalize(lerp(input.closedNormal,input.normalOS,saturate(_Bloom))));o.color=input.color;return o;}
            half4 Frag(Varyings input,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);half3 n=normalize(input.normalWS)*IS_FRONT_VFACE(face,1,-1);Light light=GetMainLight(TransformWorldToShadowCoord(input.positionWS));half d=saturate(dot(n,light.direction));return half4(input.color.rgb*(.22h+SampleSH(n)*.5h+light.color*(.2h+.6h*d)*light.shadowAttenuation),1);}
            ENDHLSL
        }
    }
}
