Shader "UI/CardBurn"
{
    // 카드(여러 Image)를 하나의 종이처럼 태우는 UI 셰이더.
    // - 노이즈는 절차적(fbm)으로 계산해 텍스처가 필요 없다.
    // - 같은 카드의 Image들이 한 머티리얼 인스턴스를 공유하고, 정점을 카드 로컬 좌표로 바꿔(_CardMatrix)
    //   레이어가 달라도 같은 자리가 같은 순간에 탄다.
    // - _Progress 0 = 멀쩡함, 1 = 완전히 타서 사라짐. 가장자리에 불꽃 띠와 그을림이 생긴다.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _Progress ("Burn Progress", Range(0, 1)) = 0
        _BurnWidth ("Burn Edge Width", Range(0.01, 0.3)) = 0.07
        _CharWidth ("Char Width", Range(0, 0.3)) = 0.08
        [HDR] _EdgeInner ("Edge Inner Color", Color) = (1, 0.92, 0.55, 1)
        [HDR] _EdgeOuter ("Edge Outer Color", Color) = (1, 0.35, 0.05, 1)
        _NoiseScale ("Noise Scale", Float) = 5
        _NoiseSeed ("Noise Seed", Float) = 0
        _DirWeight ("Bottom-up Weight", Range(0, 1)) = 0.45
        _CardRect ("Card Rect (xMin, yMin, w, h)", Vector) = (-150, -200, 300, 400)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float2 cardUV        : TEXCOORD2; // 카드 기준 0~1 좌표(레이어 공통)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _Progress;
            float _BurnWidth;
            float _CharWidth;
            float4 _EdgeInner;
            float4 _EdgeOuter;
            float _NoiseScale;
            float _NoiseSeed;
            float _DirWeight;
            float4 _CardRect;
            float4x4 _CardMatrix; // 캔버스 공간 → 카드 로컬 공간

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                for (int k = 0; k < 4; k++)
                {
                    v += valueNoise(p) * amp;
                    p *= 2.03;
                    amp *= 0.5;
                }
                return v / 0.9375; // 0~1로 정규화
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;

                float2 local = mul(_CardMatrix, float4(v.vertex.xyz, 1.0)).xy;
                o.cardUV = (local - _CardRect.xy) / max(_CardRect.zw, 0.0001);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;

                float2 uv = saturate(i.cardUV);
                float aspect = _CardRect.w / max(_CardRect.z, 0.0001);
                float noise = fbm(float2(uv.x, uv.y * aspect) * _NoiseScale + _NoiseSeed);
                // 아래쪽부터 타 올라가도록 세로 그라디언트를 섞는다
                float n = lerp(noise, uv.y, _DirWeight);

                // 0이면 하나도 안 타고 1이면 전부 타도록 범위를 넓힌다
                float p = _Progress * (1.0 + _BurnWidth + _CharWidth) - _BurnWidth;
                float d = n - p;
                if (d < 0) color.a = 0; // 이미 탄 부분

                float edge = saturate(d / _BurnWidth);                            // 0 = 불꽃 중심
                float charT = saturate((d - _BurnWidth) / max(_CharWidth, 0.0001)); // 그을림 → 원래 색
                color.rgb *= lerp(0.18, 1.0, charT);
                float glow = 1.0 - edge;
                color.rgb += lerp(_EdgeOuter.rgb, _EdgeInner.rgb, glow * glow) * glow * step(0, d);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
