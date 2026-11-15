Shader "MagicMR/CharredSurface"
{
 Properties { _CharMap("Ash and fracture height",2D)="white"{} _Metal("Heat affected metal",Range(0,1))=0 _Heat("Cooling ember heat",Range(0,1))=0 }
 SubShader {
 Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
 Pass {
 Tags { "LightMode"="UniversalForward" }
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_CharMap); SAMPLER(sampler_CharMap);
 CBUFFER_START(UnityPerMaterial)
 float4 _CharMap_ST; float _Metal; float _Heat;
 CBUFFER_END
 struct A { float4 p:POSITION; float3 n:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
 struct V { float4 p:SV_POSITION; float3 world:TEXCOORD0; float3 local:TEXCOORD1; float3 n:TEXCOORD2; float3 on:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
 V Vert(A a) { V o; UNITY_SETUP_INSTANCE_ID(a); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.p=TransformObjectToHClip(a.p.xyz); o.world=TransformObjectToWorld(a.p.xyz); o.local=a.p.xyz*float3(.025,.08,.012)*40; o.n=TransformObjectToWorldNormal(a.n); o.on=a.n; return o; }
 half4 Frag(V i):SV_Target {
 UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 float3 weights=pow(abs(normalize(i.on)),4); weights/=max(dot(weights,1),.001);
 float4 t=SAMPLE_TEXTURE2D(_CharMap,sampler_CharMap,i.local.yz)*weights.x+SAMPLE_TEXTURE2D(_CharMap,sampler_CharMap,i.local.xz)*weights.y+SAMPLE_TEXTURE2D(_CharMap,sampler_CharMap,i.local.xy)*weights.z;
 float3 n=normalize(i.n); float3 dx=ddx(i.world),dy=ddy(i.world);
 float3 r1=cross(dy,n),r2=cross(n,dx); float det=dot(dx,r1);
 float3 grad=(r1*ddx(t.a)+r2*ddy(t.a))*sign(det)/max(abs(det),1e-10);
 n=normalize(n-grad*lerp(.00028,.000008,_Metal));
 Light light=GetMainLight(); float3 l=light.direction; float3 v=GetWorldSpaceNormalizeViewDir(i.world);
 float diffuse=saturate(dot(n,l));
 float soot=SAMPLE_TEXTURE2D_LOD(_CharMap,sampler_CharMap,i.local.xy*.57+float2(.13,.41),4).r;
 float3 oxidized=lerp(float3(.075,.065,.055),float3(.34,.29,.22),saturate(soot*4));
 float3 albedo=lerp(t.rgb,oxidized,_Metal);
 float spec=pow(saturate(dot(n,normalize(l+v))),lerp(12,48,_Metal))*lerp(.035,.32,_Metal);
 float3 illumination=max(SampleSH(n),float3(.23,.23,.23))+light.color*(.16+.75*diffuse);
 float cracks = pow(saturate(1-t.a), 5);
 float pulse = .85 + .15*sin(i.local.y*42 + _Time.y*9);
 float3 ember = float3(1,.16,.015)*cracks*_Heat*2.5*pulse;
 return half4(albedo*illumination+spec*light.color+ember,1);
 }
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 }
}

