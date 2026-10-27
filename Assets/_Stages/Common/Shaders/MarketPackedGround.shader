Shader "Stage/MarketPackedGround"
{
 Properties{
 _BaseMap("Existing detailed soil",2D)="white"{} _EarthMap("Existing compacted earth",2D)="white"{}
 _BaseColor("Soil tint",Color)=(.9,.91,.85,1) _EarthColor("Compacted earth tint",Color)=(.99,.94,.84,1)
 _MarginTint("Undisturbed edge soil",Color)=(.76,.8,.76,1) _TrackTint("Worn path soil",Color)=(1.1,1.08,1.01,1)
 _Tiling("World tiling",Float)=.18 _PackedBlend("Compacted earth blend limit",Range(0,1))=.55
 _TrafficDetail("Broken wheel wear",Range(0,1))=0
 _ContactMask("Baked contact and cart wear",2D)="white"{}
 _ContactRect("Ground contact world rect",Vector)=(-24,-11,.02083333,.02777778)
 _ContactStrength("Ground contact depth",Range(0,1))=.65
 }
 SubShader{
 Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
 TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
 TEXTURE2D(_EarthMap);SAMPLER(sampler_EarthMap);
 TEXTURE2D(_ContactMask);SAMPLER(sampler_ContactMask);
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseMap_ST;float4 _EarthMap_ST;half4 _BaseColor;half4 _EarthColor;half4 _MarginTint;half4 _TrackTint;float _Tiling;half _PackedBlend;half _TrafficDetail;float4 _ContactRect;half _ContactStrength;
 CBUFFER_END
 struct A{float4 p:POSITION;float3 n:NORMAL;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;half4 mask:TEXCOORD2;half fog:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
 V Vert(A a){V o=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);VertexPositionInputs p=GetVertexPositionInputs(a.p.xyz);o.p=p.positionCS;o.w=p.positionWS;o.n=TransformObjectToWorldNormal(a.n);o.mask=a.color;o.fog=ComputeFogFactor(p.positionCS.z);return o;}
 ENDHLSL
 Pass{
 Name "Forward" Tags{"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #pragma multi_compile_fog
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 half4 Frag(V i):SV_Target{
 UNITY_SETUP_INSTANCE_ID(i);
 float2 uv=i.w.xz*_Tiling;
 half3 detailed=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb*_BaseColor.rgb;
 half3 compacted=SAMPLE_TEXTURE2D(_EarthMap,sampler_EarthMap,uv*.79+float2(3.7,5.2)).rgb*_EarthColor.rgb;
 half route=saturate(i.mask.r);
 half3 albedo=lerp(detailed,compacted,route*_PackedBlend)*i.mask.b;
 albedo*=lerp(half3(1,1,1),_TrackTint.rgb,route);
 albedo*=lerp(half3(1,1,1),_MarginTint.rgb,saturate(i.mask.a));
 float2 contactUV=(i.w.xz-_ContactRect.xy)*_ContactRect.zw;
 half inRect=step(0,contactUV.x)*step(0,contactUV.y)*step(contactUV.x,1)*step(contactUV.y,1);
 half4 groundData=SAMPLE_TEXTURE2D(_ContactMask,sampler_ContactMask,contactUV);
 half3 wear=groundData.gba*inRect*_TrafficDetail;
 albedo=lerp(albedo,lerp(compacted,detailed,.32)*i.mask.b*half3(1.16,1.11,1.035),wear.y*.78);
 albedo*=lerp(half3(1,1,1),half3(.43,.47,.41),wear.x);
 albedo*=1+wear.z*half3(.10,.07,.025);
 half3 n=normalize(i.n);Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
 half contact=lerp(1,groundData.r,inRect);
 half3 lighting=SampleSH(n)*i.mask.g*lerp(1,contact,_ContactStrength)+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation*lerp(1,contact,_ContactStrength*.6);
 return half4(MixFog(albedo*lighting,i.fog),1);
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
 float3 _LightDirection;float3 _LightPosition;
 #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
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
 half4 Frag(V i):SV_Target{
 half3 n=normalize(i.n);
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
