// ============================================================================
//  SasindoToonCore.hlsl — all shared code for the Sasindo/Toon shader.
//  Original implementation for the 사신도 project.
//
//  The lighting model is tuned to reproduce our approved look:
//  bright & flat illustration shading, warm tinted shadows, and an outline
//  that is lit like the surface (then darkened) so lines sit inside the art.
//
//  Per-pass configuration (define BEFORE including this file):
//    SASINDO_OUTLINE_PASS   - expand hull + tint color  (Outline pass)
//    SASINDO_SHADOWCASTER   - shadow bias fix           (ShadowCaster pass)
// ============================================================================
#pragma once

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// ----------------------------------------------------------------------------
// structs
// ----------------------------------------------------------------------------
struct Attributes
{
    float3 positionOS : POSITION;
    half3  normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;   // needed for normal mapping (unused otherwise)
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS      : SV_POSITION;
    float2 uv              : TEXCOORD0;
    float4 posWSAndFog     : TEXCOORD1;   // xyz = positionWS, w = fog factor
    half3  normalWS        : TEXCOORD2;
#ifdef _NORMALMAP
    half4  tangentWS       : TEXCOORD3;   // xyz = world tangent, w = bitangent sign
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// ----------------------------------------------------------------------------
// material CBUFFER — every uniform of every pass lives here (SRP batching)
// ----------------------------------------------------------------------------
sampler2D _BaseMap;
sampler2D _EmissionMap;
sampler2D _BumpMap;

CBUFFER_START(UnityPerMaterial)
    // high level
    float  _IsFace;

    // base
    float4 _BaseMap_ST;
    half4  _BaseColor;
    float  _AlphaClip;
    half   _Cutoff;

    // normal map (only sampled when _NORMALMAP keyword is on)
    float  _UseNormalMap;
    half   _NormalScale;

    // cel shading
    half   _CelMidPoint;
    half   _CelSoftness;
    half3  _IndirectMinColor;

    // shadow mapping
    half   _ShadowStrength;
    half3  _ShadowColor;
    float  _ShadowDepthBias;

    // additional lights
    half   _ExtraLightKeepCel;

    // shade band (Character only: SASINDO_ENABLE_SHADE)
    half   _ShadeStrength;
    half   _ShadeMidPoint;
    half   _ShadeSoftness;
    half3  _ShadeTint;
    half   _ShadeCastShadow;

    // key light normalize (Character only: SASINDO_ENABLE_KEY_NORMALIZE)
    half   _KeyNormalize;
    half   _KeyBrightness;

    // character key light (Character only: SASINDO_ENABLE_CHAR_LIGHT)
    float  _UseCharacterLight;

    // emission
    float  _UseEmission;
    half3  _EmissionColor;
    half   _EmissionMulBase;
    half   _EmissionPulse;

    // sasindo extras (default off)
    float  _UseHighlight;
    half4  _HighlightColor;
    half   _HighlightSize;
    half   _HighlightBlur;
    half   _HighlightStrength;

    float  _UseRim;
    half4  _RimColor;
    half   _RimPower;
    half   _RimStrength;
    half   _RimSnap;
    half   _RimLightMask;

    float  _UseWind;
    half   _WindStrength;
    half   _WindSpeed;
    half   _WindWave;
    half   _WindRootLock;

    // nature extras (leaves/grass)
    float  _FlipBackNormals;       // two-sided cards: light the side facing the camera
    float  _UseTranslucency;       // back-lit leaf glow
    half3  _TranslucColor;
    half   _TranslucPower;
    half   _TranslucStrength;

