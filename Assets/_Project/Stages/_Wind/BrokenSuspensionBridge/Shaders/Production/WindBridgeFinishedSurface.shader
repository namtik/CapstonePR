Shader "Stage/WindBridgeFinishedSurface"
{
    Properties
    {
        _BaseMap("Surface",2D)="white"{}
        _BumpMap("Fine relief",2D)="bump"{}
        _StoneMap("Stone",2D)="white"{}
        _GrassMap("Forest edge",2D)="white"{}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _MossColor("Moss",Color)=(.31,.38,.19,1)
        _Tiling("World tiling",Float)=.27
        _Ground("Ground surface",Float)=0
        _GroundStoneBlend("Ground stone blend",Range(0,1))=.72
        _AmbientStrength("Ambient strength",Range(0,1))=1
        _UseVertexPatina("Baked surface variation",Range(0,1))=0
        _ClearingMap("Shared clearing soil",2D)="white"{}
        _ClearingBlend("Clearing soil blend",Range(0,1))=0
        _ClearingOriginScale("Clearing origin and size",Vector)=(0,-6.18,16,21)
        _MossStrength("Moss amount",Float)=.55
        _ValleyFade("Depth haze",Float)=0
        _DistanceFogStrength("Distance fog blend",Range(0,1))=1
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
        TEXTURE2D(_StoneMap);SAMPLER(sampler_StoneMap);
        TEXTURE2D(_GrassMap);SAMPLER(sampler_GrassMap);
        TEXTURE2D(_ClearingMap);SAMPLER(sampler_ClearingMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;half4 _BaseColor;half4 _MossColor;
        float _Tiling,_Ground,_MossStrength,_ValleyFade,_DistanceFogStrength;
        float _GroundStoneBlend,_AmbientStrength,_UseVertexPatina,_ClearingBlend;float4 _ClearingOriginScale;
        CBUFFER_END
        struct A {float4 p:POSITION;float3 n:NORMAL;half4 color:COLOR;};
        struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;float fog:TEXCOORD2;half4 color:COLOR;};
        V Vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.w=TransformObjectToWorld(i.p.xyz);o.n=TransformObjectToWorldNormal(i.n);o.fog=ComputeFogFactor(o.p.z);o.color=i.color;return o;}
        float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
        float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
        ENDHLSL
        Pass
        {
            Name "Forward" Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            half4 Frag(V i):SV_Target
            {
                half3 n=normalize(i.n);float3 p=i.w*_Tiling;
                half3 weights=pow(abs(n),4);weights/=max(dot(weights,half3(1,1,1)),.001);
                half3 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.zy).rgb*weights.x;
                c+=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz).rgb*weights.y;
                c+=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy).rgb*weights.z;
                if(_Ground>.5)
                {
                    float macro=noise(i.w.xz*.105);
                    half3 alternative=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,float2(-p.z,p.x)*.79+float2(.37,.63)).rgb;
                    c=lerp(c,alternative,smoothstep(.3,.72,macro)*.52);
                    half3 bump=UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,p.xz));
                    n=normalize(n+half3(bump.x,0,bump.y)*.18);
                    float stoneMix=smoothstep(.70,.93,noise(i.w.xz*.29+17))*.27;
                    c=lerp(c,SAMPLE_TEXTURE2D(_StoneMap,sampler_StoneMap,p.xz*.81).rgb,stoneMix);
                    half3 stone=SAMPLE_TEXTURE2D(_StoneMap,sampler_StoneMap,p.xz*.71).rgb;
                    c=lerp(c,stone,_GroundStoneBlend);
                    float edge=smoothstep(3.95,5.1,length(i.w.xz-float2(0,-6.18)));
                    float grass=edge*smoothstep(.22,.68,noise(i.w.xz*.27+11))*.76;
                    half3 vegetation=SAMPLE_TEXTURE2D(_GrassMap,sampler_GrassMap,p.xz*.85).rgb*half3(.82,.93,.78);
                    c=lerp(c,vegetation,grass);
                }
                float clearing=smoothstep(3.9,5.2,length(i.w.xz-float2(0,-6.18)));
                float top=smoothstep(.45,.90,n.y);
                float moss=clearing*top*smoothstep(.35,.80,noise(i.w.xz*.32+31))*_MossStrength;
                c=lerp(c,c*.38+_MossColor.rgb*.62,moss);
                if(_ClearingBlend>.001){float2 cuv=(i.w.xz-_ClearingOriginScale.xy)/_ClearingOriginScale.zw+.5;float inside=1-smoothstep(.35,.49,max(abs(cuv.x-.5),abs(cuv.y-.5)));half3 soil=SAMPLE_TEXTURE2D(_ClearingMap,sampler_ClearingMap,cuv).rgb;c=lerp(c,soil,_ClearingBlend*inside*(1-clearing*.90));}
                c*=_BaseColor.rgb*(.91+.13*noise(i.w.xz*.08));
                c*=lerp(half3(1,1,1),i.color.rgb,_UseVertexPatina);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
                half3 light=max(SampleSH(n)*_AmbientStrength+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation,half3(.025,.025,.025));
                c*=light;
                c=lerp(c,unity_FogColor.rgb,(1-smoothstep(-15,-1,i.w.y))*_ValleyFade*.68);
                return half4(lerp(c,MixFog(c,i.fog),_DistanceFogStrength),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"}
            ColorMask 0 ZWrite On
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            float3 _LightDirection;
            float4 ShadowVert(A i):SV_POSITION
            {
                float3 p=TransformObjectToWorld(i.p.xyz);float3 n=TransformObjectToWorldNormal(i.n);
                float4 o=TransformWorldToHClip(ApplyShadowBias(p,n,_LightDirection));
                #if UNITY_REVERSED_Z
                o.z=min(o.z,UNITY_NEAR_CLIP_VALUE*o.w);
                #else
                o.z=max(o.z,UNITY_NEAR_CLIP_VALUE*o.w);
                #endif
                return o;
            }
            half4 ShadowFrag():SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"}
            ColorMask R ZWrite On
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            float4 DepthVert(A i):SV_POSITION{return TransformObjectToHClip(i.p.xyz);}
            half4 DepthFrag():SV_Target{return 0;}
            ENDHLSL
        }
    }
}
