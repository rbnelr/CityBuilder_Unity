using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class RoadGeometry {
	
	public static Bezier calc_curve (Junction junc, Road road0, Road road1) {
		var dir0 = road0.get_dir_to_junc(junc);
		var dir1 = road1.get_dir_from_junc(junc);

		var p0 = road0.calc_path(dir0, 0).eval(1);
		var p1 = road1.calc_path(dir1, 0).eval(0);
		return calc_curve(p0.pos, p0.forw, p1.pos, p1.forw, junc.test_curv);
	}
	public static Bezier calc_curve (Junction junc, RoadLane lane0, RoadLane lane1) {
		var p0 = lane0.road.calc_path(lane0).eval(1);
		var p1 = lane1.road.calc_path(lane1).eval(0);
		return calc_curve(p0.pos, p0.forw, p1.pos, p1.forw, junc.test_curv);
	}
	public static Bezier calc_curve (float3 pos0, float3 dir0, float3 pos1, float3 dir1, float curve_k=0.6667f) {
		
		Debug.DrawLine(pos0, pos0+dir0, Color.magenta);
		Debug.DrawLine(pos1, pos1+dir1, Color.magenta);

		float3 ctrl_in, ctrl_out;
		// Find straight line intersection of in/out lanes with their tangents
		if (MyMath.line_line_intersect(pos0.xz, dir0.xz, pos1.xz, -dir1.xz, out float2 point)) {
			Debug.DrawLine(float3(point.x, 0, point.y), float3(point.x, 1, point.y), Color.yellow);

			ctrl_in  = float3(point.x, pos0.y, point.y);
			ctrl_out = float3(point.x, pos1.y, point.y);
		}
		// Come up with seperate control points TODO: how reasonable is this?
		else {
			float dist = distance(pos0, pos1) * 0.5f;
			ctrl_in  = pos0 + float3(dir0.x, 0, dir0.z) * dist;
			ctrl_out = pos1 - float3(dir1.x, 0, dir1.z) * dist;
		}

		// NOTE: for quarter circle turns k=0.5539 would result in almost exactly a quarter circle!
		// https://pomax.github.io/bezierinfo/#circles_cubic
		// but turns that are sharper in the middle are more realistic, but we could make this customizable?
		//float curve_k = 0.6667f;

		Bezier bez = new Bezier(
			pos0,
			lerp(pos0, ctrl_in , curve_k),
			lerp(pos1, ctrl_out, curve_k),
			pos1
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
			var dir = normalizesafe(junc == road.junc0 ? road.tangent0 : road.tangent1);
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
}
