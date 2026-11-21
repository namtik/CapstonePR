Shader "CreatureAnim/Mikoshi Ink"
{
    Properties
    {
        _BaseColor("Tint and opacity", Color) = (.12,.045,.18,1)
        _Flow("Baked flow", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Flow;
            CBUFFER_END
            struct Attributes { float4 pos : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.pos=TransformObjectToHClip(i.pos.xyz);
                o.uv=i.uv;
                o.color=i.color;
                return o;
            }
            float Hash(float2 p)
            {
                float3 q=frac(p.xyx*.1031);
                q+=dot(q,q.yzx+33.33);
                return frac((q.x+q.y)*q.z);
            }
            float Noise(float2 p)
            {
                float2 cell=floor(p),f=frac(p);
                f=f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),
                    lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 p=i.uv*2-1;
                float cloud=Noise(i.uv*5+float2(_Flow*.12,0));
                float fiber=Noise(i.uv*float2(23,12)+cloud*.6);
                float edge=.77-length(p)+.45*(cloud-.5)+.16*(fiber-.5);
                half toon=smoothstep(-.045,.085,edge);
                half bleed=smoothstep(-.19,.24,edge)*lerp(.43,1,cloud);
                half rim=(1-smoothstep(.025,.27,edge))*smoothstep(.31,.69,cloud+.15*p.y);
                half3 ink=lerp(half3(.021,.007,.034),half3(.105,.027,.137),smoothstep(.27,.76,cloud));
                ink=lerp(ink,half3(.35,.105,.36),rim*.69);
                half frame=1-smoothstep(.80,1,max(abs(p.x),abs(p.y)));
                half alpha=saturate((toon*.5+bleed*.5)*i.color.a*_BaseColor.a*1.53)*frame;
                return half4(ink*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
