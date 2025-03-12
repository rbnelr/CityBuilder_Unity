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
			public RoadDirection dir;

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
	[System.Serializable]
	public class RenderMaterial {
		public Material mat;
		public Material[] mat_junc;
		public float2 texture_scale;

		public static RenderMaterial set (SubmeshMaterial m) {
			var r = new RenderMaterial();
			r.mat = new Material(m.mat);
			
			// copy textures etc, but use junction shader
			r.mat_junc = new[] { new Material(m.mat), new Material(m.mat) };
			r.mat_junc[0].shader = Shader.Find("Shader Graphs/Junction");
			r.mat_junc[1].shader = Shader.Find("Shader Graphs/Junction");

			r.texture_scale = m.texture_scale;
			return r;
		}
	};
	public SubmeshMaterial[] materials;
	public RenderMaterial[] render_materials;// { get; private set; }

	#region creation
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
		
		road.Init(refresh: false);

		junc0.Refresh();
		junc1.Refresh();

		// TODO: fix that this is needed!
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

		bezier = Bezier.from_line(junc0.position, junc1.position);

		float3 dir = normalizesafe(pos1 - pos0);

		pos0 += dir * junc0._radius;
		pos1 -= dir * junc1._radius;

		//Debug.DrawRay(pos0 + float3(0,1,0), tangent0, Color.red, 0);
		//Debug.DrawRay(pos1 + float3(0,1,0), tangent1, Color.blue, 0);

		if (refresh)
			Refresh();
	}

	[NaughtyAttributes.Button("refresh_mesh")]
	public void refresh_mesh () {
		render_materials = materials.Select(x => RenderMaterial.set(x)).ToArray();

		float road_center_length = bezier.approx_len();
		
		refresh_main_mesh();
		refresh_junc_mesh();

		var renderers = GetComponentsInChildren<MeshRenderer>();
		renderers[0].materials = render_materials.Select(x => x.mat).ToArray();
		renderers[1].materials = render_materials.Select(x => x.mat_junc[0]).ToArray();
		renderers[2].materials = render_materials.Select(x => x.mat_junc[1]).ToArray();

		renderers[1].enabled = junc0.roads.Length >= 1;
		renderers[2].enabled = junc1.roads.Length >= 1;

		refresh_bounds();
	}

	void refresh_main_mesh () {
		float length = bezier.approx_len();

		foreach (var m in render_materials) {
			m.mat.SetVector("_BezierA", (Vector3)bezier.a);
			m.mat.SetVector("_BezierB", (Vector3)bezier.b);
			m.mat.SetVector("_BezierC", (Vector3)bezier.c);
			m.mat.SetVector("_BezierD", (Vector3)bezier.d);
			
			bool worldspace = m.mat.GetInt("_WorldspaceTextures") != 0;
			
			float2 scale = m.texture_scale;
			if (!worldspace)
				scale.x /= length;
			m.mat.SetVector("_TextureScale", (Vector2)scale);
		}

		float base_asph_w = 3.0f;

		float2 asph_scale = float2(-asset.sidewalkL, asset.sidewalkR) / base_asph_w;
		float2 sidew_shift = float2(asset.sidewalkL, asset.sidewalkR) - float2(-base_asph_w, base_asph_w);
		float2 sidew_scale = float2(asset.sidewalkL, asset.sidewalkR) - float2(-base_asph_w, base_asph_w);

		render_materials[0].mat.SetVector("_TransfX", float4(asph_scale,0,0));
		render_materials[1].mat.SetVector("_TransfX", float4(1,1,sidew_shift));
		render_materials[2].mat.SetVector("_TransfX", float4(1,1,sidew_shift));
	}
	void refresh_junc_mesh () {

		void set_mat (Junction junc, int i) {
			Bezier bezL, bezR;

			if (junc.roads.Length <= 1) {
				var dir = get_dir_to_junc(junc);
				var middle = calc_path(dir, 0).eval(1);
				var left   = calc_path(dir, float3(asset.edgeL,0,0)).eval(1);
				var right  = calc_path(dir, float3(asset.edgeR,0,0)).eval(1);

				bezL = Bezier.from_quarter_circle(middle.pos, middle.forw, left.pos);
				bezR = Bezier.from_quarter_circle(middle.pos, middle.forw, right.pos);
			}
			else {
				var neighb = junc.find_neighbours(this);
				bezL = RoadGeometry.calc_curve(junc, this, neighb.left);
				bezR = RoadGeometry.calc_curve(junc, this, neighb.right);
			}

			float lengthL = bezL.approx_len();
			float lengthR = bezR.approx_len();
			float length = 0.5f * (lengthL + lengthR);

			foreach (var m in render_materials) {
				m.mat_junc[i].SetVector("_BezierL_A", (Vector3)bezL.a);
				m.mat_junc[i].SetVector("_BezierL_B", (Vector3)bezL.b);
				m.mat_junc[i].SetVector("_BezierL_C", (Vector3)bezL.c);
				m.mat_junc[i].SetVector("_BezierL_D", (Vector3)bezL.d);

				m.mat_junc[i].SetVector("_BezierR_A", (Vector3)bezR.a);
				m.mat_junc[i].SetVector("_BezierR_B", (Vector3)bezR.b);
				m.mat_junc[i].SetVector("_BezierR_C", (Vector3)bezR.c);
				m.mat_junc[i].SetVector("_BezierR_D", (Vector3)bezR.d);
				
				m.mat_junc[i].SetVector("_JunctionCenter", (Vector3)junc.position);
			
				bool worldspace = m.mat_junc[i].GetInt("_WorldspaceTextures") != 0;
			
				float2 scale = m.texture_scale;
				if (!worldspace)
					scale.x /= length;
				m.mat_junc[i].SetVector("_TextureScale", (Vector2)scale);
			}

			float base_asph_w = 3.0f;

			float2 asph_scale = float2(-asset.sidewalkL, asset.sidewalkR) / base_asph_w;
			float2 sidew_shift = float2(asset.sidewalkL, asset.sidewalkR) - float2(-base_asph_w, base_asph_w);
			float2 sidew_scale = float2(asset.sidewalkL, asset.sidewalkR) - float2(-base_asph_w, base_asph_w);

			render_materials[0].mat_junc[i].SetVector("_TransfX", float4(asph_scale,0,0));
			render_materials[1].mat_junc[i].SetVector("_TransfX", float4(1,1,sidew_shift));
			render_materials[2].mat_junc[i].SetVector("_TransfX", float4(1,1,sidew_shift));
		}
		set_mat(junc0, 0);
		set_mat(junc1, 1);
		
		var renderers = GetComponentsInChildren<MeshRenderer>();
		renderers[1].bounds = junc0.calc_bounds();
		renderers[2].bounds = junc1.calc_bounds();
	}

	void refresh_bounds () {
		var renderers = GetComponentsInChildren<MeshRenderer>();

		var bounds = bezier.approx_road_bounds(-asset.width/2, +asset.width/2, -2, +5); // calculate xz bounds based on width
		bounds.Expand(float3(1,0,1)); // extend xz by a little to catch mesh overshoot
		renderers[0].bounds = bounds;

		var coll = GetComponent<BoxCollider>();
		coll.center = bounds.center;
		coll.size   = bounds.size;
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
	public IEnumerable<RoadLane> lanes_in_dir (RoadDirection dir) {
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

	public ref float3 pos_for_junction (Junction junc) => ref junc0 == junc ? ref pos0 : ref pos1;
	
	public RoadDirection get_dir_to_junc (Junction junc) {
		Debug.Assert(junc != null && (junc == junc0 || junc == junc1));
		return junc0 != junc ? RoadDirection.Forward : RoadDirection.Backward;
	}
	public RoadDirection get_dir_from_junc (Junction junc) {
		Debug.Assert(junc != null && (junc == junc0 || junc == junc1));
		return junc0 == junc ? RoadDirection.Forward : RoadDirection.Backward;
	}

	// get bezier for lane
	// TODO: it's actually impossible to create accurate beziers relative to other beziers!
	// probably should use center bezier and eval_offset, but the math to get correct derivative and curvature might be hard or impossible
	public Bezier calc_path (RoadDirection dir) {
		return dir == RoadDirection.Forward ? bezier : bezier.reverse();
	}
	public OffsetBezier calc_path (RoadDirection dir, float3 offset) {
		var offs_bez = bezier.offset(offset);
		return dir == RoadDirection.Forward ? offs_bez : offs_bez.reverse();
	}
	public OffsetBezier calc_path (RoadLane lane) {
		return calc_path(lane.dir, float3(lane.asset.shift, lane.asset.height, 0));
	}
	#endregion

	#region visualization
	private void OnDrawGizmosSelected () {
		var bounds = GetComponent<MeshRenderer>().bounds;
		Gizmos.color = Color.red;
		Gizmos.DrawWireCube(bounds.center, bounds.size);

		Gizmos.color = Color.red;
		bezier.debugdraw();

		foreach (var lane in all_lanes()) {
			Gizmos.color = lane.dir == RoadDirection.Forward ? Color.yellow : Color.blue;
			calc_path(lane).debugdraw();
		}

		//DebugVis.DebugCurvedMeshNormals(bezier, GetComponent<MeshFilter>().sharedMesh, transform);
		DebugMeshNormals.DrawOnGizmos(GetComponent<MeshFilter>().sharedMesh, transform.localToWorldMatrix);
	}
	#endregion
}

public enum RoadDirection : byte { Forward, Backward }

public struct RoadLane {
	public readonly Road road;
	public readonly int lane_idx;

	public Road.Asset.Lane asset => road.asset.lanes[lane_idx];
	//public Road.Lane lane => road.lanes[lane_idx];

	public RoadDirection dir => asset.dir;

	public RoadLane (Road road, int lane_idx) {
		this.road = road;
		this.lane_idx = lane_idx;
	}
}
