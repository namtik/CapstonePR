Shader "Stage/HarborWater"
{
 Properties {
 _DeepColor("Deep water",Color)=(.055,.22,.30,1)
 _NearColor("Shallow water",Color)=(.08,.36,.38,1)
 _HorizonColor("Water horizon",Color)=(.25,.43,.59,1)
 _FoamColor("Salt foam",Color)=(.72,.79,.80,1)
 [Normal] _RippleNormal("Fine ripple normal",2D)="bump"{}
 _SkyCube("Preset sky reflection",Cube)=""{}
 _StageReflection("Cached pier reflection",2D)="black"{}
 _NormalTiling("Ripple frequency",Float)=.16
 _WaveStrength("Swell normal strength",Range(0,1))=.65
 _RippleStrength("Fine ripple strength",Range(0,1))=.48
 _FoamStrength("Contact foam",Range(0,1))=.38
 _SurfaceFoamStrength("Small broken surface foam",Range(0,.2))=.035
 _GlintStrength("Sun glints",Range(0,1))=.6
 _SkyReflection("Reflection strength",Range(0,1))=.58
 _RefractionStrength("Shallow refraction",Range(0,.05))=.018
 _StageReflectionAvailable("Cached pier reflection enabled",Float)=0
 _WaterDetailTime("Water preview time (-1: automatic)",Float)=-1
 _ShoreZ("Legacy shoreline",Float)=-1.55
 }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Transparent-10"}
 Pass {
 Name "HarborWater" Tags {"LightMode"="UniversalForward"}
 ZWrite On
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fog
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
 TEXTURE2D(_RippleNormal);SAMPLER(sampler_RippleNormal);
 TEXTURECUBE(_SkyCube);SAMPLER(sampler_SkyCube);
 TEXTURE2D(_StageReflection);SAMPLER(sampler_StageReflection);
 CBUFFER_START(UnityPerMaterial)
 float4 _DeepColor,_NearColor,_HorizonColor,_FoamColor;
 float4x4 _StageReflectionVP;
 float4 _ReflectionCameraPosition;
 float _NormalTiling,_WaveStrength,_RippleStrength,_FoamStrength,_SurfaceFoamStrength;
 float _GlintStrength,_SkyReflection,_RefractionStrength,_StageReflectionAvailable,_WaterDetailTime,_ShoreZ;
 float4x4 _BoatWaterWorldToLocal0,_BoatWaterWorldToLocal1,_BoatWaterWorldToLocal2;
 float4 _BoatWaterBounds0,_BoatWaterBounds1,_BoatWaterBounds2;
 float _BoatWaterMaskCount;
 CBUFFER_END
 struct A{float4 p:POSITION;};
 struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float fog:TEXCOORD1;};
 V vert(A i){
  V o;o.w=TransformObjectToWorld(i.p.xyz);float t=_WaterDetailTime<0?_Time.y:_WaterDetailTime;
  float fade=(1-smoothstep(25,50,o.w.z))*(1-smoothstep(24,36,abs(o.w.x)));
  o.w.y+=(sin(dot(o.w.xz,float2(.42,.76))-t*.63)*.027+sin(dot(o.w.xz,float2(-.83,.36))+t*.81)*.016)*fade;
  o.p=TransformWorldToHClip(o.w);o.fog=ComputeFogFactor(o.p.z);return o;
 }
 float hash21(float2 p){p=frac(p*float2(123.34,345.45));p+=dot(p,p+34.345);return frac(p.x*p.y);}
 float noise(float2 p){float2 g=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash21(g),hash21(g+float2(1,0)),f.x),lerp(hash21(g+float2(0,1)),hash21(g+1),f.x),f.y);}
 // Reject displaced water only inside the closed hull, including heave and tilt.
 // Most ocean pixels exit at the inexpensive world-space bounding rectangle.
 bool InsideSkiff(float3 world,float4 bounds,float4x4 frame){
  bool inside=false;
  [branch] if(all(abs(world.xz-bounds.xy)<=bounds.zw)){
   float3 p=mul(frame,float4(world,1)).xyz;
   float z=abs(p.z);
   [branch] if(z<=2.22&&p.y>=.015){
    float w=.65,rim=.46;
    if(z<.58){float u=z/.58;w=lerp(.65,.63,u);rim=lerp(.46,.47,u);}
    else if(z<1.28){float u=(z-.58)/.70;w=lerp(.63,.51,u);rim=lerp(.47,.52,u);}
    else if(z<1.82){float u=(z-1.28)/.54;w=lerp(.51,.28,u);rim=lerp(.52,.59,u);}
    else{float u=(z-1.82)/.40;w=lerp(.28,.022,u);rim=lerp(.59,p.z<0?.68:.71,u);}
    float section=p.y<.25?lerp(.29,.83,saturate((p.y-.025)/.225)):lerp(.83,1,saturate((p.y-.25)/(rim-.25)));
    inside=p.y<=rim+.005&&abs(p.x)<w*section+.003;
   }
  }
  return inside;
 }
 half4 frag(V i):SV_Target {
  [branch] if(_BoatWaterMaskCount>.5&&InsideSkiff(i.w,_BoatWaterBounds0,_BoatWaterWorldToLocal0))clip(-1);
  [branch] if(_BoatWaterMaskCount>1.5&&InsideSkiff(i.w,_BoatWaterBounds1,_BoatWaterWorldToLocal1))clip(-1);
  [branch] if(_BoatWaterMaskCount>2.5&&InsideSkiff(i.w,_BoatWaterBounds2,_BoatWaterWorldToLocal2))clip(-1);
  float t=_WaterDetailTime<0?_Time.y:_WaterDetailTime;float2 x=i.w.xz;
  float dist=distance(i.w,_WorldSpaceCameraPos);
  float fineFade=1-smoothstep(32,150,dist);
  float phase=noise(x*.23+float2(t*.008,0));
  float p1=dot(x,float2(.42,.76))-t*.63+phase*1.8;
  float p2=dot(x,float2(-.83,.36))+t*.81+phase*1.3;
  float2 swell=float2(.42,.76)*cos(p1)*.075+float2(-.83,.36)*cos(p2)*.035;
  half3 r1=UnpackNormal(SAMPLE_TEXTURE2D(_RippleNormal,sampler_RippleNormal,x*_NormalTiling+float2(t*.013,-t*.019)));
  float2 uv2=float2(x.x*.81-x.y*.59,x.x*.59+x.y*.81)*(_NormalTiling*1.87)+float2(-t*.017,t*.011);
  half3 r2=UnpackNormal(SAMPLE_TEXTURE2D(_RippleNormal,sampler_RippleNormal,uv2));
  float2 second=float2(r2.x*.81+r2.y*.59,-r2.x*.59+r2.y*.81);
  float2 slope=swell*_WaveStrength*(1-smoothstep(48,160,dist))
   +(r1.xy+second*.48)*_RippleStrength*fineFade;
  float3 n=normalize(float3(-slope.x,1,-slope.y));
  float3 v=GetWorldSpaceNormalizeViewDir(i.w);
  Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
  float2 ss=GetNormalizedScreenSpaceUV(i.p);
  float raw=SampleSceneDepth(ss);
  float d=raw;
  #if !UNITY_REVERSED_Z
  d=lerp(UNITY_NEAR_CLIP_VALUE,1,d);
  #endif
  float3 opaque=ComputeWorldSpacePosition(ss,d,UNITY_MATRIX_I_VP);
  float below=step(opaque.y,i.w.y-.012);
  float gap=max(.001,i.w.y-opaque.y);
  float separation=below>0?gap:distance(opaque,i.w);
  float shallow=1-smoothstep(.08,2.4,separation);
  half3 light=SampleSH(n)*.82+sun.color*(saturate(dot(n,sun.direction))*.55+.08)*lerp(.55,1,sun.shadowAttenuation);
  half3 water=lerp(_DeepColor.rgb,_NearColor.rgb,shallow*.22+(1-smoothstep(10,45,dist))*.18);
  half3 body=water*max(light,half3(.012,.012,.012));
  // Depth-checked opaque refraction is limited to geometry below the water.
  float2 refractedUV=ss+slope*_RefractionStrength*fineFade*saturate(gap*3);
  float refractedDepth=LinearEyeDepth(SampleSceneDepth(refractedUV),_ZBufferParams);
  float waterDepth=-TransformWorldToView(i.w).z;
  if(refractedDepth<waterDepth-.03)refractedUV=ss;
  half3 transmission=SampleSceneColor(refractedUV)*exp(-half3(.62,.27,.18)*min(gap,12));
  body=lerp(body,transmission,below*exp(-gap*1.1)*.58);
  float3 reflected=reflect(-v,n);
  half3 sky=SAMPLE_TEXTURECUBE_LOD(_SkyCube,sampler_SkyCube,reflected,1.5).rgb;
  float4 reflectedClip=mul(_StageReflectionVP,float4(i.w.x,-.18,i.w.z,1));
  float2 reflectedUV=reflectedClip.xy/max(.001,reflectedClip.w)*.5+.5;
  float validUV=step(0,reflectedUV.x)*step(reflectedUV.x,1)*step(0,reflectedUV.y)*step(reflectedUV.y,1)*step(.001,reflectedClip.w);
  float border=min(min(reflectedUV.x,1-reflectedUV.x),min(reflectedUV.y,1-reflectedUV.y));
  float cameraMatch=1-smoothstep(.03,.2,distance(_WorldSpaceCameraPos,_ReflectionCameraPosition.xyz));
  float reflectionWeight=validUV*saturate(border*24)*_StageReflectionAvailable*cameraMatch;
  float2 reflectionWarp=float2(slope.x,-slope.y)*.022*fineFade;
  half3 pier=SAMPLE_TEXTURE2D(_StageReflection,sampler_StageReflection,saturate(reflectedUV+reflectionWarp)).rgb;
  half3 reflection=lerp(sky,pier,reflectionWeight);
  float fresnel=.025+.975*pow(1-saturate(dot(n,v)),5);
  half3 c=lerp(body,reflection*half3(.46,.73,.92),fresnel*_SkyReflection);
  float rough=.15+(1-fineFade)*.07+min(.15,fwidth(n.x)*.4);
  float nh=saturate(dot(n,normalize(sun.direction+v)));
  float a2=rough*rough*rough*rough,den=nh*nh*(a2-1)+1;
  float ggx=a2/max(.000005,3.14159*den*den);
  c+=sun.color*min(1.5,ggx*.032)*_GlintStrength*sun.shadowAttenuation*saturate(sun.direction.y*8+.15);
  float contact=1-smoothstep(.018,.22,separation);
  float edge=saturate(fwidth(separation)*10);
  float grain=noise(x*5.1+float2(t*.11,-t*.13));
  float wash=sin(t*1.07-x.x*.61+phase*4)*.5+.5;
  float foam=contact*edge*smoothstep(.39,.67,grain+wash*.16)*_FoamStrength;
  float patches=noise(x*.92+float2(-t*.025,t*.043));
  float crest=smoothstep(.13,.26,length(r1.xy))*smoothstep(.65,.81,patches);
  c=lerp(c,_FoamColor.rgb*max(light,half3(.012,.012,.012)),saturate(foam+crest*_SurfaceFoamStrength*fineFade));
  half3 fogColor=lerp(unity_FogColor.rgb,sky*half3(.48,.70,.84),.65);
  c=lerp(c,fogColor,(1-ComputeFogIntensity(i.fog))*.36);
  return half4(c,1);
 }
 ENDHLSL
 }
 }
}