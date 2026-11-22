// Awakening color customization for the Sashindo UI.
Shader "Sashindo/UI/Awakening Colorize"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _TitleGradientEnabled ("Use title gradient", Float) = 0
        _TitleGradientTop ("Title top color", Color) = (1, 1, 1, 1)
        _TitleGradientBottom ("Title bottom color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
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

            half4 Frag(Varyings input) : SV_Target
            {
                half4 art = tex2D(_MainTex, input.uv);
                half lightness = dot(art.rgb, half3(0.2126, 0.7152, 0.0722));
                half peak = smoothstep(0.52, 1.0, lightness) * 0.6;
                half3 tint = lerp(input.color.rgb, half3(1, 1, 1), peak);
                half gradient = smoothstep(.22h, .76h, input.uv.y);
                half3 gradientInk = lerp(_TitleGradientBottom.rgb, _TitleGradientTop.rgb, gradient);
                half3 recolored = lightness * lerp(tint, gradientInk, _TitleGradientEnabled);
                half opacity = art.a * input.color.a;
                return half4(recolored * opacity, opacity);
            }
            ENDCG
        }
    }
}
