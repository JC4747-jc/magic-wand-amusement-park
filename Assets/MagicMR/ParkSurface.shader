Shader "MagicMR/ParkSurface"
{
    Properties { _BaseColor("Color",Color)=(0.3,0.6,0.4,1) _Style("0 natural 1 ink 2 pixel",Float)=0 _Roughness("Roughness",Range(0.1,1))=0.72 }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor; float _Style; float _Roughness;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; };
            V vert(A a) { V o; o.positionWS=TransformObjectToWorld(a.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); o.normalWS=TransformObjectToWorldNormal(a.normalOS); return o; }
            half4 frag(V i):SV_Target {
                float3 n=normalize(i.normalWS); float3 v=normalize(GetWorldSpaceViewDir(i.positionWS));
                Light light=GetMainLight(); float shade=saturate(dot(n,light.direction))*.65+.35;
                float ink=step(.22,abs(dot(n,v)));
                float3 halfVector=SafeNormalize(light.direction+v);
                float specular=pow(saturate(dot(n,halfVector)),lerp(90,8,_Roughness))*.06;
                float3 ambient=max(SampleSH(n),float3(.16,.17,.19));
                float3 natural=_BaseColor.rgb*(ambient+light.color*saturate(dot(n,light.direction))*.8)+specular*light.color;
                float hatch=step(.3,frac((i.positionWS.x+i.positionWS.y)*100));
                float3 wash=lerp(float3(.85,.84,.8),float3(.08,.1,.11),(1-shade)*(.7+.3*hatch))*ink;
                float3 pixel=floor(_BaseColor.rgb*4)/4*(.45+floor(shade*2)*.3)*ink;
                float3 col=_Style<1?lerp(natural,wash,saturate(_Style)):lerp(wash,pixel,saturate(_Style-1));
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
