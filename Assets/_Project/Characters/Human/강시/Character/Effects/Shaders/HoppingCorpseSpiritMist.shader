Shader "CreatureAnim/Hopping Corpse Spirit Mist"
{
    Properties
    {
        _BaseColor("Smoke tint and opacity", Color) = (.11,.09,.075,.88)
        _EdgeColor("Smoke highlight", Color) = (.58,.55,.50,1)
        _Mode("0 wake, 1 mist", Float) = 0
        _Flow("Baked flow", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _EdgeColor;
                float _Mode, _Flow;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; half fog : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
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
                float2 uv = input.uv;
                float2 p = uv*2-1;
                float cloud = Noise(uv*4.8-float2(_Flow*.08,_Flow*.12));
                float grain = Noise(uv*17+cloud*.30-float2(0,_Flow*.19));
                float edge = .72-length(p + float2(.10*(cloud-.5),.03));
                float brokenEdge = edge + .42*(cloud-.5) + .19*(grain-.5);
                half toon = smoothstep(-.06,.09,brokenEdge);
                half vapor = smoothstep(-.18,.26,brokenEdge) * lerp(.42,1,grain);
                half bands = smoothstep(.07,.20,brokenEdge) * .27 + smoothstep(.27,.43,brokenEdge) * .23;
                half detail = saturate(.26 + cloud*.42 + grain*.23 + p.y*.17);
                half3 shade = _BaseColor.rgb * lerp(.46,.90,_Mode);
                half3 toonColor = lerp(shade, _EdgeColor.rgb, bands*lerp(.31,.82,_Mode));
                half3 vaporColor = lerp(shade, _EdgeColor.rgb, detail*lerp(.28,.72,_Mode));
                half border = 1-smoothstep(.82,1,max(abs(p.x),abs(p.y)));
                half weight = saturate(input.color.a * _BaseColor.a * lerp(1.75,1.62,_Mode)) * border;
                half alpha = .5*(toon+vapor)*weight;
                half3 body = .5*(toonColor*toon+vaporColor*vapor)*weight;
                return half4(MixFogColor(body,unity_FogColor.rgb*alpha,input.fog),alpha);
            }
            ENDHLSL
        }
    }
}
