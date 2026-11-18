Shader "CreatureAnim/Corrupted Shale Dust"
{
    Properties { _BaseColor("Dust tint", Color) = (0.58,0.49,0.34,0.18) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION;float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float3 p:TEXCOORD2; };
            Varyings Vert(Attributes v)
            {
                Varyings o;o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.p=v.positionOS.xyz;return o;
            }
            float Grain(float3 p)
            {
                float3 a=floor(p),f=frac(p);
                f=f*f*(3-2*f);
                float4 h=frac(sin(dot(a,float3(41,71,17))+float4(0,41,71,112))*43758.5453);
                float4 j=frac(sin(dot(a,float3(41,71,17))+float4(17,58,88,129))*43758.5453);
                h=lerp(h,j,f.z);
                return lerp(lerp(h.x,h.y,f.x),lerp(h.z,h.w,f.x),f.y);
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 n=normalize(i.normalWS);float3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);
                half softness=pow(saturate(dot(n,view)),1.8);
                Light mainLight=GetMainLight();half lighting=.76+.24*saturate(dot(n,mainLight.direction));
                half grain=Grain(i.p*6.5)*.7+Grain(i.p*19.0)*.3;
                half lift=smoothstep(-.58,.18,i.p.y);
                half3 tint=lerp(_BaseColor.rgb*half3(.68,.61,.54),_BaseColor.rgb,grain);
                return half4(tint*lighting,_BaseColor.a*softness*lift*smoothstep(.12,.78,grain));
            }
            ENDHLSL
        }
    }
}

