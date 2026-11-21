Shader "CreatureAnim/Karakasa Muted Water"
{
    Properties
    {
        _BaseColor("Water tint and opacity", Color)=(.10,.64,.84,.88)
        _FoamColor("Reflection color", Color)=(.84,.97,1,1)
        _Bubble("Thin bubble", Range(0,1))=0
        _Sheet("Thin flowing water", Range(0,1))=0
        _Cull("Cull", Float)=2
        _UseVertexMask("Use water mesh mask", Range(0,1))=1
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
                half4 _BaseColor, _FoamColor;
                half _Bubble, _Sheet, _UseVertexMask;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half4 color:COLOR; half fog:TEXCOORD2; float2 uv:TEXCOORD3; };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.color = i.color;
                o.uv = i.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Hash(float2 p)
            {
                float3 q = frac(float3(p.x,p.y,p.x)*.1031);
                q += dot(q,q.yzx+33.33);
                return frac((q.x+q.y)*q.z);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),
                            lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }

            half4 Frag(Varyings i, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                half3 n = normalize(i.normalWS) * IS_FRONT_VFACE(face,1,-1);
                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 l = GetMainLight().direction;
                half facing = saturate(dot(n,v));
                half rim = pow(1-facing,3);
                half diffuse = saturate(dot(n,l)*.64+.36);

                float wash = Noise(i.uv*float2(12,5)+.32) * .66 + Noise(i.uv*float2(28,10)) * .34;
                float grain = Noise(i.uv*float2(48,20)+2);
                half lightValue = diffuse * .84 + wash * .24;
                half midBand = smoothstep(.34,.40,lightValue);
                half lightBand = smoothstep(.57,.64,lightValue);
                half3 body = lerp(half3(.024,.075,.19),half3(.052,.23,.44),midBand);
                body = lerp(body,half3(.12,.38,.65),lightBand);
                body *= lerp(.83,1.09,smoothstep(.25,.76,wash));

                half broken = smoothstep(.36,.59,wash*.72+grain*.28);
                half edge = smoothstep(.42,.91,max(rim,i.color.r*.95*_UseVertexMask));
                half fold = pow(saturate(dot(n,SafeNormalize(l+v))),36);
                half reflection = smoothstep(.23,.63,(edge*.88+fold*.85)*broken) * .82;
                half3 paintedLight = lerp(half3(.58,.79,.94),_FoamColor.rgb,.15);
                body = lerp(body,paintedLight,reflection);

                half perforation = lerp(1,lerp(.46,1,smoothstep(.19,.40,wash)),_Sheet);
                half transmission = lerp(.94,.88,_Sheet);
                half coverage = lerp(transmission,.98,reflection);
                coverage *= lerp(1,lerp(.13,1,edge),_Bubble);
                half mask = lerp(1,i.color.a,_UseVertexMask);
                half alpha = saturate(_BaseColor.a*mask*coverage*perforation);
                // Densify the body while keeping zero coverage at the mesh edges and fade keys.
                alpha = 1-pow(1-alpha,lerp(1.65,1.40,_Bubble));
                return half4(MixFog(body,i.fog),alpha);
            }
            ENDHLSL
        }
    }
}
