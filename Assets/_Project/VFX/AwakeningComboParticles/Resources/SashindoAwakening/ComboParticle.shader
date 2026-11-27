
Shader "Sashindo/Awakening Combo Particle"
{
    Properties
    {
        [HideInInspector] _ZTest ("Depth Test", Float) = 4
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Cull Off
        ZWrite Off
        ZTest [_ZTest]
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 position : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float r = dot(p, p);
                float falloff = exp(-4.8 * r) * saturate((1.0 - r) * 2.4);
                return fixed4(input.color.rgb * (falloff * input.color.a), 0.0);
            }
            ENDCG
        }
    }
}
