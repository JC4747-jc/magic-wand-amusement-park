Shader "MagicMR/StatueCartoon"
{
    Properties
    {
        _BaseMap("Original albedo",2D)="white" {}
        _BaseColor("Base color",Color)=(1,1,1,1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=2
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Cull [_Cull] ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;half4 _BaseColor;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            Varyings Vert(Attributes input)
            {
                Varyings output=(Varyings)0;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);output.uv=TRANSFORM_TEX(input.uv,_BaseMap);return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal=normalize(input.normalWS);Light light=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half lambert=saturate(dot(normal,light.direction))*light.shadowAttenuation;
                half band=lambert>.65h?1.0h:lambert>.25h?.65h:.30h;
                half3 albedo=SAMPLE_TEXTURE2D_BIAS(_BaseMap,sampler_BaseMap,input.uv,1.5).rgb*_BaseColor.rgb;
                // Quantize illumination, not individual RGB channels (which invents colored speckles).
                // A small pigment floor keeps the dark metallic bust readable without PBR reflections.
                half luminance=dot(albedo,half3(.2126h,.7152h,.0722h));
                albedo=lerp(luminance.xxx,albedo,.65h)*.9h+.06h;
                half rim=smoothstep(.08h,.28h,saturate(dot(normal,GetWorldSpaceNormalizeViewDir(input.positionWS))));
                half3 color=albedo*(light.color*band+SampleSH(normal)*.35h);
                return half4(lerp(half3(.025h,.028h,.035h),color,rim),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
