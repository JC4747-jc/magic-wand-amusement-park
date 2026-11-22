Shader "MagicMR/ForestGround"
{
    Properties
    {
        _BaseMap("Forest floor colour",2D)="white"{}
        _BumpMap("Forest floor normal",2D)="bump"{}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _TileMetres("Texture width in metres",Float)=2
        _BumpScale("Normal strength",Float)=0.55
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; half4 _BaseColor; float _TileMetres, _BumpScale;
            CBUFFER_END
            struct A { float4 p:POSITION; float3 n:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; half fog:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i)
            {
                V o=(V)0; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(i.p.xyz); o.p=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(i.n); o.fog=ComputeFogFactor(o.p.z); return o;
            }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv=i.world.xz/max(.1,_TileMetres);
                half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb;
                float edge=sin(i.world.z*2.3)*.15+sin(i.world.z*5.7+i.world.x*3.1)*.09;
                float path=1-smoothstep(1.15,2.05,abs(i.world.x-2.4*sin(i.world.z*.19))+edge);
                half luminance=dot(albedo,half3(.2126,.7152,.0722));
                albedo=lerp(albedo,lerp(albedo,half3(luminance*1.06,luminance*.94,luminance*.78),.7),path*.65)*_BaseColor.rgb;
                half3 detail=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv),_BumpScale);
                half3 baseN=normalize(i.normal), t=normalize(half3(baseN.y,-baseN.x,0)), b=cross(t,baseN);
                half3 n=normalize(t*detail.x+b*detail.y+baseN*detail.z);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 color=albedo*(max(SampleSH(n),half3(.16,.18,.16))+light.color*saturate(dot(n,light.direction))*light.shadowAttenuation);
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
