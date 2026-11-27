Shader "Sashindo/VFX/Guard Shield"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
        [NoScaleOffset] _ShieldProfile ("Shield Outline Profile", 2D) = "white" {}
        [NoScaleOffset] _TalismanMask ("Talisman Rim, Border and Fill", 2D) = "black" {}
        [HideInInspector] _TalismanTint ("Talisman Tint", Vector) = (0.96,0.73,0.30,1)
        [HideInInspector] _ShieldTint ("Shield Tint", Vector) = (0.96,0.73,0.30,1)
        [HideInInspector] _SolidFill ("Talisman and Shield Solid Fill", Vector) = (0,0,0,0)
        [HideInInspector] _GuardSequence ("Elapsed, Rebound Age, Aspect, Bounce Strength", Vector) = (0,-0.5,1.777778,0.4)
        [HideInInspector] _Expansion ("Expansion", Float) = 0
        [HideInInspector] _Opacity ("Opacity", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _ShieldProfile, _TalismanMask;
            half4 _TalismanTint, _ShieldTint;
            half4 _SolidFill;
            float4 _GuardSequence;
            float _Expansion, _Opacity;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float4 talismanPose : TEXCOORD1; // Rotated position, horizontal scale, rebound flash.
            };

            float4 TalismanPose(float2 uv)
            {
                float t = _GuardSequence.x, age = _GuardSequence.y;
                float2 scale;
                float offset, tilt;
                if (t < 0.28)
                {
                    float lift = 1.0 - pow(1.0 - saturate(t / 0.28), 3.0);
                    offset = 77.0 - 81.0 * lift;
                    scale = float2(lerp(0.60, 1.0, lift) * lerp(0.72, 1.0, lift), lerp(0.81, 1.0, lift));
                    tilt = lerp(-14.0, 4.0, lift);
                }
                else if (age < 0.0)
                {
                    float squash = smoothstep(-0.22, 0.0, age);
                    offset = -4.0 + 12.0 * squash;
                    scale = float2(1.0 + 0.105 * squash, 1.0 - 0.17 * squash);
                    tilt = 4.0 + 2.0 * squash;
                }
                else
                {
                    float u = saturate(age / 0.37);
                    float snap = 1.0 - pow(1.0 - saturate(u / 0.49), 2.4);
                    float settle = smoothstep(0.49, 1.0, u);
                    offset = 8.0 - 52.0 * snap - 7.0 * settle;
                    scale = float2((1.105 + 0.06 * snap - 0.10 * settle) * (1.0 - 0.085 * snap + 0.045 * settle),
                                   0.83 + 0.47 * snap - 0.15 * settle);
                    tilt = 6.0 - 12.0 * snap + 3.0 * settle;
                }
                if (t >= 0.28)
                {
                    // The lift ends at this resting pose. Scale the squash and rebound around it.
                    float strength = _GuardSequence.w;
                    offset = lerp(-4.0, offset, strength);
                    scale = lerp(float2(1.0, 1.0), scale, strength);
                    tilt = lerp(4.0, tilt, strength);
                }
                float2 pos = (uv - 0.5) * float2(_GuardSequence.z, -1.0) * 720.0;
                pos.y -= offset;
                float s, c;
                sincos(radians(tilt * 0.68), s, c);
                float pulseAge = (age - 0.095) / 0.10;
                return float4(c * pos.x + s * pos.y, (-s * pos.x + c * pos.y) / (207.0 * scale.y) + 0.5,
                              scale.x, exp(-pulseAge * pulseAge));
            }

            Varyings vert(appdata_full input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord.xy;
                output.color = input.color;
                output.talismanPose = TalismanPose(output.uv);
                return output;
            }

            half4 GlowLayer(half4 tint, float body, float rim)
            {
                half3 wash = tint.rgb;
                half peak = max(wash.r, max(wash.g, wash.b));
                half3 light = saturate(lerp(wash, peak.xxx, 0.44) * 1.085);
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    wash = GammaToLinearSpace(wash);
                    light = GammaToLinearSpace(light);
                #endif
                float inside = body * (1.0 - rim);
                return half4(light * rim + wash * inside, rim + inside) * tint.a;
            }

            half4 TalismanLayer(float4 pose)
            {
                float t = _GuardSequence.x, age = _GuardSequence.y;
                if (age >= 0.41) return 0;
                float v = pose.y, pulse = pose.w;
                float bend = (2.8 + 2.1 * pulse * _GuardSequence.w) * sin(v * 5.2 - 1.2 - t * 7.0);
                float u = (pose.x - bend) / (207.0 * (220.0 / 420.0) * pose.z * (1.0 + 0.036 * (v - 0.5))) + 0.5;
                // The baked silhouette is 220 by 420 texels with 42 texels of glow padding on each side.
                float2 maskUV = (float2(u, 1.0 - v) * float2(220, 420) + 42.0) / float2(304, 504);
                half3 mask = tex2D(_TalismanMask, maskUV).rgb;
                float release = smoothstep(0.055, 0.41, age);
                float dissolve = smoothstep(0.0, 0.37, 1.38 - 1.8 * release - v + 0.09 * sin(u * 4.1 + v * 7.2) + 0.08 * u);
                float brightness = 0.71 + 0.17 * cos(v * 5.2 + u * 2.4) + 0.12 * cos(u * 5.1 - v * 3.8);
                float visibility = smoothstep(0.0, 0.145, t) * dissolve;
                float intensity = brightness * visibility;
                float sweepDistance = (u * 0.46 + v - (0.12 + 1.45 * smoothstep(0.16, 0.71, t))) / 0.22;
                float sweep = exp(-sweepDistance * sweepDistance);
                float rim = (0.48 * mask.r * (0.89 + 0.22 * sweep + 0.24 * pulse) + 0.20 * mask.g) * intensity;
                float body = mask.b * (0.031 + 0.012 * (1.0 - v) + 0.015 * sweep + 0.011 * pulse) * intensity;
                body = lerp(body, mask.b * visibility, _SolidFill.x);
                return GlowLayer(_TalismanTint, body, rim);
            }

            half4 ShieldLayer(float2 uv)
            {
                float2 p = (uv - 0.5) / float2(0.452 * 0.72, 0.58);
                p.x *= _GuardSequence.z / (16.0 / 9.0);
                float angle = atan2(abs(p.x), p.y + 1e-6);
                float profileU = (angle * 0.318309886 * 1023.0 + 0.5) / 1024.0;
                float2 packed = tex2D(_ShieldProfile, float2(profileU, 0.5)).rg;
                float outline = dot(packed, float2(256.0, 1.0)) / 257.0 * 1.25;
                float shieldDistance = length(p) / max(outline, 0.05);
                float radius = lerp(0.038, 1.90, saturate(_Expansion));
                float distanceToRim = radius - shieldDistance;
                float pixel = max(fwidth(shieldDistance), 0.001);
                float fill = smoothstep(-pixel, pixel, distanceToRim);
                float birth = smoothstep(0.0, 0.07, _Expansion);
                float sheen = 0.72 + 0.20 * cos(angle * 2.0 + 0.66) + 0.08 * cos(angle * 9.0);
                float rim = 1.0 - smoothstep(pixel, pixel + 0.008, abs(distanceToRim));
                float innerRim = 1.0 - smoothstep(pixel, pixel + 0.008, abs(distanceToRim - 0.031));
                float halo = exp(-abs(distanceToRim - 0.019) * 28.0);

                float body = fill * (0.092 + 0.043 * smoothstep(0.12, 1.0, _Expansion));
                body = lerp(body, fill, _SolidFill.y);
                float edge = saturate(0.47 * rim * sheen + 0.17 * innerRim * sheen + 0.13 * halo);
                return GlowLayer(_ShieldTint, body, edge) * (birth * _Opacity);
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 shield = ShieldLayer(input.uv);
                half4 talisman = TalismanLayer(input.talismanPose);
                half4 combined = talisman + shield * (1.0 - talisman.a);
                return half4(combined.rgb * input.color.rgb, combined.a) * input.color.a;
            }
            ENDCG
        }
    }
}
