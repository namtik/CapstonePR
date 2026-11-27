Shader "Stage/HarborContactRipples"
{
 Properties{_BaseColor("Foam tint",Color)=(.62,.74,.77,1) _Opacity("Opacity",Range(0,1))=.11}
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-5"}
 Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 Pass{Tags{"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor;float _Opacity;
 CBUFFER_END
 struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float4 phase:COLOR;};
 struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float3 w:TEXCOORD1;float phase:TEXCOORD2;float fog:TEXCOORD3;};
 V vert(A i){V o;o.w=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.w);o.uv=i.uv*2-1;o.phase=i.phase.a;o.fog=ComputeFogFactor(o.p.z);return o;}
 float hash(float2 p){p=frac(p*float2(123.34,345.45));p+=dot(p,p+34.345);return frac(p.x*p.y);}
 float noise(float2 p){float2 g=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(g),hash(g+float2(1,0)),f.x),lerp(hash(g+float2(0,1)),hash(g+1),f.x),f.y);}
 float ring(float r,float age,float wrinkle){float rad=.23+age*.68+wrinkle;float w=max(fwidth(r)*.9,.008);return (1-smoothstep(w,w*2.4,abs(r-rad)))*smoothstep(0,.17,age)*(1-smoothstep(.55,1,age));}
 half4 frag(V i):SV_Target{float t=_Time.y;float2 q=i.uv*float2(1,1.12);float r=length(q),angle=atan2(q.y,q.x);
 float wrinkle=sin(angle*3+t*.65+i.phase*9)*.018+sin(angle*7-t*.4)*.009;
 float age=frac(t*.19+i.phase);float a=max(ring(r,age,wrinkle),ring(r,frac(age+.48),wrinkle));
 float breakup=smoothstep(.51,.74,noise(q*6+float2(t*.09+i.phase*8,-t*.06)));a*=breakup*_Opacity;
 Light sun=GetMainLight();half3 light=SampleSH(half3(0,1,0))*.9+sun.color*(saturate(sun.direction.y)*.58+.05);
 return half4(MixFog(_BaseColor.rgb*max(light,half3(.015,.015,.015)),i.fog),a);}
 ENDHLSL
 }}}
