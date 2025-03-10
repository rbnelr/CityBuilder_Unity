using System;
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

	public Road[] roads;

	public float _radius { get; private set; } = 0;

	// temp pathfinding vars
	[NonSerialized] public float _cost;
	[NonSerialized] public bool _visited;
	//[NonSerialized] public int _q_idx;
	[NonSerialized] public Junction _pred;
	[NonSerialized] public Road _pred_road;
	
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

	void Start () {
		Refresh();
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
	
	[NaughtyAttributes.Button("Refresh")]
	public void Refresh (bool refresh_roads=true) {
		_radius = 0;
		
		sort_roads();

		foreach (var r in roads) {
			_radius = max(_radius, r.width/2);
		}

		GetComponent<SphereCollider>().radius = _radius;

		if (refresh_roads) {
			foreach (var r in roads) {
				r.reset_endpoint(this);
			}

			foreach (var r in roads) {
				r.Refresh(refresh_junc: false);
			}
		}
	}

	public Bounds calc_bounds () => new Bounds(position, float3(_radius)*2);

	public static Junction between (Road src, Road dst) {
		if (src.junc0 == dst.junc0 || src.junc0 == dst.junc1) {
			return src.junc0;
		}
		else {
			Debug.Assert(src.junc1 == dst.junc0 || src.junc1 == dst.junc1);
			return src.junc1;
		}
	}
	
	public Bezier calc_curve (Road road0, Road road1) {
		var dir0 = road0.get_dir_to_junc(this);
		var dir1 = road1.get_dir_from_junc(this);

		var p0 = road0.calc_path(dir0, 0).eval(1);
		var p1 = road1.calc_path(dir1, 0).eval(0);
		return calc_curve(p0, p1);
	}
	public Bezier calc_curve (RoadLane lane0, RoadLane lane1) {
		var p0 = lane0.road.calc_path(lane0.lane).eval(1);
		var p1 = lane1.road.calc_path(lane1.lane).eval(0);
		return calc_curve(p0, p1);
	}

	public Bezier calc_curve (Bezier.OffsetPoint p0, Bezier.OffsetPoint p1) {

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

	// Sort so roads to establish neighboring roads for meshing
	void sort_roads () {
		float get_seg_angle (Road road) {
			Debug.DrawLine(position, road.pos_for_junction(this), Color.red, 0);

			float3 dir2road = road.pos_for_junction(this) - position;
			return atan2(dir2road.z, dir2road.x);
		}
		int compareer (Road l, Road r) {
			return Comparer<float>.Default.Compare(get_seg_angle(l), get_seg_angle(r));
		}

		Array.Sort(roads, compareer);
	}

	private void OnDrawGizmosSelected () {
		foreach (var (i,o) in lane_connections_without_uturn()) {
			var bez = calc_curve(i,o);
			bez.debugdraw();
		}
	}
}
