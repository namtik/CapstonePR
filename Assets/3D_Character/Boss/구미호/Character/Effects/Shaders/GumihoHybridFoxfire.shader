Shader "CreatureAnim/Gumiho Hybrid Foxfire"
{
    Properties
    {
        _BaseMap("Original flame mask", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _RibbonMode("Crescent ribbon", Float) = 0
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _RibbonMode;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _BaseColor;
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
                float2 uv = input.uv;
                float phase = _Time.y * 15 + input.color.g * 8;
                half3 tint = input.color.rgb;
                half core;
                half coverage;
                if (_RibbonMode > .5)
                {
                    float grain = Noise(float2(uv.x*24-phase*.36,uv.y*7));
                    float fibers = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,float2(uv.x-phase*.008,uv.y)).a;
                    float edge = .5-abs(uv.y-.5);
                    float broken = edge + (grain-.5)*.24;
                    half toon = smoothstep(.04,.18,edge);
                    half natural = smoothstep(-.06,.24,broken) * lerp(.35,1,fibers);
                    coverage = .5*(toon+natural) * smoothstep(0,.06,edge);
                    core = smoothstep(.24,.44,broken)*lerp(.34,1,fibers);
                }
                else
                {
                    float x = uv.x * 2 - 1;
                    float y = uv.y;
                    float swell = Noise(float2(x*3.2,y*5.5-phase*.88));
                    float shear = Noise(float2(x*8.5+y*1.4,y*8-phase*1.44));
                    float fine = Noise(float2(x*17,y*13-phase*2.05));
                    float turbulence = .52*swell + .32*shear + .16*fine;
                    float bend = (.14*sin(y*9-phase) + .22*(swell-.5))*y;
                    float licks = .10*sin(x*8+y*6-phase) + .06*sin(x*14-phase*.67);
                    float width = (.30+.53*smoothstep(0,.36,y)) * (1-smoothstep(.53,.96,y+licks));
                    float edge = width-abs(x-bend);
                    float broken = edge+(turbulence-.53)*(.23+.58*y);
                    float tip = y+(swell-.5)*.14+licks*.32;
                    half toon = smoothstep(-.03,.06,edge)*(1-smoothstep(.56,.91,tip));
                    half natural = smoothstep(-.18,.15,broken)*(1-smoothstep(.63,.97,tip));
                    natural *= lerp(.48,1,smoothstep(.25,.68,shear+max(0,edge)*.42));
                    float mask = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).a;
                    coverage = .5*(toon+natural)*smoothstep(0,.12,y)*lerp(.66,1,smoothstep(0,.62,mask));
                    core = .5*smoothstep(.20,.42,edge)+.5*smoothstep(.12,.46,broken+(turbulence-.5)*.35);
                    core *= 1-smoothstep(.54,.86,tip);
                }
                half denseFlame = step(1.2,tint.b) * (1-step(.5,_RibbonMode));
                tint.b = lerp(tint.b,1,denseFlame);
                half alpha = saturate(coverage * input.color.a * lerp(.88,2.35,denseFlame));
                half3 blue = tint * half3(.65,.90,1.05);
                half3 cyan = lerp(tint,half3(.58,1,1.23),.52);
                half3 body = lerp(blue,cyan,core);
                half3 glow = cyan * core * coverage * input.color.a * .23;
                return half4(body*alpha+glow,alpha);
            }
            ENDHLSL
        }
    }
}
