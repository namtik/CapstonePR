Shader "CreatureAnim/Sickle Weasel Wind"
{
    Properties
    {
        _BaseColor("Wind core",Color)=(.83,.97,.98,.88)
        _EdgeColor("Wind edge",Color)=(.40,.72,.75,1)
        _Flow("Baked flow",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
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
                half4 _EdgeColor;
                float _Flow;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;half fog:TEXCOORD1;};
            Varyings Vert(Attributes i){Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.color=i.color;o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            half4 Frag(Varyings i):SV_Target
            {
                half thin=.80+.20*sin(i.uv.x*37-_Flow*3.5);
                half filament=.80+.20*sin(i.uv.x*15+i.uv.y*5-_Flow*2);
                half3 tint=lerp(_EdgeColor.rgb,_BaseColor.rgb,saturate(i.color.r));
                half alpha=saturate(_BaseColor.a*i.color.a*thin*filament);
                return half4(MixFog(tint,i.fog),alpha);
            }
            ENDHLSL
        }
    }
}
