Shader "Stage/MarketHouseInteriorSurface"
{
 Properties {
 _WorldPlaster("Continuous plaster mapping",Float)=0
 _SurfaceBreakup("Texture repetition breakup",Range(0,1))=0
 _DetailContrast("Surface detail contrast",Range(0,1.5))=0
 _MacroStrength("Broad surface variation",Range(0,1))=0
 _MicroStrength("Fine albedo relief",Range(0,.01))=0
 [Enum(UnityEngine.Rendering.CullMode)] _Cull("Face culling",Float)=2
 _BaseMap("Albedo",2D)="white"{} _BaseColor("Tint",Color)=(1,1,1,1)
 _BumpMap("Normal",2D)="bump"{} _BumpScale("Normal strength",Range(0,2))=.35
 _MetallicGlossMap("Metal / smoothness",2D)="white"{}
 _Smoothness("Smoothness multiplier",Range(0,1))=.6
 _GroundMode("Ground mode",Float)=0
 _GroundDetailMap("Reused detailed earth",2D)="white"{}
 _GroundDetailBlend("Earth detail blend",Range(0,1))=0
 _GroundBlendScale("Ground pattern variation scale",Range(.1,2))=.65
 _WearStrength("Wear",Range(0,1))=.28
 _WetStrength("Wet lower wood",Range(0,1))=.30
 _CoastalWeathering("Coastal weathering",Range(0,1))=0
 _HarborWaterHeight("Harbor water height",Float)=-.68
 _CoastalWetGloss("Wet wood smoothness",Range(0,1))=.56
 _Cutoff("Cutoff",Range(0,1))=.5
 }
 SubShader {
 Cull [_Cull]
 Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
 TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
 TEXTURE2D(_GroundDetailMap);SAMPLER(sampler_GroundDetailMap);
 TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
 TEXTURE2D(_MetallicGlossMap);SAMPLER(sampler_MetallicGlossMap);
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseMap_ST,_BaseColor;float _BumpScale,_Smoothness,_GroundMode,_GroundBlendScale,_WearStrength,_WetStrength,_Cutoff,_GroundDetailBlend;
 float _CoastalWeathering,_HarborWaterHeight,_CoastalWetGloss;
 float _WorldPlaster;float _SurfaceBreakup,_DetailContrast,_MacroStrength,_MicroStrength;
 CBUFFER_END
 struct A{float4 p:POSITION;float3 n:NORMAL;float4 tangent:TANGENT;float2 uv:TEXCOORD0;float4 color:COLOR;float4 coast:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float4 tangent:TEXCOORD2;float2 uv:TEXCOORD3;float4 color:TEXCOORD4;float fog:TEXCOORD5;float4 coast:TEXCOORD6;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.tangent=float4(TransformObjectToWorldDir(a.tangent.xyz),a.tangent.w*GetOddNegativeScale());o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.color=a.color;o.coast=a.coast;o.fog=ComputeFogFactor(o.p.z);return o;}
 float hash21(float2 p){p=frac(p*float2(123.34,345.45));p+=dot(p,p+34.345);return frac(p.x*p.y);}
 float noise(float2 p){float2 g=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash21(g),hash21(g+float2(1,0)),f.x),lerp(hash21(g+float2(0,1)),hash21(g+1),f.x),f.y);}
 // Continuous UV variation avoids visible blend cells while keeping the original grain density.
 void GroundTextures(float2 uv,float packedWeight,out float4 base,out float3 normal){
  float2 p=uv*_GroundBlendScale*.3;
  float2 warp=(float2(noise(p+17.13),noise(p+63.71))-.5)*1.35;
  float2 uva=uv+warp;
  float2 uvb=mul(float2x2(.81,-.59,.59,.81),uv)*1.37+warp*.47+float2(7.13,3.71);
  // Two existing albedo samples: compact earth and shared detailed forest soil.
  float4 a=SAMPLE_TEXTURE2D_GRAD(_BaseMap,sampler_BaseMap,uva,ddx(uva),ddy(uva));
  // Shared forest texture has a transparent circular border. Sample its opaque centre with continuous mirrored repeats.
  float2 detailUV=.24+abs(frac(uvb*.425)*2-1)*.52;
  float4 b=SAMPLE_TEXTURE2D_GRAD(_GroundDetailMap,sampler_GroundDetailMap,detailUV,ddx(detailUV),ddy(detailUV));
  b.rgb=lerp(b.rgb,dot(b.rgb,float3(.30,.55,.15))*float3(1.06,1.01,.92),.28);
  base=lerp(a,b,_GroundDetailBlend*(1-saturate(packedWeight)*.58));
  normal=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv),_BumpScale);
 }
 half4 frag(V i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC):SV_Target{
 UNITY_SETUP_INSTANCE_ID(i);
 float3 geom=normalize(i.n),tangent=normalize(i.tangent.xyz);
 float2 normalUV=i.uv;
 if(_WorldPlaster>.5){
 float3 face=abs(normalize(cross(ddx(i.w),ddy(i.w))));
 float2 planar=face.x>face.z?i.w.zy:i.w.xy;
 if(face.y>max(face.x,face.z))planar=i.w.xz;
 normalUV=planar*3.1;
 i.uv=planar*1.1*_BaseMap_ST.xy+_BaseMap_ST.zw;
 // Mirror nonseamless source plaster to keep both sides of repeat boundaries continuous.
 i.uv=abs(frac(i.uv*.5)*2-1);
 }
 float3 nts;float4 tex,packed;
 if(_GroundMode>.5){GroundTextures(i.uv,i.color.a,tex,nts);packed=float4(0,0,0,0);}
 else{
  nts=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,normalUV),_BumpScale);
  tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
  packed=SAMPLE_TEXTURE2D(_MetallicGlossMap,sampler_MetallicGlossMap,i.uv);
 }
 if(_SurfaceBreakup>.001){
 float2 uv2=mul(float2x2(.91,-.414,.414,.91),i.uv)*1.173+float2(3.17,7.61);
 if(_WorldPlaster>.5)uv2=abs(frac(uv2*.5)*2-1);
 half3 other=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv2).rgb;
 half blend=noise(float2(i.w.x*.31+i.w.z*.27,i.w.y*.47+i.w.z*.17));
 half3 detail=tex.rgb-other;
 tex.rgb=max(.003,lerp(tex.rgb,other,blend*_SurfaceBreakup*.6)+detail*_DetailContrast);
 }
 float3 n=normalize(tangent*nts.x+cross(geom,tangent)*i.tangent.w*nts.y+geom*nts.z);
 float faceSign=IS_FRONT_VFACE(facing,1.0,-1.0);n*=faceSign;geom*=faceSign;
 if(_MicroStrength>.00001){
 float h=dot(tex.rgb,float3(.30,.55,.15));
 float3 dx=ddx(i.w),dy=ddy(i.w);
 float3 bx=cross(dy,geom),by=cross(geom,dx);
 float det=dot(dx,bx);
 float3 grad=sign(det)*(ddx(h)*bx+ddy(h)*by)/max(abs(det),.000001);
 grad*=min(1,80/max(1,length(grad)));
 n=normalize(n-grad*_MicroStrength);
 }
 half3 col=tex.rgb*_BaseColor.rgb;
 float macro=noise(float2(i.w.x*.23+i.w.z*.19,i.w.y*.4+i.w.z*.11));
 col*=lerp(1,.73+macro*.54,_MacroStrength);
 float wear=noise(i.w.xz*.9+i.w.y*.13);
 float wet=0;float smoothness=packed.a*_Smoothness;
 if(_GroundMode<.5){
 col*=max(i.color.rgb,half3(.7,.7,.7));
 float faded=smoothstep(.5,.8,wear)*_WearStrength;
 col=lerp(col,col*.83+half3(.08,.075,.061),faded);
 wet=(1-smoothstep(-.10,.36,i.w.y+noise(i.w.xz*5)*.13))*_WetStrength;
 if(_CoastalWeathering>.01){
  float vertical=1-saturate(abs(geom.y)*1.5);
  float tidal=1-smoothstep(_HarborWaterHeight+.08,_HarborWaterHeight+.46,i.w.y+(wear-.5)*.10);
  float painted=smoothstep(.28,.62,saturate(i.coast.x)*.80+wear*.28)*smoothstep(.04,.22,i.coast.x);
  wet=lerp(wet,saturate(max(tidal*max(_WetStrength,.65),painted*.88)),_CoastalWeathering);
  float saltBand=(1-saturate(abs(i.w.y-(_HarborWaterHeight+.44))/.19))*vertical;
  float salt=saturate(saltBand*.50+i.coast.y*.50)*smoothstep(.42,.80,wear)*(1-wet*.60)*_CoastalWeathering;
  float luminance=dot(col,half3(.30,.55,.15));
  col=lerp(col,luminance*.88+half3(.08,.081,.071),salt*.55);
  float algae=(1-saturate(abs(i.w.y-(_HarborWaterHeight+.025))/.14))*vertical*smoothstep(.28,.72,wear)*_CoastalWeathering;
  col=lerp(col,col*half3(.72,.82,.58),algae*.42);
  // Targeted tidal and rope wear: UV4.w=1.5 hull / 1.75 post. Existing raw timber tags remain unchanged.
  float tidalDetail=step(1.25,i.coast.w)*(1-step(1.95,i.coast.w));
  if(tidalDetail>.5){
   float saltNoise=noise(float2(i.w.x*.9+i.w.z*1.3,i.w.y*1.7)*9.1);
   float tideHeight=i.w.y+(wear-.5)*.065+(saltNoise-.5)*.045;
   float oldWaterline=1-smoothstep(_HarborWaterHeight+.11,_HarborWaterHeight+.26,tideHeight);
   col*=1-oldWaterline*vertical*.09;
   float greenBand=(1-smoothstep(.035,.15,abs(tideHeight-(_HarborWaterHeight+.055))))*vertical;
   greenBand*=smoothstep(.28,.76,saltNoise)*.65;
   col=lerp(col,col*half3(.72,.82,.58),greenBand);
   float mineral=(1-smoothstep(.014,.055,abs(tideHeight-(_HarborWaterHeight+.19))))*vertical;
   col=lerp(col,col*.91+half3(.025,.025,.019),mineral*smoothstep(.34,.8,saltNoise)*.44);
   if(i.coast.w>1.65){
    float scrape=1-smoothstep(.039,.077,abs(i.w.y-i.coast.z+(saltNoise-.5)*.012));
    scrape*=vertical;
    col=lerp(col,col*.83+half3(.073,.066,.047),scrape*.8);
    n=normalize(lerp(n,geom,scrape*.20));
    // Narrow inset contact shadow sits under the old rope wrap.
    float groove=1-smoothstep(.0015,.0055,abs(i.w.y-i.coast.z+.004));
    col*=1-groove*vertical*.20;
   }
  }
  float rubbed=saturate(i.coast.z)*smoothstep(.30,.72,wear)*_CoastalWeathering;
  col=lerp(col,col*.87+half3(.042,.040,.033),rubbed*.32);
  n=normalize(lerp(n,geom,wet*.20*_CoastalWeathering));
 }
 // UV4.w uses explicit tags above 2; legacy meshes (missing W or W=0/1) keep the original finish.
 float rawTimber=saturate(i.coast.w-2.0);
 if(rawTimber>.001){
  float rawVertical=1-saturate(abs(geom.y)*1.5);
  float rawLuma=dot(col,half3(.30,.55,.15));
  col=lerp(col,rawLuma*half3(1.02,.98,.89),rawTimber*.55);
  col*=1-rawTimber*(.03+rawVertical*.13+smoothstep(.40,.70,wear)*.23);
  wet=lerp(wet,.18+smoothstep(.25,.85,saturate(i.coast.x))*.72,rawTimber);
  // Fine relief follows the reused albedo fibres rather than the old regular wave normal.
  // Reuses the already sampled albedo: no additional texture fetches.
  float grainHeight=dot(tex.rgb,float3(.30,.55,.15));
  float3 dx=ddx(i.w),dy=ddy(i.w);
  float3 basisX=cross(dy,geom),basisY=cross(geom,dx);
  float determinant=dot(dx,basisX);
  float3 gradient=sign(determinant)*(ddx(grainHeight)*basisX+ddy(grainHeight)*basisY)/max(abs(determinant),.000001);
  gradient*=min(1,260/max(1,length(gradient)));
  float grainFade=1-smoothstep(8,28,distance(i.w,_WorldSpaceCameraPos));
  float3 fibreNormal=normalize(geom-gradient*(.0018*grainFade));
  n=normalize(lerp(n,fibreNormal,rawTimber*.82));
  packed.r*=1-rawTimber;
  float endGrain=saturate(i.coast.w-4.0);
  if(endGrain>.001){
   float2 radial=(i.uv-float2(.47,1.28))*float2(1.18,.62);
   float radius=length(radial);
   float rings=smoothstep(.72,.96,sin(radius*86.0+wear*.65));
   col*=1-rings*endGrain*.19;
  }
 }
 float mudEntrance=exp(-pow((i.w.z+.7)/.60,2))*exp(-pow(i.w.x/8,4))*saturate(geom.y);
 float scuff=smoothstep(.35,.75,noise(float2(i.w.x*3.6,i.w.z*.70)));
 col=lerp(col,col*half3(.78,.76,.71),mudEntrance*(.08+scuff*.14));
 col*=1-wet*lerp(.47,.45,_CoastalWeathering);float wetFinish=lerp(saturate(smoothness+.20),_CoastalWetGloss,_CoastalWeathering);smoothness=lerp(smoothness,wetFinish,wet);
 smoothness*=lerp(.75,1,i.color.a);
 smoothness=lerp(smoothness,lerp(.11,.34,smoothstep(.30,.85,wet)),rawTimber);
 }else{
 float macro=noise(i.w.xz*.11)*.55+noise(i.w.xz*.43)*.45;
 col*=.85+macro*.27;
 // World-painted vertex masses reuse the existing ground mesh and textures.
 col*=max(i.color.rgb,half3(.25,.25,.25));
 float approach=exp(-pow((i.w.z+.65)/1.8,2))*exp(-pow(i.w.x/8,4));
 float drag=smoothstep(.45,.70,noise(float2(i.w.x*2.8,i.w.z*.32)));
 float stain=approach*(.25+drag*.32);
 col=lerp(col,col*half3(.76,.79,.80),stain*_WearStrength*2);
 float routeX=.35+.55*sin((i.w.z+5)*.30);
 float track=saturate(i.color.a);
 float run=1-smoothstep(-.5,1.8,i.w.z);
 float rutA=exp(-pow((i.w.x-routeX-.62)/.115,2));
 float rutB=exp(-pow((i.w.x-routeX+.62)/.115,2));
 float rut=max(rutA,rutB)*run*smoothstep(.28,.64,wear);
 col=lerp(col,col*half3(.77,.79,.75),rut*.32);
 col=lerp(col,col*half3(1.05,1.02,.96),track*.34);
 smoothness=.035;
 }
 InputData d=(InputData)0;d.positionWS=i.w;d.positionCS=i.p;d.normalWS=n;d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.w);d.shadowCoord=TransformWorldToShadowCoord(i.w);d.bakedGI=SampleSH(n);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);d.shadowMask=half4(1,1,1,1);
 SurfaceData s=(SurfaceData)0;s.albedo=col;s.metallic=packed.r;s.specular=half3(.04,.04,.04);s.smoothness=smoothness;s.normalTS=nts;s.occlusion=1;s.alpha=1;
 half4 c=UniversalFragmentPBR(d,s);return half4(MixFog(c.rgb,i.fog),1);
 }

 #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
 float3 _LightDirection;float3 _LightPosition;
 V vertShadow(A a){V o=vert(a);
 #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
 float3 lightDirection=normalize(_LightPosition-o.w);
 #else
 float3 lightDirection=_LightDirection;
 #endif
 o.p=TransformWorldToHClip(ApplyShadowBias(o.w,normalize(o.n),lightDirection));
 #if UNITY_REVERSED_Z
 o.p.z=min(o.p.z,UNITY_NEAR_CLIP_VALUE*o.p.w);
 #else
 o.p.z=max(o.p.z,UNITY_NEAR_CLIP_VALUE*o.p.w);
 #endif
 return o;}
 half4 fragDepth(V i):SV_Target{return 0;}
 half4 fragNormals(V i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC):SV_Target{
 UNITY_SETUP_INSTANCE_ID(i);float3 n=normalize(i.n)*IS_FRONT_VFACE(facing,1.0,-1.0);
 #if defined(_GBUFFER_NORMALS_OCT)
 return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n)*.5+.5)),0);
 #else
 return half4(n,0);
 #endif
 }
 ENDHLSL
 Pass {
 Name "ForwardLit" Tags{"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fog
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
 #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
 #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #pragma multi_compile _ _ADDITIONAL_LIGHTS
 #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
 #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

 #pragma multi_compile_instancing
 #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
 ENDHLSL
 }

 Pass {Name "ShadowCaster" Tags{"LightMode"="ShadowCaster"} ZWrite On ColorMask 0
 HLSLPROGRAM
 #pragma vertex vertShadow
 #pragma fragment fragDepth
 #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

 #pragma multi_compile_instancing
 #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
 ENDHLSL
 }
 Pass {Name "DepthOnly" Tags{"LightMode"="DepthOnly"} ZWrite On ColorMask R
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment fragDepth

 #pragma multi_compile_instancing
 #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
 ENDHLSL
 }
 Pass {Name "DepthNormals" Tags{"LightMode"="DepthNormals"} ZWrite On
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment fragNormals
 #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

 #pragma multi_compile_instancing
 #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
 ENDHLSL
 }
 }
}
