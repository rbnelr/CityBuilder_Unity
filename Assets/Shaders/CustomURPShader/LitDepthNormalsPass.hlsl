// Derived from com.unity.render-pipelines.universal@15fef0f41df6\Shaders\LitDepthNormalsPass.hlsl

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#if defined(LOD_FADE_CROSSFADE)
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

#if defined(_DETAIL_MULX2) || defined(_DETAIL_SCALED)
#define _DETAIL
#endif

#if defined(_PARALLAXMAP)
#define REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR
#endif

#if (defined(_NORMALMAP) || (defined(_PARALLAXMAP) && !defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR))) || defined(_DETAIL)
#define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
#endif

#if defined(_ALPHATEST_ON) || defined(_PARALLAXMAP) || defined(_NORMALMAP) || defined(_DETAIL)
#define REQUIRES_UV_INTERPOLATOR
#endif

//struct Attributes
//{
//	float4 positionOS   : POSITION;
//	float4 tangentOS    : TANGENT;
//	float2 texcoord     : TEXCOORD0;
//	float3 normal       : NORMAL;
//	UNITY_VERTEX_INPUT_INSTANCE_ID
//};

struct Varyings
{
//	#if defined(REQUIRES_UV_INTERPOLATOR)
	float2 uv          : TEXCOORD0;
//	#endif
	
	float3 positionWS : TEXCOORD1;
	half3 normalWS  : TEXCOORD2;
//	#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
	half4 tangentWS    : TEXCOORD4;    // xyz: tangent, w: sign
//	#endif

	half3 viewDirWS    : TEXCOORD5;

	#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
	half3 viewDirTS    : TEXCOORD8;
	#endif
	
	CUSTOM_VARYINGS
	
	float4 positionCS  : SV_POSITION;

	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO
};

#include ACTUAL_SHADER_FILE

Varyings DepthNormalsVertex(Attributes input)
{
	Varyings output = (Varyings)0;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
	
	// Shader-specific custom vertex function
	vertex(input, output);

//	#if defined(REQUIRES_UV_INTERPOLATOR)
//		output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
//	#endif
//	output.positionCS = TransformObjectToHClip(input.position.xyz);
//
//	VertexPositionInputs vertexInput = GetVertexPositionInputs(input.position.xyz);
//	VertexNormalInputs normalInput = GetVertexNormalInputs(input.normal, input.tangent);
//
//	output.normalWS = half3(normalInput.normalWS);
//	#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR) || defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
//		float sign = input.tangent.w * float(GetOddNegativeScale());
//		half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
//	#endif
//
//	#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
//		output.tangentWS = tangentWS;
//	#endif

	#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
		half3 viewDirWS = GetWorldSpaceNormalizeViewDir(output.positionWS);
		half3 viewDirTS = GetViewDirectionTangentSpace(output.tangentWS, output.normalWS, viewDirWS);
		output.viewDirTS = viewDirTS;
	#endif

	return output;
}

void DepthNormalsFragment(
	Varyings input
	, out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
	, out float4 outRenderingLayers : SV_Target1
#endif
)
{
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

	#if defined(_ALPHATEST_ON)
		Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
	#endif

	#if defined(LOD_FADE_CROSSFADE)
		LODFadeCrossFade(input.positionCS);
	#endif

	#if defined(_GBUFFER_NORMALS_OCT)
		float3 normalWS = normalize(input.normalWS);
		float2 octNormalWS = PackNormalOctQuadEncode(normalWS);           // values between [-1, +1], must use fp32 on some platforms
		float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);   // values between [ 0,  1]
		half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);      // values between [ 0,  1]
		outNormalWS = half4(packedNormalWS, 0.0);
	#else
		#if defined(_PARALLAXMAP)
			#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
				half3 viewDirTS = input.viewDirTS;
			#else
				half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, input.viewDirWS);
			#endif
			ApplyPerPixelDisplacement(viewDirTS, input.uv);
		#endif

		#if defined(_NORMALMAP) || defined(_DETAIL)
			float sgn = input.tangentWS.w;      // should be either +1 or -1
			float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
			float3 normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_Normal, sampler_Normal), 1.0);

			#if defined(_DETAIL)
				half detailMask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, input.uv).a;
				float2 detailUv = input.uv * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
				normalTS = ApplyDetailNormal(detailUv, normalTS, detailMask);
			#endif

			float3 normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz));
		#else
			float3 normalWS = input.normalWS;
		#endif

		outNormalWS = half4(NormalizeNormalPerPixel(normalWS), 0.0);
	#endif

	#ifdef _WRITE_RENDERING_LAYERS
		uint renderingLayers = GetMeshRenderingLayer();
		outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
	#endif
}
