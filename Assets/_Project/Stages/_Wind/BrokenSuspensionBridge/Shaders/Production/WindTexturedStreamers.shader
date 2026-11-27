Shader "Stage/WindTexturedStreamers"
{
    Properties{_BaseMap("Woven cloth",2D)="white"{} _BaseColor("Tint",Color)=(1,1,1,1)}
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"} Cull Off
        Pass
        {
            Tags{"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)float4 _BaseMap_ST;half4 _BaseColor;CBUFFER_END
            struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
            struct V{float4 p:SV_POSITION;float3 n:TEXCOORD0;float2 uv:TEXCOORD1;float fog:TEXCOORD2;};
            V Vert(A i){V o;float3 p=i.p.xyz;float s=i.uv.x*i.uv.x;p.z+=(sin(_Time.y*2.7-i.uv.x*7)*.19+sin(_Time.y*4.2-i.uv.x*13)*.06)*s;p.y+=sin(_Time.y*2.2-i.uv.x*8)*.07*s;o.p=TransformObjectToHClip(p);o.n=TransformObjectToWorldNormal(i.n);o.uv=i.uv;o.fog=ComputeFogFactor(o.p.z);return o;}
            half4 Frag(V i):SV_Target{float end=1-.045*sin(i.uv.y*31)-.012*sin(i.uv.y*83);clip(end-i.uv.x);Light sun=GetMainLight();half3 n=normalize(i.n);half3 light=max(SampleSH(n)+sun.color*(abs(dot(n,sun.direction))*.7+.04),half3(.025,.025,.025));half3 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb*light;return half4(MixFog(c,i.fog),1);}
            ENDHLSL
        }
    }
}
