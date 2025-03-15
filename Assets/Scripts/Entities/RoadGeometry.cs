using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEditor;

public class RoadGeometry {
	
	//public static Bezier calc_curve (Junction junc, Road road0, Road road1) {
	//	return calc_curve(road0.endpoint(junc, 0), road1.endpoint(junc, 0), junc.test_curv);
	//}
	public static Bezier calc_curve (Junction junc, RoadLane lane0, RoadLane lane1) {
		var p0 = lane0.road.endpoint(junc, lane0);
		var p1 = lane1.road.endpoint(junc, lane1);
		if (lane0.road == lane1.road) {
			// uturn
			return Bezier.from_half_circle(p0.pos, p0.dir, (p0.pos+p1.pos)*0.5f);
		}
		else {
			return calc_curve(p0, p1, junc.test_curv);
		}
	}
	public static Bezier calc_curve (PointDir p0, PointDir p1, float curve_k=0.6667f) {
		
		//Debug.DrawLine(p0.pos, p0.pos+p0.dir, Color.magenta);
		//Debug.DrawLine(p1.pos, p1.pos+p1.dir, Color.magenta);

		float cos_ang = abs(dot(p0.dir.xz, -p1.dir.xz));
		bool is_straight = cos_ang >= cos(radians(1.0f));

		float3 ctrl_in, ctrl_out;
		// Find straight line intersection of in/out lanes with their tangents
		if (!is_straight && MyMath.line_line_intersect(p0.pos.xz, p0.dir.xz, p1.pos.xz, -p1.dir.xz, out float2 point)) {
			//Debug.DrawLine(float3(point.x, 0, point.y), float3(point.x, 1, point.y), Color.yellow);

			ctrl_in  = float3(point.x, p0.pos.y, point.y);
			ctrl_out = float3(point.x, p1.pos.y, point.y);
		}
		// Come up with seperate control points TODO: how reasonable is this?
		else {
			float dist = distance(p0.pos, p1.pos) * 0.5f;
			ctrl_in  = p0.pos + float3(p0.dir.x, 0, p0.dir.z) * dist;
			ctrl_out = p1.pos + float3(p1.dir.x, 0, p1.dir.z) * dist;
		}

		Bezier bez = new Bezier(
			p0.pos,
			lerp(p0.pos, ctrl_in , curve_k),
			lerp(p1.pos, ctrl_out, curve_k),
			p1.pos
		);
		return bez;
	}

	/*
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
	*/

	public static void reset_endpoint (Road road, Junction junc) {
		if (junc.roads.Length <= 1) {
			road.pos_for_junction(junc) = junc.position;
			return;
		}

		float additional_radius = 1; // TODO: get from junction?
		var neighb = junc.find_neighbours(road);
		
		(float3 dir, float3 right, float3 posL, float3 posR) clac_seg (Road road) {
			float eL = junc == road.junc0 ? road.asset.edgeL : -road.asset.edgeR; // need mirror road params if direction points away from node
			float eR = junc == road.junc0 ? road.asset.edgeR : -road.asset.edgeL;

			var pos = road.pos_for_junction(junc) - junc.position;
			var dir = normalizesafe(road.tangent_for_junction(junc));
			var right = -MyMath.rotate90_right(dir); // right as seen facing into junc
			var posL = pos + right * eL; // left edge of road going into intersection
			var posR = pos + right * eR; // right edge

			// force 2d
			dir.xz = normalize(dir.xz);
			dir.y = 0;

			return (dir, right, posL, posR);
		}
		
		var l = clac_seg(neighb.left);
		var s = clac_seg(road);
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

		float dist = 1.0f;
		float max_dist = distance(road.junc0.position, road.junc1.position) * 0.5f;
		
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
		dist += additional_radius;

		float2 new_pos2d = junc.position.xz + s.dir.xz * dist;

		road.pos_for_junction(junc) = float3(new_pos2d.x, junc.position.y, new_pos2d.y);
	}
	
#region meshing

