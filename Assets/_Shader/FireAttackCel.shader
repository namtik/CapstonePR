Shader "Sashindo/VFX/Fire Attack Cel"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Flame silhouette", 2D) = "white" {}
        [NoScaleOffset] _CustomFadeFadeMask ("Flame fade mask", 2D) = "white" {}
        [NoScaleOffset] _NoiseTexture ("Fade noise", 2D) = "white" {}
        _CustomFadeNoiseScale ("Fade noise scale", Vector) = (1, 1, 0, 0)
        _CustomFadeNoiseFactor ("Fade noise strength", Range(0, 0.5)) = 0.035
        _CustomFadeSmoothness ("Fade smoothness", Float) = 0.1
        _CustomFadeAlpha ("Fade opacity", Range(0, 1)) = 1
        _InvFade ("Soft particles factor", Range(0.01, 3)) = 1
        _RimTint ("Outer flame tint", Color) = (0.78, 0.37, 0.27, 1)
        _HotColor ("Flame core", Color) = (1, 0.82, 0.29, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Blend SrcAlpha One, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_particles
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                #ifdef SOFTPARTICLES_ON
                    float4 depthPosition : TEXCOORD2;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex, _CustomFadeFadeMask, _NoiseTexture;
            float2 _CustomFadeNoiseScale;
            float _CustomFadeNoiseFactor, _CustomFadeSmoothness, _CustomFadeAlpha, _InvFade;
            half4 _RimTint, _HotColor;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                #ifdef SOFTPARTICLES_ON
                    o.depthPosition = ComputeScreenPos(o.position);
                    COMPUTE_EYEDEPTH(o.depthPosition.z);
                #endif
                UNITY_TRANSFER_FOG(o, o.position);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float particleAlpha = i.color.a;
                #ifdef SOFTPARTICLES_ON
                    float sceneDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.depthPosition)));
                    particleAlpha *= saturate(_InvFade * (sceneDepth - i.depthPosition.z));
                #endif

                // Fire_Burst drives its existing dissolve mask with particle alpha.
                float shape = tex2D(_MainTex, i.uv).a;
                float mask = tex2D(_CustomFadeFadeMask, i.uv).r;
                float noise = tex2D(_NoiseTexture, i.uv * _CustomFadeNoiseScale).r;
                float fade = saturate(2.0 * particleAlpha - 1.0 + mask + noise * _CustomFadeNoiseFactor);
                float alpha = shape * pow(fade, _CustomFadeSmoothness / max(mask, 0.05)) * _CustomFadeAlpha;

                // Use the flame's own contour again for the smaller, off-centre hot core.
                float2 coreUV = (i.uv - float2(0.49, 0.40)) * 1.62 + 0.5;
                float coreShape = tex2D(_MainTex, coreUV).a;
                half body = smoothstep(0.58, 0.69, shape);
                half core = body * smoothstep(0.54, 0.65, coreShape);
                half3 tint = i.color.rgb;
                half intensity = max(tint.r, max(tint.g, tint.b));
                half3 surface = lerp(tint * _RimTint.rgb, tint, body);
                surface = lerp(surface, lerp(tint, _HotColor.rgb * intensity, 0.87), core);

                half4 result = half4(surface * alpha, alpha);
                UNITY_APPLY_FOG(i.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }
}
