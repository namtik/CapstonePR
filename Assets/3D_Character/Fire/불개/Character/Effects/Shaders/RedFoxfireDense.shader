Shader "CreatureAnim/RedFoxfireDense"
{
    Properties
    {
        _BaseMap("Flame / glow atlas",2D)="white"{}
        _BaseColor("Tint / animated fade",Color)=(1,1,1,1)
        _FlameDensity("Flame opacity density",Range(1,4))=2.4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _FlameDensity;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;half fog:TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=TRANSFORM_TEX(v.uv,_BaseMap);o.color=v.color*_BaseColor;o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half4 texel=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*i.color;
                half visibility=MixFogColor(half3(1,1,1),half3(0,0,0),i.fog).r;
                half a=saturate(texel.a)*visibility;
                // The atlas's right half holds Gumiho's radial glow: keep its additive halo.
                if(i.uv.x>=.5)return half4(texel.rgb*a,0);
                // The left half is the branching flame. Give its body real coverage of the background.
                half opacity=1-pow(max(0,1-a),_FlameDensity);
                return half4(texel.rgb*opacity,opacity);
            }
            ENDHLSL
        }
    }
}
