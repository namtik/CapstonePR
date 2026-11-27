Shader "Sashindo/VFX/Water Attack Cel"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Splash silhouette", 2D) = "white" {}
        [NoScaleOffset] _CustomFadeFadeMask ("Flow and dissolve mask", 2D) = "white" {}
        [NoScaleOffset] _NoiseTexture ("Ripple noise", 2D) = "gray" {}
        _RippleAmount ("Ripple displacement", Range(0,0.08)) = 0.022
        _RippleVelocity ("Ripple velocity", Vector) = (0.2,0.1,0,0)
        _DeepColor ("Deep water", Color) = (0.055,0.34,0.68,1)
        _WaterColor ("Shallow water", Color) = (0.17,0.68,0.94,1)
        _FoamColor ("Foam", Color) = (0.83,0.98,1,1)
        [HideInInspector] _StencilComp ("Stencil comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil write mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil read mask", Float) = 255
        [HideInInspector] _ColorMask ("Color mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex, _CustomFadeFadeMask, _NoiseTexture;
            half4 _DeepColor, _WaterColor, _FoamColor;
            float4 _RippleVelocity;
            float _RippleAmount;

            struct Varyings
            {
                float4 position : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(appdata_full input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = input.texcoord.xy;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 noiseUV = input.uv * 0.6 + _Time.y * _RippleVelocity.xy;
                float ripple = tex2D(_NoiseTexture, noiseUV).r - 0.5;
                float2 uv = saturate(input.uv + ripple * _RippleAmount);
                // The silhouette supplies the lip and the painted mask supplies ridges inside it.
                const float2 tap = float2(0.014, 0);
                float shape = tex2D(_MainTex, uv).a;
                float flow = tex2D(_CustomFadeFadeMask, uv).r;
                float2 slope = float2(
                    tex2D(_CustomFadeFadeMask, uv + tap.xy).r - tex2D(_CustomFadeFadeMask, uv - tap.xy).r,
                    tex2D(_CustomFadeFadeMask, uv + tap.yx).r - tex2D(_CustomFadeFadeMask, uv - tap.yx).r);
                float lip = shape - tex2D(_MainTex, uv + float2(0.012, -0.020)).a;
                float3 normal = normalize(float3(-2.6 * slope, 1));
                float facing = saturate(dot(normal, normalize(float3(-0.48, 0.58, 0.68))));
                float reflection = pow(facing, 9.0);
                float thickness = saturate(0.16 + 0.48 * shape + 0.32 * flow);

                // Thin water transmits the target color. Folded crests reflect more light.
                float crest = saturate(0.78 * reflection + 0.51 * max(lip, 0.0) + 0.31 * flow);
                float dissolve = saturate(2.0 * input.color.a - 1.0 + flow + 0.09 * ripple);
                float coverage = shape * pow(dissolve, 0.42) * input.color.a;
                coverage *= saturate(0.12 + 0.29 * thickness + 0.42 * crest);

                half3 scattered = lerp(_DeepColor.rgb, _WaterColor.rgb, saturate(0.28 + 0.56 * facing));
                half3 surface = lerp(scattered, _FoamColor.rgb, crest);
                surface += _FoamColor.rgb * (0.24 * reflection * reflection);
                surface *= lerp(half3(1, 1, 1), input.color.rgb, 0.16);
                return half4(surface, coverage);
            }
            ENDCG
        }
    }
}

