using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

public class Junction : MonoBehaviour {
	public float3 position {
		get { return transform.position; }
		set { transform.position = value; }
	}

	public Road[] roads { get; private set; } = new Road[0];

	public float _radius { get; private set; } = 0;

	// temp pathfinding vars
	public float _cost;
	public bool _visited;
	//public int _q_idx;
	public Junction _pred;
	public Road _pred_road;
	
	static int _counter = 0;
	public void set_name (string custom_name=null) {
		name = custom_name ?? $"Junction #{_counter++}";
	}

	public static Junction create (string name=null) {
		var junction = Instantiate(g.entities.junction_prefab, g.entities.junctions_go.transform);
		junction.set_name(name);
		return junction;
	}

	public void destroy () {
		foreach (var road in roads) {
			road.destroy(); // triggers multiple refreshes on this junction! TODO: fix by skipping this?
		}

		Destroy(gameObject);
	}
	// Handle destruction from editor
	void OnDestroy () {
		destroy();
	}

	public void connect_road (Road road, bool refresh=true) {
		Debug.Assert(!roads.Contains(road));

		var list = roads.ToList();
		list.Add(road);
		roads = list.ToArray();

		if (refresh) Refresh();
	}
	public void disconnect_road (Road road, bool refresh=true, bool keep_empty_junc=false) {
		// Allow non connected road

		var list = roads.ToList();
		bool changed = list.Remove(road);
		roads = list.ToArray();

		if (roads.Length == 0 && !keep_empty_junc) {
			destroy();
		}
		else if (changed && refresh) Refresh();
	}
	
	public void Refresh () {
		_radius = 0;
		
		foreach (var r in roads) {
			_radius = max(_radius, r.width/2);
		}

		foreach (var r in roads) {
			r.Refresh(adjust_ends: true);
		}

		GetComponent<SphereCollider>().radius = _radius;
	}

	public static Junction between (Road src, Road dst) {
		if (src.junc0 == dst.junc0 || src.junc0 == dst.junc1) {
			return src.junc0;
		}
		else {
			Debug.Assert(src.junc1 == dst.junc0 || src.junc1 == dst.junc1);
			return src.junc1;
		}
	}
	
	public Bezier calc_curve (RoadLane lane0, RoadLane lane1) {
		var p0 = lane0.road.calc_path(lane0.lane).eval(1);
		var p1 = lane1.road.calc_path(lane1.lane).eval(0);

		float3 ctrl_in, ctrl_out;
		// Find straight line intersection of in/out lanes with their tangents
		if (MyMath.line_line_intersect(p0.pos.xz, p0.forw.xz, p1.pos.xz, -p1.forw.xz, out float2 point)) {
			ctrl_in  = float3(point.x, p0.pos.y, point.y);
			ctrl_out = float3(point.x, p1.pos.y, point.y);
		}
		// Come up with seperate control points TODO: how reasonable is this?
		else {
			float dist = distance(p0.pos, p1.pos) * 0.5f;
			ctrl_in  = p0.pos + float3(p0.forw.x, 0, p0.forw.z) * dist;
			ctrl_out = p1.pos - float3(p1.forw.x, 0, p1.forw.z) * dist;
		}

		// NOTE: for quarter circle turns k=0.5539 would result in almost exactly a quarter circle!
		// https://pomax.github.io/bezierinfo/#circles_cubic
		// but turns that are sharper in the middle are more realistic, but we could make this customizable?
		float k = 0.6667f;

		Bezier bez = new Bezier(
			p0.pos,
			lerp(p0.pos, ctrl_in , k),
			lerp(p1.pos, ctrl_out, k),
			p1.pos
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
	public IEnumerable<(RoadLane, RoadLane)> lane_connections_without_uturn () {
		foreach (var (i,o) in road_connections_without_uturn()) {
			foreach (var il in i.lanes_to_junc(this)) {
				foreach (var ol in o.lanes_from_junc(this)) {
					yield return (new RoadLane(i,il), new RoadLane(o,ol));
				}
			}
		}
	}

	private void OnDrawGizmosSelected () {
		foreach (var (i,o) in lane_connections_without_uturn()) {
			var bez = calc_curve(i,o);
			bez.debugdraw();
		}
	}
}
