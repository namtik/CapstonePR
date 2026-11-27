Shader "CreatureAnim/Hopping Corpse Talisman"
{
    Properties
    {
        _BaseColor("Paper tint and animated opacity", Color) = (.84,.70,.33,.95)
        _EdgeColor("Paper highlight", Color) = (.58,.55,.50,1)
        _Mode("Paper mode", Float) = 3
        _Flow("Baked flow", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                float _Mode, _Flow;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 position = input.positionOS.xyz;
                float end = abs(input.uv.y - .5) * 2;
                position.z += .035 * end * end * sin(input.uv.y * 9 - _Flow * 2);
                output.positionCS = TransformObjectToHClip(position);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash(float2 p)
            {
                float3 q = frac(p.xyx * .1031);
                q += dot(q,q.yzx+33.33);
                return frac((q.x+q.y)*q.z);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f*f*(3-2*f);
                return lerp(lerp(Hash(cell), Hash(cell+float2(1,0)), f.x),
                            lerp(Hash(cell+float2(0,1)), Hash(cell+1), f.x), f.y);
            }

            float Stroke(float2 p, float2 a, float2 b, float startWidth, float endWidth)
            {
                float2 axis = b-a;
                float along = saturate(dot(p-a,axis)/dot(axis,axis));
                return lerp(startWidth,endWidth,along)-length(p-a-axis*along);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float fiber = Noise(uv*float2(105,21));
                float wear = Noise(uv*float2(12,27));
                float stain = Noise(uv*float2(4,7)+2);
                float edge = min(min(uv.x,1-uv.x),min(uv.y,1-uv.y)*1.6);
                float nick = .007+.029*pow(wear,2) + .045*pow(Noise(uv*float2(23,43)),6);
                float coverage = smoothstep(-.005,.007,edge-nick);
                float edgeShade = smoothstep(.012,.11,edge);
                float crease = 1-smoothstep(.005,.055,abs(uv.x-.46-.11*sin(uv.y*5)));
                half3 paper = _BaseColor.rgb * lerp(half3(.52,.36,.19),half3(1.25,1.18,.99),edgeShade);
                paper *= .74+.21*stain+.13*fiber+.10*crease;

                float2 p = uv*float2(1,2.24);
                p += float2(wear-.5,fiber-.5)*.021;
                float mark = Stroke(p,float2(.29,1.88),float2(.70,1.91),.036,.015);
                mark = max(mark,Stroke(p,float2(.48,2.01),float2(.43,1.62),.029,.022));
                mark = max(mark,Stroke(p,float2(.25,1.67),float2(.73,1.71),.028,.018));
                mark = max(mark,Stroke(p,float2(.31,1.53),float2(.24,1.33),.024,.019));
                mark = max(mark,Stroke(p,float2(.30,1.53),float2(.70,1.56),.027,.024));
                mark = max(mark,Stroke(p,float2(.70,1.56),float2(.64,1.33),.030,.020));
                mark = max(mark,Stroke(p,float2(.24,1.33),float2(.64,1.33),.025,.016));
                mark = max(mark,Stroke(p,float2(.53,1.59),float2(.43,1.17),.030,.031));
                mark = max(mark,Stroke(p,float2(.43,1.17),float2(.70,1.09),.032,.021));
                mark = max(mark,Stroke(p,float2(.70,1.09),float2(.31,.80),.033,.025));
                mark = max(mark,Stroke(p,float2(.31,.80),float2(.53,.66),.030,.019));
                mark = max(mark,Stroke(p,float2(.53,.66),float2(.41,.49),.025,.009));
                mark = max(mark,Stroke(p,float2(.30,1.11),float2(.19,.88),.025,.009));
                mark = max(mark,Stroke(p,float2(.66,.87),float2(.78,.69),.031,.010));
                float stamp = abs(max(abs(p.x-.50),abs(p.y-.30))-.105);
                mark = max(mark,.017-stamp);
                mark = max(mark,Stroke(p,float2(.46,.35),float2(.55,.25),.015,.012));

                float ink = smoothstep(-.013,.009,mark)*( .77 + .23*fiber );
                float bleed = smoothstep(-.034,-.003,mark+wear*.009);
                paper = lerp(paper,half3(.41,.13,.055),bleed*.35);
                half3 color = lerp(paper,lerp(half3(.23,.024,.010),half3(.48,.056,.020),wear),ink);
                return half4(MixFog(color,input.fog),coverage*_BaseColor.a);
            }
            ENDHLSL
        }
    }
}
