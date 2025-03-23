using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class Road : MonoBehaviour {
	public Junction junc0;
	public Junction junc1;

	public float3 pos0;
	public float3 pos1;

	public float3 tangent0;
	public float3 tangent1;

	//public float3 pos0_ctrl => pos0 + tangent0;
	//public float3 pos1_ctrl => pos1 + tangent1;

	public Bezier bezier {
		get { return new Bezier(pos0, pos0 + tangent0, pos1 + tangent1, pos1); }
		set {
			pos0 = value.a;
			tangent0 = value.b - value.a;
			tangent1 = value.c - value.d;
			pos1 = value.d;
		}
	}

//// TODO: goes into RoadAsset
	
	[System.Serializable]
	public class Asset {
		public float width;

		public float edgeL => -width / 2; // TODO
		public float edgeR => width / 2; // TODO

		public float sidewalkL => edgeL + 2;
		public float sidewalkR => edgeR - 2;

		public float speed_limit => 70 / 3.6f;
	
		[System.Serializable]
		public struct Lane {
			public float shift;
			public RoadDir dir;

			public float height => 0.10f;
		}

		public Lane[] lanes;
	}
	public Asset asset;

	[System.Serializable]
	public class SubmeshMaterial {
		public Material mat;
		public float2 texture_scale;
	};

	public SubmeshMaterial[] materials;

	public Color tint = new Color(0,0,0,0); // TODO: just used for previews, optimize away for common roads?
	
	Material get_road_mat (Material orig) {
		var m = new Material(orig);
		//m.SetTexture("_Albedo", orig.GetTexture("_Albedo"));
		//m.SetTexture("_Normal", orig.GetTexture("_Normal"));
		//m.SetInt("_WorldspaceTextures", orig.GetInt("_WorldspaceTextures"));
		//m.SetFloat("_Smoothness", orig.GetFloat("_Smoothness"));
		//m.SetFloat("_Metallic", orig.GetFloat("_Metallic"));
		return m;
	}
	Material get_junc_mat (Material orig) {
		var m = get_road_mat(orig);
		//m.shader = Shader.Find("Shader Graphs/Junction");
		m.shader = Shader.Find("Custom/CurvedRoadJunction");
		return m;
	}
	public Material[] make_road_mats () => materials.Select(x => get_road_mat(x.mat)).ToArray();
	public Material[] make_junc_mats () => materials.Select(x => get_junc_mat(x.mat)).ToArray();

	#region creation
	static int _counter = 0;
	public void set_name (string custom_name=null) {
		name = custom_name ?? $"Road #{_counter++}";
	}
	
	public static Road create (Road prefab, Junction junc0, Junction junc1, string name=null, Color? tint=null) {
		Debug.Assert(junc0 != junc1);

		var road = Instantiate(prefab, g.entities.roads_go.transform);
		road.set_name(name);
		if (tint.HasValue) road.tint = tint.Value;

		road.junc0 = junc0;
		junc0.connect_road(road, refresh: false);
		road.junc1 = junc1;
		junc1.connect_road(road, refresh: false);
		
		road.Init(refresh: false);

		junc0.Refresh();
		junc1.Refresh();
		
		g.entities.roads.add(road);
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
	#endregion

	#region refresh
	[NaughtyAttributes.Button("Refresh")]
	public void Refresh (bool refresh_junc=true) {
		if (refresh_junc) {
			junc0.Refresh();
			junc1.Refresh();
		}
		else {
			refresh_mesh();
		}
	}

	public void Init (bool refresh=true) {
		Debug.Assert(junc0 && junc1);
		
		float3 p0 = junc0.position;
		float3 p1 = junc1.position;

		float3 dir = normalizesafe(p1 - p0);
		p0 += dir * junc0._radius;
		p1 -= dir * junc1._radius;

		bezier = Bezier.from_line(p0, p1);

		if (refresh)
			Refresh();
	}

	[NaughtyAttributes.Button("refresh_mesh")]
	public void refresh_mesh () {
		var renderers = GetComponentsInChildren<MeshRenderer>();
		renderers[0].materials = RoadGeometry.refresh_main_mesh(this);
		renderers[1].materials = RoadGeometry.refresh_junc_mesh(junc0, this);
		renderers[2].materials = RoadGeometry.refresh_junc_mesh(junc1, this);

		refresh_bounds(renderers);
	}

	void refresh_bounds (MeshRenderer[] renderers) {

		var bounds = bezier.approx_road_bounds(-asset.width/2, +asset.width/2, -2, +5); // calculate xz bounds based on width
		bounds.Expand(float3(1,0,1)); // extend xz by a little to catch mesh overshoot
		renderers[0].bounds = bounds;

		var coll = GetComponent<BoxCollider>();
		coll.center = bounds.center;
		coll.size   = bounds.size;

		renderers[1].bounds = junc0.calc_bounds();
		renderers[2].bounds = junc1.calc_bounds();
	}
	
	#endregion
	
	#region helpers
	public float length_for_pathfinding => distance(pos0, pos1);
	
	public IEnumerable<RoadLane> all_lanes () {
		// TODO: can be optimized if lanes are stored in sorted order
		for (int i=0; i<asset.lanes.Length; ++i) {
			var lane = new RoadLane(this, i);
			yield return new RoadLane(this, i);
		}
	}
	public IEnumerable<RoadLane> lanes_in_dir (RoadDir dir) {
		// TODO: can be optimized if lanes are stored in sorted order
		for (int i=0; i<asset.lanes.Length; ++i) {
			var lane = new RoadLane(this, i);
			if (lane.dir == dir) yield return lane;
		}
	}

	public IEnumerable<RoadLane> lanes_to_junc (Junction junc) {
		return lanes_in_dir(get_dir_to_junc(junc));
	}
	public IEnumerable<RoadLane> lanes_from_junc (Junction junc) {
		return lanes_in_dir(get_dir_from_junc(junc));
	}
	
	public Junction other_junction (Junction junc) => junc0 != junc ? junc0 : junc1;
	
	public ref float3 get_tangent (RoadDir side) => ref side == RoadDir.Forward ? ref tangent1 : ref tangent0;
	public ref float3 get_pos (RoadDir side) => ref side == RoadDir.Forward ? ref pos1 : ref pos0;
	public ref float3 tangent_for_junction (Junction junc) => ref junc0 == junc ? ref tangent0 : ref tangent1;
	public ref float3 pos_for_junction (Junction junc) => ref junc0 == junc ? ref pos0 : ref pos1;
	
	public RoadDir get_dir_to_junc (Junction junc) {
		Debug.Assert(junc != null && (junc == junc0 || junc == junc1));
		return junc0 != junc ? RoadDir.Forward : RoadDir.Backward;
	}
	public RoadDir get_dir_from_junc (Junction junc) {
		Debug.Assert(junc != null && (junc == junc0 || junc == junc1));
		return junc0 == junc ? RoadDir.Forward : RoadDir.Backward;
	}
	
	// offset.x as facing forwards on road (matching edgeL edgeR etc)
	// offset.z facing away from road (into intersection)
	// dir will point into from road end into road middle
	public PointDir endpoint (RoadDir side, float3 offset) {
		var pos = get_pos(side);
		var dir = get_tangent(side);
		
		if (side == RoadDir.Forward)
			offset.x = -offset.x;
		offset.z = -offset.z;

		var mat = MyMath.rotate_to_direction(dir);
		return new PointDir {
			pos = pos + mul(mat, offset),
			dir = mul(mat, float3(0,0,1))
		};
	}
	// offset as facing towards junction
	// dir will point into junction
	public PointDir endpoint (Junction junc, float3 offset) {
		var side = get_dir_to_junc(junc);
		var pos = get_pos(side);
		var dir = -get_tangent(side);

		var mat = MyMath.rotate_to_direction(dir);
		return new PointDir {
			pos = pos + mul(mat, offset),
			dir = mul(mat, float3(0,0,1))
		};
	}
	public PointDir endpoint (RoadDir side, float offsetX) => endpoint(side, float3(offsetX, 0,0));
	public PointDir endpoint (Junction for_junc, float offsetX) => endpoint(for_junc, float3(offsetX, 0,0));

	public PointDir endpoint (Junction for_junc, RoadLane lane) {
		var side = get_dir_to_junc(for_junc);
		var ep = endpoint(side, float3(lane.asset.shift, lane.asset.height, 0));
		ep.dir = -ep.dir;
		return ep;
	}

	// get bezier for lane
	// TODO: it's actually impossible to create accurate beziers relative to other beziers!
	// probably should use center bezier and eval_offset, but the math to get correct derivative and curvature might be hard or impossible
	public Bezier calc_path (RoadDir dir) {
		return dir == RoadDir.Forward ? bezier : bezier.reverse();
	}
	public OffsetBezier calc_path (RoadDir dir, float3 offset) {
		var offs_bez = bezier.offset(offset);
		return dir == RoadDir.Forward ? offs_bez : offs_bez.reverse();
	}
	public OffsetBezier calc_path (RoadLane lane) {
		return calc_path(lane.dir, float3(lane.asset.shift, lane.asset.height, 0));
	}
	#endregion
	
	public void highlight_junc_mesh (Junction junc, bool is_highlighted, Color tint) {
		var renderers = GetComponentsInChildren<MeshRenderer>();
		var renderer = junc == junc0 ? renderers[1] : renderers[2];
		foreach (var mat in renderer.sharedMaterials) {
			mat.SetVector("_AlbedoTint", tint);
		}
	}

	#region visualization
	private void OnDrawGizmosSelected () {
		var bounds = GetComponent<MeshRenderer>().bounds;
		Gizmos.color = Color.red;
		Gizmos.DrawWireCube(bounds.center, bounds.size);

		bezier.debugdraw(Color.red);

		foreach (var lane in all_lanes()) {
			var col = lane.dir == RoadDir.Forward ? Color.yellow : Color.blue;
			calc_path(lane).debugdraw(col);
		}

		//DebugVis.DebugCurvedMeshNormals(bezier, GetComponent<MeshFilter>().sharedMesh, transform);
		DebugMeshNormals.DrawOnGizmos(GetComponent<MeshFilter>().sharedMesh, transform.localToWorldMatrix);
	}
	#endregion
}

public enum RoadDir : byte { Forward, Backward }

public struct RoadLane {
	public readonly Road road;
	public readonly int lane_idx;

	public Road.Asset.Lane asset => road.asset.lanes[lane_idx];
	//public Road.Lane lane => road.lanes[lane_idx];

	public RoadDir dir => asset.dir;

	public RoadLane (Road road, int lane_idx) {
		this.road = road;
		this.lane_idx = lane_idx;
	}
}
