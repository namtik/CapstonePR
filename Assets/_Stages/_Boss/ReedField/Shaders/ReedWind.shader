Shader "Stage/BossReedWind"
{
 Properties {
 _PlumeMap("Silvergrass plume RGBA",2D)="white"{}
 _Cutoff("Plume alpha cutoff",Range(.1,.7))=.25
 _Tint("Reed tint",Color)=(1,1,1,1)
 _ClusterStrength("Subtle field colour variation",Range(0,1))=.65
 _ClusterShadeStrength("Broad reed-cluster shade",Range(0,.5))=.26
 _ClusterWarmTint("Pale gold plume tint",Color)=(1,.945,.80,1)
 _ClusterCoolTint("Grey-white plume tint",Color)=(.84,.91,1,1)
 _WindDirection("Wind direction",Vector)=(.94,0,.34,0)
 _Amplitude("Wind bend in metres",Range(0,1))=.42
 _Speed("Wind speed",Range(.1,3))=.95
 _GustScale("Travelling gust scale",Float)=.18
 [HideInInspector] _PreviewTime("Preview time",Float)=-1
 }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
 Cull Off ZWrite On
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
 TEXTURE2D(_PlumeMap);SAMPLER(sampler_PlumeMap);
 CBUFFER_START(UnityPerMaterial)
 half4 _Tint,_ClusterWarmTint,_ClusterCoolTint;float4 _WindDirection;float _Amplitude,_Speed,_GustScale,_PreviewTime,_Cutoff,_ClusterStrength,_ClusterShadeStrength;
 CBUFFER_END
 struct A {float4 p:POSITION;float3 n:NORMAL;half4 c:COLOR;float2 uv:TEXCOORD0;float2 wind:TEXCOORD1;float2 root:TEXCOORD2;float4 tip:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;half3 c:TEXCOORD2;half fog:TEXCOORD3;half head:TEXCOORD4;float2 plume:TEXCOORD5;UNITY_VERTEX_OUTPUT_STEREO};
 half4 Plume(float2 uv,half head){
  half4 tex=half4(1,1,1,1);
  if(head>.5){tex=SAMPLE_TEXTURE2D(_PlumeMap,sampler_PlumeMap,uv);clip(tex.a-_Cutoff);}
  return tex;
 }
 // Rotation from an axis-angle vector. Small-angle polynomials are accurate
 // over the bounded reed bend, with no extra trigonometry per rotation.
 float3 RotateReed(float3 v,float2 curve) {
  float q=dot(curve,curve);
  float sineOverAngle=1-q*(1.0/6.0)+q*q*(1.0/120.0);
  float oneMinusCosOverAngle2=.5-q*(1.0/24.0)+q*q*(1.0/720.0);
  float3 axis=float3(curve.y,0,-curve.x);
  float3 first=cross(axis,v);
  return v+sineOverAngle*first+oneMinusCosOverAngle2*cross(axis,first);
 }
 void Bend(A a,out float3 p,out float3 normalWS) {
  float t=(_PreviewTime>=0?_PreviewTime:_Time.y)*_Speed;
  float3 rest=TransformObjectToWorld(a.p.xyz);
  float3 root=TransformObjectToWorld(float3(a.root.x,0,a.root.y));
  float3 tip=TransformObjectToWorld(a.tip.xyz);
  float stemHeight=max(.02,tip.y-root.y);
  float fullHeight=max(stemHeight,abs(TransformObjectToWorld(float3(a.root.x,a.tip.w,a.root.y)).y-root.y));
  float2 dir=_WindDirection.xz*rsqrt(max(dot(_WindDirection.xz,_WindDirection.xz),.0001));
  float2 side=float2(-dir.y,dir.x);
  // The travelling front is sampled at each stalk root, never at the card corners.
  float seed=a.wind.y+dot(root.xz,float2(1.73,2.49));
  float variation=sin(seed*1.31);
  float front=dot(root.xz,dir)*_GustScale-t*.90+variation*.10;
  float incoming=.68*sin(front)+.32*sin(front*.47+1.4);
  float gust=smoothstep(-.55,.80,incoming);
  // A small free response gives neighbouring stems a different recoil/settling.
  float recoil=sin(t*(2.1+.24*variation)+seed-.9)*.115;
  float force=.22+.68*gust+recoil;
  float theta=clamp((_Amplitude/max(fullHeight-.5*stemHeight,.12))*(.88+.12*variation)*force,-.08,.62);
  float crosswind=sin(t*1.67+seed*1.47)*.10;
  float2 curve=theta*(dir+side*crosswind);
  bool head=a.uv.x>1.5;
  float u=head?1:saturate(a.p.y/max(a.tip.y,.01));
  float2 k=curve*u;float q=dot(k,k);
  float arcSin=1-q*(1.0/6.0)+q*q*(1.0/120.0);
  float arcCos=.5-q*(1.0/24.0)+q*q*(1.0/720.0);
  float3 lean=float3(tip.x-root.x,0,tip.z-root.z);
  float3 centre=root+lean*(u*u)+float3(curve.x*stemHeight*u*u*arcCos,stemHeight*u*arcSin,curve.y*stemHeight*u*u*arcCos);
  float3 restCentre=head?tip:lerp(root,tip,u);
  // The plume is a rigid head attached to the curved stem. Its length is preserved.
  float flutter=head?sin(t*4.8+seed*1.91)*.018*(.3+.7*gust):0;
  float2 localRotation=k+side*flutter;
  p=centre+RotateReed(rest-restCentre,localRotation);
  normalWS=normalize(RotateReed(TransformObjectToWorldNormal(a.n),localRotation));
 }
 // Broad smooth colour masses, evaluated only where the forward pass needs colour.
 // No new texture, per-renderer material, instanced property or random trig.
 half3 FieldTint(float2 xz,half head) {
  float2 a=(xz-float2(-3.0,-3.0))*float2(.16,.13);
  float2 b=(xz-float2(8.0,2.5))*float2(.24,.20);
  float2 c=(xz-float2(3.8,-1.4))*float2(.145,.13);
  float warmA=saturate(1-dot(a,a));warmA*=warmA;
  float warmB=saturate(1-dot(b,b));warmB*=warmB;
  float cool=saturate(1-dot(c,c));cool*=cool;
  half warm=max(warmA,.55*warmB)*_ClusterStrength;
  half3 tint=lerp(half3(1,1,1),_ClusterWarmTint.rgb,warm);
  tint*=lerp(half3(1,1,1),_ClusterCoolTint.rgb,cool*_ClusterStrength*.65);
  // Large oblique masses add depth without removing or shrinking a stalk.
  float2 q=float2(xz.x+.38*xz.y,xz.y-.20*xz.x);
  float2 shadeA=(q-float2(-7.0,-6.0))*float2(.23,.32);
  float2 shadeB=(q-float2(3.5,-3.8))*float2(.24,.29);
  float2 shadeC=(q-float2(.5,3.5))*float2(.19,.36);
  float sa=saturate(1-dot(shadeA,shadeA));sa*=sa;
  float sb=saturate(1-dot(shadeB,shadeB));sb*=sb;
  float sc=saturate(1-dot(shadeC,shadeC));sc*=sc;
  half shade=max(sa,max(sb*.88,sc*.76))*_ClusterShadeStrength;
  tint*=1-shade;
  return lerp(half3(1,1,1),tint,head);
 }
 V Vert(A a) {
  V o=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
  float3 p,n;Bend(a,p,n);
  o.p=TransformWorldToHClip(p);o.w=p;o.n=n;
  float2 rootXZ=TransformObjectToWorld(float3(a.root.x,0,a.root.y)).xz;
  o.c=a.c.rgb*_Tint.rgb*FieldTint(rootXZ,step(1.5,a.uv.x));o.fog=ComputeFogFactor(o.p.z);o.head=step(1.5,a.uv.x);o.plume=float2(a.uv.x-2,a.uv.y);return o;
 }
 ENDHLSL
 Pass {
 Name "Forward" Tags {"LightMode"="UniversalForward"} AlphaToMask On
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #pragma multi_compile_fog
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 half4 Frag(V i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target {
  half4 plume=Plume(i.plume,i.head);i.c*=plume.rgb;
  half3 n=normalize(i.n)*IS_FRONT_VFACE(face,1,-1);n=normalize(lerp(n,half3(0,1,0),i.head*.65));Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
  half diffuse=saturate(dot(n,sun.direction)*.65+.35);
  half3 ambient=max(SampleSH(n),half3(.045,.045,.045));
  half3 lit=i.c*(ambient+sun.color*diffuse*lerp(.50,1,sun.shadowAttenuation));
  half through=pow(saturate(dot(normalize(GetWorldSpaceViewDir(i.w)),-sun.direction)),4)*i.head*.10;
  lit+=i.c*sun.color*through;
  return half4(MixFog(lit,i.fog),plume.a);
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
 float3 _LightDirection;float3 _LightPosition;
 struct SV{float4 p:SV_POSITION;float2 plume:TEXCOORD0;half head:TEXCOORD1;};
 SV ShadowVert(A a) {
  UNITY_SETUP_INSTANCE_ID(a);float3 p,n;Bend(a,p,n);
  #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
  float3 direction=normalize(_LightPosition-p);
  #else
  float3 direction=_LightDirection;
  #endif
  float4 q=TransformWorldToHClip(ApplyShadowBias(p,n,direction));
  #if UNITY_REVERSED_Z
  q.z=min(q.z,UNITY_NEAR_CLIP_VALUE*q.w);
  #else
  q.z=max(q.z,UNITY_NEAR_CLIP_VALUE*q.w);
  #endif
  SV o;o.p=q;o.plume=float2(a.uv.x-2,a.uv.y);o.head=step(1.5,a.uv.x);return o;
 }
 half4 ShadowFrag(SV i):SV_Target{Plume(i.plume,i.head);return 0;}
 ENDHLSL
 }
 Pass {
 Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ColorMask R
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 half4 Frag(V i):SV_Target{Plume(i.plume,i.head);return 0;}
 ENDHLSL
 }
 Pass {
 Name "DepthNormals" Tags {"LightMode"="DepthNormals"}
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
 half4 Frag(V i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target{
  Plume(i.plume,i.head);half3 n=normalize(i.n)*IS_FRONT_VFACE(face,1,-1);
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
