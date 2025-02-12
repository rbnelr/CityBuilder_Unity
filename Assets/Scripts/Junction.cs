using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;
using static UnityEngine.Rendering.HableCurve;
using static UnityEditor.PlayerSettings;
using System.Drawing;
using Unity.VisualScripting;
using static Road;

public class Junction : MonoBehaviour {
	public float3 position {
		get { return transform.position; }
		set { transform.position = value; }
	}

	public Road[] roads { get; private set; } = new Road[0];

	public float _radius;

	// temp pathfinding vars
	public float _cost;
	public bool _visited;
	//public int _q_idx;
	public Junction _pred;
	public Road _pred_road;
	
	public static Junction create () {
		var junction = Instantiate(g.entities.junction_prefab, g.entities.junctions_go.transform);
		return junction;
	}

	public void connect_road (Road road) {
		Debug.Assert(!roads.Contains(road));

		var list = roads.ToList();
		list.Add(road);
		roads = list.ToArray();
	}

	public static Junction between (Road src, Road dst) {
		if (src.junc_a == dst.junc_a || src.junc_a == dst.junc_b) {
			return src.junc_a;
		}
		else {
			Debug.Assert(src.junc_b == dst.junc_a || src.junc_b == dst.junc_b);
			return src.junc_b;
		}
	}
	
	public Bezier calc_curve (Road road0, Road road1, Lane lane0, Lane lane1) {
		// calc_curve works with shifts 
		float shift0 = lane0.dir == RoadDirection.Forward ? lane0.shift : -lane0.shift;
		float shift1 = lane1.dir == RoadDirection.Forward ? -lane1.shift : lane1.shift;

		return calc_curve(road0, road1, float2(shift0, lane0.height), float2(shift1, lane1.height));
	}
	
	public Bezier calc_curve (Road road0, Road road1, float2 shift_0, float2 shift_1) {
		var i0 = road0.get_end_info(this, shift_0);
		var i1 = road1.get_end_info(this, shift_1);

		float3 ctrl_in, ctrl_out;
		// Find straight line intersection of in/out lanes with their tangents
		if (MyMath.line_line_intersect(i0.pos.xz, i0.forw.xz, i1.pos.xz, i1.forw.xz, out float2 point)) {
			ctrl_in  = float3(point.x, i0.pos.y, point.y);
			ctrl_out = float3(point.x, i1.pos.y, point.y);
		}
		// Come up with seperate control points TODO: how reasonable is this?
		else {
			float dist = distance(i0.pos, i1.pos) * 0.5f;
			ctrl_in  = i0.pos + float3(i0.forw.x, 0, i0.forw.z) * dist;
			ctrl_out = i1.pos + float3(i1.forw.x, 0, i1.forw.z) * dist;
		}

		// NOTE: for quarter circle turns k=0.5539 would result in almost exactly a quarter circle!
		// https://pomax.github.io/bezierinfo/#circles_cubic
		// but turns that are sharper in the middle are more realistic, but we could make this customizable?
		float k = 0.6667f;

		Bezier bez = new Bezier(
			i0.pos,
			lerp(i0.pos, ctrl_in , k),
			lerp(i1.pos, ctrl_out, k),
			i1.pos
		);
		return bez;
	}
	
	public IEnumerable<(Road, Road)> road_connections_without_uturn () {
		foreach (var i in roads) {
			foreach (var o in roads) {
				if (i != o)
					yield return (i,o);
			}
		}
	}
	public IEnumerable<(Road, Road.Lane, Road, Road.Lane)> lane_connections_without_uturn () {
		foreach (var (i,o) in road_connections_without_uturn()) {
			foreach (var il in i.lanes_to_junc(this)) {
				foreach (var ol in o.lanes_from_junc(this)) {
					yield return (i,il, o,ol);
				}
			}
		}
	}

	private void OnDrawGizmosSelected () {
		foreach (var (i,il, o,ol) in lane_connections_without_uturn()) {
			var bez = calc_curve(i,o, il,ol);
			bez.debugdraw();
		}
	}
}
