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

	public Bezier get_bez () => new Bezier(
		obj_a.transform.position, obj_b.transform.position,
		obj_c.transform.position, obj_d.transform.position);

	public void set_bez (Bezier bez) {
		obj_a.transform.position = bez.a;
		obj_d.transform.position = bez.d;

		obj_b.transform.position = bez.b;
		obj_c.transform.position = bez.c;
	}

	// TODO: goes into RoadAsset
	[System.Serializable]
	public class SubmeshMaterial {
		public Material mat;
		public float2 texture_scale;
	};
	public SubmeshMaterial[] materials;

	void Start () {
		foreach (var mat in materials) {
			// copy material, so we don't accidentally affect all objects?
			mat.mat = new Material(mat.mat);
		}
	}

	public void set_controls_active (bool active) {
		obj_a.SetActive(active);
		obj_b.SetActive(active);
		obj_c.SetActive(active);
		obj_d.SetActive(active);
	}

	void Update () {
		refresh();
	}

	public void refresh () {
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
		
		GetComponent<MeshRenderer>().materials = materials.Select(x => x.mat).ToArray();

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
