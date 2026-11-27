Shader "CreatureAnim/Fallen Samurai Steel"
{
    Properties
    {
        _BaseColor("Animated fade", Color) = (1,1,1,1)
        _Flow("Baked flow", Float) = 0
        _Sparks("Ash mode", Float) = 0
        _DenseBuff("Dense buff", Range(0,1)) = 0
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
                float _Flow, _Sparks, _DenseBuff;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv;
                output.color=input.color;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            float Hash(float2 p)
            {
                float3 q=frac(p.xyx*.1031);
                q+=dot(q,q.yzx+33.33);
                return frac((q.x+q.y)*q.z);
            }
            float Noise(float2 p)
            {
                float2 cell=floor(p), f=frac(p);
                f=f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),
                    lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float u=input.uv.x, v=input.uv.y;
                float grain=Noise(input.uv*float2(32,19));
                half alpha=saturate(input.color.a*_BaseColor.a*lerp(1.45,2.15,_DenseBuff));
                if (_Sparks>.5)
                {
                    float2 p=input.uv*2-1;
                    float edge=.73-length(p)+.36*(grain-.5);
                    half dust=smoothstep(-.12,.27,edge)*lerp(.45,1,grain);
                    half3 ash=lerp(half3(.024,.020,.019),half3(.25,.18,.14),grain);
                    ash=lerp(ash,lerp(half3(.012,.010,.014),half3(.145,.113,.134),grain),_DenseBuff);
                    alpha*=dust;
                    return half4(ash*alpha,alpha);
                }
                half3 normal=normalize(input.normalWS);
                half facing=dot(normal,GetWorldSpaceNormalizeViewDir(input.positionWS));
                clip(facing);
                float2 field=float2(input.positionWS.x+input.positionWS.y*.71,input.positionWS.z-input.positionWS.y*.43);
                float smoke=Noise(field*12+float2(-_Flow*.12,v*.8));
                float wisps=Noise(field*29+float2(_Flow*.05,v*.9));
                half diffuse=saturate(dot(normal,normalize(half3(-.52,.77,-.36))));
                half3 shadow=lerp(half3(.017,.013,.021),half3(.20,.145,.17),diffuse*lerp(.48,1,smoke));
                shadow*=lerp(1,.72,_DenseBuff);
                half body=smoothstep(0,.78,facing);
                half dense=1-pow(1-alpha,1.9);
                alpha=dense*body*lerp(.33,1,smoothstep(.19,.74,smoke*.68+wisps*.32));
                return half4(shadow*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
