using Unity;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class BezierTesting : MonoBehaviour {
	public Transform pos_a;
	public Transform pos_b;
	public Transform pos_c;
	public Transform pos_d;

	public float width0 = 12;
	public float width1 = 10;

	[Range(0,2)]
	public int method = 0;

	[Range(0,1)]
	public float curv = 0.6667f;

	private void OnDrawGizmos () {
		var bez = new Bezier(pos_a.position, pos_b.position, pos_c.position, pos_d.position);

		float3 l0 = bez.eval_offset(0, float3(-width0/2,0.01f,0)).pos;
		float3 r0 = bez.eval_offset(0, float3(+width0/2,0.01f,0)).pos;
		float3 l1 = bez.eval_offset(1, float3(-width1/2,0.01f,0)).pos;
		float3 r1 = bez.eval_offset(1, float3(+width1/2,0.01f,0)).pos;
		Debug.DrawLine(l0, r0, Color.blue);
		Debug.DrawLine(l1, r1, Color.blue);

		Gizmos.color = Color.red;
		bez.debugdraw(float3(0,0.01f,0), 20);
		
		if (method == 0) {
			Gizmos.color = Color.grey;
			for (int i=0; i<=10; ++i) {
				float t = i/10.0f;
				float x0 = t*width0 - width0/2;
				float x1 = t*width1 - width1/2;
				bez.debugdraw(float3(x0,0.01f,0), float3(x1,0.01f,0), 20);
			}
		}
		else if (method == 1) {
			var mat0 = MyMath.rotate_to_direction(pos_b.position - pos_a.position);
			var mat1 = MyMath.rotate_to_direction(pos_d.position - pos_c.position);

			Gizmos.color = Color.grey;
			for (int i=0; i<=10; ++i) {
				float t = i/10.0f;
				float x0 = t*width0 - width0/2;
				float x1 = t*width1 - width1/2;

				float3 a = (float3)pos_a.position + mul(mat0, float3(x0, 0.01f, 0));
				float3 b = (float3)pos_b.position + mul(mat0, float3(x0, 0.01f, 0));
				float3 c = (float3)pos_c.position + mul(mat1, float3(x1, 0.01f, 0));
				float3 d = (float3)pos_d.position + mul(mat1, float3(x1, 0.01f, 0));

				new Bezier(a,b,c,d).debugdraw(20);
			}
		}
		else {
			var mat0 = MyMath.rotate_to_direction(pos_b.position - pos_a.position);
			var mat1 = MyMath.rotate_to_direction(pos_d.position - pos_c.position);
			
			Gizmos.color = Color.grey;
			for (int i=0; i<=10; ++i) {
				float t = i/10.0f;
				float x0 = t*width0 - width0/2;
				float x1 = t*width1 - width1/2;

				float3 p0 = (float3)pos_a.position + mul(mat0, float3(x0, 0.01f, 0));
				float3 p1 = (float3)pos_d.position + mul(mat1, float3(x1, 0.01f, 0));
				float3 d0 = mul(mat0, float3(0,0,1));
				float3 d1 = mul(mat1, float3(0,0,1));

				var local_bez = RoadGeometry.calc_curve(p0,d0, p1,d1, curv);
				local_bez.debugdraw(20);
			}
		}
	}
}
