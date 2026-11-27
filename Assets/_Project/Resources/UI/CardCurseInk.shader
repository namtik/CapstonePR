Shader "UI/CardCurseInk"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Attributes
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 clipPosition : TEXCOORD1;
                fixed2 modeAndStrength : TEXCOORD2;
            };

            float4 _ClipRect;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.clipPosition = input.position.xy;
                output.uv = float2(input.uv.x, 1.0 - input.uv.y);
                output.modeAndStrength = input.color.ra;
                return output;
            }

            float RoundedBox(float2 uv, float2 center, float2 halfSize, float radius)
            {
                float2 d = abs(uv - center) - halfSize + radius;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - radius;
            }

            float CardOutline(float2 uv, float awakened)
            {
                float body = RoundedBox(uv, float2(.493, .574), float2(.424, .385), .020);
                float heading = RoundedBox(uv, float2(.49, .130), float2(.32, .047), .010);
                float badge = RoundedBox(uv, float2(.144, .130), float2(.094, .092), .020);
                float medallion = (length((uv - float2(.854, .135)) * float2(1.0, 1.28)) - .103);
                float normalCard = min(min(body, heading), min(badge, medallion));
                float talisman = RoundedBox(uv, .5, float2(.492, .493), .026);
                return lerp(normalCard, talisman, awakened);
            }

            float2 InkSweep(float2 uv, float phase, float offset)
            {
                float flow = 6.283185 * (uv.y + .52 * uv.x + offset) - phase;
                flow += .54 * sin(12.0 * uv.x - phase + offset);
                flow += .31 * sin(19.0 * uv.x + 6.0 * uv.y + phase + offset);
                float ridge = 6.283185 * (frac(flow / 6.283185 + .5) - .5);
                float wave = sin(7.0 * uv.x + 6.0 * uv.y - phase + offset);
                float swell = .63 + .37 * wave * wave;
                float filament = .27 + .13 * sin(17.0 * uv.x - phase + offset);
                float2 distance = float2(ridge / .68, (ridge - filament) / .14);
                return exp(-.5 * distance * distance) * swell;
            }

            float SealInk(float2 uv, float awakened)
            {
                float2 center = lerp(float2(.828, .555), float2(.828, .842), awakened);
                float2 p = (uv - center) * float2(1.0, 1.333) / .040;
                p = mul(float2x2(.997, .08, -.08, .997), p);
                float d = max(abs(p.x), abs(p.y));
                float border = 1.0 - smoothstep(.01, .08, abs(d - .83));
                float rails = (1.0 - smoothstep(.035, .10, abs(abs(p.x) - .43))) * step(abs(p.y), .54);
                float bars = (1.0 - smoothstep(.035, .10, abs(abs(p.y) - .36))) * step(abs(p.x), .42);
                float centerStroke = (1.0 - smoothstep(.035, .105, abs(p.x - .13))) * step(abs(p.y), .55);
                float tooth = (1.0 - smoothstep(.025, .08, abs(p.y - .08))) * step(abs(p.x + .14), .32);
                float split = smoothstep(.016, .052, abs(p.x + .40 * p.y - .27));
                return max(border, max(max(rails, bars), max(centerStroke, tooth))) * split;
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float awakened = input.modeAndStrength.x;
                float strength = input.modeAndStrength.y;
                float phase = _Time.y * (6.283185 / 3.12);
                float outline = CardOutline(uv, awakened);
                float mask = 1.0 - smoothstep(-.007, .004, outline);
                float2 sweep = max(InkSweep(uv, phase, .04), InkSweep(uv, phase, .51));

                float desc = smoothstep(.58, .66, uv.y) * (1.0 - smoothstep(.89, .95, uv.y));
                float center = smoothstep(.10, .16, uv.x) * (1.0 - smoothstep(.84, .91, uv.x));
                float readable = 1.0 - .48 * desc * center * (1.0 - awakened);
                float edgePulse = .5 + .5 * sin(6.283185 * (uv.y - .46 * uv.x) - phase);
                float rim = (1.0 - smoothstep(.004, .023, abs(outline))) * (.12 + .44 * edgePulse * edgePulse);

                float wash = (.10 + .48 * sweep.x) * readable;
                float violet = max(.42 * sweep.y * readable, rim);
                float seal = .90 * SealInk(uv, awakened);
                float alpha = 1.0 - (1.0 - wash) * (1.0 - violet) * (1.0 - seal);
                float3 rgb = float3(.12, .047, .18) * wash * (1.0 - violet) * (1.0 - seal);
                rgb += float3(.57, .27, .69) * violet * (1.0 - seal);
                rgb += float3(.61, .10, .11) * seal;
                rgb /= max(alpha, .001);

                float visibility = mask * strength;
                #ifdef UNITY_UI_CLIP_RECT
                visibility *= UnityGet2DClipping(input.clipPosition, _ClipRect);
                #endif
                alpha *= visibility;
                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - .001);
                #endif
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}
