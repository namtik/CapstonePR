Shader "Stage/HarborWindCloth"
{
 Properties {
  _BaseMap("Faded linen",2D)="white"{}
  _BaseColor("Tint",Color)=(1,1,1,1)
  _WindAmplitude("Wind amplitude",Range(0,.4))=.15
  _WindSpeed("Wind speed",Range(0,3))=.95
  _WindPhase("Wind phase offset",Float)=0
 }
 SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
  Cull Off
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
  CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST,_BaseColor;
   float _WindAmplitude,_WindSpeed,_WindPhase;
  CBUFFER_END
  struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
  float3 WindPosition(A i) {
   float pin=saturate(i.uv.y);pin*=pin;
   float phase=TransformObjectToWorld(float3(0,0,0)).x*.37+_WindPhase;
   float time=_Time.y*_WindSpeed;
   float gust=.6+.25*sin(time*.47+phase);
   float bend=(.55+sin(time*1.7+i.uv.y*3.3+phase)*.34+sin(time*3.2+i.uv.x*5.1-i.uv.y*4.5+phase)*.12)*gust;
   float3 p=i.p.xyz+i.n*(bend*_WindAmplitude*pin);
   p.x+=sin(time*1.3+i.uv.y*3+phase)*_WindAmplitude*.38*pin;
   return p;
  }
  ENDHLSL
  Pass {
   Name "ClothForward" Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #pragma multi_compile_instancing
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;float fog:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID};
   V vert(A i) {V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);o.w=TransformObjectToWorld(WindPosition(i));o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(i.n);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);o.fog=ComputeFogFactor(o.p.z);return o;}
   half4 frag(V i,FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target {
    UNITY_SETUP_INSTANCE_ID(i);
    half3 n=normalize(i.n)*IS_FRONT_VFACE(front,1,-1);
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
    half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
    half ndl=saturate(dot(n,sun.direction));
    half transmission=saturate(dot(-n,sun.direction))*.12;
    half3 illumination=SampleSH(n)+sun.color*(ndl+transmission)*lerp(.18,1,sun.shadowAttenuation);
    return half4(MixFog(albedo*max(illumination,half3(.008,.008,.008)),i.fog),1);
   }
   ENDHLSL
  }
  Pass {
   Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"}
   ZWrite On ZTest LEqual ColorMask 0
   HLSLPROGRAM
   #pragma vertex shadowVert
   #pragma fragment shadowFrag
   #pragma multi_compile_instancing
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   float3 _LightDirection,_LightPosition;
   float4 shadowVert(A i):SV_POSITION {
    UNITY_SETUP_INSTANCE_ID(i);
    float3 p=TransformObjectToWorld(WindPosition(i));float3 n=TransformObjectToWorldNormal(i.n);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
     float3 light=normalize(_LightPosition-p);
    #else
     float3 light=_LightDirection;
    #endif
    float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,light));
    #if UNITY_REVERSED_Z
     clip.z=min(clip.z,clip.w*UNITY_NEAR_CLIP_VALUE);
    #else
     clip.z=max(clip.z,clip.w*UNITY_NEAR_CLIP_VALUE);
    #endif
    return clip;
   }
   half4 shadowFrag():SV_Target{return 0;}
   ENDHLSL
  }
  Pass {
   Name "DepthOnly" Tags {"LightMode"="DepthOnly"}
   ZWrite On ColorMask R
   HLSLPROGRAM
   #pragma vertex depthVert
   #pragma fragment depthFrag
   #pragma multi_compile_instancing
   float4 depthVert(A i):SV_POSITION{UNITY_SETUP_INSTANCE_ID(i);return TransformObjectToHClip(WindPosition(i));}
   half4 depthFrag():SV_Target{return 0;}
   ENDHLSL
  }
 }
}
