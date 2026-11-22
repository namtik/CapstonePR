Shader "Sasindo/Environment - Decree Camera Wash"
{
    Properties
    {
        _Opacity("Opacity", Range(0, 1)) = 0
        _Sweep("Sweep", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Opacity;
                float _Sweep;
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
                float3 p = frac(cell.xyx * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), f.x), f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float current = Noise(float2(uv.x * 8.0, uv.y * 3.0 + _Sweep * 2.5));
                float grain = Noise(uv * float2(37.0, 19.0) + float2(_Sweep * 1.6, _Sweep * 7.3));
                float crest = 0.88 - 0.94 * _Sweep + 0.12 * (current - 0.5) + 0.052 * sin(uv.x * 14.0);
                float over = uv.y - crest;
                float sheet = smoothstep(-0.055, 0.095, over);
                float rim = 1.0 - smoothstep(0.018, 0.14, abs(over + 0.025 * (grain - 0.5)));
                float bubbles = smoothstep(0.51, 0.84, grain * 0.61 + current * 0.39);
                half3 color = lerp(half3(0.19, 0.43, 0.52), half3(0.86, 0.96, 0.96),
                    saturate(0.80 * rim + 0.62 * bubbles));
                return half4(color, _Opacity * saturate(0.64 * sheet + 0.56 * rim + 0.19 * bubbles * sheet));
            }
            ENDHLSL
        }
    }
}
