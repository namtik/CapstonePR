// Character / monster toon shader for URP 17.3.
// Three ingredients that hide low-poly / low-res weaknesses:
//   1) Cel shading  - main light is quantised into a few flat bands instead of a smooth ramp.
//   2) Rim light    - a bright edge that separates the silhouette from the background.
//   3) Outline      - an inverted-hull back-face pass that draws a clean contour line.
// Uses the same include / pass layout as S_StageGroundBlend so it stays in the ToonScapes palette.
Shader "Sasindo/Toon Character"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Texture", 2D) = "white" {}
        [MainColor]   _BaseColor ("Base Tint", Color) = (1,1,1,1)

        [Header(Cel Shading)]
        _Bands        ("Shading Bands", Range(1,5)) = 2
        _ShadowTint   ("Shadow Tint", Color) = (0.55,0.6,0.75,1)
        _ShadowStrength ("Shadow Strength", Range(0,1)) = 0.7
        _AmbientBoost ("Ambient Boost", Range(0,2)) = 1

        [Header(Rim Light)]
        _RimColor     ("Rim Color", Color) = (1,1,1,1)
        _RimPower     ("Rim Width", Range(0.2,8)) = 3
        _RimIntensity ("Rim Intensity", Range(0,3)) = 0.6

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.05,0.05,0.07,1)
        _OutlineWidth ("Outline Width", Range(0,5)) = 1.2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        // ---------------------------------------------------------------- OUTLINE
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }   // URP renders this alongside the forward pass
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _Bands;
                half4  _ShadowTint;
                half   _ShadowStrength;
                half   _AmbientBoost;
                half4  _RimColor;
                half   _RimPower;
                half   _RimIntensity;
                half4  _OutlineColor;
                half   _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nrmWS = TransformObjectToWorldNormal(IN.normalOS);
                // constant-ish screen width: scale expansion by clip-space distance
                float4 posCS = TransformWorldToHClip(posWS);
                float3 nrmCS = normalize(TransformWorldToHClipDir(nrmWS));
                posCS.xy += nrmCS.xy * (_OutlineWidth * 0.01) * posCS.w;
                OUT.positionCS = posCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return half4(_OutlineColor.rgb, 1); }
            ENDHLSL
        }

        // ---------------------------------------------------------------- FORWARD LIT
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
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 viewDirWS  : TEXCOORD3;
                float  fogCoord   : TEXCOORD4;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _Bands;
                half4  _ShadowTint;
                half   _ShadowStrength;
                half   _AmbientBoost;
                half4  _RimColor;
                half   _RimPower;
                half   _RimIntensity;
                half4  _OutlineColor;
                half   _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS  = GetWorldSpaceViewDir(pos.positionWS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogCoord   = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb * _BaseColor.rgb;

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // --- cel shading: quantise half-lambert into flat bands ---
                half NdotL = saturate(dot(N, mainLight.direction) * 0.5 + 0.5);
                half atten = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);
                half lit   = NdotL * atten;
                half bands = max(_Bands, 1.0h);
                half step  = floor(lit * bands) / bands;          // stepped light term 0..~1

                half3 ambient  = SampleSH(N) * _AmbientBoost;
                half3 litColor = albedo * (ambient + mainLight.color * step);
                // tint the darkest band toward the shadow color for a stylised look
                half3 shadowColor = albedo * ambient * _ShadowTint.rgb;
                half3 col = lerp(shadowColor, litColor, saturate(step + 0.001));

                // --- rim light ---
                half rim = pow(1.0h - saturate(dot(N, V)), _RimPower);
                col += _RimColor.rgb * rim * _RimIntensity;

                col = MixFog(col, IN.fogCoord);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------- SHADOW CASTER
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

        // ---------------------------------------------------------------- DEPTH ONLY
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
