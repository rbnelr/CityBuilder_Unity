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
}
