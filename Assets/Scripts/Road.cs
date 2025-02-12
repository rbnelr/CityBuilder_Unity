using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;
using static Road;
using UnityEditor.Experimental.GraphView;

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

		public float height => 0.01f;
	}

	public Lane[] lanes;

	public IEnumerable<Lane> lanes_in_dir (RoadDirection dir) {
		// TODO: can be optimized if lanes are stored in sorted order
		foreach (var lane in lanes) {
			if (lane.dir == dir) yield return lane;
		}
	}
	public IEnumerable<Lane> lanes_to_junc (Junction junc) {
		return lanes_in_dir(get_dir_to_junc(junc));
	}
	public IEnumerable<Lane> lanes_from_junc (Junction junc) {
		return lanes_in_dir(get_dir_from_junc(junc));
	}
	
	public Junction other_junction (Junction junc) => junc_a != junc ? junc_a : junc_b;
	
	RoadDirection get_dir_to_junc (Junction junc) {
		Debug.Assert(junc != null && (junc == junc_a || junc == junc_b));
		return junc_a != junc ? RoadDirection.Forward : RoadDirection.Backward;
	}
	RoadDirection get_dir_from_junc (Junction junc) {
		Debug.Assert(junc != null && (junc == junc_a || junc == junc_b));
		return junc_a == junc ? RoadDirection.Forward : RoadDirection.Backward;
	}

	public Bezier get_lane_path (Lane lane) {
		float3 a = transform.TransformPoint(lane.shift*10/width, lane.height, -5);
		float3 b = transform.TransformPoint(lane.shift*10/width, lane.height, +5);

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

	// TODO: rework this
	public struct EndInfo {
	public float3 pos;
	public float3 forw;
		public float3 right;
	}
	public EndInfo get_end_info (RoadDirection dir, float2 shiftXY) {
		EndInfo i;
		if (dir == RoadDirection.Forward) {
			i.pos = pos_b;
			i.forw = normalizesafe(pos_b - pos_a);
		}
		else {
			i.pos = pos_a;
			i.forw = normalizesafe(pos_a - pos_b);
		}
		i.right = MyMath.rotate90_right(i.forw);
		i.pos += i.right * shiftXY.x;
		i.pos += float3(0, shiftXY.y, 0);
		return i;
	}
	public EndInfo get_end_info (Junction junc, float2 shiftXY) {
		return get_end_info(get_dir_to_junc(junc), shiftXY);
	}


	private void OnDrawGizmosSelected () {
		foreach (var lane in lanes) {
			Gizmos.color = lane.dir == RoadDirection.Forward ? Color.yellow : Color.blue;
			get_lane_path(lane).debugdraw();
		}
	}
}

public enum RoadDirection : byte { Forward, Backward }