Shader "MagicMR/RoomPassthroughStyle"
{
    Properties
    {
        _RoomStyle("Watercolor / cyber",Float)=1
        _Reveal("Reveal",Range(0,1))=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _RoomStyle;
            float _Reveal;
            CBUFFER_END

            struct Attributes
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = TransformObjectToHClip(input.position.xyz);
                output.uv = input.uv * 2 - 1;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = input.uv;
                float radius = length(p);
                float time = _Time.y;
                float fade = 1 - smoothstep(.42, 1, radius);
                float reveal = 1 - smoothstep(0, .16, radius - 1.1 * _Reveal);
                float wash = sin(p.x*8 + sin(p.y*7 - time*.9) + time) * .5 + .5;
                float3 tint = lerp(float3(.15,.65,.68), float3(.95,.52,.29), wash);
                float opacity = .26 * fade * lerp(.45,1,wash);
                if (_RoomStyle > 1.5)
                {
                    float2 cell = abs(frac(p*6 + float2(0,time*.22)) - .5);
                    float lines = smoothstep(.43,.48,max(cell.x,cell.y));
                    float pulse = .6 + .4 * sin(radius*20 - time*3);
                    tint = lerp(float3(.16,.9,1),float3(.82,.22,.72),pulse);
                    opacity = fade * (.06 + lines * .42 * pulse);
                }
                return half4(tint, opacity * reveal * _Reveal);
            }
            ENDHLSL
        }
    }
}
