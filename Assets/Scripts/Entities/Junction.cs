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

	[Range(0,1)]
	public float test_curv = 0.6667f;
	
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
			_radius = max(_radius, r.asset.width/2);
		}

		GetComponent<SphereCollider>().radius = _radius;

		if (refresh_roads) {
			foreach (var r in roads) {
				RoadGeometry.reset_endpoint(r, this);
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
					yield return (il, ol);
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

	public (Road left, Road right) find_neighbours (Road road) {
		var road_idx = Array.IndexOf(roads, road);
		if (road_idx < 0) return (null, null);
		return (
			left:  roads[MyMath.wrap(road_idx-1, roads.Length)],
			right: roads[MyMath.wrap(road_idx+1, roads.Length)]
		);
	}

	private void OnDrawGizmosSelected () {
		foreach (var (i,o) in lane_connections_without_uturn()) {
			var bez = RoadGeometry.calc_curve(this, i,o);
			bez.debugdraw();
		}
	}
}