    // outline
    float  _OutlineWidth;
    half3  _OutlineColor;
    float  _OutlineZOffset;
CBUFFER_END

// ShadowCaster-only globals (set by URP, not per-material)
float3 _LightDirection;
float3 _LightPosition;

// character key light globals, pushed by StageLightingManager
// (xyz = direction toward the light, w = 1 when active)
float4 _SasindoCharLightDir;
float4 _SasindoCharLightColor;

// ----------------------------------------------------------------------------
// helpers
// ----------------------------------------------------------------------------

// wind sway — feet locked by object-space height; identical in every pass.
// Compiled ONLY when a shader defines SASINDO_ENABLE_WIND (e.g. Nature),
// so leftover _UseWind on a non-foliage material can never make it sway.
float3 SasindoWind(float3 positionWS, float3 positionOS)
{
#if defined(SASINDO_ENABLE_WIND)
    if (_UseWind > 0.5)
    {
        float sway = saturate(positionOS.y * _WindRootLock);
        float t = _Time.y * _WindSpeed;
        float2 gust;
        gust.x = sin(t + (positionWS.x + positionWS.z) * _WindWave);
        gust.y = cos(t * 0.83 + positionWS.z * _WindWave * 1.37);
        positionWS.xz += gust * (_WindStrength * sway);
    }
#endif
    return positionWS;
}

// current camera vertical fov in degrees, derived from the projection matrix
float SasindoCameraFovDeg()
{
    float m11 = unity_CameraProjection._m11;    // = 1 / tan(fov/2)
    return degrees(atan(1.0 / m11) * 2.0);
}

// world-space outline expansion that stays visually constant on screen:
// scale by view depth (perspective shrink) and by fov (zoom shrink)
float3 SasindoOutlineExpand(float3 positionWS, half3 normalWS, float viewDepth)
{
    float screenConstant = saturate(abs(viewDepth)) * SasindoCameraFovDeg() * 0.00005;
    return positionWS + normalWS * (_OutlineWidth * screenConstant);
}

// overwrite only the depth of a clip position, as if the vertex were pushed
// 'amountVS' toward the camera in view space (perspective projection algebra).
// Hides inner outline lines on hard-surface models & faces.
float4 SasindoDepthTowardCamera(float4 positionCS, float amountVS)
{
    float2 projRowZ = UNITY_MATRIX_P[2].zw;
    float pushedVS_Z = -positionCS.w - amountVS;
    float pushedCS_Z = pushedVS_Z * projRowZ.x + projRowZ.y;
    positionCS.z = pushedCS_Z * positionCS.w / (-pushedVS_Z);
    return positionCS;
}

void SasindoAlphaClip(half alpha)
{
    if (_AlphaClip > 0.5) clip(alpha - _Cutoff);
}

// ----------------------------------------------------------------------------
// shared vertex shader (all passes)
// ----------------------------------------------------------------------------
Varyings SasindoVert(Attributes IN)
{
    Varyings OUT;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

    VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS);
    half3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

    float3 positionWS = SasindoWind(vp.positionWS, IN.positionOS);

#if defined(SASINDO_OUTLINE_PASS)
    positionWS = SasindoOutlineExpand(positionWS, normalWS, vp.positionVS.z);
#endif

    OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
    OUT.posWSAndFog = float4(positionWS, ComputeFogFactor(vp.positionCS.z));
    OUT.normalWS = normalWS;
#ifdef _NORMALMAP
    half3 tangentWS = TransformObjectToWorldDir(IN.tangentOS.xyz);
    OUT.tangentWS = half4(tangentWS, IN.tangentOS.w * unity_WorldTransformParams.w);
#endif
    OUT.positionCS = TransformWorldToHClip(positionWS);

#if defined(SASINDO_OUTLINE_PASS)
    if (_OutlineWidth <= 1e-4)
    {
        // width 0 => no outline wanted. Cull the whole hull off-screen so it
        // never z-fights / darkens the surface (important for two-sided foliage).
        OUT.positionCS = float4(2, 2, 2, 1);
    }
    else
    {
        // faces get an extra fixed push so inner face lines never show
        OUT.positionCS = SasindoDepthTowardCamera(OUT.positionCS, _OutlineZOffset + 0.03 * _IsFace);
    }
#endif

#if defined(SASINDO_SHADOWCASTER)
    // apply URP shadow bias so the caster doesn't self-shadow acne
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
        float3 lightDir = normalize(_LightPosition - positionWS);
    #else
        float3 lightDir = _LightDirection;
    #endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
    #if UNITY_REVERSED_Z
        positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #else
        positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #endif
    OUT.positionCS = positionCS;
#endif

    return OUT;
}

// ----------------------------------------------------------------------------
// lighting — tuned to the approved Sasindo look
// ----------------------------------------------------------------------------

// flat indirect: average SH only (no directionality = 2D illustration feel)
half3 SasindoIndirect(half3 albedo)
{
    half3 flatSH = max(_IndirectMinColor, SampleSH(0));
    return flatSH; // albedo multiplied at composite time
}

