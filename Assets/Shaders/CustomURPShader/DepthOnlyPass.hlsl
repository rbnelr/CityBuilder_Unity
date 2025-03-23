// Derived from com.unity.render-pipelines.universal@15fef0f41df6\Shaders\DepthOnlyPass.hlsl

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#if defined(LOD_FADE_CROSSFADE)
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

//struct Attributes
//{
//	float4 position     : POSITION;
//	float2 texcoord     : TEXCOORD0;
//	UNITY_VERTEX_INPUT_INSTANCE_ID
//};

struct Varyings
{
//	#if defined(_ALPHATEST_ON)
		float2 uv       : TEXCOORD0;
//	#endif
	
	float3 positionWS : TEXCOORD1;
	half3 normalWS  : TEXCOORD2;
	half4 tangentWS  : TEXCOORD3;
	
	CUSTOM_VARYINGS
	
	float4 positionCS   : SV_POSITION;
	
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO
};

#include ACTUAL_SHADER_FILE

Varyings DepthOnlyVertex(Attributes input)
{
	Varyings output = (Varyings)0;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
	
	// Shader-specific custom vertex function
	vertex(input, output);

//	#if defined(_ALPHATEST_ON)
//		output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
//	#endif
//	output.positionCS = TransformObjectToHClip(input.position.xyz);
	return output;
}

half DepthOnlyFragment(Varyings input) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

	#if defined(_ALPHATEST_ON)
		Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
	#endif

	#if defined(LOD_FADE_CROSSFADE)
		LODFadeCrossFade(input.positionCS);
	#endif

	return input.positionCS.z;
}
