using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.InputSystem;

[ExecuteAlways]
public class RoadTest : MonoBehaviour {

	public GameObject obj_a;
	public GameObject obj_b;
	public GameObject obj_c;
	public GameObject obj_d;

	Bezier get_bez () => new Bezier(
		obj_a.transform.position, obj_b.transform.position,
		obj_c.transform.position, obj_d.transform.position);

	Material mat;
	void Start () {
		mat = this.GetComponent<MeshRenderer>().sharedMaterial;

		var b = this.GetComponent<MeshRenderer>().bounds;
		b.min = float3(-10000);
		b.max = float3(+10000);
		this.GetComponent<MeshRenderer>().bounds = b;
	}
	void Update () {
		var bez = get_bez();
		
		mat.SetVector("_BezierA", transform.InverseTransformPoint(bez.a));
		mat.SetVector("_BezierB", transform.InverseTransformPoint(bez.b));
		mat.SetVector("_BezierC", transform.InverseTransformPoint(bez.c));
		mat.SetVector("_BezierD", transform.InverseTransformPoint(bez.d));
	}

	void OnDrawGizmos () {
		Gizmos.color = Color.red;
		get_bez().debugdraw(20);
		
		DebugNormals();
	}
	
	void DebugNormals () {
		
		float3x3 TBN_from_forward (float3 forw) {
			forw = normalize(forw);
			float3 up = float3(0,1,0);
			float3 right = cross(up, forw);
	
			up = normalize(cross(forw, right));
			right = normalize(right);
	
			// unlike hlsl float3x3 takes columns already!
			return float3x3(right, up, forw);
		}

		void curve_mesh_float (float3 a, float3 b, float3 c, float3 d,
				float3 pos_obj, float3 norm_obj, float3 tang_obj,
				out float3 pos_out, out float3 norm_out, out float3 tang_out) {
			
			float x = -pos_obj.x;
			float y = pos_obj.y;
			float t = -pos_obj.z / 20.0f;
	
			var res = new Bezier(a,b,c,d).eval(t);
	
			float3x3 bez2world = TBN_from_forward(res.vel);
	
			pos_out = res.pos + mul(bez2world, float3(x,y,0));
			norm_out = mul(bez2world, norm_obj * float3(-1,1,-1));
			tang_out = mul(bez2world, tang_obj * float3(-1,1,-1));
		}
		
		var bez = get_bez();
		var a = transform.InverseTransformPoint(bez.a);
		var b = transform.InverseTransformPoint(bez.b);
		var c = transform.InverseTransformPoint(bez.c);
		var d = transform.InverseTransformPoint(bez.d);

		DebugMeshNormals.DrawOnGizmos(this.GetComponent<MeshFilter>().sharedMesh, transform.localToWorldMatrix, 0.25f,
			v => {
				var ret = new DebugMeshNormals.Vertex();
				curve_mesh_float(a, b, c, d, v.position, v.normal, v.tangent,
					out ret.position, out ret.normal, out ret.tangent);
				return ret;
			});
	}
}
