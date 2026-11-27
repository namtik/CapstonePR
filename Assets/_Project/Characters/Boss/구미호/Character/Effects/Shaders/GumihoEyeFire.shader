Shader "Sasindo/Gumiho Eye Fire"
{
    Properties
    {
        [HDR]_FireColor("Blue Flame", Color)=(.035,.38,1.45,1)
        [HDR]_CoreColor("Inner Flame", Color)=(.65,1.25,1.7,1)
        _Intensity("Intensity", Range(0,2))=1
        _SpiritTime("Preview Time (-1 = Live)", Float)=-1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Name "EyeFire"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FireColor, _CoreColor;
                float _Intensity, _SpiritTime;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float Hash(float2 p)
            {
                float3 q = frac(float3(p.x,p.y,p.x) * .1031);
                q += dot(q,q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(cell),Hash(cell + float2(1,0)),f.x),
                            lerp(Hash(cell + float2(0,1)),Hash(cell + 1),f.x),f.y);
            }

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float Tongue(float x, float y, float center, float height, float width)
            {
                float progress = saturate(y / height);
                float taper = pow(1 - progress,.72) * (.64 + .36 * smoothstep(0,.24,progress));
                float edge = width * taper - abs(x - center);
                return smoothstep(-.06,.055,edge) * (1 - smoothstep(.86,1,progress));
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float time = (_SpiritTime < 0 ? _Time.y : _SpiritTime) + (unity_WorldTransformParams.w < 0 ? .41 : 0);
                float x = 2 * i.uv.x - 1;
                float y = i.uv.y;
                float flow = Noise(float2(x * 2.8,y * 7.5 - time * 6.2));
                float detail = Noise(float2(x * 6.1 + 4,y * 14 - time * 11.1));
                float bend = (.31 * sin(13*y - time*9) + .46*(flow-.5)) * y;
                float mainHeight = .75 + .13 * sin(time*7.6) + .04 * sin(time*15);
                float forkHeight = .69 + .14 * sin(time*9.1 + 2);

                float main = Tongue(x,y,bend,mainHeight,.58);
                float outer = Tongue(x,y,bend + .78*y,forkHeight,.33);
                float inner = Tongue(x,y,bend - .64*y,.56 + .12*sin(time*8.3),.29);
                float body = max(main,max(.87*outer,.76*inner));
                float breakup = smoothstep(.13,.48,flow*.7 + detail*.3 + .24*(1-y));
                float root = smoothstep(0,.15,y);
                float alpha = saturate(body * breakup * root * .91 * _Intensity);
                float core = Tongue(x,y,bend*.65,.55 + .10*sin(time*9),.30);
                core *= root * (.55 + .45*flow);

                float halo = exp(-abs(x-bend)*4.7) * exp(-pow((y-.32)*3.0,2));
                halo *= root * (1-smoothstep(.65,1,y)) * (1-smoothstep(.78,1,abs(x)));
                half3 color = lerp(_FireColor.rgb*.65,_FireColor.rgb,saturate(body + flow*.4));
                color = color * alpha + _CoreColor.rgb * core * .67 * _Intensity;
                color += _FireColor.rgb * halo * .13 * _Intensity;
                for (int ember = 0; ember < 2; ember++)
                {
                    float age = frac(time * (1.27 + ember*.24) + ember*.53);
                    float sparkX = (ember == 0 ? -.43 : .43) + .13*sin(age*5 + ember);
                    float2 spark = float2((x-sparkX)*23,(y-(.56+.42*age))*33);
                    float sparkFade = smoothstep(0,.18,age) * (1-smoothstep(.53,1,age));
                    color += _CoreColor.rgb * exp(-dot(spark,spark)) * sparkFade * .66 * _Intensity;
                }
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
