Shader "Sasindo/Environment - Deep Sea Tidal Breaker"
{
    Properties
    {
        [Toggle] _DepthWrite("Write Depth", Float) = 1
        _CrestOnly("Crest Foot Fade", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+6" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite [_DepthWrite]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _CrestOnly;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 cell)
            {
                float3 p = frac(float3(cell.xyx) * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 blend = frac(p);
                blend = blend * blend * (3.0 - 2.0 * blend);
                return lerp(
                    lerp(Hash(cell), Hash(cell + float2(1, 0)), blend.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), blend.x),
                    blend.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 motion = float2(_Time.y * 0.47, -_Time.y * 1.24);
                float streak = Noise(uv * float2(35, 5) + motion);
                float fleck = Noise(uv * float2(140, 26) + motion * 1.71);
                float foamEdge = uv.y + 0.22 * (streak - 0.5) + 0.10 * (fleck - 0.5);
                float foam = smoothstep(0.60, 0.86, foamEdge);
                float bubbles = smoothstep(0.49, 0.78, fleck) * smoothstep(0.32, 0.78, uv.y);
                float baseFade = lerp(smoothstep(0.0, 0.28, uv.y),
                    smoothstep(0.1 + 0.18 * streak, 0.64, uv.y), _CrestOnly);
                float sideFade = smoothstep(0.0, 0.13, uv.x) * (1.0 - smoothstep(0.87, 1.0, uv.x));
                float alpha = baseFade * sideFade * (1.0 - smoothstep(0.94, 1.0, uv.y));
                clip(alpha - 0.055);

                half3 water = lerp(half3(0.045, 0.17, 0.24), half3(0.24, 0.46, 0.52),
                    saturate(uv.y * 0.88 + streak * 0.16));
                half3 crest = lerp(half3(0.58, 0.76, 0.80), half3(0.88, 0.95, 0.95), fleck);
                half3 color = lerp(water, crest, saturate(foam * 0.86 + bubbles * 0.27));
                return half4(color, alpha * lerp(0.83, 0.97, foam));
            }
            ENDHLSL
        }
    }
}
