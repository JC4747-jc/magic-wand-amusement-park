Shader "MagicMR/PetOutline"
{
    Properties { _Ink("Ink",Color)=(0.025,0.027,0.035,1) _Width("World outline width",Float)=0.0025 _NormalSign("Normal orientation",Float)=1 [Enum(UnityEngine.Rendering.CullMode)] _HullCull("Hull culling",Float)=1 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Cull [_HullCull] ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Ink;float _Width;float _NormalSign;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings {float4 positionCS:SV_POSITION;UNITY_VERTEX_OUTPUT_STEREO};
            Varyings Vert(Attributes input)
            {
                Varyings output=(Varyings)0;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 position=TransformObjectToWorld(input.positionOS.xyz);
                position+=TransformObjectToWorldNormal(normalize(input.normalOS))*clamp(_Width,0,.004)*_NormalSign;
                output.positionCS=TransformWorldToHClip(position);return output;
            }
            half4 Frag(Varyings input):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);return _Ink;}
            ENDHLSL
        }
    }
}
