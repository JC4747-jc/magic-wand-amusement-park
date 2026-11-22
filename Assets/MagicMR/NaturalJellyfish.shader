Shader "MagicMR/NaturalJellyfish"
{
    Properties
    {
        _BaseColor("Pearl tint",Color)=(0.72,0.83,0.88,1)
        _Phase("Pulse phase",Float)=0
        _Opacity("Visibility",Range(0,1))=1
        _Motion("Motion strength",Range(0,1))=1
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;float _Phase,_Opacity,_Motion;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 colour:COLOR;float2 uv:TEXCOORD0;float2 anatomy:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 colour:COLOR;float2 uv:TEXCOORD2;float role:TEXCOORD3;UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A input)
            {
                V output=(V)0;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 p=input.positionOS.xyz;float pulse=sin(_Phase)*_Motion;
                p.xz*=1-pulse*.07;p.y*=1+pulse*.10;
                float along=input.anatomy.y,role=input.anatomy.x;
                if(role>0.5&&role<2.5)
                {
                    float amplitude=role<1.5?.0035:.015;
                    // Phase wraps at 2*pi: integer temporal frequencies keep
                    // both position and velocity continuous at that boundary.
                    p.x+=sin(_Phase+input.uv.x*6.283185+along*2.5)*amplitude*along*along*_Motion;
                    p.z+=cos(_Phase+input.uv.x*6.283185-along*1.8)*amplitude*.8*along*along*_Motion;
                }
                output.positionWS=TransformObjectToWorld(p);output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);output.colour=input.colour;output.uv=input.uv;output.role=role;return output;
            }
            half4 Frag(V input,FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal=normalize(input.normalWS),view=SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half rim=pow(1-saturate(abs(dot(normal,view))),2.2);
                half vein=0;
                if(input.role<.5)
                {
                    float radial=abs(sin(input.uv.x*100.53096+sin(input.uv.y*10)*.13));
                    vein=(1-smoothstep(.015,.09,radial))*smoothstep(.13,.7,input.uv.y);
                }
                Light light=GetMainLight();half diffuse=abs(dot(normal,light.direction));
                half3 colour=input.colour.rgb*_BaseColor.rgb*(.55+diffuse*.25)+half3(.45,.52,.55)*rim*.35;
                half pulse=.5+.5*cos(_Phase-input.uv.y*4);
                half3 glow=lerp(half3(.22,.54,.72),half3(.83,.28,.52),pulse);
                colour+=(vein*.6+rim*.28)*glow;
                if(input.role>0.5 && input.role<2.5)
                    colour+=glow*.18*pow(pulse,3);
                half alpha=saturate(input.colour.a*(.55+rim*.8)+vein*.075)*saturate(_Opacity);
                // Keep the inner membrane visible from below, with less
                // accumulation where front/back bell triangles overlap.
                if(input.role<.5)alpha*=IS_FRONT_VFACE(front,1,.35);
                if(input.role>1.5&&input.role<2.5)
                {
                    // A soft folded membrane, without high-frequency alpha
                    // patterns that reveal triangles as a wire-like lattice.
                    alpha=input.colour.a*.55*saturate(_Opacity);
                    colour=input.colour.rgb*_BaseColor.rgb*(.76+diffuse*.08);
                }
                return half4(colour*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
