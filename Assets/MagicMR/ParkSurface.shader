Shader "MagicMR/ParkSurface"
{
    Properties { _BaseColor("Color",Color)=(0.3,0.6,0.4,1) _Style("0 comic 1 ink 2 pixel",Float)=0 }
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
            half4 _BaseColor; float _Style;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; };
            V vert(A a) { V o; o.positionWS=TransformObjectToWorld(a.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); o.normalWS=TransformObjectToWorldNormal(a.normalOS); return o; }
            half4 frag(V i):SV_Target {
                float3 n=normalize(i.normalWS); float3 v=normalize(GetWorldSpaceViewDir(i.positionWS));
                Light light=GetMainLight(); float shade=saturate(dot(n,light.direction))*.65+.35;
                float bands=floor(shade*3)/3; float ink=step(.22,abs(dot(n,v)));
                float3 comic=_BaseColor.rgb*(.4+bands*.7)*ink;
                float hatch=step(.3,frac((i.positionWS.x+i.positionWS.y)*100));
                float3 wash=lerp(float3(.85,.84,.8),float3(.08,.1,.11),(1-shade)*(.7+.3*hatch))*ink;
                float3 pixel=floor(_BaseColor.rgb*4)/4*(.45+floor(shade*2)*.3)*ink;
                float3 col=_Style<1?lerp(comic,wash,_Style):lerp(wash,pixel,saturate(_Style-1));
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
