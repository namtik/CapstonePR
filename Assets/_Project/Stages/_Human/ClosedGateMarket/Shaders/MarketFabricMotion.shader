Shader "Stage/MarketFabricMotion"
{
 Properties{_FabricDetail("Woven cloth finish",Range(0,1))=0 [Enum(UnityEngine.Rendering.CullMode)]_Cull("Face culling",Float)=2 _BaseMap("Existing stone albedo",2D)="white"{} _BaseColor("Stone tint",Color)=(1,1,1,1) _BumpMap("Existing stone normal",2D)="bump"{} _BumpScale("Normal strength",Range(0,1))=.20 _Smoothness("Smoothness",Range(0,1))=.035 _WindAmplitude("Free edge sway (metres)",Range(0,.12))=.045 _WindSpeed("Breeze speed",Range(0,2))=.65 _WindSide("Free side direction",Float)=1}
 SubShader{
 Cull [_Cull]
 Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
 #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
 TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseMap_ST;float4 _BumpMap_ST;half4 _BaseColor;half _BumpScale;half _Smoothness;float _WindAmplitude;float _WindSpeed;float _WindSide;float _FabricDetail;float _Cull;
 CBUFFER_END
 struct A{float4 p:POSITION;float3 n:NORMAL;float4 tangent:TANGENT;float2 uv:TEXCOORD0;float2 wind:TEXCOORD1;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;float4 tangent:TEXCOORD2;float2 uv:TEXCOORD3;half4 color:TEXCOORD4;half fog:TEXCOORD5;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
 float3 FabricPosition(A a){float3 p=TransformObjectToWorld(a.p.xyz);float t=_Time.y*_WindSpeed;float w=saturate(a.wind.x);float phase=a.wind.y;float sway=(sin(t+phase)*.67+sin(t*1.73+phase*1.41)*.33)*_WindAmplitude*w;p.x+=_WindSide*(_WindAmplitude*.55*w+sway*.45);p.z+=sin(t*.81+phase+1.2)*_WindAmplitude*.26*w;return p;}
 V Vert(A a){V o=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);float3 p=FabricPosition(a);o.p=TransformWorldToHClip(p);o.w=p;o.n=TransformObjectToWorldNormal(a.n);o.tangent=float4(TransformObjectToWorldDir(a.tangent.xyz),a.tangent.w*GetOddNegativeScale());o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.color=a.color;o.fog=ComputeFogFactor(o.p.z);return o;}
 half3 WorldNormal(V i,out half3 normalTS){normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv*_BumpMap_ST.xy/max(_BaseMap_ST.xy,.001)+_BumpMap_ST.zw),_BumpScale);half3 n=normalize(i.n);half3 t=normalize(i.tangent.xyz);return normalize(t*normalTS.x+cross(n,t)*i.tangent.w*normalTS.y+n*normalTS.z);}
 ENDHLSL
 Pass{
 Name "ForwardLit" Tags{"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #pragma multi_compile_fog
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #pragma multi_compile _ _ADDITIONAL_LIGHTS
 #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
 #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
 half4 Frag(V i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC):SV_Target{
 UNITY_SETUP_INSTANCE_ID(i);half3 nts;half3 n=WorldNormal(i,nts);n*=IS_FRONT_VFACE(facing,1.0,-1.0);
 InputData d=(InputData)0;d.positionWS=i.w;d.positionCS=i.p;d.normalWS=n;d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.w);d.shadowCoord=TransformWorldToShadowCoord(i.w);d.bakedGI=SampleSH(n);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);d.shadowMask=half4(1,1,1,1);
 if(_FabricDetail>.001){
 // Thin cloth receives soft light through its opposite face.
 Light clothSun=GetMainLight(TransformWorldToShadowCoord(i.w));
 d.bakedGI=lerp(d.bakedGI,SampleSH(-n),.24*_FabricDetail);
 d.bakedGI+=clothSun.color*saturate(dot(-n,clothSun.direction))*.085*_FabricDetail;
 }
 SurfaceData s=(SurfaceData)0;s.albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb*max(i.color.rgb,half3(.7,.7,.7));if(_FabricDetail>.001){
 float2 fabricUV=(i.uv-_BaseMap_ST.zw)/max(_BaseMap_ST.xy,.001);
 float2 threads=fabricUV*float2(190,285);
 float2 threadFade=1-smoothstep(.18,.85,fwidth(threads));
 half weave=sin(threads.x*6.283185)*sin(threads.y*6.283185)*threadFade.x*threadFade.y;
 s.albedo*=1+weave*.115*_FabricDetail;
 // One slightly uneven joined panel, with small separate stitches.
 float seamX=.463+sin(fabricUV.y*13.7)*.0016+sin(fabricUV.y*31.1)*.0008;
 float seamDistance=abs(fabricUV.x-seamX);
 float seam=1-smoothstep(.001,.0045,seamDistance);
 float stitch=(1-smoothstep(.14,.29,abs(frac(fabricUV.y*125)-.5)))*(1-smoothstep(.001,.003,abs(seamDistance-.0048)));
 s.albedo*=1-seam*.18*_FabricDetail;
 s.albedo+=half3(.034,.027,.017)*stitch*_FabricDetail;
 float edge=1-smoothstep(.008,.06,min(fabricUV.y,1-fabricUV.y));
 float worn=.58+.42*sin(fabricUV.x*15.3+sin(fabricUV.x*43)*.5);
 s.albedo*=1-edge*worn*.17*_FabricDetail;
 }
 s.metallic=0;s.specular=half3(.04,.04,.04);s.smoothness=_Smoothness;s.normalTS=nts;s.occlusion=1;s.alpha=1;
 half4 c=UniversalFragmentPBR(d,s);return half4(MixFog(c.rgb,i.fog),1);
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
 Name "ShadowCaster" Tags{"LightMode"="ShadowCaster"} ColorMask 0 ZWrite On
 HLSLPROGRAM
 #pragma vertex ShadowVert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
 float3 _LightDirection;float3 _LightPosition;
 float4 ShadowVert(A a):SV_POSITION{
 UNITY_SETUP_INSTANCE_ID(a);float3 p=FabricPosition(a);float3 n=TransformObjectToWorldNormal(a.n);
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
 return q;}
 half4 Frag():SV_Target{return 0;}
 ENDHLSL
 }
 Pass{
 Name "DepthNormals" Tags{"LightMode"="DepthNormals"} ZWrite On
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
 half4 Frag(V i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC):SV_Target{half3 nts;half3 n=WorldNormal(i,nts);n*=IS_FRONT_VFACE(facing,1.0,-1.0);
 #if defined(_GBUFFER_NORMALS_OCT)
 return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n)*.5+.5)),0);
 #else
 return half4(n,0);
 #endif
 }
 ENDHLSL
 }
 }
}