// one direct light (main or additional) -> light energy (albedo applied later)
half3 SasindoOneLight(half3 N, half3 V, Light light, bool isExtra)
{
    half3 L = light.direction;
    half NoL = dot(N, L);

    half distanceAtten = min(4.0h, light.distanceAttenuation); // clamp close-range blowout

    // single-break cel: everything above the midpoint is fully, flatly lit
    half litArea = smoothstep(_CelMidPoint - _CelSoftness, _CelMidPoint + _CelSoftness, NoL);

    // faces: NoL banding looks bad, remap to a gentle 0.5..1
#if defined(SASINDO_ENABLE_FACE)
    litArea = _IsFace ? lerp(0.5h, 1.0h, litArea) : litArea;
#endif

    // cast shadows push the area into shade
    litArea *= lerp(1.0h, light.shadowAttenuation, _ShadowStrength);

    // additional lights (lanterns...) can skip the banding to act as soft fills
    litArea = isExtra ? lerp(litArea, 1.0h, _ExtraLightKeepCel) : litArea;

    // shade is a warm tint, never black
    half3 litOrShade = lerp(_ShadowColor, 1.0h, litArea);

    half3 energy = saturate(light.color) * litOrShade * distanceAtten;
    energy *= isExtra ? 0.25h : 1.0h; // additional lights are additive, keep them modest

    // ---------- optional Sasindo extras (main light only) ----------
    // Each is compiled ONLY when the category shader opts in, so leftover
    // material values (e.g. a Character material re-assigned to Nature) can
    // never trigger a feature the category isn't supposed to have.
    if (!isExtra)
    {
#if defined(SASINDO_ENABLE_HIGHLIGHT)
        if (_UseHighlight > 0.5)
        {
            half3 H = normalize(L + V);
            half pool = smoothstep(1.0h - _HighlightSize - _HighlightBlur,
                                   1.0h - _HighlightSize + _HighlightBlur,
                                   saturate(dot(N, H)));
            energy += _HighlightColor.rgb * (pool * _HighlightStrength * litArea);
        }
#endif
#if defined(SASINDO_ENABLE_RIM)
        if (_UseRim > 0.5)
        {
            half fres = pow(saturate(1.0h - saturate(dot(N, V))), _RimPower);
            fres = smoothstep(_RimSnap, _RimSnap + 0.05h, fres);
            half mask = lerp(1.0h, saturate(NoL), _RimLightMask);
            energy += _RimColor.rgb * (fres * _RimStrength * mask);
        }
#endif
    }
    return energy;
}

// shade band: a real shadow side on top of the flat bright look.
// The base cel (midpoint -0.5, near-white warm shadow) lights almost every face,
// so characters read flat. This adds a second, deeper break on N·L plus cast
// shadows, multiplied over the final light so the flat indirect floor can't
// wash it out. Compiled ONLY in shaders that define SASINDO_ENABLE_SHADE.
half3 SasindoShadeBand(half3 N, Light sun)
{
#if defined(SASINDO_ENABLE_SHADE)
    half lit = smoothstep(_ShadeMidPoint - _ShadeSoftness, _ShadeMidPoint + _ShadeSoftness, dot(N, sun.direction));
    lit *= lerp(1.0h, smoothstep(0.5h - _ShadeSoftness, 0.5h + _ShadeSoftness, sun.shadowAttenuation), _ShadeCastShadow);
    half shade = (1.0h - lit) * _ShadeStrength;
    #if defined(SASINDO_ENABLE_FACE)
    shade *= _IsFace ? 0.5h : 1.0h;   // faces: keep the shade gentle
    #endif
    return lerp(1.0h, _ShadeTint, shade);
#else
    return 1.0h;
#endif
}

// key light normalize: dim time-of-day presets (dawn, sunset, night) darken the
// stage AND the characters together, so the subject sinks into the background.
// Keep the preset's light COLOUR but pull its brightness toward _KeyBrightness
// (≈ the afternoon sun, so the afternoon look stays unchanged).
// Compiled ONLY in shaders that define SASINDO_ENABLE_KEY_NORMALIZE.
half SasindoKeyScale(Light sun, half3 indirect)
{
#if defined(SASINDO_ENABLE_KEY_NORMALIZE)
    half3 key = max(indirect, saturate(sun.color));
    half lum = dot(key, half3(0.2126h, 0.7152h, 0.0722h));
    half scale = clamp(_KeyBrightness / max(lum, 0.05h), 0.5h, 3.0h);
    return lerp(1.0h, scale, _KeyNormalize);
#else
    return 1.0h;
#endif
}

half3 SasindoEmission(half3 albedo, float2 uv)
{
    half3 e = 0;
#if defined(SASINDO_ENABLE_EMISSION)
    if (_UseEmission > 0.5)
    {
        e = tex2D(_EmissionMap, uv).rgb * _EmissionColor;
        e = lerp(e, e * albedo, _EmissionMulBase);
        if (_EmissionPulse > 0.0h)
            e *= 0.6h + 0.4h * sin(_Time.y * _EmissionPulse);
    }
#endif
    return e;
}