	static void set_mat_bez (Material mat, string name, Bezier bez) {
		//mat.SetVector($"{name}_A", (Vector3)bez.a);
		//mat.SetVector($"{name}_B", (Vector3)bez.b);
		//mat.SetVector($"{name}_C", (Vector3)bez.c);
		//mat.SetVector($"{name}_D", (Vector3)bez.d);

		// Encode Bezier as 4x4 matrix to avoid me going crazy with too many vector params in shader graph
		Matrix4x4 m = new Matrix4x4();
		m.SetRow(0, float4(bez.a, 0));
		m.SetRow(1, float4(bez.b, 0));
		m.SetRow(2, float4(bez.c, 0));
		m.SetRow(3, float4(bez.d, 0));
		mat.SetMatrix(name, m);
	}
	
	static void uv_tiling (Road.SubmeshMaterial m, Material rm, float lenL, float lenR) {
		bool worldspace = rm.GetInt("_WorldspaceTextures") != 0;

		float4 scale = float4(m.texture_scale, m.texture_scale);
		if (worldspace) {
			scale = 1.0f / scale;
		} else {
			float repeatsL = lenL / scale.x;
			float repeatsR = lenR / scale.x;
			repeatsL = clamp((int)round(repeatsL), 1, 1000);
			repeatsR = clamp((int)round(repeatsR), 1, 1000);

			scale.x = repeatsL;
			scale.z = repeatsR;
		}
		rm.SetVector("_TextureScale", (Vector4)scale);
		rm.SetVector("_TextureOffset", (Vector4)float4(0));
	}

