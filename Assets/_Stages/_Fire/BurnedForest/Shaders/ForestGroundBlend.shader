Shader "Stage/ForestGroundBlend"
{
 Properties {
  _EarthMap("Earth",2D)="white"{} _ForestMap("Forest soil",2D)="white"{} _StoneMap("Stone",2D)="white"{}
  _EarthColor("Earth tint",Color)=(.76,.72,.63,1) _StoneColor("Stone tint",Color)=(.70,.71,.68,1)
  _Tiling("World tiling",Float)=.31
  _EarthNormal("Earth normal",2D)="bump"{}
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
   TEXTURE2D(_EarthMap);SAMPLER(sampler_EarthMap);TEXTURE2D(_ForestMap);SAMPLER(sampler_ForestMap);
   TEXTURE2D(_StoneMap);SAMPLER(sampler_StoneMap);TEXTURE2D(_EarthNormal);SAMPLER(sampler_EarthNormal);
   CBUFFER_START(UnityPerMaterial)
    float4 _EarthMap_ST;float4 _StoneMap_ST;half4 _EarthColor;half4 _StoneColor;float _Tiling;
   CBUFFER_END
   struct Attributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;};
   struct Varyings{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half3 masks:TEXCOORD2;half fog:TEXCOORD3;};
   Varyings Vert(Attributes v){Varyings o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.masks=v.color.rgb;o.fog=ComputeFogFactor(p.positionCS.z);return o;}
   half4 Frag(Varyings i):SV_Target{
    half3 n=normalize(i.normalWS);half3 w=pow(abs(n),4);w/=max(w.x+w.y+w.z,.001);
    float3 p=i.positionWS*_Tiling;
    half3 earth=SAMPLE_TEXTURE2D(_EarthMap,sampler_EarthMap,p.zy).rgb*w.x+SAMPLE_TEXTURE2D(_EarthMap,sampler_EarthMap,p.xz).rgb*w.y+SAMPLE_TEXTURE2D(_EarthMap,sampler_EarthMap,p.xy).rgb*w.z;
    half3 stone=SAMPLE_TEXTURE2D(_StoneMap,sampler_StoneMap,p.xz*.8).rgb;
    float strata=sin(i.positionWS.y*2.4+sin(i.positionWS.x*.33)*.65+sin(i.positionWS.z*.29)*.35);
    half slope=1-saturate(n.y);half layers=lerp(1,.80+.20*smoothstep(-.7,.6,strata),slope);
    half macro=.95+.05*sin(i.positionWS.x*.29+i.positionWS.z*.21);
    earth=lerp(half3(.40,.40,.40),earth,.88)*_EarthColor.rgb*layers*macro;
    float bandCoord=i.positionWS.y*1.22+sin(i.positionWS.x*.37)*.52+sin(i.positionWS.z*.29)*.1;
    float bands=sin(bandCoord*6.283);
    half exposure=smoothstep(.12,.45,slope)*(1-i.masks.g);
    half3 strataTint=lerp(half3(.88,.84,.78),half3(1.04,1.04,1.0),smoothstep(-.55,.7,bands));
    earth*=lerp(half3(1,1,1),strataTint,exposure*.40);
    half3 col=lerp(stone*_StoneColor.rgb,earth,i.masks.r);
    half3 forestSoil=SAMPLE_TEXTURE2D(_ForestMap,sampler_ForestMap,p.xz*.67).rgb*half3(.29,.32,.22);
    col=lerp(col,forestSoil,i.masks.g*.90)*i.masks.b;
    half exposedStone=smoothstep(.32,.72,slope)*(1-i.masks.g)*(.20+.08*sin(i.positionWS.x*.51+i.positionWS.y*.74));
    col=lerp(col,stone*_StoneColor.rgb*.87,exposedStone);
    half3 nx=UnpackNormal(SAMPLE_TEXTURE2D(_EarthNormal,sampler_EarthNormal,p.zy));
    half3 ny=UnpackNormal(SAMPLE_TEXTURE2D(_EarthNormal,sampler_EarthNormal,p.xz));
    half3 nz=UnpackNormal(SAMPLE_TEXTURE2D(_EarthNormal,sampler_EarthNormal,p.xy));
    nx.xy*=.40;ny.xy*=.40;nz.xy*=.40;
    n=normalize(half3(n.x*nx.z,n.y+nx.y,n.z+nx.x)*w.x+half3(n.x+ny.x,n.y*ny.z,n.z+ny.y)*w.y+half3(n.x+nz.x,n.y+nz.y,n.z*nz.z)*w.z);
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    half3 illumination=SampleSH(n)+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation;
    return half4(MixFog(col*illumination,i.fog),1);
   }
   ENDHLSL
  }
  Pass {
   Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"}
   ColorMask 0 ZWrite On
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
   float3 _LightDirection;
   struct Attributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;};
   float4 Vert(Attributes v):SV_POSITION{
    float3 p=TransformObjectToWorld(v.positionOS.xyz);float3 n=TransformObjectToWorldNormal(v.normalOS);
    float4 o=TransformWorldToHClip(ApplyShadowBias(p,n,_LightDirection));
    #if UNITY_REVERSED_Z
     o.z=min(o.z,UNITY_NEAR_CLIP_VALUE*o.w);
    #else
     o.z=max(o.z,UNITY_NEAR_CLIP_VALUE*o.w);
    #endif
    return o;
   }
   half4 Frag():SV_Target{return 0;}
   ENDHLSL
  }
  Pass {
   Name "DepthOnly" Tags {"LightMode"="DepthOnly"}
   ColorMask R ZWrite On
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Attributes{float4 positionOS:POSITION;};
   float4 Vert(Attributes v):SV_POSITION{return TransformObjectToHClip(v.positionOS.xyz);}
   half4 Frag():SV_Target{return 0;}
   ENDHLSL
  }
 }
}
