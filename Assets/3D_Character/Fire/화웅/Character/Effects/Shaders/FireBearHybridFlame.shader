Shader "CreatureAnim/Fire Bear Flame"
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
                half visibility=MixFogColor(half3(1,1,1),half3(0,0,0),i.fog).r;
                if(i.uv.y<-4.5)
                {
                    float turn=i.uv.x;
                    float crest=-i.uv.y-5;
                    float tile=frac(turn*14+.12*sin(turn*6.2831853*5));
                    half flame=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,float2(lerp(.006,.488,tile),crest)).a;
                    half foot=smoothstep(0,.12,crest)*(1-smoothstep(.36,.68,crest));
                    half tongue=flame*smoothstep(.13,.42,crest)*(1-smoothstep(.77,1,crest));
                    half coverage=saturate((foot*(.22+.28*flame)+tongue*.44)*i.color.a*visibility);
                    half3 ringColor=lerp(half3(.63,.026,.009),half3(1,.29,.047),foot*(.18+.48*flame));
                    return half4(ringColor*coverage,coverage);
                }
                if(i.uv.y<-2.5)
                {
                    float2 disk=float2(i.uv.x,-i.uv.y-3);
                    float2 center=disk*2-1;
                    half flow=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,float2(disk.x*.49,disk.y)).a;
                    half radius=length(center)+.11*sin(center.y*9+center.x*4)*(1-flow);
                    half body=1-smoothstep(.46,.98,radius);
                    half heat=(1-smoothstep(.15,.83,radius))*(.60+.40*flow);
                    half3 color=lerp(half3(.87,.095,.023),half3(1,.82,.40),heat);
                    half opacity=saturate(body*i.color.a*visibility);
                    return half4(color*opacity,opacity);
                }
                if(i.uv.y<0)
                {
                    float height=-i.uv.y-1;
                    float edge=abs(i.uv.x*2-1);
                    half flow=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,float2(i.uv.x*.49,height)).a;
                    half body=(1-smoothstep(.24,1,edge))*smoothstep(0,.13,height)*(1-smoothstep(.44,1,height));
                    half heat=(1-smoothstep(.08,.74,edge+height*.48))*lerp(.56,1,flow);
                    half3 color=lerp(half3(.79,.065,.018),half3(1,.70,.29),heat);
                    half coverage=saturate(body*i.color.a*visibility*lerp(.65,1.12,flow));
                    half opacity=1-pow(1-coverage,_FlameDensity);
                    return half4(color*opacity,opacity);
                }
                half4 texel=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*i.color;
                half a=saturate(texel.a)*visibility;
                float2 bodyUV=float2(i.uv.x*2,i.uv.y);
                half breakup=.78+.22*sin(bodyUV.y*26+sin(bodyUV.x*13)*1.6);
                a*=lerp(.70,1,breakup);
                half hot=smoothstep(.28,.78,a)*(1-smoothstep(.16,.90,bodyUV.y));
                half3 flame=lerp(texel.rgb*half3(1,.66,.49),half3(1.0,.69,.31),hot*.70);
                // The atlas's right half holds Gumiho's radial glow: keep its additive halo.
                if(i.uv.x>=.5)return half4(texel.rgb*a,0);
                // The left half is the branching flame. Give its body real coverage of the background.
                half opacity=1-pow(max(0,1-a),_FlameDensity);
                return half4(flame*opacity,opacity);
            }
            ENDHLSL
        }
    }
}
