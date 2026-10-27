Shader "Stage/CommonWindTrails"
{
 Properties
 {
  _BaseColor("Wind tint",Color)=(.83,.93,.96,1)
  _Opacity("Opacity",Range(0,1))=.7
  _Brightness("Brightness",Range(.2,2))=1.1
  _NightVisibility("Minimum light",Range(0,1))=.25
  _FogBlend("Wind fog color blend",Range(0,1))=1
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Blend SrcAlpha OneMinusSrcAlpha
  ZWrite Off Cull Off
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor;float _Opacity,_Brightness,_NightVisibility,_FogBlend;
   CBUFFER_END
   struct A {float4 p:POSITION;half4 c:COLOR;float2 uv:TEXCOORD0;};
   struct V {float4 p:SV_POSITION;half4 c:COLOR;float2 uv:TEXCOORD0;float fog:TEXCOORD1;};
   V Vert(A i){V o;float3 w=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(w);o.c=i.c;o.uv=i.uv;o.fog=ComputeFogFactor(o.p.z);return o;}
   half4 Frag(V i):SV_Target
   {
    float edge=1-smoothstep(.10,.5,abs(i.uv.y-.5));
    float tips=smoothstep(0,.13,i.uv.x)*(1-smoothstep(.8,1,i.uv.x));
    Light sun=GetMainLight();
    half3 light=max(SampleSH(half3(0,1,0))+sun.color*.35,half3(_NightVisibility,_NightVisibility,_NightVisibility));
    half3 color=_BaseColor.rgb*i.c.rgb*min(light,1.2)*_Brightness;
    color=lerp(color,MixFog(color,i.fog),_FogBlend);
    return half4(color,saturate(i.c.a*_BaseColor.a*_Opacity*edge*tips));
   }
   ENDHLSL
  }
 }
}
