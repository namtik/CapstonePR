// ============================================================================
//  Sasindo/Toon - Character  —  캐릭터·몬스터 전용
//  Shares SasindoToonCore.hlsl with the Nature/Artifact variants:
//  edit the core once, every category updates together.
//
//  Category focus:
//    * silhouette rim (on by default, subtle warm)   — 실루엣 강조
//    * face mode (_IsFace)                           — 얼굴 셀 밴딩 보정
//    * highlight pool / emission pulse                — 원소 발광 연출
//    * slight outline depth-push default (hard-surface monsters)
//    * shade band (real shadow side + cast shadows, Strength 0 = old look)
//    * outline is always pure black (0,0,0); width 0..4, default 4
//    * key light normalize: keeps preset colour, steadies brightness across time presets
//    * character light: lit from the camera's side with the preset sun colour (never back-lit)
// ============================================================================
Shader "Sasindo/Toon - Character"
{
    Properties
    {
        [Header(High Level)]
        [ToggleUI]_IsFace("Is Face? (face/eye/mouth)", Float) = 0

        [Header(Base)]
        [MainTexture]_BaseMap("Base Map", 2D) = "white" {}
        [HDR][MainColor]_BaseColor("Base Color", Color) = (1,1,1,1)
        [Toggle]_AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("    Cutoff", Range(0,1)) = 0.5

        [Header(Normal Map)]
        [Toggle(_NORMALMAP)]_UseNormalMap("Enable?", Float) = 0
        [Normal][NoScaleOffset]_BumpMap("    Normal Map", 2D) = "bump" {}
        _NormalScale("    Strength", Range(0,2)) = 1

        [Header(Cel Shading)]
        _CelMidPoint("Cel MidPoint", Range(-1,1)) = -0.5
        _CelSoftness("Cel Softness", Range(0.001,1)) = 0.05
        _IndirectMinColor("Indirect Min Color", Color) = (0.1,0.1,0.1,1)

        [Header(Shadow Mapping)]
        _ShadowStrength("Receive Shadow Strength", Range(0,1)) = 0.65
        _ShadowColor("    Shadow Color", Color) = (1,0.825,0.78,1)
        _ShadowDepthBias("    Depth Bias", Float) = 0

        [Header(Shade Band (shadow side))]
        _ShadeStrength("Strength (0 = old flat look)", Range(0,1)) = 1
        _ShadeMidPoint("    Light Edge", Range(-1,1)) = 0.1
        _ShadeSoftness("    Edge Softness", Range(0.001,0.5)) = 0.04
        _ShadeTint("    Shade Tint", Color) = (0.68,0.6,0.7,1)
        _ShadeCastShadow("    Include Cast Shadows", Range(0,1)) = 1

        [Header(Character Light (from StageLightingManager))]
        [Toggle]_UseCharacterLight("Use Character Light (off = real sun)", Float) = 1

        [Header(Key Light Normalize (readable in every time preset))]
        _KeyNormalize("Normalize Strength (0 = follow stage light)", Range(0,1)) = 0.6
        _KeyBrightness("    Target Brightness", Range(0.2,1.5)) = 0.9

        [Header(Additional Lights)]
        _ExtraLightKeepCel("Remove Cel Banding", Range(0,1)) = 0.9

        [Header(Silhouette Rim)]
        [Toggle]_UseRim("Enable?", Float) = 1
        _RimColor("    Color", Color) = (1,0.82,0.6,1)
        _RimPower("    Tightness", Range(0.5,16)) = 6
        _RimStrength("    Strength", Range(0,4)) = 0.5
        _RimSnap("    Edge Snap", Range(0,1)) = 0.15
        _RimLightMask("    Follow Main Light", Range(0,1)) = 0.4

        [Header(Highlight Pool)]
        [Toggle]_UseHighlight("Enable?", Float) = 0
        _HighlightColor("    Color", Color) = (1,0.97,0.88,1)
        _HighlightSize("    Size", Range(0,1)) = 0.12
        _HighlightBlur("    Edge Softness", Range(0.001,0.3)) = 0.02
        _HighlightStrength("    Strength", Range(0,3)) = 0.8

        [Header(Element Emission)]
        [Toggle]_UseEmission("Enable?", Float) = 0
        [HDR]_EmissionColor("    Color", Color) = (0,0,0,1)
        [NoScaleOffset]_EmissionMap("    Map", 2D) = "white" {}
        _EmissionMulBase("    Mul Base Color", Range(0,1)) = 0
        _EmissionPulse("    Pulse Speed (0 = steady)", Range(0,12)) = 0

        [Header(Outline)]
        _OutlineWidth("Width", Range(0,4)) = 4
        _OutlineZOffset("ZOffset (hide inner lines)", Range(0,1)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "UniversalMaterialType" = "ComplexLit"
            "Queue" = "Geometry"
        }

        // Character compiles these features (wind / translucency are NOT compiled).
        HLSLINCLUDE
        #define SASINDO_ENABLE_RIM
        #define SASINDO_ENABLE_HIGHLIGHT
        #define SASINDO_ENABLE_EMISSION
        #define SASINDO_ENABLE_FACE
        #define SASINDO_ENABLE_SHADE
        #define SASINDO_OUTLINE_BLACK
        #define SASINDO_ENABLE_KEY_NORMALIZE
        #define SASINDO_ENABLE_CHAR_LIGHT
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend One Zero
            ZWrite On
            Cull Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SasindoVert
            #pragma fragment SasindoLitFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _NORMALMAP
            #include "SasindoToonCore.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One Zero
            ZWrite On
            Cull Front
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SasindoVert
            #pragma fragment SasindoLitFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #define SASINDO_OUTLINE_PASS
            #include "SasindoToonCore.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SasindoVert
            #pragma fragment SasindoShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #define SASINDO_SHADOWCASTER
            #include "SasindoToonCore.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ZTest LEqual
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SasindoVert
            #pragma fragment SasindoDepthFrag
            #pragma multi_compile_instancing
            // NOTE: no SASINDO_OUTLINE_PASS here — the depth prepass must match
            // the base mesh exactly, or depth priming culls the whole object.
            #include "SasindoToonCore.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SasindoVert
            #pragma fragment SasindoDepthNormalsFrag
            #pragma multi_compile_instancing
            #include "SasindoToonCore.hlsl"
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
