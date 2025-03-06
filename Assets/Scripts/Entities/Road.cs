using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

public class Road : MonoBehaviour {
	public Junction junc0 { get; private set; }
	public Junction junc1 { get; private set; }

	public float3 pos0;
	public float3 pos1;
	public float3 pos0_ctrl;
	public float3 pos1_ctrl;

	public Bezier bezier {
		get { return new Bezier(pos0, pos0_ctrl, pos1_ctrl, pos1); }
		set {
			pos0 = value.a;
			pos0_ctrl = value.b;
			pos1_ctrl = value.c;
			pos1 = value.d;
		}
	}

//// TODO: goes into RoadAsset
	
	public float width;

	public float edgeL => -width / 2; // TODO
	public float edgeR => width / 2; // TODO

	public float speed_limit => 70 / 3.6f;
	
	[System.Serializable]
	public struct Lane {
		public float shift;
		public RoadDirection dir;

		public float height => 0.10f;
	}

	public Lane[] lanes;

	[System.Serializable]
	public class SubmeshMaterial {
		public Material mat;
		public float2 texture_scale;

		public SubmeshMaterial copy () {
			return new SubmeshMaterial{
				mat = new Material(mat),
				texture_scale = texture_scale
			};
		}
	};
	public SubmeshMaterial[] materials; // shared between roads

	//// 
	static int _counter = 0;
	public void set_name (string custom_name=null) {
		name = custom_name ?? $"Road #{_counter++}";
	}

	public static Road create (Road prefab, Junction junc0, Junction junc1, string name=null) {
		Debug.Assert(junc0 != junc1);

		var road = Instantiate(prefab, g.entities.roads_go.transform);
		road.set_name(name);

		road.junc0 = junc0;
		junc0.connect_road(road, refresh: false);
		road.junc1 = junc1;
		junc1.connect_road(road, refresh: false);

		// defer refresh, to avoid missing connection, but this still refreshes us twice TODO: fix!
		junc0.Refresh();
		junc1.Refresh();

		return road;
	}

	public void destroy (bool keep_empty_junc=false) {
		if (junc0) junc0.disconnect_road(this, true, keep_empty_junc); // refresh on all remaining roads
		junc0 = null;
		if (junc1) junc1.disconnect_road(this, true, keep_empty_junc); // refresh on all remaining roads
		junc1 = null;

		Destroy(gameObject);
	}
	// Handle destruction from editor
	void OnDestroy () {
		destroy();
	}

	////
	[NaughtyAttributes.Button("Refresh")]
	public void Refresh (bool adjust_ends=false) {
		Debug.Assert(junc0 && junc1);

		if (adjust_ends) {
			pos0 = junc0.position;
			pos1 = junc1.position;

			float3 dir = normalizesafe(pos1 - pos0);

			pos0 += dir * junc0._radius;
			pos1 -= dir * junc1._radius;

			bezier = Bezier.from_line(pos0, pos1);
		}

		refresh_mesh();
	}
	
	[NaughtyAttributes.Button("refresh_mesh")]
	public void refresh_mesh () {
		var local_mat = materials.Select(x => x.copy()).ToArray();

		float road_center_length = bezier.approx_len();
		
		foreach (var mat in local_mat) {
			//mat.mat.SetVector("_BezierA", (Vector3)bezier.a);
			//mat.mat.SetVector("_BezierB", (Vector3)bezier.b);
			//mat.mat.SetVector("_BezierC", (Vector3)bezier.c);
			//mat.mat.SetVector("_BezierD", (Vector3)bezier.d);
			//
			//bool worldspace = mat.mat.GetInt("_WorldspaceTextures") != 0;
			//
			//float2 scale = mat.texture_scale;
			//if (!worldspace)
			//	scale.x /= road_center_length;
			//mat.mat.SetVector("_TextureScale", (Vector2)scale);
		}
		
		GetComponent<MeshRenderer>().materials = local_mat.Select(x => x.mat).ToArray();

		refresh_bounds();
	}

	void refresh_bounds () {
		var bounds = bezier.approx_road_bounds(-width/2, +width/2, -2, +5); // calculate xz bounds based on width
		bounds.Expand(float3(1,0,1)); // extend xz by a little to catch mesh overshoot
		GetComponent<MeshRenderer>().bounds = bounds;

		var coll = GetComponent<BoxCollider>();
		coll.center = bounds.center;
		coll.size   = bounds.size;
	}


//// Util
	public float length_for_pathfinding => distance(pos0, pos1);

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
	
	public Junction other_junction (Junction junc) => junc0 != junc ? junc0 : junc1;
	
	RoadDirection get_dir_to_junc (Junction junc) {
		Debug.Assert(junc != null && (junc == junc0 || junc == junc1));
		return junc0 != junc ? RoadDirection.Forward : RoadDirection.Backward;
	}
	RoadDirection get_dir_from_junc (Junction junc) {
		Debug.Assert(junc != null && (junc == junc0 || junc == junc1));
		return junc0 == junc ? RoadDirection.Forward : RoadDirection.Backward;
	}

	// get bezier for lane
	// TODO: it's actually impossible to create accurate beziers relative to other beziers!
	// probably should use center bezier and eval_offset, but the math to get correct derivative and curvature might be hard or impossible
	public OffsetBezier calc_path (RoadDirection dir, float3 offset) {
		var offs_bez = bezier.offset(offset);
		return dir == RoadDirection.Forward ? offs_bez : offs_bez.reverse();
	}
	public OffsetBezier calc_path (Lane lane) {
		return calc_path(lane.dir, float3(lane.shift, lane.height, 0));
	}

//// Vis
	private void OnDrawGizmosSelected () {
		var bounds = GetComponent<MeshRenderer>().bounds;
		Gizmos.color = Color.red;
		Gizmos.DrawWireCube(bounds.center, bounds.size);

		Gizmos.color = Color.red;
		bezier.debugdraw();

		foreach (var lane in lanes) {
			Gizmos.color = lane.dir == RoadDirection.Forward ? Color.yellow : Color.blue;
			calc_path(lane).debugdraw();
		}

		//DebugVis.DebugCurvedMeshNormals(bezier, GetComponent<MeshFilter>().sharedMesh, transform);
		DebugMeshNormals.DrawOnGizmos(GetComponent<MeshFilter>().sharedMesh, transform.localToWorldMatrix);
	}
}

public enum RoadDirection : byte { Forward, Backward }

public struct RoadLane {
	public Road road;
	public Road.Lane lane;

	public RoadLane (Road road, Road.Lane lane) {
		this.road = road;
		this.lane = lane;
	}
}
