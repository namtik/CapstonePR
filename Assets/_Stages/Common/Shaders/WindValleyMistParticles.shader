Shader "Stage/WindValleyMistParticles"
{
    Properties
    {
        _BaseMap("Soft mist texture",2D)="white"{}
        _BaseColor("Tint",Color)=(.80,.87,.88,1)
        _Opacity("Opacity",Range(0,4))=2.8
        _LowLightOpacity("Cloud opacity in low light",Range(0,1))=1
        _SoftDistance("Terrain intersection softness",Float)=3
        _DepthFade("Depth intersection fade (requires depth before transparents)",Range(0,1))=0
        _TopFadeStart("Upper fade start",Float)=-10
        _TopFadeEnd("Upper fade end",Float)=24
        _NearCeiling("Near valley ceiling",Float)=-9
        _CeilingDistance("Distance to full cloud height",Float)=70
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;float _Opacity,_SoftDistance,_TopFadeStart,_TopFadeEnd,_DepthFade,_NearCeiling,_CeilingDistance,_LowLightOpacity;
            CBUFFER_END
            struct A{float4 p:POSITION;half4 c:COLOR;float2 uv:TEXCOORD0;};
            struct V{float4 p:SV_POSITION;half4 c:COLOR;float2 uv:TEXCOORD0;float3 w:TEXCOORD1;float eye:TEXCOORD2;float fog:TEXCOORD3;};
            V Vert(A i){V o;o.w=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.w);o.uv=i.uv;o.c=i.c;o.eye=-TransformWorldToView(o.w).z;o.fog=ComputeFogFactor(o.p.z);return o;}
            half4 Frag(V i):SV_Target
            {
                half mask=pow(saturate(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a),.65);
                float distant=smoothstep(22,max(23,_CeilingDistance),distance(i.w.xz,_WorldSpaceCameraPos.xz));
                float cap=lerp(_NearCeiling,_TopFadeEnd,distant)+sin(i.w.x*.065+i.w.z*.042)*lerp(2,6,distant);
                float height=1-smoothstep(min(_TopFadeStart,cap-8),cap,i.w.y);
                float floorFade=smoothstep(-115,-85,i.w.y);
                float nearFade=smoothstep(12,25,distance(i.w,_WorldSpaceCameraPos));
                float soft=1;
                if(_DepthFade>.001)
                {
                    float sceneDepth=LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.p)),_ZBufferParams);
                    soft=lerp(1,saturate((sceneDepth-i.eye)/max(.01,_SoftDistance)),_DepthFade);
                }
                half alpha=mask*i.c.a*_BaseColor.a*_Opacity*height*floorFade*nearFade*soft;
                Light sun=GetMainLight();
                half3 ambient=SampleSH(half3(0,1,0));
                half3 light=max(ambient+sun.color*.50,half3(.022,.025,.029));
                alpha*=lerp(_LowLightOpacity,1,saturate(dot(light,half3(.2126,.7152,.0722))*1.7));
                half3 color=_BaseColor.rgb*i.c.rgb*light;
                color=lerp(color,unity_FogColor.rgb,.32);
                color=MixFog(color,i.fog);
                return half4(color,saturate(alpha));
            }
            ENDHLSL
        }
    }
}