// ----------------------------------------------------------------------------
// fragment entries
// ----------------------------------------------------------------------------
half4 SasindoLitFrag(Varyings IN) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

    half4 baseTex = tex2D(_BaseMap, IN.uv) * _BaseColor;
    SasindoAlphaClip(baseTex.a);
    half3 albedo = baseTex.rgb;

    float3 positionWS = IN.posWSAndFog.xyz;
    half3 N = normalize(IN.normalWS);
    half3 V = SafeNormalize(GetCameraPositionWS() - positionWS);

    // perturb the surface normal by the tangent-space normal map (opt-in)
#ifdef _NORMALMAP
    half3 normalTS = UnpackNormalScale(tex2D(_BumpMap, IN.uv), _NormalScale);
    half3 T = normalize(IN.tangentWS.xyz);
    half3 B = normalize(cross(N, T) * IN.tangentWS.w);
    N = normalize(mul(normalTS, half3x3(T, B, N)));
#endif

    // two-sided foliage cards: always light the side facing the camera
#if defined(SASINDO_ENABLE_TRANSLUCENCY)
    if (_FlipBackNormals > 0.5)
        N *= (dot(N, V) < 0.0h) ? -1.0h : 1.0h;
#endif

    // main light + shadow (test position pushed toward the light to avoid
    // ugly self-shadowing on shadow-sensitive surfaces like faces)
    Light sun = GetMainLight();
    float3 shadowTestPos = positionWS + sun.direction * (_ShadowDepthBias + _IsFace);
#if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    sun.shadowAttenuation = MainLightRealtimeShadow(TransformWorldToShadowCoord(shadowTestPos));
#endif
    // character key light: preset sun colour, but always from the camera's side
    // (StageLightingManager). No self-shadow from it — the real sun's shadow map
    // doesn't match its direction. Ground shadows still come from the real sun.
#if defined(SASINDO_ENABLE_CHAR_LIGHT)
    if (_UseCharacterLight > 0.5 && _SasindoCharLightDir.w > 0.5)
    {
        sun.direction = normalize(_SasindoCharLightDir.xyz);
        sun.color = _SasindoCharLightColor.rgb;
        sun.shadowAttenuation = 1.0h;
    }
#endif
    half3 direct = SasindoOneLight(N, V, sun, false);

    // back-lit leaf glow: sun shining through foliage toward the camera
#if defined(SASINDO_ENABLE_TRANSLUCENCY)
    if (_UseTranslucency > 0.5)
    {
        half backLit = pow(saturate(dot(V, -sun.direction)), _TranslucPower);
        half sunShadow = lerp(1.0h, sun.shadowAttenuation, _ShadowStrength);
        direct += _TranslucColor.rgb * saturate(sun.color) * (backLit * _TranslucStrength * sunShadow);
    }
#endif

    // additional lights (Forward & Forward+ safe loop)
    half3 extra = 0;
#if defined(_ADDITIONAL_LIGHTS)
    InputData inputData = (InputData)0; // Forward+ macro requires this exact name
    inputData.positionWS = positionWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
    uint lightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightCount)
        Light lamp = GetAdditionalLight(lightIndex, positionWS);
        lamp.shadowAttenuation = AdditionalLightRealtimeShadow(lightIndex, shadowTestPos);
        extra += SasindoOneLight(N, V, lamp, true);
    LIGHT_LOOP_END
#endif

    half3 indirect = SasindoIndirect(albedo);
    half3 emission = SasindoEmission(albedo, IN.uv);

    // composite: brightest of (flat indirect | direct sum), applied to albedo.
    // keeps lit areas flat & bright, prevents double-brightening — the 2D look.
    half3 color = albedo * max(indirect, direct + extra) * SasindoShadeBand(N, sun) * SasindoKeyScale(sun, indirect) + emission;

#if defined(SASINDO_OUTLINE_PASS)
    #if defined(SASINDO_OUTLINE_BLACK)
    return half4(0, 0, 0, baseTex.a); // Character: always pure black ink, no light / fog
    #endif
    color *= _OutlineColor; // lit like the surface, then darkened => painterly line
#endif

    color = MixFog(color, IN.posWSAndFog.w);
    return half4(color, baseTex.a);
}

// depth-ish passes share this tiny fragment work
void SasindoClipOnly(Varyings IN)
{
    half a = (tex2D(_BaseMap, IN.uv) * _BaseColor).a;
    SasindoAlphaClip(a);
}

half4 SasindoShadowFrag(Varyings IN) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(IN);
    SasindoClipOnly(IN);
    return 0;
}

half SasindoDepthFrag(Varyings IN) : SV_TARGET
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
    SasindoClipOnly(IN);
    return IN.positionCS.z;
}

half4 SasindoDepthNormalsFrag(Varyings IN) : SV_TARGET
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
    SasindoClipOnly(IN);
    return half4(NormalizeNormalPerPixel(IN.normalWS), 0.0);
}
