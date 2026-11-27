Shader "ExportedCreatures/RockGiant/CreatureAnim/SoftEarthDust"
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
            struct Varyings { float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(v.normalOS);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 n=normalize(i.normalWS);float3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);
                half softness=pow(saturate(dot(n,view)),1.8);
                Light mainLight=GetMainLight();half lighting=.76+.24*saturate(dot(n,mainLight.direction));
                return half4(_BaseColor.rgb*lighting,_BaseColor.a*softness);
            }
            ENDHLSL
        }
    }
}

