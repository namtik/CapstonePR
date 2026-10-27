Shader "Stage/MarketMasonry"
{
 Properties{_BaseMap("Existing stone albedo",2D)="white"{} _BaseColor("Stone tint",Color)=(1,1,1,1) _BumpMap("Existing stone normal",2D)="bump"{} _BumpScale("Normal strength",Range(0,1))=.20 _Smoothness("Smoothness",Range(0,1))=.02}
 SubShader{
 Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
 #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
 TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseMap_ST;float4 _BumpMap_ST;half4 _BaseColor;half _BumpScale;half _Smoothness;
 CBUFFER_END
 struct A{float4 p:POSITION;float3 n:NORMAL;float4 tangent:TANGENT;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;float4 tangent:TEXCOORD2;float2 uv:TEXCOORD3;half4 color:TEXCOORD4;half fog:TEXCOORD5;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
 V Vert(A a){V o=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);VertexPositionInputs p=GetVertexPositionInputs(a.p.xyz);o.p=p.positionCS;o.w=p.positionWS;o.n=TransformObjectToWorldNormal(a.n);o.tangent=float4(TransformObjectToWorldDir(a.tangent.xyz),a.tangent.w*GetOddNegativeScale());o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.color=a.color;o.fog=ComputeFogFactor(p.positionCS.z);return o;}
 half3 WorldNormal(V i,out half3 normalTS){normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale);half3 n=normalize(i.n);half3 t=normalize(i.tangent.xyz);return normalize(t*normalTS.x+cross(n,t)*i.tangent.w*normalTS.y+n*normalTS.z);}
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
 half4 Frag(V i):SV_Target{
 UNITY_SETUP_INSTANCE_ID(i);half3 nts;half3 n=WorldNormal(i,nts);
 InputData d=(InputData)0;d.positionWS=i.w;d.positionCS=i.p;d.normalWS=n;d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.w);d.shadowCoord=TransformWorldToShadowCoord(i.w);d.bakedGI=SampleSH(n)*saturate(i.color.a);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);d.shadowMask=half4(1,1,1,1);
 SurfaceData s=(SurfaceData)0;s.albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb*i.color.rgb;s.metallic=0;s.specular=half3(.04,.04,.04);s.smoothness=_Smoothness;s.normalTS=nts;s.occlusion=1;s.alpha=1;
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
 UNITY_SETUP_INSTANCE_ID(a);float3 p=TransformObjectToWorld(a.p.xyz);float3 n=TransformObjectToWorldNormal(a.n);
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
 half4 Frag(V i):SV_Target{half3 nts;half3 n=WorldNormal(i,nts);
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
