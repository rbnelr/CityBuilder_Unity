// Derived from com.unity.render-pipelines.universal@15fef0f41df6\Shaders\LitForwardPass.hlsl

#ifndef CUSTOM_VARYINGS
#define CUSTOM_VARYINGS
#endif

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ParallaxMapping.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

////// com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
//
//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
//#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
//#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

TEXTURE2D(_Albedo);
SAMPLER(sampler_Albedo);
UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_Albedo);
TEXTURE2D(_Normal);
SAMPLER(sampler_Normal);

///////////////////////////////////////////////////////////////////////////////
//                      Material Property Helpers                            //
///////////////////////////////////////////////////////////////////////////////

half3 SampleNormal(float2 uv, TEXTURE2D_PARAM(bumpMap, sampler_bumpMap), half scale = half(1.0))
{
#ifdef _NORMALMAP
	half4 n = SAMPLE_TEXTURE2D(bumpMap, sampler_bumpMap, uv);
	#if BUMP_SCALE_NOT_SUPPORTED
		return UnpackNormal(n);
	#else
		return UnpackNormalScale(n, scale);
	#endif
#else
	return half3(0.0h, 0.0h, 1.0h);
#endif
}

// }
//////

#if defined(_DETAIL_MULX2) || defined(_DETAIL_SCALED)
#define _DETAIL
#endif

// NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
CBUFFER_START(UnityPerMaterial)
half4 _AlbedoTint;
half _Smoothness;
half _Metallic;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

#define _Surface 0.0 // opaque

// NOTE: Do not ifdef the properties for dots instancing, but ifdef the actual usage.
// Otherwise you might break CPU-side as property constant-buffer offsets change per variant.
// NOTE: Dots instancing is orthogonal to the constant buffer above.
#ifdef UNITY_DOTS_INSTANCING_ENABLED

UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
	UNITY_DOTS_INSTANCED_PROP(float4, _AlbedoTint)
	UNITY_DOTS_INSTANCED_PROP(float , _Smoothness)
	UNITY_DOTS_INSTANCED_PROP(float , _Metallic)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

// Here, we want to avoid overriding a property like e.g. _BaseColor with something like this:
// #define _BaseColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor0)
//
// It would be simpler, but it can cause the compiler to regenerate the property loading code for each use of _BaseColor.
//
// To avoid this, the property loads are cached in some static values at the beginning of the shader.
// The properties such as _BaseColor are then overridden so that it expand directly to the static value like this:
// #define _BaseColor unity_DOTS_Sampled_BaseColor
//
// This simple fix happened to improve GPU performances by ~10% on Meta Quest 2 with URP on some scenes.
static float4 unity_DOTS_Sampled_AlbedoTint;
static float  unity_DOTS_Sampled_Smoothness;
static float  unity_DOTS_Sampled_Metallic;

void SetupDOTSLitMaterialPropertyCaches()
{
	unity_DOTS_Sampled_AlbedoTint           = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _AlbedoTint);
	unity_DOTS_Sampled_Smoothness           = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Smoothness);
	unity_DOTS_Sampled_Metallic             = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Metallic);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSLitMaterialPropertyCaches()

#define _BaseColor              unity_DOTS_Sampled_AlbedoTint
#define _Smoothness             unity_DOTS_Sampled_Smoothness
#define _Metallic               unity_DOTS_Sampled_Metallic

#endif

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
	outSurfaceData.albedo = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, uv).rgb;
	outSurfaceData.alpha = 1.0;
	outSurfaceData.specular = half3(0,0,0);
	outSurfaceData.metallic = _Metallic;
	outSurfaceData.smoothness = _Smoothness;
	outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_Normal, sampler_Normal), 1.0);
	outSurfaceData.emission = half3(0,0,0);
	outSurfaceData.occlusion = half(1);
	outSurfaceData.clearCoatMask       = half(0);
	outSurfaceData.clearCoatSmoothness = half(0);
	
	outSurfaceData.albedo = lerp(outSurfaceData.albedo, _AlbedoTint.rgb, _AlbedoTint.a);
	//outSurfaceData.normalTS = lerp(outSurfaceData.normalTS, half3(0,0,1), _AlbedoTint.a);
}

