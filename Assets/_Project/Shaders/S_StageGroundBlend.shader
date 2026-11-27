// Stage ground: blends a grass texture and a dirt/path texture using vertex colour.
// vertexColor.r = 0 -> grass, 1 -> dirt. Because the weight is per-vertex and
// interpolated, the path edge becomes a soft gradient instead of a hard submesh seam.
// Toon shading is driven by the same ramp texture the ToonScapes kit uses, so the
// ground keeps sitting in the same palette as the trees and rocks.
Shader "Sasindo/Stage Ground Blend"
{
    Properties
    {
        [NoScaleOffset] _GrassMap ("Grass Texture", 2D) = "white" {}
        [NoScaleOffset] _DirtMap  ("Dirt / Path Texture", 2D) = "white" {}
        _GrassColor ("Grass Tint", Color) = (1,1,1,1)
        _DirtColor  ("Dirt Tint", Color) = (1,1,1,1)
        _BlendContrast ("Blend Contrast", Range(0.2, 6)) = 1.6

        [NoScaleOffset] _RampMap ("Toon Ramp", 2D) = "white" {}
        _RampScale  ("Ramp Scale", Range(0,1)) = 0.5
        _RampOffset ("Ramp Offset", Range(0,1)) = 0.5

        _AmbientBoost ("Ambient Boost", Range(0,2)) = 1
        _ShadowStrength ("Shadow Strength", Range(0,1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 color      : TEXCOORD3;
                float  fogCoord   : TEXCOORD4;
            };

            TEXTURE2D(_GrassMap); SAMPLER(sampler_GrassMap);
            TEXTURE2D(_DirtMap);  SAMPLER(sampler_DirtMap);
            TEXTURE2D(_RampMap);  SAMPLER(sampler_RampMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _GrassColor;
                half4 _DirtColor;
                half  _BlendContrast;
                half  _RampScale;
                half  _RampOffset;
                half  _AmbientBoost;
                half  _ShadowStrength;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv         = IN.uv;
                OUT.color      = IN.color;
                OUT.fogCoord   = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 grass = SAMPLE_TEXTURE2D(_GrassMap, sampler_GrassMap, IN.uv).rgb * _GrassColor.rgb;
                half3 dirt  = SAMPLE_TEXTURE2D(_DirtMap,  sampler_DirtMap,  IN.uv).rgb * _DirtColor.rgb;

                // push the interpolated weight toward 0/1 a little so the path still reads
                // as a path, but never snaps to a hard edge
                half w = saturate(IN.color.r);
                w = saturate((w - 0.5) * _BlendContrast + 0.5);
                half3 albedo = lerp(grass, dirt, w);

                float3 N = normalize(IN.normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half NdotL = saturate(dot(N, mainLight.direction) * 0.5 + 0.5);   // half lambert, flatter falloff
                half ramp  = SAMPLE_TEXTURE2D(_RampMap, sampler_RampMap, float2(saturate(NdotL * _RampScale + _RampOffset), 0.5)).r;
                half atten = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);

                half3 ambient = SampleSH(N) * _AmbientBoost;
                half3 direct  = mainLight.color * ramp * atten;

                half3 col = albedo * max(ambient, direct);
                col = MixFog(col, IN.fogCoord);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct SAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct SVaryings   { float4 positionCS : SV_POSITION; };

            SVaryings shadowVert(SAttributes IN)
            {
                SVaryings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nrmWS = TransformObjectToWorldNormal(IN.normalOS);
                float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, nrmWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionCS = posCS;
                return OUT;
            }

            half4 shadowFrag(SVaryings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DAttributes { float4 positionOS : POSITION; };
            struct DVaryings   { float4 positionCS : SV_POSITION; };

            DVaryings depthVert(DAttributes IN)
            {
                DVaryings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }
            half4 depthFrag(DVaryings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack Off
}
