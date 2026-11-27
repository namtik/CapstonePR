Shader "Sashindo/VFX/Wind Attack Ribbon"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Wind texture", 2D) = "white" {}
        [NoScaleOffset] _FlowMask ("Original dissolve mask", 2D) = "white" {}
        [NoScaleOffset] _NoiseTex ("Dissolve noise", 2D) = "black" {}
        _DissolvePower ("Dissolve smoothness", Range(0.01, 2)) = 1
        _NoiseAmount ("Dissolve noise amount", Range(0, 0.5)) = 0
        _CelStrength ("Cel shading", Range(0, 1)) = 0.7
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
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex, _FlowMask, _NoiseTex;
            float _DissolvePower, _NoiseAmount, _CelStrength;

            struct Varyings
            {
                float4 position : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
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
                UNITY_TRANSFER_FOG(output, output.position);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 brush = tex2D(_MainTex, input.uv);
                float mask = tex2D(_FlowMask, input.uv).r;
                float noise = tex2D(_NoiseTex, input.uv * 0.2).r;
                // The source wind materials use vertex alpha to dissolve along this mask.
                float coverage = saturate(2.0 * input.color.a - 1.0 + mask + noise * _NoiseAmount);
                float fade = pow(coverage, _DissolvePower / max(mask, 0.05));

                // Shade the original brush before fading it to keep the moving tips translucent.
                float density = brush.a;
                float bands = 0.23 * smoothstep(0.08, 0.20, density)
                            + 0.39 * smoothstep(0.32, 0.42, density)
                            + 0.38 * smoothstep(0.69, 0.78, density);
                float alpha = lerp(density, bands, _CelStrength) * fade;
                // Paint a narrow light edge inside the existing strokes and a jade midtone below it.
                float2 radial = input.uv - 0.5;
                float2 inset = radial * rsqrt(max(dot(radial, radial), 0.001)) * 0.014;
                float innerDensity = tex2D(_MainTex, input.uv - inset).a;
                float rim = smoothstep(0.12, 0.40, density - innerDensity);
                float brightBand = smoothstep(0.76, 0.84, density) * smoothstep(0.53, 0.63, mask);
                float light = saturate(rim + 0.66 * brightBand);
                half3 source = brush.rgb * input.color.rgb;
                half3 jade = source * half3(0.65, 0.85, 0.81);
                half3 mint = lerp(source, half3(0.83, 1.0, 0.90), 0.72);
                half3 color = lerp(source, lerp(jade, mint, light), _CelStrength);
                half4 result = half4(color, alpha);
                UNITY_APPLY_FOG(input.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }
}

