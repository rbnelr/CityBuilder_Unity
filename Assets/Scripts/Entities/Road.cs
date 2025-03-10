using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;
using System;

public class Road : MonoBehaviour {
	public Junction junc0;
	public Junction junc1;

	public float3 pos0;
	public float3 pos1;

	public float3 tangent0;
	public float3 tangent1;

	public float3 pos0_ctrl => pos0 + tangent0;
	public float3 pos1_ctrl => pos1 + tangent1;

	public Bezier bezier {
		get { return new Bezier(pos0, pos0_ctrl, pos1_ctrl, pos1); }
		set {
			pos0 = value.a;
			tangent0 = value.b - value.a;
			tangent1 = value.c - value.d;
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
	};
	[System.Serializable]
	public class RenderMaterial {
		public Material mat;
		public Material mat_junc0;
		public Material mat_junc1;
		public float2 texture_scale;

		public static RenderMaterial set (SubmeshMaterial m) {
			var r = new RenderMaterial();
			r.mat = new Material(m.mat);
			
			// copy textures etc, but use junction shader
			r.mat_junc0 = new Material(m.mat);
			r.mat_junc0.shader = Shader.Find("Shader Graphs/Junction");
			r.mat_junc1 = new Material(m.mat);
			r.mat_junc1.shader = Shader.Find("Shader Graphs/Junction");

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
	
	//void Start () { // Handle copy pasting roads for testing purposed (pseudo-serialized persitance)
	//	Init();
	//}

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

		float dist = length(pos1 - pos0);
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
		renderers[1].materials = render_materials.Select(x => x.mat_junc0).ToArray();
		renderers[2].materials = render_materials.Select(x => x.mat_junc1).ToArray();

		renderers[1].enabled = junc0.roads.Length >= 2;
		renderers[2].enabled = junc1.roads.Length >= 2;

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
	}
	void refresh_junc_mesh () {
		{
			var junc = junc0;

			var neighb = find_neighbours(junc);

			Bezier bezL = junc.calc_curve(this, neighb.left);
			Bezier bezR = junc.calc_curve(this, neighb.right);
			float lengthL = bezL.approx_len();
			float lengthR = bezR.approx_len();
			float length = 0.5f * (lengthL + lengthR);

			foreach (var m in render_materials) {
				m.mat_junc0.SetVector("_BezierL_A", (Vector3)bezL.a);
				m.mat_junc0.SetVector("_BezierL_B", (Vector3)bezL.b);
				m.mat_junc0.SetVector("_BezierL_C", (Vector3)bezL.c);
				m.mat_junc0.SetVector("_BezierL_D", (Vector3)bezL.d);

				m.mat_junc0.SetVector("_BezierR_A", (Vector3)bezR.a);
				m.mat_junc0.SetVector("_BezierR_B", (Vector3)bezR.b);
				m.mat_junc0.SetVector("_BezierR_C", (Vector3)bezR.c);
				m.mat_junc0.SetVector("_BezierR_D", (Vector3)bezR.d);
				
				m.mat_junc0.SetVector("_JunctionCenter", (Vector3)junc.position);
			
				bool worldspace = m.mat_junc0.GetInt("_WorldspaceTextures") != 0;
			
				float2 scale = m.texture_scale;
				if (!worldspace)
					scale.x /= length;
				m.mat_junc0.SetVector("_TextureScale", (Vector2)scale);
			}
		}
		{
			var junc = junc1;

			var road_idx = Array.IndexOf(junc.roads, this);
			var roadL = junc.roads[MyMath.wrap(road_idx-1, junc.roads.Length)];
			var roadR = junc.roads[MyMath.wrap(road_idx+1, junc.roads.Length)];

			Bezier bezL = junc.calc_curve(this, roadL);
			Bezier bezR = junc.calc_curve(this, roadR);
			float lengthL = bezL.approx_len();
			float lengthR = bezR.approx_len();
			float length = 0.5f * (lengthL + lengthR);

			foreach (var m in render_materials) {
				m.mat_junc1.SetVector("_BezierL_A", (Vector3)bezL.a);
				m.mat_junc1.SetVector("_BezierL_B", (Vector3)bezL.b);
				m.mat_junc1.SetVector("_BezierL_C", (Vector3)bezL.c);
				m.mat_junc1.SetVector("_BezierL_D", (Vector3)bezL.d);

				m.mat_junc1.SetVector("_BezierR_A", (Vector3)bezR.a);
				m.mat_junc1.SetVector("_BezierR_B", (Vector3)bezR.b);
				m.mat_junc1.SetVector("_BezierR_C", (Vector3)bezR.c);
				m.mat_junc1.SetVector("_BezierR_D", (Vector3)bezR.d);
				
				m.mat_junc1.SetVector("_JunctionCenter", (Vector3)junc.position);
			
				bool worldspace = m.mat_junc1.GetInt("_WorldspaceTextures") != 0;
			
				float2 scale = m.texture_scale;
				if (!worldspace)
					scale.x /= length;
				m.mat_junc1.SetVector("_TextureScale", (Vector2)scale);
			}
		}
		
		var renderers = GetComponentsInChildren<MeshRenderer>();
		renderers[1].bounds = junc0.calc_bounds();
		renderers[2].bounds = junc1.calc_bounds();
	}

	void refresh_bounds () {
		var renderers = GetComponentsInChildren<MeshRenderer>();

		var bounds = bezier.approx_road_bounds(-width/2, +width/2, -2, +5); // calculate xz bounds based on width
		bounds.Expand(float3(1,0,1)); // extend xz by a little to catch mesh overshoot
		renderers[0].bounds = bounds;

		var coll = GetComponent<BoxCollider>();
		coll.center = bounds.center;
		coll.size   = bounds.size;
	}
	
#if false
	public void reset_endpoint (Junction junc) {
		if (junc.roads.Length <= 1) {
			if (junc == junc0) pos0 = junc.position;
			else               pos1 = junc.position;
			return;
		}

		float additional_radius = 1;
		
		//float max_dist = distance(junc0.position, junc1.position) * 0.5f;
		float max_dist = distance(junc0.position, junc1.position) * 0.5f;
		float min_dist = junc._radius + additional_radius;
		float dist = min(min_dist, max_dist);
		
		{
			if (junc == junc0) pos0 = junc.position + dist * normalizesafe(tangent0);
			else               pos1 = junc.position + dist * normalizesafe(tangent1);
		}
	}
#else
	public void reset_endpoint (Junction junc) {
		if (junc.roads.Length <= 1) {
			if (junc == junc0) pos0 = junc.position;
			else               pos1 = junc.position;
			return;
		}

		float additional_radius = 0; // TODO: get from junction?
		var neighb = find_neighbours(junc);
		
		(float3 dir, float3 right, float3 posL, float3 posR) clac_seg (Road road) {
			bool forw = junc == road.junc0;
			float eL = forw ? road.edgeL : -road.edgeR; // need mirror road params if direction points away from node
			float eR = forw ? road.edgeR : -road.edgeL;

			var pos = (forw ? road.pos0 : road.pos1) - junc.position;
			var dir = normalizesafe(forw ? road.tangent0 : road.tangent1);
			var right = -MyMath.rotate90_right(dir); // right as seen facing into junc
			var posL = pos + right * eL; // left edge of road going into intersection
			var posR = pos + right * eR; // right edge

			// force 2d
			dir.xz = normalize(dir.xz);
			dir.y = 0;

			return (dir, right, posL, posR);
		}
		
		var l = clac_seg(neighb.left);
		var s = clac_seg(this);
		var r = clac_seg(neighb.right);

		//int debug_time = 0;
		//if (this == junc.roads[0]) {
		//	Debug.DrawRay(junc.position,          s.dir*10,    new Color(1,0,0,1), debug_time);
		//	Debug.DrawRay(junc.position,          s.right*10,  new Color(0,1,0,1), debug_time);
		//	Debug.DrawRay(junc.position + s.posL, s.dir*10, new Color(0,0,1,1), debug_time);
		//	Debug.DrawRay(junc.position + s.posR, s.dir*10, new Color(0,1,1,1), debug_time);
		//	//Debug.DrawRay(junc.position + l.posR, l.dir*10, new Color(1,0,1,1), debug_time);
		//	//Debug.DrawRay(junc.position + r.posL, r.dir*10, new Color(1,0,1,1), debug_time);
		//}

		float dist = max(1.0f, additional_radius);
		float max_dist = distance(junc0.position, junc1.position) * 0.5f;
		
		// intersection of neighbouring road edges relative to junction
		if (MyMath.line_line_intersect(s.posL.xz, s.dir.xz, l.posR.xz, l.dir.xz, out var inters)) {
			//if (this == junc.roads[0])
			//	Debug.DrawRay(junc.position + float3(inters.x,0,inters.y), float3(0,5,0), Color.red, debug_time);

			dist = max(dist, dot(inters, s.dir.xz));
		}
		if (MyMath.line_line_intersect(s.posR.xz, s.dir.xz, r.posL.xz, r.dir.xz, out var inters2)) {
			//if (this == junc.roads[0])
			//	Debug.DrawRay(junc.position + float3(inters2.x,0,inters2.y), float3(0,5,0), Color.yellow, debug_time);

			dist = max(dist, dot(inters2, s.dir.xz));
		}

		dist = min(dist, max_dist);

		float2 new_pos2d = junc.position.xz + s.dir.xz * dist;

		var point = float3(new_pos2d.x, junc.position.y, new_pos2d.y);
		if (junc == junc0) pos0 = point;
		else               pos1 = point;
	}
#endif
	#endregion
	
	#region helpers
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

	public float3 pos_for_junction (Junction junc) => junc0 == junc ? pos0 : pos1;
	
	public RoadDirection get_dir_to_junc (Junction junc) {
		Debug.Assert(junc != null && (junc == junc0 || junc == junc1));
		return junc0 != junc ? RoadDirection.Forward : RoadDirection.Backward;
	}
	public RoadDirection get_dir_from_junc (Junction junc) {
		Debug.Assert(junc != null && (junc == junc0 || junc == junc1));
		return junc0 == junc ? RoadDirection.Forward : RoadDirection.Backward;
	}
	
	(Road left, Road right) find_neighbours (Junction junc) {
		var road_idx = Array.IndexOf(junc.roads, this);
		return (
			left:  junc.roads[MyMath.wrap(road_idx-1, junc.roads.Length)],
			right: junc.roads[MyMath.wrap(road_idx+1, junc.roads.Length)]
		);
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
	public OffsetBezier calc_path (Lane lane) {
		return calc_path(lane.dir, float3(lane.shift, lane.height, 0));
	}
	#endregion

	#region visualization
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
	#endregion
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
