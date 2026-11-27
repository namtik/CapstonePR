Shader "Sasindo/Environment - Deep Sea Disruptions"
{
    Properties
    {
        [Enum(Seal,0,Current,1,Card,2,Miasma,3,PeripheralFog,4,Talisman,5)] _Style("Style", Float) = 0
        _Tint("Tint", Color) = (0.26,0.78,0.74,1)
        _Opacity("Opacity", Range(0,1)) = 0
        _Phase("Flow phase", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth Test", Float) = 4
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+9" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest [_ZTest]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Style, _Opacity, _Phase;
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
                float2 t = frac(p);
                t = t * t * (3.0 - 2.0 * t);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), t.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), t.x), t.y);
            }

            float Stroke(float d, float width)
            {
                return 1.0 - smoothstep(width * 0.4, width + max(fwidth(d), 0.002), abs(d));
            }

            half4 Seal(float2 uv)
            {
                float2 p = uv * 2.0 - 1.0;
                float r = length(p);
                float angle = atan2(p.y, p.x);
                float teeth = abs(sin(angle * 12.0));
                float ring = Stroke(r - .86, .018) + .63 * Stroke(r - .66, .016);
                float rune = Stroke(r - (.76 + .029 * cos(angle * 24.0)), .026) * smoothstep(.46, .71, teeth);
                float spokes = Stroke(sin(angle * 4.0), .10) * smoothstep(.32, .40, r) * (1.0 - smoothstep(.65, .69, r));
                float sign = Stroke(abs(p.x) + abs(p.y) - .47, .025) + Stroke(length(p * float2(1.0, 1.62)) - .25, .023);
                sign += .83 * Stroke(r - .078, .034);
                float glow = (1.0 - smoothstep(.22, .96, r)) * (.52 + .48 * Noise(p * 7.0 + _Phase));
                float light = saturate(ring + rune + spokes + sign);
                half3 color = lerp(_Tint.rgb * .36, lerp(_Tint.rgb, half3(.88,.89,1), .52), light);
                return half4(color, _Opacity * saturate(light * .92 + glow * .39));
            }

            half4 Current(float2 uv)
            {
                float2 flow = float2(uv.x * 12.0 - _Phase, uv.y * 4.7 + _Phase * .32);
                float coarse = Noise(flow);
                float fragments = Noise(flow * float2(3.9, 2.4) + 1.1);
                float curve = .53 + .19 * (coarse - .5);
                float edge = smoothstep(0.0,.24,uv.y) * (1.0 - smoothstep(.74,1.0,uv.y));
                float tail = smoothstep(.0,.14,uv.x) * (1.0 - smoothstep(.89,1.0,uv.x));
                float strand = Stroke(uv.y - curve, .10 + coarse * .09);
                float foam = smoothstep(.42,.86,coarse * .72 + fragments * .61) * strand;
                half3 color = lerp(_Tint.rgb * .48, half3(.78,.97,.96), saturate(foam * .82 + strand * .19));
                return half4(color, _Opacity * edge * tail * (.20 + .51 * strand + .31 * foam));
            }

            half4 Card(float2 uv)
            {
                float2 p = abs(uv - .5);
                float2 outside = max(p - float2(.405,.438), 0.0);
                float card = length(outside) - .032;
                float shape = 1.0 - smoothstep(0.0,.013,card);
                float outer = Stroke(card + .021,.012);
                float frame = Stroke(max(p.x - .36,p.y - .40), .012);
                float diamond = Stroke(p.x * 1.57 + p.y - .235,.018);
                float pip = Stroke(length(p * float2(1.45,1.0)) - .071,.013);
                float stripe = Stroke(uv.y - uv.x * .75 - frac(_Phase * .18) * .62 + .14,.028) * .38;
                float glyph = saturate(outer + frame * .45 + diamond * .87 + pip + stripe);
                half3 color = lerp(half3(.023,.10,.14), lerp(_Tint.rgb, half3(.83,1,1),.46), glyph);
                return half4(color, shape * _Opacity * lerp(.74,1.0,glyph));
            }

            half4 Talisman(float2 uv)
            {
                float fiber = Noise(uv * float2(91, 24));
                float stain = Noise(uv * float2(9, 17));
                float fray = Noise(uv * float2(42, 67));
                float bend = .016 * sin(uv.y * 9.0 + _Phase * .76) * uv.y;
                float halfWidth = lerp(.361, .317, uv.y);
                float side = halfWidth - abs(uv.x - .5 - bend);
                float ends = min(uv.y - .025, .978 - uv.y);
                float edge = min(side, ends * 1.7) + (fray - .58) * .025;
                float paper = smoothstep(-.007, .009, edge);
                float rim = 1.0 - smoothstep(.006, .075, edge);
                half3 parchment = lerp(half3(.52, .32, .118), half3(.97, .79, .42),
                    saturate(.38 + stain * .34 + fiber * .35 - rim * .39));
                parchment *= 1.0 - .095 * smoothstep(.83, .98, Noise(uv * float2(104, 9)));

                return half4(parchment, paper * _Opacity);
            }

            half4 Miasma(float2 uv)
            {
                float flow = Noise(float2(uv.y * 3.4 - _Phase, uv.x * 5.5));
                float detail = Noise(uv * float2(17.0, 12.0) + float2(_Phase * .81, -_Phase * 1.7));
                float center = .5 + .21 * sin(uv.y * 5.6 - _Phase * .9) + .086 * (flow - .5);
                float width = lerp(.33, .105, uv.y) * (.70 + .59 * flow);
                float body = 1.0 - smoothstep(width * .29, width, abs(uv.x - center));
                float broken = smoothstep(.24, .76, flow * .58 + detail * .50 + .22 * (1.0 - uv.y));
                float fade = smoothstep(0.0,.16,uv.y) * (1.0 - smoothstep(.61,1.0,uv.y));
                half3 color = lerp(_Tint.rgb * .30, _Tint.rgb * .86, detail * .74 + .11);
                return half4(color, _Opacity * fade * body * broken);
            }

            half4 PeripheralFog(float2 uv)
            {
                float2 p = (uv - .5) * float2(3.40, 3.30);
                float current = Noise(uv * float2(8.0, 6.0) + float2(_Phase * .48,-_Phase * .4));
                float streak = Noise(uv * float2(25.0, 10.0) + float2(-_Phase * .82,_Phase * .6));
                float edge = length(p * float2(1.07,.91)) + .29 * (current - .5) + .064 * (streak - .5);
                float fog = smoothstep(.38,1.04,edge);
                half3 color = lerp(_Tint.rgb * .30, _Tint.rgb * .68, current * .67 + (1.0 - fog) * .29);
                float alpha = fog * _Opacity * (.53 + .38 * current + .12 * streak);
                return half4(color, saturate(alpha));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_Style < .5)
                    return Seal(input.uv);
                if (_Style < 1.5)
                    return Current(input.uv);
                if (_Style < 2.5)
                    return Card(input.uv);
                if (_Style < 3.5)
                    return Miasma(input.uv);
                if (_Style < 4.5)
                    return PeripheralFog(input.uv);
                return Talisman(input.uv);
            }
            ENDHLSL
        }
    }
}
