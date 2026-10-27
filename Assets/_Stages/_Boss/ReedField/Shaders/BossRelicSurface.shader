Shader "Stage/BossRelicSurface"
{
 Properties {
  _BaseColor("Tint",Color)=(1,1,1,1)
  _DetailStrength("Fine surface detail",Range(0,1))=0
  _MetalPolish("Steel polish",Range(0,1))=.7
  [Toggle] _BladeSurfaceEnabled("Dedicated steel surface",Float)=0
  _BladeBaseMap("Blade steel base color",2D)="white"{}
  _BladeNormalRoughness("Blade RG normal, B roughness, A grain",2D)="gray"{}
  _BladeReflection("Baked blade environment RGBM6",Cube)=""{}
  _BladeReflectionStrength("Steel environment strength",Range(0,3))=1.25
  _BladeRelief("Steel micro relief",Range(0,2))=.65
  _SurfaceDetails("Surface details: RG normal, B tone, A roughness",2D)="gray" {}
  _MicroNormal("Surface relief",Range(0,1.5))=.65
  _WearStrength("Fitting patina",Range(0,1))=.4
  _AmbientMetalReflection("Ambient metal readability",Range(0,5))=3.2
  _WindAmplitude("Ribbon flutter, metres",Range(0,.3))=0
  _WindSpeed("Ribbon wind speed",Range(.1,3))=1
  _WindDirection("World wind direction",Vector)=(-.894427,0,-.447214,0)
  [HideInInspector] _PreviewTime("Preview time",Float)=-1
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Cull Off ZWrite On
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor;float4 _WindDirection;float _WindAmplitude,_WindSpeed,_PreviewTime,_DetailStrength,_MetalPolish,_MicroNormal,_WearStrength,_AmbientMetalReflection;
   float _BladeSurfaceEnabled,_BladeReflectionStrength,_BladeRelief;
  CBUFFER_END
  TEXTURE2D(_SurfaceDetails);SAMPLER(sampler_SurfaceDetails);
  TEXTURE2D(_BladeBaseMap);SAMPLER(sampler_BladeBaseMap);
  TEXTURE2D(_BladeNormalRoughness);SAMPLER(sampler_BladeNormalRoughness);
  TEXTURECUBE(_BladeReflection);SAMPLER(sampler_BladeReflection);
  struct A {float4 p:POSITION;float3 n:NORMAL;half4 c:COLOR;float2 uv:TEXCOORD0;float2 detail:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
  float3 Position(A a) {
   float3 p=TransformObjectToWorld(a.p.xyz);
   if(_WindAmplitude<.0001)return p;
   float u=saturate(a.uv.x),pin=u*u;
   float t=(_PreviewTime>=0?_PreviewTime:_Time.y)*_WindSpeed;
   float3 d=normalize(float3(_WindDirection.x,0,_WindDirection.z));
   float3 side=float3(-d.z,0,d.x);
   float wave=sin(t*4.2-u*8.5)*.70+sin(t*6.1-u*15)*.20;
   float gust=.78+.22*sin(t*.91+u*.6);
   p+=(side*wave+float3(0,.70*sin(t*4.2-u*8.5+.7)+.18*sin(t*7.3-u*11),0)+d*.18*sin(t*1.1-u*2))*(_WindAmplitude*pin*gust);
   return p;
  }
  ENDHLSL
  Pass {
   Name "Forward" Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;half4 c:TEXCOORD2;half fog:TEXCOORD3;float2 uv:TEXCOORD4;float2 detail:TEXCOORD5;float3 local:TEXCOORD6;UNITY_VERTEX_OUTPUT_STEREO};
   V Vert(A a){V o=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.w=Position(a);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.c=a.c;o.uv=a.uv;o.detail=a.detail;o.local=a.p.xyz;o.fog=ComputeFogFactor(o.p.z);return o;}

   float2 BladeUV(float3 p) {
    float t=saturate(p.y/.95);
    float centre=-.066*pow(1-t,1.75);
    float width=.037*(.70+.30*t);
    if(p.y<.105)width*=max(.004,pow(saturate(p.y/.105),.72));
    return float2((p.x-centre)/max(width,.0001)+.55,p.y*2.4);
   }
   // A single packed 512px atlas serves every surface on the existing mesh.
   // Derivatives are taken before wrapping to keep mip selection continuous.
   half4 SurfaceDetails(float2 uv,float tile,half defaultRoughness,out half fade) {
    float2 gx=ddx(uv)*.46875,gy=ddy(uv)*.46875;
    float footprint=max(length(gx),length(gy))*512;
    fade=1-smoothstep(3,18,footprint);
    float clampGradient=min(1,8/max(footprint,.001));
    float2 offset=float2(fmod(tile,2),floor(tile*.5))*.5;
    float2 atlasUV=offset+.015625+frac(uv)*.46875;
    half4 packed=SAMPLE_TEXTURE2D_GRAD(_SurfaceDetails,sampler_SurfaceDetails,atlasUV,gx*clampGradient,gy*clampGradient);
    return lerp(half4(.5,.5,.5,defaultRoughness),packed,fade);
   }
   half3 ReliefNormal(float3 world,half3 n,float2 uv,half2 normalXY,half strength) {
    float3 dp1=ddx(world),dp2=ddy(world);
    float2 duv1=ddx(uv),duv2=ddy(uv);
    float3 a=cross(dp2,n),b=cross(n,dp1);
    float3 tangent=a*duv1.x+b*duv2.x,bitangent=a*duv1.y+b*duv2.y;
    float largest=max(dot(tangent,tangent),dot(bitangent,bitangent));
    if(largest<1e-16)return n;
    float inv=rsqrt(largest);
    half2 bump=(normalXY*2-1)*strength;
    return normalize(n*sqrt(saturate(1-dot(bump,bump)))+(tangent*bump.x+bitangent*bump.y)*inv);
   }
   half4 PolishedBlade(V i,half3 n) {
    float2 blade=BladeUV(i.local);
    float2 uv=float2(saturate(blade.x*.5+.25),saturate(i.local.y/.95));
    half3 steel=SAMPLE_TEXTURE2D(_BladeBaseMap,sampler_BladeBaseMap,uv).rgb*_BaseColor.rgb;
    half4 data=SAMPLE_TEXTURE2D(_BladeNormalRoughness,sampler_BladeNormalRoughness,uv);
    n=ReliefNormal(i.w,n,uv,data.rg,_BladeRelief);
    float aa=max(fwidth(blade.x),.004);
    float hamon=.265+.020*sin(i.local.y*57)+.008*sin(i.local.y*143)+(data.a-.5)*.025;
    half edge=1-smoothstep(hamon-aa,hamon+.025+aa,blade.x);
    half temper=exp2(-abs(blade.x-hamon)*42);
    half roughness=lerp(data.b+.025,data.b-.085,edge)+temper*.04;
    steel*=1+temper*.040;
    half variance=max(dot(ddx(n),ddx(n)),dot(ddy(n),ddy(n)));
    roughness=clamp(sqrt(roughness*roughness+min(variance,.025)),.13,.43);
    half3 view=GetWorldSpaceNormalizeViewDir(i.w);
    BRDFData brdf;half alpha=1;
    InitializeBRDFData(steel,1,half3(0,0,0),1-roughness,alpha,brdf);
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
    half3 color=LightingPhysicallyBased(brdf,sun,n,view);
    half3 direction=reflect(-view,n);
    float mip=roughness*(1.7-.7*roughness)*7;
    half4 encoded=SAMPLE_TEXTURECUBE_LOD(_BladeReflection,sampler_BladeReflection,direction,mip);
    half3 environment=encoded.rgb*encoded.a*6;
    half envLuma=dot(environment,half3(.2126,.7152,.0722));
    environment=lerp(environment,envLuma.xxx,.70h);
    half fresnel=Pow4(1-saturate(dot(n,view)));
    color+=EnvironmentBRDF(brdf,half3(0,0,0),environment*_BladeReflectionStrength,fresnel);
    return half4(MixFog(color,i.fog),1);
   }
   half4 Frag(V i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target {
    half3 n=normalize(i.n)*IS_FRONT_VFACE(face,1,-1);
    if(_WindAmplitude>.001){n=normalize(cross(ddy(i.w),ddx(i.w)));n*=dot(n,GetWorldSpaceNormalizeViewDir(i.w))>=0?1:-1;}
    half3 albedo=i.c.rgb*_BaseColor.rgb;
    if(_DetailStrength>.001) {
     float part=i.detail.x;
     if(part<.5 && _BladeSurfaceEnabled>.5)return PolishedBlade(i,n);
     float2 uv;float tile;half roughness;half metallic;half normalScale=_MicroNormal;
     float2 blade=BladeUV(i.local);
     if(part<.5) {
      uv=blade;tile=0;roughness=.29h;metallic=1.0h;normalScale*=.32h;
     } else if(part>2.5 && part<3.5) {
      uv=i.uv*80;tile=2;roughness=.87h;metallic=.015h;
     } else if(part>3.5 && part<4.5) {
      uv=float2(atan2(i.local.z/.014,i.local.x/.021)*.15915494*4.5,i.local.y*36);
      tile=3;roughness=.72h;metallic=0;
     } else {
      uv=part>4.5?i.local.xz*24:i.local.xy*float2(40,24);
      tile=1;roughness=.58h;metallic=.87h;normalScale*=.65h;
     }
     half fade;half4 packed=SurfaceDetails(uv,tile,roughness,fade);
     half amount=_DetailStrength;
     n=ReliefNormal(i.w,n,uv,packed.rg,normalScale*amount);
     albedo*=lerp(1,1+(packed.b-.5h)*1.45h,amount);
     roughness=lerp(roughness,packed.a,amount);
     if(part<.5) {
      // Polished carbon steel: neutral silver, quiet grind, hard bright bevel.
      half steelLuma=dot(albedo,half3(.2126,.7152,.0722));
      albedo=lerp(albedo,steelLuma.xxx,.72h)*half3(.985h,.997h,1.012h);
      half cloud=sin(i.local.y*15+blade.x*4)*sin(i.local.y*7-blade.x*9);
      albedo*=1+cloud*.028h;
      roughness=.185h+(roughness-.29h)*.65h;
      roughness+=cloud*.008h-(saturate(_MetalPolish)-.7h)*.12h;
      float hamon=.265+.028*sin(i.local.y*91)+.010*sin(i.local.y*173);
      float aa=max(fwidth(blade.x),.003);
      half hardenedEdge=1-smoothstep(hamon-aa,hamon+.025+aa,blade.x);
      half temper=exp2(-abs(blade.x-hamon)*52);
      albedo*=1+temper*.055h;
      roughness+=temper*.035h-hardenedEdge*.045h;
      roughness=clamp(roughness,.105h,.32h);
     } else if(tile==1) {
      half brass=part<2.5?1:smoothstep(.11h,.28h,i.c.r);
      roughness=lerp(roughness,roughness-.17h,brass);
      half patina=saturate((.50h-packed.b)*3)*_WearStrength*amount;
      albedo*=1-patina*.48h;
      roughness+=patina*.18h;
      // Worn edges follow the actual collar/guard dimensions, not random paint.
      half edgeWear=0;
      if(part>4.5 && i.local.y>.960 && i.local.y<.982) {
       float angle=atan2(i.local.z/.046,i.local.x/.066);
       float outer=length(i.local.xz/float2(.066,.046))/(1+.075*cos(angle*4)-.014*cos(angle*8));
       edgeWear=smoothstep(.968,1.005,outer)*.38h;
      } else if(part>1.5 && part<2.5 && i.local.y>.923 && i.local.y<.958) {
       float edgeDistance=min(abs(i.local.y-.924),abs(i.local.y-.957));
       edgeWear=(1-smoothstep(.0007,.0022,edgeDistance))*.27h;
      }
      albedo=lerp(albedo,max(albedo*1.35h,half3(.19,.18,.16)),edgeWear*amount);
      roughness-=edgeWear*.15h;
     }
     SurfaceData surface=(SurfaceData)0;
     surface.albedo=max(albedo,.005h);surface.alpha=1;surface.metallic=metallic;
     surface.specular=half3(0,0,0);surface.smoothness=saturate(1-roughness);
     surface.normalTS=half3(0,0,1);surface.occlusion=1;
     InputData input=(InputData)0;
     input.positionWS=i.w;input.positionCS=i.p;input.normalWS=n;
     input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.w);
     input.shadowCoord=TransformWorldToShadowCoord(i.w);input.bakedGI=SampleSH(n);
     input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);
     input.shadowMask=half4(1,1,1,1);
     // Native URP metal response follows the actual sun, sky/probe and time preset.
     half3 color=UniversalFragmentPBR(input,surface).rgb;
     // This stage changes a procedural sky without a per-frame reflection bake.
     // A low-frequency SH reflection keeps metal readable in the frozen presets.
     // It reuses existing ambient data, with no probe, cubemap or extra texture read.
     half3 reflected=reflect(-input.viewDirectionWS,n);
     half3 ambientReflection=max(SampleSH(reflected),0);
     half edgeFresnel=pow(1-saturate(dot(n,input.viewDirectionWS)),4);
     if(part<.5) {
      // Slightly white-balance steel reflections, keeping the actual preset intensity.
      half reflectionLuma=dot(ambientReflection,half3(.2126,.7152,.0722));
      half3 neutralReflection=lerp(ambientReflection,reflectionLuma.xxx,.62h);
      color+=surface.albedo*neutralReflection*_MetalPolish*(_AmbientMetalReflection*1.10h+.30h*edgeFresnel);
      half3 sky=max(SampleSH(half3(0,1,0)),0);
      half skyLuma=dot(sky,half3(.2126,.7152,.0722));
      half3 silverSky=lerp(sky,skyLuma.xxx,.62h);
      // Reflections split on the existing ridge/bevel; no painted moving glints.
      half cloudGlint=exp2(-abs(reflected.y+reflected.x*.18h-.18h)*lerp(12,23,surface.smoothness));
      half ridge=exp2(-abs(blade.x-.61h)*22);
      half edge=1-smoothstep(.05h,.26h,blade.x);
      half polishBand=cloudGlint*(2.0h+.80h*ridge)+ridge*1.40h+edge*.55h;
      color+=surface.albedo*silverSky*polishBand*_MetalPolish*2.5h;
     } else {
      color+=surface.albedo*ambientReflection*metallic*_MetalPolish*(_AmbientMetalReflection+.30h*edgeFresnel);
     }
     return half4(MixFog(color,i.fog),1);
    }
    // Preserve the existing lightweight animated tie material exactly.
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
    half diffuse=.20h+.80h*saturate(dot(n,sun.direction));
    half shadow=lerp(.30h,1.0h,sun.shadowAttenuation);
    half3 lighting=max(SampleSH(n)+sun.color*diffuse*shadow,half3(.012,.012,.016));
    half3 h=SafeNormalize(sun.direction+GetWorldSpaceNormalizeViewDir(i.w));
    half spec=pow(saturate(dot(n,h)),52)*i.c.a*.55h*shadow;
    return half4(MixFog(albedo*lighting+sun.color*spec,i.fog),1);
   }
   ENDHLSL
  }
  Pass {
   Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ColorMask 0
   HLSLPROGRAM
   #pragma vertex ShadowVert
   #pragma fragment ShadowFrag
   #pragma multi_compile_instancing
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   float3 _LightDirection,_LightPosition;
   float4 ShadowVert(A a):SV_POSITION {
    UNITY_SETUP_INSTANCE_ID(a);float3 p=Position(a);float3 n=TransformObjectToWorldNormal(a.n);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
     float3 light=normalize(_LightPosition-p);
    #else
     float3 light=_LightDirection;
    #endif
    float4 h=TransformWorldToHClip(ApplyShadowBias(p,n,light));
    #if UNITY_REVERSED_Z
     h.z=min(h.z,h.w*UNITY_NEAR_CLIP_VALUE);
    #else
     h.z=max(h.z,h.w*UNITY_NEAR_CLIP_VALUE);
    #endif
    return h;
   }
   half4 ShadowFrag():SV_Target{return 0;}
   ENDHLSL
  }
  Pass {
   Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ColorMask R
   HLSLPROGRAM
   #pragma vertex DepthVert
   #pragma fragment DepthFrag
   #pragma multi_compile_instancing
   float4 DepthVert(A a):SV_POSITION{UNITY_SETUP_INSTANCE_ID(a);return TransformWorldToHClip(Position(a));}
   half4 DepthFrag():SV_Target{return 0;}
   ENDHLSL
  }
 }
}

