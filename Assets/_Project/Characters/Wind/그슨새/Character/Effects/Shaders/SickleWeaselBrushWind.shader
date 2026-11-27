Shader "CreatureAnim/Sickle Weasel Brush Wind"
{
    Properties
    {
        _BaseColor("Wind tint and opacity", Color)=(.83,.97,.98,.88)
        _Flow("Baked flow", Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Flow;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; half fog : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash(float2 coordinate)
            {
                float3 q = frac(float3(coordinate.x,coordinate.y,coordinate.x) * .1031);
                q += dot(q,q.yzx+33.33);
                return frac((q.x+q.y)*q.z);
            }

            float Noise(float2 coordinate)
            {
                float2 cell = floor(coordinate), f = frac(coordinate);
                f = f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),
                            lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float pressure = Noise(float2(uv.x*8 - _Flow*.42,uv.y*2));
                float grain = Noise(float2(uv.x*13 - _Flow*.65,uv.y*8));
                float center = .49 + .13*(pressure-.5);
                float spread = .30 + .14*pressure;
                float distanceToEdge = spread - abs(uv.y-center);
                half silhouette = smoothstep(-.012,.035,distanceToEdge);
                half gaps = lerp(.18,1,smoothstep(.22,.48,grain + input.color.r*.30));
                half midTone = smoothstep(.36,.43,uv.y + .14*(pressure-.5));
                half lightTone = smoothstep(.59,.65,uv.y + .18*(pressure-.5));
                half3 wind = lerp(half3(.028,.24,.09),half3(.09,.57,.23),midTone);
                wind = lerp(wind,half3(.43,.89,.36),lightTone);
                half crest = (1-smoothstep(.035,.10,distanceToEdge)) * step(center,uv.y);
                wind = lerp(wind,half3(.79,.99,.57),crest*smoothstep(.36,.60,grain));
                float current = uv.y + .11*(pressure-.5) + .045*sin(uv.x*18-_Flow);
                half opening = 1-smoothstep(.015,.060,abs(current-.45));
                opening *= smoothstep(.38,.60,Noise(float2(uv.x*8-_Flow*.55,2)));
                half stream = (1-smoothstep(.010,.042,abs(current-.68))) * smoothstep(.28,.58,grain);
                wind = lerp(wind,half3(.86,1,.66),stream*.85);
                half alpha = saturate(_BaseColor.a * input.color.a * silhouette * gaps * 1.52) * (1-opening*.92);
                return half4(MixFog(wind,input.fog),alpha);
            }
            ENDHLSL
        }
    }
}
