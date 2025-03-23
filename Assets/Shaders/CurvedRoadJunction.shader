// Derived from com.unity.render-pipelines.universal@15fef0f41df6\Shaders\Lit.shader
Shader "Custom/CurvedRoadJunction" {
	Properties {
		// Specular vs Metallic workflow
		//_WorkflowMode("WorkflowMode", Float) = 1.0

		[NoScaleOffset] [MainColor] _AlbedoTint("AlbedoTint", Color) = (0,0,0,0)
		
		[NoScaleOffset] [MainTexture] _Albedo("Albedo", 2D) = "white" {}
		[NoScaleOffset] [Normal] _Normal("Normal", 2D) = "bump" {}

		[Range(0,1)] _Smoothness("Smoothness", Float) = 0.5
		[Range(0,1)] _Metallic("Metallic", Float) = 0.0

		[ToggleUI]_WorldspaceTextures("WorldspaceTextures", Float) = 1
		_TextureScale("TextureScale", Vector) = (1, 1, 1, 1)
		_TextureOffset("TextureOffset", Vector) = (0, 0, 0, 0)
	}

	SubShader {
		Tags {
			"RenderType" = "Opaque"
			"RenderPipeline" = "UniversalPipeline"
			"UniversalMaterialType" = "Lit"
			"IgnoreProjector" = "True"
		}
		LOD 300

		Pass {
			// Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
			// no LightMode tag are also rendered by Universal Render Pipeline
			Name "ForwardLit"
			Tags {
				"LightMode" = "UniversalForward"
			}

			HLSLPROGRAM
			#pragma target 2.0

			// -------------------------------------
			// Shader Stages
			#pragma vertex LitPassVertex
			#pragma fragment LitPassFragment

			// -------------------------------------
			// Material Keywords
			#pragma shader_feature_local _NORMALMAP
			#pragma shader_feature_local _PARALLAXMAP
			#pragma shader_feature_local _RECEIVE_SHADOWS_OFF
			#pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
			#pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
			#pragma shader_feature_local_fragment _ALPHATEST_ON
			#pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON
			#pragma shader_feature_local_fragment _EMISSION
			#pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
			#pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
			#pragma shader_feature_local_fragment _OCCLUSIONMAP
			#pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
			#pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
			#pragma shader_feature_local_fragment _SPECULAR_SETUP

			// -------------------------------------
			// Universal Pipeline keywords
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
			#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
			#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
			#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile_fragment _ _LIGHT_COOKIES
			#pragma multi_compile _ _LIGHT_LAYERS
			#pragma multi_compile _ _FORWARD_PLUS
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"


			// -------------------------------------
			// Unity defined keywords
			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ SHADOWS_SHADOWMASK
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON
			#pragma multi_compile _ USE_LEGACY_LIGHTMAPS
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile_fog
			#pragma multi_compile_fragment _ DEBUG_DISPLAY
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

			//--------------------------------------
			// GPU Instancing
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			
			#define ACTUAL_SHADER_FILE "CurvedRoadJunction.hlsl"
			#include "CustomURPShader/LitCommon.hlsl"
			#include "CustomURPShader/LitForwardPass.hlsl"
			ENDHLSL
		}

		Pass {
			Name "ShadowCaster"
			Tags {
				"LightMode" = "ShadowCaster"
			}

			ZWrite On
			ZTest LEqual
			ColorMask 0

			HLSLPROGRAM
			#pragma target 2.0

			// -------------------------------------
			// Shader Stages
			#pragma vertex ShadowPassVertex
			#pragma fragment ShadowPassFragment

			// -------------------------------------
			// Material Keywords
			#pragma shader_feature_local _ALPHATEST_ON
			#pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

			//--------------------------------------
			// GPU Instancing
			#pragma multi_compile_instancing
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

			// -------------------------------------
			// Universal Pipeline keywords

			// -------------------------------------
			// Unity defined keywords
			#pragma multi_compile _ LOD_FADE_CROSSFADE

			// This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
			#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

			// -------------------------------------
			// Includes
			#define ACTUAL_SHADER_FILE "CurvedRoadJunction.hlsl"
			#include "CustomURPShader/LitCommon.hlsl"
			#include "CustomURPShader/ShadowCasterPass.hlsl"
			ENDHLSL
		}

		Pass {
			// Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
			// no LightMode tag are also rendered by Universal Render Pipeline
			Name "GBuffer"
			Tags {
				"LightMode" = "UniversalGBuffer"
			}

			ZTest LEqual

			HLSLPROGRAM
			#pragma target 4.5

			// Deferred Rendering Path does not support the OpenGL-based graphics API:
			// Desktop OpenGL, OpenGL ES 3.0, WebGL 2.0.
			#pragma exclude_renderers gles3 glcore

			// -------------------------------------
			// Shader Stages
			#pragma vertex LitGBufferPassVertex
			#pragma fragment LitGBufferPassFragment

			// -------------------------------------
			// Material Keywords
			#pragma shader_feature_local _NORMALMAP
			#pragma shader_feature_local_fragment _ALPHATEST_ON
			//#pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON
			#pragma shader_feature_local_fragment _EMISSION
			#pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
			#pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
			#pragma shader_feature_local_fragment _OCCLUSIONMAP
			#pragma shader_feature_local _PARALLAXMAP
			#pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED

			#pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
			#pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
			#pragma shader_feature_local_fragment _SPECULAR_SETUP
			#pragma shader_feature_local _RECEIVE_SHADOWS_OFF

			// -------------------------------------
			// Universal Pipeline keywords
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			//#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			//#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
			#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
			#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

			// -------------------------------------
			// Unity defined keywords
			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ SHADOWS_SHADOWMASK
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON
			#pragma multi_compile _ USE_LEGACY_LIGHTMAPS
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

			//--------------------------------------
			// GPU Instancing
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

			// -------------------------------------
			// Includes
			#define ACTUAL_SHADER_FILE "CurvedRoadJunction.hlsl"
			#include "CustomURPShader/LitCommon.hlsl"
			#include "CustomURPShader/LitGBufferPass.hlsl"
			ENDHLSL
		}

		Pass {
			Name "DepthOnly"
			Tags {
				"LightMode" = "DepthOnly"
			}

			ZWrite On
			ColorMask R

			HLSLPROGRAM
			#pragma target 2.0

			// -------------------------------------
			// Shader Stages
			#pragma vertex DepthOnlyVertex
			#pragma fragment DepthOnlyFragment

			// -------------------------------------
			// Material Keywords
			#pragma shader_feature_local _ALPHATEST_ON
			#pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

			// -------------------------------------
			// Unity defined keywords
			#pragma multi_compile _ LOD_FADE_CROSSFADE

			//--------------------------------------
			// GPU Instancing
			#pragma multi_compile_instancing
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

			// -------------------------------------
			// Includes
			#define ACTUAL_SHADER_FILE "CurvedRoadJunction.hlsl"
			#include "CustomURPShader/LitCommon.hlsl"
			#include "CustomURPShader/DepthOnlyPass.hlsl"
			ENDHLSL
		}

		// This pass is used when drawing to a _CameraNormalsTexture texture
		Pass {
			Name "DepthNormals"
			Tags {
				"LightMode" = "DepthNormals"
			}

			// -------------------------------------
			// Render State Commands
			ZWrite On

			HLSLPROGRAM
			#pragma target 2.0

			// -------------------------------------
			// Shader Stages
			#pragma vertex DepthNormalsVertex
			#pragma fragment DepthNormalsFragment

			// -------------------------------------
			// Material Keywords
			#pragma shader_feature_local _NORMALMAP
			#pragma shader_feature_local _PARALLAXMAP
			#pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
			#pragma shader_feature_local _ALPHATEST_ON
			#pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

			// -------------------------------------
			// Unity defined keywords
			#pragma multi_compile _ LOD_FADE_CROSSFADE

			// -------------------------------------
			// Universal Pipeline keywords
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

			//--------------------------------------
			// GPU Instancing
			#pragma multi_compile_instancing
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

			// -------------------------------------
			// Includes
			#define ACTUAL_SHADER_FILE "CurvedRoadJunction.hlsl"
			#include "CustomURPShader/LitCommon.hlsl"
			#include "CustomURPShader/LitDepthNormalsPass.hlsl"
			ENDHLSL
		}

	/*
		// This pass it not used during regular rendering, only for lightmap baking.
		Pass {
			Name "Meta"
			Tags {
				"LightMode" = "Meta"
			}

			// -------------------------------------
			// Render State Commands
			Cull Off

			HLSLPROGRAM
			#pragma target 2.0

			// -------------------------------------
			// Shader Stages
			#pragma vertex UniversalVertexMeta
			#pragma fragment UniversalFragmentMetaLit

			// -------------------------------------
			// Material Keywords
			#pragma shader_feature_local_fragment _SPECULAR_SETUP
			#pragma shader_feature_local_fragment _EMISSION
			#pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
			#pragma shader_feature_local_fragment _ALPHATEST_ON
			#pragma shader_feature_local_fragment _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
			#pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
			#pragma shader_feature_local_fragment _SPECGLOSSMAP
			#pragma shader_feature EDITOR_VISUALIZATION

			// -------------------------------------
			// Includes
			#include "CustomURPShader/LitCommon.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"

			ENDHLSL
		}

		Pass {
			Name "Universal2D"
			Tags {
				"LightMode" = "Universal2D"
			}

			// -------------------------------------
			// Render State Commands
			Blend[_SrcBlend][_DstBlend]
			ZWrite[_ZWrite]
			Cull[_Cull]

			HLSLPROGRAM
			#pragma target 2.0

			// -------------------------------------
			// Shader Stages
			#pragma vertex vert
			#pragma fragment frag

			// -------------------------------------
			// Material Keywords
			#pragma shader_feature_local_fragment _ALPHATEST_ON
			#pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

			// -------------------------------------
			// Includes
			#include "CustomURPShader/LitCommon.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Universal2D.hlsl"
			ENDHLSL
		}

		Pass {
			Name "MotionVectors"
			Tags { "LightMode" = "MotionVectors" }
			ColorMask RG

			HLSLPROGRAM
			#pragma shader_feature_local _ALPHATEST_ON
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY

			#include "CustomURPShader/LitCommon.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
			ENDHLSL
		}

		Pass {
			Name "XRMotionVectors"
			Tags { "LightMode" = "XRMotionVectors" }
			ColorMask RGBA

			// Stencil write for obj motion pixels
			Stencil
			{
				WriteMask 1
				Ref 1
				Comp Always
				Pass Replace
			}

			HLSLPROGRAM
			#pragma shader_feature_local _ALPHATEST_ON
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY
			#define APLICATION_SPACE_WARP_MOTION 1

			#include "CustomURPShader/LitCommon.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
			ENDHLSL
		}
	*/
	}

	FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
