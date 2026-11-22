Shader "Hidden/Sashindo/Guard Block Wave"
{
    Properties
    {
        _GuardFilm ("Film Color and Opacity", Color) = (0.87, 0.65, 0.17, 0.1)
        _GuardWaveColor ("Wave Color", Color) = (1, 0.96, 0.81, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Guard Block Wave"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GuardFilm;
                half4 _GuardWaveColor;
                float4 _GuardTiming; // elapsed, fade in, hold, fade out
                float4 _GuardShape;  // origin UV, aspect, vertical ratio
                float4 _GuardMotion; // radius, travel time, width, brightness
                float4 _GuardEcho;   // delay, strength, refraction as a fraction of screen height
            CBUFFER_END

            float Bell(float value)
            {
                return exp(-value * value);
            }

            // Each wave contributes a light crest and a narrow strip of refraction.
            float2 Wave(float age, float distance)
            {
                float progress = age / _GuardMotion.y;
                if (progress <= 0 || progress >= 1)
                    return 0;

                float front = _GuardMotion.x * progress * (1.18 - 0.18 * progress);
                float offset = distance - front;
                float width = _GuardMotion.z * lerp(0.76, 1.25, progress);
                float strength = smoothstep(0.02, 0.17, progress) * (1 - smoothstep(0.50, 1, progress));
                float crest = Bell(offset / width) + 0.24 * Bell((offset + width * 1.8) / (width * 1.35));
                float bend = Bell(offset / (width * 2.8)) * sin(offset * 1.45 / width);
                return float2(crest, bend) * strength;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 origin = _GuardShape.xy;

                float2 screenScale = float2(_GuardShape.z, 1 / _GuardShape.w);
                float2 delta = (uv - origin) * screenScale;
                float distance = length(delta);
                float2 dir = delta / max(0.0001, distance);
                float angle = atan2(dir.y, dir.x + 0.00001);
                float age = _GuardTiming.x;
                float2 wave = Wave(age, distance) + _GuardEcho.y * Wave(age - _GuardEcho.x, distance);

                // Keep brighter highlights on opposite sides of the circular front.
                float lightArc = smoothstep(0.38, 0.90, 0.5 + 0.5 * cos(angle * 2 + 0.94));
                float2 offset = (dir / float2(_GuardShape.z, 1)) * (wave.y * _GuardEcho.z);
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv + offset));
                float film = smoothstep(0, _GuardTiming.y, age) *
                    (1 - smoothstep(0, _GuardTiming.w, age - _GuardTiming.y - _GuardTiming.z));
                scene.rgb = lerp(scene.rgb, _GuardFilm.rgb, film * _GuardFilm.a);
                scene.rgb = lerp(scene.rgb, _GuardWaveColor.rgb,
                    saturate(wave.x * lightArc * _GuardMotion.w * _GuardWaveColor.a));
                return scene;
            }
            ENDHLSL
        }
    }
}
