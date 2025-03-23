
// Ignore that certain passes might not actally need all attributes
struct Attributes {
	float3 position     : POSITION;
	float3 normal       : NORMAL;
	float4 tangent      : TANGENT;
	float2 texcoord     : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

CBUFFER_START(UnityPerMaterial)
bool _WorldspaceTextures;
half4 _TextureScale;
half4 _TextureOffset;

float3 _JunctionCenter;
float4x4 _BezierL0; // use float3[] or even float3[][]?
float4x4 _BezierL1;
float4x4 _BezierR0;
float4x4 _BezierR1;
CBUFFER_END

#include "Util.hlsl"

void vertex (Attributes v, inout Varyings o) {
	float3 tang;
	mesh_junction_float(_BezierL0, _BezierL1, _BezierR0, _BezierR1, _JunctionCenter,
		v.position, v.normal, v.tangent.xyz,
		o.positionWS, o.normalWS, tang);
	
	o.positionCS = TransformWorldToHClip(o.positionWS);
	
	road_uv_map(v.texcoord, v.position, o.positionWS, float4(tang, v.tangent.w), o.uv, o.tangentWS);
}
