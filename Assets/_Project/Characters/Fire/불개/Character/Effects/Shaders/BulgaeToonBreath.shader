Shader "CreatureAnim/Bulgae Toon Breath"
{
    Properties
    {
        _BaseColor("Animated opacity", Color) = (1,1,1,1)
        _Flow("Flame phase", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Flow;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 variation : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; float2 variation : TEXCOORD2; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.variation = input.variation;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash(float2 p)
            {
                float3 q = frac(p.xyx * .1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f*f*(3-2*f);
                return lerp(lerp(Hash(cell), Hash(cell+float2(1,0)), f.x),
                            lerp(Hash(cell+float2(0,1)), Hash(cell+1), f.x), f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float y = input.uv.y;
                float x = input.uv.x * 2 - 1;
                float phase = _Flow + input.variation.x;
                float bend = .16 * sin(y * 9 - phase) * y;
                float licks = sin(x * 8 + y * 6 - phase) * .10 + sin(x * 14 - phase * .67) * .06;
                float width = (.24 + .54 * smoothstep(0,.37,y)) * (1 - smoothstep(.57,.93,y + licks));
                width += .07 * sin(y * 27 - phase * 1.8 + sign(x)*2) * smoothstep(.15,.78,y);
                float edge = width - abs(x - bend);
                half toonBody = smoothstep(-.025,.045,edge);
                half orange = smoothstep(.09,.18,edge) * (1-smoothstep(.67,.92,y+licks));
                half inner = smoothstep(.29,.45,edge) * (1-smoothstep(.49,.78,y+licks));
                half3 toon = lerp(half3(.64,.025,.013),half3(1,.22,.023),orange);
                toon = lerp(toon,half3(1,.66,.15),inner);

                float swell = Noise(float2(x*3.1,y*5.5-phase*.88));
                float shear = Noise(float2(x*9+y*1.4,y*7.7-phase*1.44));
                float wisps = Noise(float2(x*18,y*13-phase*2.05));
                float turbulence = .53*swell + .32*shear + .15*wisps;
                float tornEdge = edge + (turbulence-.53)*(.26+.58*y);
                half flameBody = smoothstep(-.18,.13,tornEdge);
                flameBody *= lerp(.55,1,smoothstep(.26,.65,shear + max(0,edge)*.48));
                float heat = saturate(.28 + .74*tornEdge + (turbulence-.36)*.94 - .39*y);
                half3 flame = lerp(half3(.39,.019,.009),half3(1.20,.16,.018),smoothstep(.16,.49,heat));
                flame = lerp(flame,half3(1.42,.89,.32),smoothstep(.42,.83,heat));

                half baseFade = _BaseColor.a * smoothstep(0,.06,y) * (1-input.variation.y);
                float tip = y + (swell-.5)*.15 + licks*.36;
                half toonAlpha = toonBody * (1-smoothstep(.54,.76,tip)) * baseFade;
                half flameAlpha = flameBody * (1-smoothstep(.56,.82,tip)) * baseFade;
                half alpha = lerp(toonAlpha, flameAlpha, .5);
                half3 fire = lerp(toon * toonAlpha, flame * flameAlpha, .5);
                return half4(MixFogColor(fire, unity_FogColor.rgb*alpha, input.fog),alpha);
            }
            ENDHLSL
        }
    }
}
