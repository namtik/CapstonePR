Shader "CreatureAnim/Human Monster Accents"
{
    Properties
    {
        _BaseColor("Accent tint",Color)=(.12,.61,.50,.80)
        _EdgeColor("Accent highlight",Color)=(.60,.96,.82,1)
        _Mode("0 spirit, 1 ink, 2 blood, 3 paper, 4 steel",Float)=0
        _Flow("Baked flow",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                float _Mode;
                float _Flow;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;half4 color:COLOR;half fog:TEXCOORD3;};
            Varyings Vert(Attributes i){Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.uv=i.uv;o.color=i.color;o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(a),Hash(a+float2(1,0)),f.x),lerp(Hash(a+float2(0,1)),Hash(a+1),f.x),f.y);}
            half4 Frag(Varyings i):SV_Target
            {
                half3 n=normalize(i.normalWS),v=GetWorldSpaceNormalizeViewDir(i.positionWS);Light light=GetMainLight();
                half detail=Noise(i.uv*float2(13,7)-float2(_Flow*.14,_Flow*.07));
                half coverage=smoothstep(.16,.51,detail)*.72+.28;
                half3 tint=lerp(_BaseColor.rgb,_EdgeColor.rgb,saturate(i.color.r)*.60);
                half alpha=_BaseColor.a*i.color.a*coverage;
                if(_Mode>.5&&_Mode<1.5){tint=_BaseColor.rgb*(.78+.30*detail);alpha*=pow(saturate(abs(dot(n,v))),.55)*.90;}
                if(_Mode>1.5&&_Mode<2.5){tint=_BaseColor.rgb*(.55+.45*saturate(dot(n,light.direction)))+_EdgeColor.rgb*pow(saturate(dot(n,normalize(light.direction+v))),64)*.35;alpha=_BaseColor.a*i.color.a;}
                if(_Mode>2.5&&_Mode<3.5){tint=lerp(_BaseColor.rgb,half3(.45,.025,.025),i.color.r);alpha=_BaseColor.a*i.color.a;}
                if(_Mode>3.5){tint=lerp(_BaseColor.rgb,half3(.96,.99,1),saturate(i.color.r)*.65);alpha=_BaseColor.a*i.color.a*(.88+.12*detail);}
                return half4(MixFog(tint,i.fog),saturate(alpha));
            }
            ENDHLSL
        }
    }
}
