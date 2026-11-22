Shader "Sashindo/UI/Awakening Brush Title"
{
    Properties
    {
        [PerRendererData] _MainTex ("Reference title data", 2D) = "black" {}
        _TitleGradientEnabled ("Use title gradient", Float) = 0
        _TitleGradientTop ("Title top color", Color) = (1, 1, 1, 1)
        _TitleGradientBottom ("Title bottom color", Color) = (1, 1, 1, 1)
        _WriteProgress ("Stroke progress", Range(0, 1)) = 0
        _OutlineRadius ("Outline radius in screen pixels", Float) = 7
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct AppData
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            half _TitleGradientEnabled;
            half4 _TitleGradientTop;
            half4 _TitleGradientBottom;
            float _WriteProgress;
            float _OutlineRadius;

            Varyings Vert(AppData input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half RecordedCoverage(half4 ink)
            {
                // R: white ink vs. black outline; G: first frame where ink appears.
                // B and A: coverage for gamma and linear color space.
                #if defined(UNITY_COLORSPACE_GAMMA)
                    return ink.b;
                #else
                    return ink.a;
                #endif
            }

            half WrittenInk(half4 ink)
            {
                half written = smoothstep(ink.g - .015h, ink.g + .014h, _WriteProgress);
                return RecordedCoverage(ink) * ink.r * written;
            }

            half InkAt(float2 uv)
            {
                return WrittenInk(tex2D(_MainTex, uv));
            }

            half SampleRing(float2 uv, float2 right, float2 up)
            {
                const float diagonalScale = 0.70710678;
                float2 diagonal = (right + up) * diagonalScale;
                float2 opposite = (right - up) * diagonalScale;
                half axial = max(max(InkAt(uv + right), InkAt(uv - right)),
                    max(InkAt(uv + up), InkAt(uv - up)));
                half diagonalInk = max(max(InkAt(uv + diagonal), InkAt(uv - diagonal)),
                    max(InkAt(uv + opposite), InkAt(uv - opposite)));
                return max(axial, diagonalInk);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // UV derivatives convert the same screen-pixel radius to texture offsets
                // at every title scale, including the peak of the bounce.
                float2 right = ddx(input.uv) * _OutlineRadius;
                float2 up = ddy(input.uv) * _OutlineRadius;
                half white = InkAt(input.uv);
                half core = SampleRing(input.uv, right * .68, up * .68);
                half feather = SampleRing(input.uv, right, up);
                half outline = max(core * .98h, feather * .58h);
                half coverage = white + outline * (1.0h - white);

                half gradient = smoothstep(.22h, .76h, input.uv.y);
                half3 gradientInk = lerp(_TitleGradientBottom.rgb, _TitleGradientTop.rgb, gradient);
                half3 ink = lerp(input.color.rgb, gradientInk, _TitleGradientEnabled);
                return half4(ink * (white * input.color.a), coverage * input.color.a);
            }

            ENDCG
        }
    }
}
