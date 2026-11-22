Shader "CreatureAnim/Tsurubebi Chorus Flame"
{
    Properties
    {
        _BaseMap("Flame and glow atlas", 2D) = "white" {}
        _BaseColor("Tint and fade", Color) = (1,1,1,1)
        _FlameDensity("Flame density", Range(1,4)) = 2.8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _FlameDensity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float3 positionWS : TEXCOORD1; half3 normalWS : TEXCOORD2; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _BaseColor;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float Hash(float2 p)
            {
                float3 q=frac(p.xyx*.1031);
                q+=dot(q,q.yzx+33.33);
                return frac((q.x+q.y)*q.z);
            }

            float Noise(float2 p)
            {
                float2 cell=floor(p),f=frac(p);
                f=f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),
                    lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                // The resonance and pendulum crests use the glow atlas with full blue coverage.
                if (uv.x >= 3.5 && uv.x <= 4)
                {
                    half4 glow = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, float2(uv.x - 3, uv.y));
                    half density = input.color.a * smoothstep(.03,.66,glow.a);
                    half opacity = saturate(density * 1.44);
                    half heat = smoothstep(.14,.76,density);
                    half3 blue = lerp(half3(.020,.24,.77), half3(.22,.71,1.20), heat);
                    half3 emission = half3(.035,.21,.62) * density * (.28 + .27 * heat);
                    return half4(blue * opacity + emission, opacity);
                }
                if (uv.x > 1)
                {
                    half3 normal = normalize(input.normalWS);
                    half facing = dot(normal,GetWorldSpaceNormalizeViewDir(input.positionWS));
                    clip(facing);
                    float y = uv.y;
                    float angle = (uv.x-1.25)*6.2831853;
                    float arc = cos(angle)*2.8 + sin(angle)*1.3;
                    float phase = _Time.y*4.8 + input.color.g*7;
                    float plume = Noise(float2(arc,y*4.7-phase));
                    float fibers = Noise(float2(arc*2.7+plume,y*10.0-phase*1.7));
                    float heat = saturate(.18+.35*facing+.49*plume+.24*fibers-.59*y);
                    float tips = y+.31*(plume-.5)+.14*(fibers-.5);
                    half root = smoothstep(.01,.13,y);
                    half shaped = smoothstep(.03,.38,facing)*(1-smoothstep(.64,.95,tips));
                    half natural = smoothstep(.015,.73,facing+.27*(plume-.5))
                        *smoothstep(.13,.59,plume*.62+fibers*.38+.21*(1-y))
                        *(1-smoothstep(.68,1,tips));
                    half3 deepBlue = input.color.rgb*half3(.24,.46,1.05);
                    half3 cyan = half3(.075,.74,1.42);
                    half3 hot = half3(.46,1.12,1.36);
                    half3 toonColor = lerp(deepBlue,cyan,smoothstep(.35,.44,heat));
                    toonColor = lerp(toonColor,hot,smoothstep(.66,.75,heat));
                    half3 naturalColor = lerp(deepBlue,cyan,smoothstep(.16,.66,heat));
                    naturalColor = lerp(naturalColor,hot,smoothstep(.50,.92,heat));
                    half strength = root * input.color.a;
                    half alpha = saturate(strength * (.5*shaped+.5*natural)*1.36);
                    half3 color = lerp(toonColor,naturalColor,.5);
                    return half4(color*alpha+hot*strength*natural*smoothstep(.62,.91,heat)*.13,alpha);
                }
                half4 texel = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv);
                if (uv.x >= .5) return half4(texel.rgb * input.color.rgb * texel.a * input.color.a,0);

                float x = uv.x * 4 - 1;
                float y = uv.y;
                float phase = input.color.g * 11;
                float tip = y + .10*sin(x*9+y*5+phase) + .055*sin(x*18-phase);
                float width = (.17+.69*smoothstep(.02,.34,y))*(1-smoothstep(.48,.95,tip));
                float edge = width - abs(x-.20*sin(y*6+phase)*y);
                half toon = smoothstep(-.025,.065,edge)*(1-smoothstep(.66,.96,tip));
                half natural = 1-pow(1-saturate(texel.a),_FlameDensity);
                half fade = smoothstep(0,.10,y)*(1-smoothstep(.92,1,y));
                half alpha = saturate(.5*(toon+natural)*input.color.a*fade*1.38);
                half heat = saturate(smoothstep(.12,.42,edge)*.55+texel.a*.42);
                half3 outer = input.color.rgb * half3(.62,.83,1.1);
                half3 inner = lerp(input.color.rgb,half3(.48,1.03,1.25),.58);
                return half4(lerp(outer,inner,heat)*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
