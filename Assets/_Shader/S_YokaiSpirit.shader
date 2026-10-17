// 요괴(몬스터) 전용 수묵 툰 셰이더 — URP 17.3.
// S_ToonCharacter의 셀 셰이딩/외곽선을 바탕으로, 한국 요괴·원소 컨셉에 맞춘 네 가지를 더했다.
//   1) 먹선 외곽선   - 두께가 위치마다 조금씩 달라 붓으로 그은 듯한 윤곽선.
//   2) 붓터치 그림자 - 명암 경계를 노이즈로 흐트러뜨려 수묵화처럼 거친 그림자 경계.
//   3) 요기 오라     - 실루엣 가장자리에서 위로 흘러오르는 원소색 기운(HDR, 블룸과 함께 쓰면 빛남).
//   4) 소멸(Dissolve) - _Dissolve 0→1로 몸이 불티 가장자리를 남기며 흩어짐(사망 연출, EnemyView3D가 구동).
// 텍스처 없이 절차적 노이즈만 사용하므로 머티리얼에 셰이더만 지정하면 바로 동작한다.
// 피격 플래시(EnemyView3D)는 기존처럼 _BaseColor를 덮어쓰므로 그대로 호환된다.
Shader "Sasindo/Yokai Spirit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Texture", 2D) = "white" {}
        [MainColor]   _BaseColor ("Base Tint", Color) = (1,1,1,1)

        [Header(Ink Cel Shading)]
        _Bands            ("Shading Bands", Range(1,5)) = 2
        _ShadowTint       ("Shadow Tint (ink)", Color) = (0.42,0.45,0.6,1)
        _ShadowStrength   ("Shadow Strength", Range(0,1)) = 0.7
        _AmbientBoost     ("Ambient Boost", Range(0,2)) = 1
        _InkBrushScale    ("Brush Stroke Scale", Range(1,60)) = 14
        _InkBrushStrength ("Brush Stroke Strength", Range(0,1)) = 0.35

        [Header(Spirit Aura)]
        [HDR] _SpiritColor ("Spirit Color (element)", Color) = (0.45,1.6,2.0,1)
        _RimPower         ("Aura Width", Range(0.5,8)) = 2.5
        _RimIntensity     ("Aura Intensity", Range(0,4)) = 1.2
        _AuraNoiseScale   ("Aura Wisp Scale", Range(0.5,12)) = 3
        _AuraFlowSpeed    ("Aura Rise Speed", Range(0,3)) = 0.6
        _AuraPulse        ("Aura Pulse", Range(0,1)) = 0.25

        [Header(Ink Outline)]
        _OutlineColor     ("Outline Color", Color) = (0.06,0.05,0.08,1)
        _OutlineWidth     ("Outline Width", Range(0,5)) = 1.4
        _OutlineJitter    ("Brush Width Variation", Range(0,1)) = 0.5
        _OutlineSpiritTint("Outline Spirit Tint", Range(0,1)) = 0.15

        [Header(Dissolve)]
        _Dissolve         ("Dissolve", Range(0,1)) = 0
        _DissolveScale    ("Dissolve Noise Scale", Range(0.5,20)) = 4
        _DissolveEdgeWidth("Edge Width", Range(0.005,0.3)) = 0.08
        [HDR] _DissolveEdgeColor ("Edge Color (embers)", Color) = (2.5,1.1,0.35,1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _Bands;
            half4  _ShadowTint;
            half   _ShadowStrength;
            half   _AmbientBoost;
            half   _InkBrushScale;
            half   _InkBrushStrength;
            half4  _SpiritColor;
            half   _RimPower;
            half   _RimIntensity;
            half   _AuraNoiseScale;
            half   _AuraFlowSpeed;
            half   _AuraPulse;
            half4  _OutlineColor;
            half   _OutlineWidth;
            half   _OutlineJitter;
            half   _OutlineSpiritTint;
            half   _Dissolve;
            half   _DissolveScale;
            half   _DissolveEdgeWidth;
            half4  _DissolveEdgeColor;
        CBUFFER_END

        // ── 절차적 노이즈(텍스처 불필요) ──
        float Hash31(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float ValueNoise(float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(
                lerp(lerp(Hash31(i + float3(0,0,0)), Hash31(i + float3(1,0,0)), f.x),
                     lerp(Hash31(i + float3(0,1,0)), Hash31(i + float3(1,1,0)), f.x), f.y),
                lerp(lerp(Hash31(i + float3(0,0,1)), Hash31(i + float3(1,0,1)), f.x),
                     lerp(Hash31(i + float3(0,1,1)), Hash31(i + float3(1,1,1)), f.x), f.y), f.z);
        }

        float Fbm(float3 p)
        {
            float v = 0.0, a = 0.5;
            [unroll] for (int k = 0; k < 3; k++) { v += a * ValueNoise(p); p *= 2.03; a *= 0.5; }
            return v;
        }

        // 소멸 임계값 — 0이면 아무것도 지우지 않고 1이면 전부 지운다
        float DissolveThreshold() { return lerp(-0.05, 1.05, _Dissolve); }
        float DissolveNoise(float3 positionOS) { return Fbm(positionOS * _DissolveScale); }
        void ClipDissolve(float3 positionOS)
        {
            if (_Dissolve > 0.0001) clip(DissolveNoise(positionOS) - DissolveThreshold());
        }
        ENDHLSL

        // ---------------------------------------------------------------- INK OUTLINE
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nrmWS = TransformObjectToWorldNormal(IN.normalOS);
                float4 posCS = TransformWorldToHClip(posWS);
                float3 nrmCS = normalize(TransformWorldToHClipDir(nrmWS));
                // 붓 압력처럼 위치마다 두께가 달라지는 먹선
                float brush = 1.0 + (ValueNoise(IN.positionOS.xyz * 6.0) - 0.5) * 2.0 * _OutlineJitter;
                posCS.xy += nrmCS.xy * (_OutlineWidth * 0.01 * max(brush, 0.0)) * posCS.w;
                OUT.positionCS = posCS;
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                ClipDissolve(IN.positionOS);
                half3 col = lerp(_OutlineColor.rgb, _SpiritColor.rgb * 0.25, _OutlineSpiritTint);
                return half4(col, 1);
            }
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
                float3 positionOS : TEXCOORD4;
                float  fogCoord   : TEXCOORD5;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS  = GetWorldSpaceViewDir(pos.positionWS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogCoord   = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // --- 소멸 ---
                float dn = DissolveNoise(IN.positionOS);
                float threshold = DissolveThreshold();
                if (_Dissolve > 0.0001) clip(dn - threshold);

                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb * _BaseColor.rgb;

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // --- 붓터치 셀 셰이딩: 명암 경계를 세로로 긴 붓결 노이즈로 흐트러뜨린 뒤 단계화 ---
                half NdotL = saturate(dot(N, mainLight.direction) * 0.5 + 0.5);
                half atten = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);
                float brush = ValueNoise(IN.positionOS * float3(1.0, 0.18, 1.0) * _InkBrushScale);
                half lit   = saturate(NdotL * atten + (brush - 0.5) * _InkBrushStrength);
                half bands = max(_Bands, 1.0h);
                half stepL = floor(lit * bands) / bands;

                half3 ambient     = SampleSH(N) * _AmbientBoost;
                half3 litColor    = albedo * (ambient + mainLight.color * stepL);
                half3 shadowColor = albedo * ambient * _ShadowTint.rgb; // 먹빛 그림자
                half3 col = lerp(shadowColor, litColor, saturate(stepL + 0.001));

                // --- 요기 오라: 가장자리에서 위로 흘러오르는 원소색 기운 ---
                half rim  = pow(1.0h - saturate(dot(N, V)), _RimPower);
                float wisp = Fbm(IN.positionWS * _AuraNoiseScale + float3(0, -_Time.y * _AuraFlowSpeed, 0));
                half aura = rim * smoothstep(0.35, 0.75, wisp + rim * 0.35);
                half pulse = 1.0h + sin(_Time.y * 2.2) * _AuraPulse;
                col += _SpiritColor.rgb * aura * _RimIntensity * pulse;

                // --- 소멸 가장자리 불티 ---
                if (_Dissolve > 0.0001)
                {
                    half edge = 1.0h - saturate((dn - threshold) / _DissolveEdgeWidth);
                    col = lerp(col, _DissolveEdgeColor.rgb, edge);
                }

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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct SAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct SVaryings   { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; };

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
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 shadowFrag(SVaryings IN) : SV_Target
            {
                ClipDissolve(IN.positionOS);
                return 0;
            }
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

            struct DAttributes { float4 positionOS : POSITION; };
            struct DVaryings   { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; };

            DVaryings depthVert(DAttributes IN)
            {
                DVaryings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 depthFrag(DVaryings IN) : SV_Target
            {
                ClipDissolve(IN.positionOS);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
