using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;
using static Road;

public class Road : MonoBehaviour {
	public Junction junc_a { get; private set; }
	public Junction junc_b { get; private set; }

	public float3 pos_a { get; set; } // TODO: make set private later
	public float3 pos_b { get; set; }

	public float speed_limit => 70 / 3.6f;
	public float length => distance(pos_a, pos_b);
	
	public float width;

	public float edgeL => -width / 2; // TODO
	public float edgeR => width / 2; // TODO
	
	[System.Serializable]
	public struct Lane {
		public float shift;
		public RoadDirection dir;
	}

	public Lane[] lanes;

	private void OnDrawGizmosSelected () {
		foreach (var lane in lanes) {
			Gizmos.color = lane.dir == RoadDirection.Forward ? Color.yellow : Color.blue;
			get_lane_path(lane).debugdraw();
		}
	}

	public Bezier get_lane_path (Lane lane) {
		float3 a = transform.TransformPoint(lane.shift*10/width,0,-5);
		float3 b = transform.TransformPoint(lane.shift*10/width,0,+5);
		var bez = new Bezier(a, lerp(a,b,0.333f), lerp(a,b,0.667f), b);
		return lane.dir == RoadDirection.Forward ? bez : bez.reverse();
	}

	public static Road create (Road prefab, Junction a, Junction b) {
		var road = Instantiate(prefab, g.entities.roads_go.transform);
		road.junc_a = a;
		road.junc_b = b;

		a.connect_road(road);
		b.connect_road(road);
		return road;
	}

	public void refresh (bool reset=false) {
		if (reset) {
			pos_a = junc_a.position;
			pos_b = junc_b.position;

			float3 dir = normalizesafe(pos_b - pos_a);

			pos_a += dir * junc_a._radius;
			pos_b -= dir * junc_b._radius;
		}

		transform.position = (pos_a + pos_b) * 0.5f + float3(0, 0.01f, 0);
		transform.rotation = Quaternion.LookRotation(pos_b - pos_a);
		transform.localScale = float3(width / 10.0f, 1, length / 10.0f); // unity plane mesh size 10
	}
	
	public Junction other_junction (Junction junc) => junc_a != junc ? junc_a : junc_b;
}

public enum RoadDirection : byte { Forward, Backward }