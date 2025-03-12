using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.InputSystem;
using System;

// Use either by calling staic methods or by attaching to renderer
public class DebugMeshNormals : MonoBehaviour {
	public static void DrawOnGizmos (Mesh mesh, Matrix4x4 transform, float line_length=0.1f) {

		var positions = mesh.vertices;
		var normals = mesh.normals;
		var tangents = mesh.tangents;

		if (tangents.Length != normals.Length) {
			mesh.RecalculateTangents(); // does this modify the original mesh? (for sharedMesh? for mesh?)
			tangents = mesh.tangents;
		}

		Gizmos.matrix = transform;
		
		for (int i=0; i<positions.Length; ++i) {
			float3 pos = positions[i];
			float3 norm = normals[i];
			float4 tang = tangents[i];
			
			float3 bitang = tang.w * cross(norm, tang.xyz);

			Gizmos.color = Color.blue;
			Gizmos.DrawLine(pos, pos + norm * line_length);

			Gizmos.color = Color.magenta;
			Gizmos.DrawLine(pos, pos + tang.xyz * line_length);

			Gizmos.color = Color.green;
			Gizmos.DrawLine(pos, pos + bitang * line_length);
		}

		Gizmos.matrix = Matrix4x4.identity;
	}
	public struct Vertex {
		public float3 position;
		public float3 normal;
		public float3 tangent;
	}
	public static void DrawOnGizmos (Mesh mesh, Matrix4x4 transform, float line_length, Func<Vertex, Vertex> distort) {

		var positions = mesh.vertices;
		var normals = mesh.normals;
		var tangents = mesh.tangents;

		if (tangents.Length != normals.Length) {
			mesh.RecalculateTangents(); // does this modify the original mesh? (for sharedMesh? for mesh?)
			tangents = mesh.tangents;
		}
		
		Gizmos.matrix = transform;

		for (int i=0; i<positions.Length; ++i) {
			Vertex v;
			v.position = positions[i];
			v.normal = normals[i];
			var tang = (float4)tangents[i];
			v.tangent = tang.xyz;

			v = distort(v);

			float3 bitang = tang.w * cross(v.normal, v.tangent);

			Gizmos.color = Color.blue;
			Gizmos.DrawLine(v.position, v.position + v.normal * line_length);

			Gizmos.color = Color.magenta;
			Gizmos.DrawLine(v.position, v.position + v.tangent * line_length);

			Gizmos.color = Color.green;
			Gizmos.DrawLine(v.position, v.position + bitang * line_length);
		}

		Gizmos.matrix = Matrix4x4.identity;
	}
	
	public bool DrawWhenUnselected = false;
	public float line_length = 0.1f;

	void OnDrawGizmos () {
		if (DrawWhenUnselected)
			Draw();
	}
	void OnDrawGizmosSelected () {
		if (!DrawWhenUnselected)
			Draw();
	}

	void Draw () {
		var mesh = GetComponentInChildren<MeshFilter>()?.sharedMesh ??
			GetComponentInChildren<SkinnedMeshRenderer>()?.sharedMesh ??
			null;

		if (mesh != null) {
			DrawOnGizmos(mesh, transform.localToWorldMatrix, line_length);
		}
	}
}
