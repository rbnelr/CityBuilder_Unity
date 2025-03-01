using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.InputSystem;

public class RoadTest : MonoBehaviour {

	public GameObject obj_a;
	public GameObject obj_b;
	public GameObject obj_c;
	public GameObject obj_d;

	public float width = 9;

	float road_center_length;

	Bezier get_bez () => new Bezier(
		obj_a.transform.position, obj_b.transform.position,
		obj_c.transform.position, obj_d.transform.position);

	// TODO: goes into RoadAsset
	[System.Serializable]
	public struct SubmeshMaterial {
		public Material mat;
		public float2 texture_scale;
	};
	public SubmeshMaterial[] materials;

	void Update () {
		var bez = get_bez();

		road_center_length = bez.approx_len();
		
		foreach (var mat in materials) {
			mat.mat.SetVector("_BezierA", (Vector3)bez.a);
			mat.mat.SetVector("_BezierB", (Vector3)bez.b);
			mat.mat.SetVector("_BezierC", (Vector3)bez.c);
			mat.mat.SetVector("_BezierD", (Vector3)bez.d);

			bool worldspace = mat.mat.GetInt("_WorldspaceTextures") != 0;

			float2 scale = mat.texture_scale;
			if (!worldspace)
				scale.x /= road_center_length;
			mat.mat.SetVector("_TextureScale", (Vector2)scale);
		}
		
		GetComponent<MeshRenderer>().sharedMaterials = materials.Select(x => x.mat).ToArray();

		refresh_bounds();
	}

	void refresh_bounds () {
		var bounds = get_bez().approx_road_bounds(-width/2, +width/2, -2, +5); // calculate xz bounds based on width
		bounds.Expand(float3(1,0,1)); // extend xz by a little to catch mesh overshoot
		GetComponent<MeshRenderer>().bounds = bounds;

		var coll = GetComponent<BoxCollider>();
		coll.center = bounds.center;
		coll.size   = bounds.size;
	}

	void OnMouseOver () {

	}

	void OnDrawGizmosSelected () {
		Gizmos.color = Color.red;
		get_bez().debugdraw(20);

		//var bounds = GetComponent<MeshRenderer>().bounds;
		//Gizmos.color = Color.red;
		//Gizmos.DrawWireCube(bounds.center, bounds.size);
		
		//DebugNormals();
	}
	
	void DebugNormals () {
		var bez = get_bez();

		DebugMeshNormals.DrawOnGizmos(this.GetComponent<MeshFilter>().sharedMesh, transform.localToWorldMatrix, 0.25f,
			v => {
				var ret = new DebugMeshNormals.Vertex();
				bez.curve_mesh(v.position, v.normal, v.tangent,
					out ret.position, out ret.normal, out ret.tangent);
				return ret;
			});
	}
}
