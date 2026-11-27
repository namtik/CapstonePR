Shader "Sasindo/Environment - Abyssal Eye Water"
{
    Properties
    {
        _Surface("Foam on the sea surface", Range(0, 1)) = 0
        _FlowOffset("Foam travel", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Surface;
                float _FlowOffset;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
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

            half4 SurfaceFoam(float2 uv)
            {
                float run = uv.x * 28.0 - _FlowOffset;
                float flow = Noise(float2(run, uv.y * 3.0));
                float grain = Noise(float2(run * 3.9, uv.y * 9.0 + 1.7));
                float layer = 0.31 + 0.29 * flow;
                float ridge = 1.0 - smoothstep(0.02, 0.33, abs(uv.y - layer));
                float threads = pow(saturate(grain * 1.42), 2.4) * ridge;
                float tail = smoothstep(0.0, 0.09, uv.x) * (1.0 - smoothstep(0.92, 1.0, uv.x));
                float edge = smoothstep(0.0, 0.23, uv.y) * (1.0 - smoothstep(0.79, 1.0, uv.y));
                half3 color = lerp(half3(0.18, 0.32, 0.37), half3(0.83, 0.94, 0.93), saturate(threads * 1.55));
                return half4(color, tail * edge * (0.10 + 0.83 * threads));
            }

            half4 WaterColumn(Varyings input)
            {
                float2 uv = input.uv;
                float flowY = uv.y * 12.0 - _Time.y * 8.5;
                float strand = Noise(float2(uv.x * 32.0 + uv.y * 5.0, flowY * 0.44));
                float foam = Noise(float2(uv.x * 93.0 - 2.6 * uv.y, flowY));
                float crest = smoothstep(0.43, 0.80, strand * 0.83 + foam * 0.44 + uv.y * 0.17);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1.0 - saturate(dot(normalize(input.normalWS), view)), 2.0);
                float edges = smoothstep(0.0, 0.085, uv.y) * (1.0 - smoothstep(0.79 + 0.10 * strand, 1.0, uv.y));
                half3 water = lerp(half3(0.055, 0.23, 0.31), half3(0.22, 0.46, 0.53), strand);
                half3 color = lerp(water, half3(0.78, 0.92, 0.92), saturate(crest * 0.87 + fresnel * 0.36));
                return half4(color, edges * lerp(0.82, 0.96, crest));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_Surface > 0.5)
                    return SurfaceFoam(input.uv);
                return WaterColumn(input);
            }
            ENDHLSL
        }
    }
}
