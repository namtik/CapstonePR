Shader "ExportedCreatures/Slime/CreatureAnim/Slime Polluted Water"
{
    Properties
    {
        _BaseColor("Water tint",Color)=(0.08,0.62,0.94,0.88)
        _FoamColor("Foam and reflections",Color)=(0.82,0.97,1,1)
        _Bubble("Thin bubble",Range(0,1))=0
        _Sheet("Thin flowing water",Range(0,1))=0
        _Pool("Sludge pool",Range(0,1))=0
        _Cull("Cull",Float)=2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _FoamColor;
                float _Bubble;
                float _Sheet;
                float _Pool;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 color:COLOR;half fog:TEXCOORD2;float2 uv:TEXCOORD3; };
            Varyings Vert(Attributes i)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.color=i.color;o.fog=ComputeFogFactor(o.positionCS.z);o.uv=i.uv;return o;
            }
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(a),Hash(a+float2(1,0)),f.x),lerp(Hash(a+float2(0,1)),Hash(a+1),f.x),f.y);}
            half4 Frag(Varyings i, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                half3 n=normalize(i.normalWS)*IS_FRONT_VFACE(face,1,-1),v=GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light light=GetMainLight();half3 l=normalize(light.direction),h=normalize(l+v);
                half facing=saturate(dot(n,v)),rim=pow(1-facing,3.5);
                half diffuse=saturate(dot(n,l)*.65+.35);
                half detail=Noise(i.uv*float2(24,11))*.64+Noise(i.uv*float2(49,23))*.36;
                half thread=1-smoothstep(.018,.065,abs(detail-.55));
                half perforation=lerp(1,smoothstep(.23,.42,detail),_Sheet);
                half sharp=pow(saturate(dot(n,h)),110)*1.2;
                half broad=pow(saturate(dot(n,h)),32)*.36;
                half foam=saturate(i.color.r*(.26+.74*smoothstep(.36,.70,detail))+thread*.24*_Sheet);
                half3 body=lerp(_BaseColor.rgb*float3(.47,.76,.88),_BaseColor.rgb,.40+.60*diffuse);
                half sky=smoothstep(.12,.85,reflect(-v,n).y);
                half sludge=Noise(i.uv*float2(7,9)+Noise(i.uv*19)*.7);
                body=lerp(body*float3(.55,.60,.40),body, .45+.55*sludge);
                body=lerp(body,half3(.25,.19,.055),smoothstep(.56,.76,sludge)*.36);
                body=lerp(body,_FoamColor.rgb,saturate(foam*.66+rim*.24+sky*.09));
                half biofilm=smoothstep(.62,.80,detail)*smoothstep(.40,.70,sludge)*_Pool;
                body=lerp(body,_FoamColor.rgb,biofilm*.42);
                body+=_FoamColor.rgb*(sharp+broad)*.72;
                half alpha=_BaseColor.a*i.color.a*lerp(.88,1,rim)*perforation;
                alpha*=lerp(1,lerp(.34,1,rim),_Bubble);
                alpha=saturate(alpha+(sharp*.22+foam*.50)*_BaseColor.a*i.color.a*perforation);
                body=MixFog(body,i.fog);return half4(body,alpha);
            }
            ENDHLSL
        }
    }
}
