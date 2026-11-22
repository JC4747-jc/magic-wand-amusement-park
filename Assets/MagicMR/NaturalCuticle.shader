Shader "MagicMR/NaturalCuticle"
{
    Properties {_BaseColor("Component tint",Color)=(1,1,1,1) _Style("0 natural 1 ink 2 pixel",Float)=0}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;float _Style;
        CBUFFER_END
        struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 colour:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
        struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 colour:COLOR;float2 uv:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
        V Vert(A input)
        {
            V output=(V)0;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(output.positionWS);
            output.normalWS=TransformObjectToWorldNormal(input.normalOS);output.colour=input.colour;output.uv=input.uv;return output;
        }
        half4 Depth(V input):SV_Target{return 0;}
        ENDHLSL
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            half4 Frag(V input,FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal=normalize(input.normalWS)*IS_FRONT_VFACE(front,1,-1);float3 view=SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                Light light=GetMainLight(TransformWorldToShadowCoord(input.positionWS));half diffuse=saturate(dot(normal,light.direction));
                half roughness=clamp(input.colour.a,.12,.85);half3 albedo=input.colour.rgb*_BaseColor.rgb;
                half grain=sin(input.uv.x*30+sin(input.uv.y*5))*sin(input.uv.y*30);
                albedo*=.96+grain*.045+sin(input.uv.y*18+input.uv.x*3)*.025;
                half specular=pow(saturate(dot(normal,SafeNormalize(light.direction+view))),lerp(130,12,roughness))*.16;
                half3 colour=albedo*(max(SampleSH(normal),half3(.19,.20,.21))+light.color*diffuse*.8*light.shadowAttenuation)+specular*light.color*light.shadowAttenuation;
                half3 wash=lerp(half3(.82,.80,.75),half3(.055,.065,.07),1-diffuse);
                // Flat voxel faces and an RGB 3/3/2-bit palette: 256 possible colours.
                half3 paletteLevels=half3(7,7,3);
                half3 pixelBase=input.colour.rgb*_BaseColor.rgb*(.48+floor(diffuse*3)*.22);
                half3 pixel=floor(saturate(pixelBase)*paletteLevels+.5)/paletteLevels;
                colour=_Style<1?lerp(colour,wash,saturate(_Style)):lerp(wash,pixel,saturate(_Style-1));return half4(colour,1);
            }
            ENDHLSL
        }
        Pass
        {
            Tags {"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment Depth
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection,_LightPosition;
            V ShadowVert(A input)
            {
                V output=Vert(input);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 direction=normalize(_LightPosition-output.positionWS);
                #else
                float3 direction=_LightDirection;
                #endif
                output.positionCS=ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(output.positionWS,output.normalWS,direction)));return output;
            }
            ENDHLSL
        }
        Pass
        {
            Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Depth
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
