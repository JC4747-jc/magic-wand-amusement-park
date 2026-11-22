Shader "MagicMR/NaturalTreeLeaves"
{
    Properties
    {
        _BaseMap("Original leaf colour",2D)="white"{} _BumpMap("Original leaf normal",2D)="bump"{}
        _AlphaMap("Original leaf silhouette",2D)="white"{} _Cutoff("Silhouette cutoff",Range(0,1))=0.4
        _BaseColor("Component tint",Color)=(1,1,1,1) _BumpScale("Normal strength",Float)=0.5
        _SeasonTint("Season colour",Color)=(0.6,0.3,0.08,1) _SeasonMix("Season mix",Range(0,1))=0
        _LeafCoverage("Retained whole leaves",Range(0,1))=1 _Style("0 natural 1 ink 2 pixel",Float)=0
        _ToonMix("Season tree anime canopy",Range(0,1))=0
        _LeafVertexTint("Authored leaf colour variation",Range(0,1))=0
        _WillowNightFill("Dark willow leaf fill",Range(0,1))=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
        TEXTURE2D(_AlphaMap);SAMPLER(sampler_AlphaMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;half4 _BaseColor,_SeasonTint;float _BumpScale,_SeasonMix,_LeafCoverage,_Style,_Cutoff,_ToonMix,_LeafVertexTint,_WillowNightFill;
        CBUFFER_END
        struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;half4 colour:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
        struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 tangentWS:TEXCOORD2;float2 uv:TEXCOORD3;half mask:TEXCOORD4;half3 leafTint:TEXCOORD5;UNITY_VERTEX_OUTPUT_STEREO};
        Varyings Vert(Attributes input)
        {
            Varyings output=(Varyings)0;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexNormalInputs basis=GetVertexNormalInputs(input.normalOS,input.tangentOS);
            output.positionWS=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(output.positionWS);
            output.normalWS=basis.normalWS;output.tangentWS=half4(basis.tangentWS,input.tangentOS.w*GetOddNegativeScale());
            output.uv=TRANSFORM_TEX(input.uv,_BaseMap);output.mask=input.colour.a;output.leafTint=input.colour.rgb;return output;
        }
        void ClipLeaf(Varyings input){clip(_LeafCoverage-input.mask);clip(SAMPLE_TEXTURE2D(_AlphaMap,sampler_AlphaMap,input.uv).r-_Cutoff);}
        half4 ClipOnly(Varyings input):SV_Target {ClipLeaf(input);return 0;}
        ENDHLSL
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            half4 Frag(Varyings input,FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);ClipLeaf(input);
                half3 normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,input.uv),_BumpScale);
                half3 tangent=normalize(input.tangentWS.xyz),normal=normalize(input.normalWS);
                half3 bitangent=cross(normal,tangent)*input.tangentWS.w;
                normal=normalize(TransformTangentToWorld(normalTS,half3x3(tangent,bitangent,normal)))*IS_FRONT_VFACE(front,1,-1);
                half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb*lerp(half3(1,1,1),input.leafTint,_LeafVertexTint);
                half luminance=dot(albedo,half3(.2126,.7152,.0722));
                albedo=lerp(albedo,_SeasonTint.rgb*luminance*2,_SeasonMix)*_BaseColor.rgb;
                Light light=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse=saturate(dot(normal,light.direction));half transmission=saturate(dot(-normal,light.direction))*.13;
                half3 colour=albedo*(max(SampleSH(normal),half3(.12,.14,.13))+light.color*(diffuse*.8+transmission)*light.shadowAttenuation);
                half shade=diffuse*.65+.35;half3 wash=lerp(half3(.85,.84,.8),half3(.08,.1,.11),1-shade);
                half3 pixel=floor(albedo*4)/4*(.45+floor(shade*2)*.3);
                colour=_Style<1?lerp(colour,wash,saturate(_Style)):lerp(wash,pixel,saturate(_Style-1));
                // A silver-green fill keeps narrow, unlit fronds visible in the dark variant.
                half grazing=1-saturate(abs(dot(normal,GetWorldSpaceNormalizeViewDir(input.positionWS))));
                colour+=half3(.045,.095,.074)*_WillowNightFill*(.48+.52*grazing*grazing);
                half3 celAlbedo=lerp(half3(.24,.42,.14),_SeasonTint.rgb,_SeasonMix)*lerp(1,clamp(luminance*2,.55,1.2),.25);
                half celDiffuse=saturate(dot(normalize(input.normalWS)*IS_FRONT_VFACE(front,1,-1),light.direction));
                half celShade=celDiffuse>.65?.95:celDiffuse>.25?.68:.4;
                half3 cel=celAlbedo*(half3(.24,.25,.24)+light.color*celShade*lerp(.55,1,light.shadowAttenuation));
                return half4(lerp(colour,cel,saturate(_ToonMix)),1);
            }
            ENDHLSL
        }
        Pass
        {
            Tags {"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ClipOnly
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection,_LightPosition;
            Varyings ShadowVert(Attributes input)
            {
                Varyings output=Vert(input);
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
            #pragma fragment ClipOnly
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
