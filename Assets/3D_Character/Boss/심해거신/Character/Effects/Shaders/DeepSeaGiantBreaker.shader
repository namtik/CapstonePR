Shader "Sasindo/Environment - Deep Sea Breaker"
{
    Properties
    {
        _DeepColor("Deep water", Color) = (.024,.18,.25,1)
        _CrestColor("Crest water", Color) = (.20,.53,.61,1)
        _FoamColor("Foam", Color) = (.73,.91,.92,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor, _CrestColor, _FoamColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.uv=v.uv;
                return o;
            }
            float Noise(float2 p)
            {
                float2 i=floor(p),f=frac(p);
                f=f*f*(3-2*f);
                float4 d=dot(i,float2(127.1,311.7))+float4(0,127.1,311.7,438.8);
                float4 h=frac(sin(d)*43758.5453);
                return lerp(lerp(h.x,h.y,f.x),lerp(h.z,h.w,f.x),f.y);
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv=i.uv;
                float flow=Noise(uv*float2(24,8)+float2(0,-_Time.y*1.8));
                float bubbles=Noise(uv*float2(95,24)+float2(-_Time.y*.8,-_Time.y*2.1));
                float foam=smoothstep(.63,.92,uv.y+.26*(flow-.5));
                foam*=smoothstep(.23,.68,flow*.60+bubbles*.40);
                half3 color=lerp(_DeepColor.rgb,_CrestColor.rgb,smoothstep(.07,.87,uv.y+flow*.19));
                color=lerp(color,_FoamColor.rgb,saturate(foam*1.46));
                half fade=smoothstep(0,.12,uv.y)*(1-smoothstep(.94,1,uv.y))
                    *smoothstep(0,.13,uv.x)*(1-smoothstep(.87,1,uv.x));
                return half4(color,fade*lerp(.68,.98,foam));
            }
            ENDHLSL
        }
    }
}
