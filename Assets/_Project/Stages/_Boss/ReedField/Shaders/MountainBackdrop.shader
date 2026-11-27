Shader "Stage/BossMountainBackdrop"
{
 Properties{
 _BaseMap("Existing mountain albedo",2D)="white"{} _BumpMap("Existing mountain relief",2D)="bump"{}
 _Tint("Mountain tint",Color)=(1,1,1,1) _NormalStrength("Relief",Range(0,2))=.9
 _AerialHaze("Layer aerial perspective",Range(0,.8))=0 _FogStrength("Distance haze",Range(0,1))=.35 _BaseHaze("Mist at mountain foot",Range(0,1))=.5
 _SnowHeight("Snow line",Float)=35 _SnowStrength("Pale summit",Range(0,1))=0
 }
 SubShader{
 Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
 TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseMap_ST;half4 _Tint;half _NormalStrength,_FogStrength,_BaseHaze,_AerialHaze;float _SnowHeight,_SnowStrength;
 CBUFFER_END
 struct A{float4 p:POSITION;float3 n:NORMAL;float4 t:TANGENT;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;half4 t:TEXCOORD2;float2 uv:TEXCOORD3;half fog:TEXCOORD4;half2 relief:TEXCOORD5;};
 V Vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.t=half4(TransformObjectToWorldDir(a.t.xyz),a.t.w*GetOddNegativeScale());o.uv=a.uv;o.fog=ComputeFogFactor(o.p.z);o.relief=a.color.rg;return o;}
 half3 Normal(V i){half3 n=normalize(i.n);half3 t=normalize(i.t.xyz);half3 b=cross(n,t)*i.t.w;half3 m=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_NormalStrength);return normalize(m.x*t+m.y*b+m.z*n);}
 ENDHLSL
 Pass{
 Name "Forward" Tags{"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #pragma multi_compile_fog
 half4 Frag(V i):SV_Target{
 half3 n=Normal(i);Light sun=GetMainLight();half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_Tint.rgb;
 half snow=smoothstep(_SnowHeight-1.8,_SnowHeight+3.5,i.w.y)*smoothstep(.03,.65,n.y)*_SnowStrength;albedo=lerp(albedo,half3(.67,.71,.74),snow);
 half diffuse=saturate(dot(n,sun.direction));
 half skyForm=smoothstep(-.25,.55,dot(n,normalize(half3(-.90,.25,-.25))));
 half3 ambient=max(SampleSH(n),half3(.025,.025,.025))*(.15+1.30*skyForm);
 half3 lit=albedo*lerp(.60,1,i.relief.x)*lerp(.72,1.15,i.relief.y)*(ambient+sun.color*diffuse);
 half3 color=lerp(lit,MixFog(lit,i.fog),_FogStrength);half low=exp2(-max(0,i.w.y+40)*.055)*_BaseHaze;
 half3 hazeColor=unity_FogColor.rgb*half3(.90,.98,1.04); color=lerp(color,hazeColor,_AerialHaze); return half4(lerp(color,hazeColor,low),1);
 }
 ENDHLSL
 }
 Pass{
 Name "DepthOnly" Tags{"LightMode"="DepthOnly"} ColorMask R ZWrite On
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 half4 Frag(V i):SV_Target{return 0;}
 ENDHLSL
 }
 Pass{
 Name "DepthNormals" Tags{"LightMode"="DepthNormals"} ZWrite On
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
 half4 Frag(V i):SV_Target{half3 n=Normal(i);
 #if defined(_GBUFFER_NORMALS_OCT)
 float2 oct=PackNormalOctQuadEncode(n);return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
 #else
 return half4(n,0);
 #endif
 }
 ENDHLSL
 }
 }
}
