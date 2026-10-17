// 2026-10-16: preset-controlled star thinning and sparse, smooth twinkle pulses.
// 2026-10-16: coherent cloud-bank advection; bounded subtle edge evolution, 8 lookup taps.
// 2026-10-16: shared filtered cloud lookup for low-spec PC.
// 2026-10-15: filtered cloud frequencies, shared lighting work, stable horizon and stars.
// =============================================================================
//  PerfectSky_V4  —  절차적 스카이박스 + 완전한 낮/밤 사이클 대응
// =============================================================================
//  v3(격자 없는 구름 + 크기 다양성 + 살아있는 구름 + 라이트 추종 태양)의 모든 것 +
//  밤하늘 요소(별/달). 시간의 흐름 자체는 셰이더가 아니라 StageLightingManager.cs가 몰고,
//  이 셰이더는 그 컨트롤러가 넣어주는 값(하늘/구름/태양 색, _SunDir, 별/달 가시성)을
//  받아 렌더링만 한다.
//
//  [StageLightingManager.cs가 매 프레임 주입하는 값]
//    - 하늘/구름/태양 색(팔레트 Gradient 샘플), _SunCoreBright(밤=0)
//    - _SunDir (라이트 회전에서 도출), 라이트 색/세기, 앰비언트
//    - _StarVisibility, _MoonVisibility (낮=0 / 밤=1)
//
//  달 방향은 태양의 반대(-_SunDir)로 자동 도출한다(보름달 근사).
//  전체 해설: Assets/설명서/PerfectSky_가이드.md / PerfectSky_코드설명서.md
// =============================================================================
Shader "Skybox/PerfectSky_V4"
{
    Properties
    {
        [Header(Sky Gradient)]
        _SkyTopColor("Sky Top Color", Color) = (0.06, 0.26, 0.72, 1)
        _SkyMidColor("Sky Mid Color", Color) = (0.20, 0.50, 0.90, 1)
        _SkyHorizonColor("Sky Horizon Color", Color) = (0.86, 0.90, 0.90, 1)

        [Header(Clouds)]
        _CloudColor("Cloud Lit Color", Color) = (1.0, 0.99, 0.95, 1)
        _CloudShadowColor("Cloud Shade Color", Color) = (0.46, 0.56, 0.78, 1)
        _CloudScale("Cloud Scale", Float) = 1.9
        _CloudCoverage("Cloud Coverage", Range(0,1)) = 0.42
        _CloudSoftness("Cloud Edge Softness", Range(0.005, 0.4)) = 0.06
        _CloudBrightness("Cloud Brightness", Range(1, 1.6)) = 1.25
        _CloudLightStep("Cloud Light Offset", Range(0.05, 1.0)) = 0.42
        _CloudContrast("Cloud Shade Contrast", Range(0.5, 6)) = 3.2

        [Header(Cloud Seed V3)]
        _CloudSeed("Cloud Seed (reroll layout)", Float) = 0

        [Header(Cloud Shape V3)]
        _CloudClumpScale("Clump Scale (smaller_bigger masses)", Range(0.1, 1.5)) = 0.38
        _CloudClump("Clump Strength (size variety)", Range(0, 1.5)) = 0.8
        _CloudDetail("Detail Amount (wispy_to_solid)", Range(0, 0.8)) = 0.28

        [Header(Cloud Shape Motion)]
        [HideInInspector] _Wind2X("Legacy Wind 2 X (unused)", Float) = -0.004
        [HideInInspector] _Wind2Y("Legacy Wind 2 Y (unused)", Float) = 0.006
        _CloudMix("Layer Mix (A_to_B)", Range(0,1)) = 0.5
        _CloudEvolve("Edge Evolution Speed", Range(0, 0.2)) = 0.03
        _CloudMorph("Edge Evolution Multiplier", Range(0, 2)) = 0.7
        _CloudShapeMotion("Edge Shape Motion (0 locks shape)", Range(0, 0.25)) = 0.06

        [Header(Sun)]
        _SunColor("Sun Glow Color", Color) = (1.0, 0.9, 0.72, 1)
        _SunCoreColor("Sun Core Color", Color) = (1.0, 0.98, 0.92, 1)
        _SunDiskSize("Sun Disk Size (deg)", Range(0.2, 8)) = 2.2
        _SunDiskSoftness("Sun Disk Edge (deg)", Range(0.02, 4)) = 0.6
        _SunCoreBright("Sun Core Brightness (HDR)", Range(0, 25)) = 10
        _SunHaloSize("Sun Halo Tightness", Range(50, 4000)) = 700
        _SunHaloStrength("Sun Halo Strength", Range(0, 4)) = 1.6
        _SunGlow("Sun Glow Strength", Range(0, 2)) = 0.7
        _SunSize("Sun Glow Size", Range(2, 64)) = 10
        _SilverLining("Silver Lining", Range(0, 2)) = 1.2
        _SunDir("Sun Dir (set from script)", Vector) = (0.25, 0.55, 0.80, 0)

        // ── V4: 밤하늘(StageLightingManager가 가시성을 구동) ──
        [Header(Night Sky V4)]
        _StarVisibility("Star Visibility (0 day - 1 night)", Range(0,1)) = 0
        _StarDensity("Star Density", Range(20, 400)) = 150
        _StarBrightness("Star Brightness", Range(0,3)) = 1.3
        _StarAmount("Star Count Multiplier", Range(0,1)) = 1
        _StarTwinkleStrength("Occasional Star Twinkle", Range(0,1)) = 0
        _MoonVisibility("Moon Visibility (0 day - 1 night)", Range(0,1)) = 0
        _MoonColor("Moon Color", Color) = (0.90, 0.93, 1.0, 1)
        _MoonSize("Moon Size (deg)", Range(1, 12)) = 4
        _MoonGlow("Moon Glow", Range(0, 2)) = 0.6

        [Header(Animation)]
        _WindX("Wind Speed X", Float) = 0.008
        _WindY("Wind Speed Y", Float) = 0.003

        [NoScaleOffset] [NonModifiableTextureData] _CloudNoiseAtlas("Shared Cloud Noise", 2D) = "white" {}

        [Header(General)]
        _Exposure("Exposure", Range(0,2)) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _SkyTopColor, _SkyMidColor, _SkyHorizonColor, _CloudColor, _CloudShadowColor, _SunColor, _SunCoreColor, _MoonColor;
            float _CloudScale, _CloudCoverage, _CloudSoftness, _CloudBrightness, _CloudLightStep, _CloudContrast;
            float _CloudSeed;
            float _CloudClumpScale, _CloudClump, _CloudDetail;
            float _CloudMix, _CloudEvolve, _CloudMorph, _CloudShapeMotion;
            float _SunGlow, _SunSize, _SilverLining;
            float _SunDiskSize, _SunDiskSoftness, _SunCoreBright, _SunHaloSize, _SunHaloStrength;
            float _StarVisibility, _StarDensity, _StarBrightness, _MoonVisibility, _MoonSize, _MoonGlow;
            float _StarAmount, _StarTwinkleStrength;
            float4 _SunDir;
            float _WindX, _WindY, _Exposure;
            sampler2D _CloudNoiseAtlas;
            float4 _CloudNoiseAtlas_TexelSize;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            // 스칼라 해시(별 배치용). 별 셀 좌표는 방향 투영이라 값이 크지 않아 sin 해시로 충분.
            float hash1(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            // 격자점당 랜덤 방향(그라디언트 노이즈용) — sin-free, 큰 좌표 안정
            float2 hash2(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy) * 2.0 - 1.0;
            }

            // 그라디언트(Perlin) 노이즈 — 값노이즈의 네모 격자 아티팩트 제거 (v3와 동일)
            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                float a = dot(hash2(i),                 f);
                float b = dot(hash2(i + float2(1, 0)),  f - float2(1, 0));
                float c = dot(hash2(i + float2(0, 1)),  f - float2(0, 1));
                float d = dot(hash2(i + float2(1, 1)),  f - float2(1, 1));
                float g = lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
                return 0.5 + g * 0.7;
            }

            // Four resolved octaves retain the original six-octave mean.
            // Fade unresolved frequencies to their mean BEFORE thresholding them.
            // This filters both cloud silhouettes and lighting instead of only the edge.
            float filteredNoise(float2 p, float footprint)
            {
                float resolved = 1.0 - smoothstep(0.25, 0.65, footprint);
                return lerp(0.5, valueNoise(p), resolved);
            }

            float fbm4(float2 p, out float coarse)
            {
                float footprint = max(length(ddx(p)), length(ddy(p)));
                float v = 0.0, amp = 0.5;
                const float2x2 rot = float2x2(0.80, -0.60, 0.60, 0.80);
                [unroll] for (int k = 0; k < 3; k++)
                {
                    v += amp * filteredNoise(p, footprint);
                    p = mul(rot, p) * 2.0 + 100.0;
                    footprint *= 2.0;
                    amp *= 0.5;
                }
                coarse = v + 0.0546875;
                return v + amp * filteredNoise(p, footprint) + 0.0234375;
            }

            float fbm5(float2 p, out float coarse)
            {
                float footprint = max(length(ddx(p)), length(ddy(p)));
                float v = 0.0, amp = 0.5;
                const float2x2 rot = float2x2(0.80, -0.60, 0.60, 0.80);
                coarse = 0.0;
                [unroll] for (int k = 0; k < 5; k++)
                {
                    v += amp * filteredNoise(p, footprint);
                    if (k == 2) coarse = v + 0.0546875;
                    p = mul(rot, p) * 2.0 + 100.0;
                    footprint *= 2.0;
                    amp *= 0.5;
                }
                return v + 0.0078125;
            }

            float fbm3(float2 p, out float coarse)
            {
                float footprint = max(length(ddx(p)), length(ddy(p)));
                float v = 0.0, amp = 0.5;
                const float2x2 rot = float2x2(0.80, -0.60, 0.60, 0.80);
                [unroll] for (int k = 0; k < 2; k++)
                {
                    v += amp * filteredNoise(p, footprint);
                    p = mul(rot, p) * 2.0 + 100.0;
                    footprint *= 2.0;
                    amp *= 0.5;
                }
                coarse = v + 0.1171875;
                return v + amp * filteredNoise(p, footprint) + 0.0546875;
            }

            // Morph is shared with the lighting tap; keep broad envelope shading.
            // Shade only the coherent low frequencies, keeping fine wisps out of the normal.
            // Every density and lighting layer rides the same advected UV.
            // A bounded detail offset adds minute edge evolution without dissolving banks.
            float2 cloudEdgeDrift(float t)
            {
                float phase = t * _CloudEvolve * 0.5;
                return float2(sin(phase), cos(phase * 0.83) - 1.0) * (_CloudMorph * _CloudShapeMotion);
            }

            void cloudFieldProcedural(float2 uv, float2 lightOffset, out float n, out float slope)
            {
                float dummy, lowA, lowB, lowDetail;
                float env = fbm3(uv * _CloudClumpScale + 7.0, dummy);
                float t = _Time.y;
                float2 edgeDrift = cloudEdgeDrift(t);
                const float2 drift = float2(41.3, 41.3);
                float baseA = fbm5(uv, lowA);
                float baseB = fbm5(uv * 1.27 + drift, lowB);
                float detail = fbm4(uv * 2.6 + 11.0 + edgeDrift, lowDetail);
                float envelope = (env - 0.5) * _CloudClump;
                n = lerp(baseA, baseB, _CloudMix) + envelope + (detail - 0.5) * _CloudDetail;
                float coarse = lerp(lowA, lowB, _CloudMix) + envelope + (lowDetail - 0.5) * _CloudDetail;
                float neighborA = fbm3(uv + lightOffset, dummy);
                float neighborB = fbm3((uv + lightOffset) * 1.27 + drift, dummy);
                float neighborDetail = fbm3((uv + lightOffset) * 2.6 + 11.0 + edgeDrift, dummy);
                float neighborEnv = fbm3((uv + lightOffset) * _CloudClumpScale + 7.0, dummy);
                float towardSun = lerp(neighborA, neighborB, _CloudMix) + (neighborEnv - 0.5) * _CloudClump + (neighborDetail - 0.5) * _CloudDetail;
                slope = towardSun - coarse;
            }

            // Shared mipmapped RG16F tile: R=5 octave density, G=3 octave lighting.
            // No frame cache or per-frame CPU updates. Repeat wrapping preserves derivatives.
            float2 cloudNoiseLookup(float2 p)
            {
                return tex2D(_CloudNoiseAtlas, p * (1.0 / 16.0)).rg;
            }

            void cloudField(float2 uv, float2 lightOffset, out float n, out float slope)
            {
                n = 0.4921875;
                slope = 0.0;
                // Safe compatibility fallback for an incomplete legacy package import.
                [branch] if (_CloudNoiseAtlas_TexelSize.z < 16.0)
                {
                    cloudFieldProcedural(uv, lightOffset, n, slope);
                    return;
                }
                float env = cloudNoiseLookup(uv * _CloudClumpScale + 7.0).g;
                float t = _Time.y;
                float2 edgeDrift = cloudEdgeDrift(t);
                const float2 drift = float2(41.3, 41.3);
                float2 a = cloudNoiseLookup(uv);
                float2 b = cloudNoiseLookup(uv * 1.27 + drift);
                float2 detail = cloudNoiseLookup(uv * 2.6 + 11.0 + edgeDrift);
                float envelope = (env - 0.5) * _CloudClump;
                n = lerp(a.r, b.r, _CloudMix) + envelope + (lerp(detail.g, detail.r, 0.8) - 0.5) * _CloudDetail;
                float coarse = lerp(a.g, b.g, _CloudMix) + envelope + (detail.g - 0.5) * _CloudDetail;
                float neighborA = cloudNoiseLookup(uv + lightOffset).g;
                float neighborB = cloudNoiseLookup((uv + lightOffset) * 1.27 + drift).g;
                float neighborDetail = cloudNoiseLookup((uv + lightOffset) * 2.6 + 11.0 + edgeDrift).g;
                float neighborEnv = cloudNoiseLookup((uv + lightOffset) * _CloudClumpScale + 7.0).g;
                slope = lerp(neighborA, neighborB, _CloudMix) + (neighborEnv - 0.5) * _CloudClump + (neighborDetail - 0.5) * _CloudDetail - coarse;
            }

            // Pixel-footprint filtering prevents tiny stars from popping like sensor noise.
            // The center stays inside the cell so the filtered footprint does not get clipped.
            float starField(float3 dir)
            {
                float2 suv = dir.xz / (max(dir.y, 0.0) + 1.0) * _StarDensity;
                float2 cell = floor(suv);
                float2 fr = frac(suv);
                float h = hash1(cell);
                // Thin the existing constellation without changing star size or position.
                float present = step(1.0 - 0.045 * saturate(_StarAmount), h);
                float2 sp = 0.18 + 0.64 * float2(frac(h * 57.0), frac(h * 169.0));
                float2 delta = fr - sp;
                float footprint = max(length(ddx(suv)), length(ddy(suv)));
                float radius = lerp(0.055, 0.10, frac(h * 23.7));
                float filteredRadius = max(radius, footprint * 0.65);
                float energy = radius * radius / (filteredRadius * filteredRadius);
                float twinkle;
                [branch] if (_StarTwinkleStrength > 0.001)
                {
                    // Independent phase/speed per cell; only 35% of stars pulse.
                    // 8-16 second cycles with a short, smooth bright peak; never blink off.
                    float seed = frac(h * 317.13);
                    float phase = frac(h * 613.71) * 6.2831853;
                    float wave = 0.5 + 0.5 * sin(_Time.y * lerp(0.4, 0.8, seed) + phase);
                    float pulse = smoothstep(0.83, 0.99, wave);
                    float selected = step(0.65, frac(h * 97.31));
                    twinkle = lerp(1.0, 0.90 + 0.90 * pulse * selected, _StarTwinkleStrength);
                }
                else
                    twinkle = 0.88 + 0.12 * sin(_Time.y * 0.9 + h * 63.0);
                float star = exp2(-2.8854 * dot(delta, delta) / (filteredRadius * filteredRadius));
                return star * energy * present * twinkle * smoothstep(0.015, 0.18, dir.y);
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float3 sunDir = normalize(_SunDir.xyz);

                // ── 하늘 그라디언트(팔레트가 시간대별 색을 주입) ──
                float h = saturate(dir.y);
                float3 sky = lerp(_SkyHorizonColor.rgb, _SkyMidColor.rgb, pow(saturate(h / 0.35), 0.8));
                sky = lerp(sky, _SkyTopColor.rgb, saturate((h - 0.35) / 0.65));

                // ── 밤하늘: 별 + 달 (하늘에 더함 → 나중에 구름이 가림) ──
                //  밤엔 _StarVisibility/_MoonVisibility ~1, 낮엔 ~0 (컨트롤러가 구동).
                // Uniform branches skip invisible night features in daytime presets.
                [branch] if (_StarVisibility > 0.001 && dir.y > 0.0)
                    sky += starField(dir) * _StarBrightness * _StarVisibility;

                [branch] if (_MoonVisibility > 0.001)
                {
                    float cosM = dot(dir, -sunDir);
                    float mcd = cos(radians(_MoonSize));
                    float mce = cos(radians(_MoonSize + 0.8));
                    float moonDisk = smoothstep(mce, mcd, cosM);
                    float moonGlow = pow(saturate(cosM), 200.0) * _MoonGlow;
                    sky += _MoonColor.rgb * (moonDisk * 1.5 + moonGlow) * _MoonVisibility;
                }

                // ===================== 태양(3단 레이어) =====================
                //  밤엔 태양이 지평선 아래(cosA<0 → disk=0)이고 팔레트가 _SunCoreBright=0으로
                //  낮추므로 자동으로 사라진다.
                float cosA  = dot(dir, sunDir);
                float sunDot = saturate(cosA);
                float scatter = pow(sunDot, _SunSize) * _SunGlow;
                float halo    = pow(sunDot, _SunHaloSize) * _SunHaloStrength;
                float cd   = cos(radians(_SunDiskSize));
                float ce   = cos(radians(_SunDiskSize + _SunDiskSoftness));
                float disk = smoothstep(ce, cd, cosA);
                sky += _SunColor.rgb * scatter;
                sky += _SunColor.rgb * halo;
                sky += _SunCoreColor.rgb * disk * _SunCoreBright;

                float3 col = sky;
                // Clamp the projection BEFORE noise/derivatives: no singularity at y=-0.16.
                // The lower hemisphere needs no cloud work at all.
                [branch] if (dir.y > 0.0 && _CloudCoverage > 0.001)
                {
                    float2 seedOffset = float2(_CloudSeed * 78.233, _CloudSeed * 127.19);
                    float2 uv = dir.xz / (max(dir.y, 0.0) + 0.16) * _CloudScale + seedOffset;
                    uv += float2(_WindX, _WindY) * _Time.y;
                    float2 lightDir2D = normalize(sunDir.xz + 1e-4);
                    float n, slope;
                    cloudField(uv, lightDir2D * _CloudLightStep, n, slope);
                    float coverage = 1.0 - saturate(_CloudCoverage);
                    float edge = max(max(_CloudSoftness, 0.005), fwidth(n) * 0.7);
                    float clouds = smoothstep(coverage - edge, coverage + edge, n);
                    clouds *= smoothstep(0.0, 0.07, dir.y);
                    float lighting = saturate(0.5 + slope * _CloudContrast);
                    float density = saturate((n - coverage) / max(_CloudCoverage, 0.001));
                    float ao = lerp(0.7, 1.0, smoothstep(0.0, 0.6, density));
                    float3 cloudCol = lerp(_CloudShadowColor.rgb, _CloudColor.rgb, lighting) * ao;
                    float rim = saturate(slope * _CloudContrast) * (1.0 - density);
                    cloudCol += _SunColor.rgb * rim * _SilverLining;
                    cloudCol *= _CloudBrightness;
                    col = lerp(sky, cloudCol, clouds);
                }
                col *= _Exposure;

                return half4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
