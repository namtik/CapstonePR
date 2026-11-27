Shader "Stage/SakuraPetal"
{
 Properties {
  _BaseMap("Existing tree atlas",2D)="white"{}
  _BaseColor("Petal tint",Color)=(1.15,1.55,.95,1)
  _NightVisibility("Minimum ambient light",Range(0,.5))=.12
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Blend SrcAlpha OneMinusSrcAlpha
  ZWrite Off Cull Back
  Pass {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor;float4 _BaseMap_ST;half _NightVisibility;
   CBUFFER_END
   struct A {float4 p:POSITION;half3 n:NORMAL;half4 c:COLOR;float2 uv:TEXCOORD0;};
   struct V {float4 p:SV_POSITION;half3 n:TEXCOORD0;half4 c:COLOR;float2 uv:TEXCOORD1;half fog:TEXCOORD2;};
   V Vert(A a){V o;float3 w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(w);o.n=TransformObjectToWorldNormal(a.n);o.c=a.c;o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.fog=ComputeFogFactor(o.p.z);return o;}
   half4 Frag(V i):SV_Target {
    half4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);half3 n=normalize(i.n);Light sun=GetMainLight();
    // Petals are thin translucent sheets: both orientations receive broad diffuse light.
    half diffuse=.45h+.55h*abs(dot(n,sun.direction));
    half3 ambient=SampleSH(half3(0,1,0));
    half3 light=clamp(ambient+sun.color*diffuse,_NightVisibility,1.25h);
    half3 color=tex.rgb*_BaseColor.rgb*i.c.rgb*light;
    return half4(MixFog(color,i.fog),tex.a*_BaseColor.a*i.c.a);
   }
   ENDHLSL
  }
 }
}