	// refresh road mesh, which is fit to road beziers in vertex shader, by setting up materials
	public static Material[] refresh_main_mesh (Road road) {
		var mats = road.make_road_mats();
		
		var BACK = RoadDir.Backward;
		var FORW = RoadDir.Forward;

		var bezL0 = calc_curve(road.endpoint(BACK, road.asset.edgeL    ), road.endpoint(FORW, road.asset.edgeL    ));
		var bezL1 = calc_curve(road.endpoint(BACK, road.asset.sidewalkL), road.endpoint(FORW, road.asset.sidewalkL));
		var bezR0 = calc_curve(road.endpoint(BACK, road.asset.sidewalkR), road.endpoint(FORW, road.asset.sidewalkR));
		var bezR1 = calc_curve(road.endpoint(BACK, road.asset.edgeR    ), road.endpoint(FORW, road.asset.edgeR    ));
		
		{
			bezL0.debugdraw(Color.red);
			bezL1.debugdraw(Color.green);
			bezR0.debugdraw(Color.green);
			bezR1.debugdraw(Color.red);
		}

		foreach (var (m, rm) in road.materials.Zip(mats, (x,y) => (x,y))) {
			set_mat_bez(rm, "_BezierL0", bezL0);
			set_mat_bez(rm, "_BezierL1", bezL1);
			set_mat_bez(rm, "_BezierR0", bezR0);
			set_mat_bez(rm, "_BezierR1", bezR1);
			
			rm.SetVector("_AlbedoTint", road.tint);
		}
		
		float road_len = road.bezier.approx_len();
		float sidewL_len = MyMath.avg(bezL0.approx_len(), bezL1.approx_len());
		float sidewR_len = MyMath.avg(bezR0.approx_len(), bezR1.approx_len());
		
		uv_tiling(road.materials[0], mats[0], road_len, road_len);
		uv_tiling(road.materials[1], mats[1], sidewL_len, sidewR_len);
		uv_tiling(road.materials[2], mats[2], sidewL_len, sidewR_len);

		return mats;
	}
	// refresh road junction mesh pieces, can call for lone junction without mesh (pass prefab for road)
	public static Material[] refresh_junc_mesh (Junction junc, Road road, float junc_forw=0, Color? override_tint=null) {
		var mats = road.make_junc_mats();

		Bezier bezL0, bezL1, bezR0, bezR1;
		float3 center;

		void semicircle (float3 middle, float3 forw, float3 l0, float3 l1, float3 r0, float3 r1) {
			bezL0 = Bezier.from_quarter_circle(l0, forw, middle);
			bezL1 = Bezier.from_quarter_circle(l1, forw, middle);
			bezR0 = Bezier.from_quarter_circle(r0, forw, middle);
			bezR1 = Bezier.from_quarter_circle(r1, forw, middle);

			center = middle + forw * 0.5f; // Move center point off of start point to avoid bezier.vel == 0 causing 0 matrix
		}
		
		if (junc.roads.Length == 0) {
			// TODO: test and fix for asym edge/sidewalk, use similar as roads.Length == 1

			var middle = junc.position;
			var forw   = float3(0,0,junc_forw);
			var left   = junc.position + junc_forw * float3(road.asset.edgeL, 0,0);
			var leftS  = junc.position + junc_forw * float3(road.asset.sidewalkL, 0,0);
			var rightS = junc.position + junc_forw * float3(road.asset.sidewalkR, 0,0);
			var right  = junc.position + junc_forw * float3(road.asset.edgeR, 0,0);
			
			semicircle(middle, forw, left, leftS, rightS, right);
		}
		else if (junc.roads.Length == 1) {
			// TODO: test and fix for asym edge/sidewalk x ie. left sidewalk 5m, right 2 will not transition properly

			var middle = road.endpoint(junc, 0);
			var left   = road.endpoint(junc, road.asset.edgeL).pos;
			var leftS  = road.endpoint(junc, road.asset.sidewalkL).pos;
			var rightS = road.endpoint(junc, road.asset.sidewalkR).pos;
			var right  = road.endpoint(junc, road.asset.edgeR).pos;
			
			semicircle(middle.pos, middle.dir, left, leftS, rightS, right);
		}
		else {
			(var L, var R) = junc.find_neighbours(road);
			
			(float l, float r) getEdges (Road r) => r.get_dir_to_junc(junc) == RoadDir.Forward ?
				(r.asset.edgeL, r.asset.edgeR) : (-r.asset.edgeR, -r.asset.edgeL);
			(float l, float r) getSidew (Road r) => r.get_dir_to_junc(junc) == RoadDir.Forward ?
				(r.asset.sidewalkL, r.asset.sidewalkR) : (-r.asset.sidewalkR, -r.asset.sidewalkL);
		
			bezL0 = calc_curve(road.endpoint(junc, getEdges(road).l), L.endpoint(junc, getEdges(L).r), junc.test_curv);
			bezL1 = calc_curve(road.endpoint(junc, getSidew(road).l), L.endpoint(junc, getSidew(L).r), junc.test_curv);
			bezR0 = calc_curve(road.endpoint(junc, getSidew(road).r), R.endpoint(junc, getSidew(R).l), junc.test_curv);
			bezR1 = calc_curve(road.endpoint(junc, getEdges(road).r), R.endpoint(junc, getEdges(R).l), junc.test_curv);

			bezL0 = bezL0.subdiv(0.5f).first;
			bezL1 = bezL1.subdiv(0.5f).first;
			bezR0 = bezR0.subdiv(0.5f).first;
			bezR1 = bezR1.subdiv(0.5f).first;

			center = junc.position; // TODO: 
		}

		//if (junc.roads.Length > 0 && road == junc.roads[0]) {
		//{
		//	bezL0.debugdraw(Color.red);
		//	bezL1.debugdraw(Color.green);
		//	bezR0.debugdraw(Color.green);
		//	bezR1.debugdraw(Color.red);
		//}

		float lengthL = bezL0.approx_len();
		float lengthR = bezR0.approx_len();
		float length = MyMath.avg(lengthL, lengthR);
		
		foreach (var (m, rm) in road.materials.Zip(mats, (x,y) => (x,y))) {
			set_mat_bez(rm, "_BezierL0", bezL0);
			set_mat_bez(rm, "_BezierL1", bezL1);
			set_mat_bez(rm, "_BezierR0", bezR0);
			set_mat_bez(rm, "_BezierR1", bezR1);
			
			rm.SetVector("_JunctionCenter", (Vector3)center);
			
			rm.SetVector("_AlbedoTint", override_tint ?? road.tint);
		}

		float road_len = 10; // TODO: fix
		float sidewL_len = MyMath.avg(bezL0.approx_len(), bezL1.approx_len());
		float sidewR_len = MyMath.avg(bezR0.approx_len(), bezR1.approx_len());
		
		uv_tiling(road.materials[0], mats[0], road_len, road_len);
		uv_tiling(road.materials[1], mats[1], sidewL_len, sidewR_len);
		uv_tiling(road.materials[2], mats[2], sidewL_len, sidewR_len);

		return mats;
	}
#endregion
}
