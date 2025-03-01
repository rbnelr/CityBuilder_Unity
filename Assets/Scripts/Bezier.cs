﻿using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

public struct Bezier {
	public float3 a, b, c, d;
	
	public Bezier (float3 a, float3 b, float3 c, float3 d) {
		this.a = a;
		this.b = b;
		this.c = c;
		this.d = d;
	}

	public static Bezier from_line (float3 a, float3 b) {
		return new Bezier(a, lerp(a,b,0.333333f), lerp(a,b,0.666667f), b);
	}

	public struct PosVel {
		public float3 pos;
		public float3 vel; // velocity (delta position / delta bezier t)
	}
	public PosVel eval (float t) {
		float3 c0 = a;                   // a
		float3 c1 = 3 * (b - a);         // (-3a +3b)t
		float3 c2 = 3 * (a + c) - 6*b;   // (3a -6b +3c)t^2
		float3 c3 = 3 * (b - c) - a + d; // (-a +3b -3c +d)t^3

		float t2 = t*t;
		float t3 = t2*t;
		
		float3 value = c3*t3     + c2*t2    + c1*t + c0; // f(t)
		float3 deriv = c3*(t2*3) + c2*(t*2) + c1;        // f'(t)
		
		return new PosVel { pos=value, vel=deriv };
	}
	
	public void debugdraw (int res=10) {
		float3 prev = a;

		for (int i=0; i<res; i++) {
			float t = (float)(i+1) * (1.0f / res);
			float3 pos = eval(t).pos;

			if (i < res-1) Gizmos.DrawLine(prev, pos);
			else           Util.GizmosDrawArrow(prev, pos-prev, 1);

			prev = pos;
		}
	}

	public Bezier reverse () {
		return new Bezier(d,c,b,a);
	}
	
	public float approx_len (int res=10) {
		float3 prev = a;

		float len = 0;
		for (int i=0; i<res; ++i) {
			float t = (float)(i+1) * (1.0f / res);
			float3 pos = eval(t).pos;

			len += length(pos - prev);

			prev = pos;
		}

		return len;
	}

	// approximate bounds of bezier-based road with x0,x1 being left-right extents following curve
	// and h0,h1 being bottom-top extents (always straight up/down)
	public Bounds approx_road_bounds (float x0, float x1, float h0, float h1, int res=10) {
		float3 lo = float.PositiveInfinity;
		float3 hi = float.NegativeInfinity;
		
		for (int i=0; i<res+1; ++i) {
			float t = (float)i * (1.0f / res);

			var bez_res = eval(t);
			var mat = rotate_to_direction(bez_res.vel);
			var p0 = bez_res.pos + mul(mat, float3(x0, 0,0));
			var p1 = bez_res.pos + mul(mat, float3(x1, 0,0));

			lo = min(lo, min(p0, p1));
			hi = max(hi, max(p0, p1));
		}

		lo.y += h0;
		hi.y += h1;

		var bounds = new Bounds();
		bounds.SetMinMax(lo, hi);
		return bounds;
	}
	
	static float3x3 rotate_to_direction (float3 forw) {
		forw = normalize(forw);
		float3 up = float3(0,1,0);
		float3 right = cross(up, forw);
	
		up = normalize(cross(forw, right));
		right = normalize(right);
	
		// unlike hlsl float3x3 takes columns already!
		return float3x3(right, up, forw);
	}
	// TODO: seperate t?
	public void curve_mesh (float3 pos_obj, float3 norm_obj, float3 tang_obj,
			out float3 pos_out, out float3 norm_out, out float3 tang_out) {
		// NOTE: distorting/extruding the mesh along a bezier like this results in technically not-correct normals
		// while the normals are curved correctly, the mesh is streched/squashed along the length of the bezier
		// so any normals pointing forwards or backwards relative to the bezier, will be wrong, just like a sphere scaled on one axis will have wrong normals
		// it might be possible to fix this based on an estimate of streching along this axis?
	
		// fix X and Z being flipped when importing mesh here
		pos_obj  *= float3(-1,1,-1 / 20.0f); // mesh currently 20 units long
		norm_obj *= float3(-1,1,-1);
		tang_obj *= float3(-1,1,-1);
		float t = pos_obj.z;
		
		var res = eval(t);
	
		float3x3 rotate_to_bezier = rotate_to_direction(res.vel);
	
		pos_out = res.pos + mul(rotate_to_bezier, float3(pos_obj.xy,0));
		norm_out = mul(rotate_to_bezier, norm_obj);
		tang_out = mul(rotate_to_bezier, tang_obj);
	}
	public float3 follow_curve (float t, float3 pos) {
		var res = eval(t);
		return res.pos + mul(rotate_to_direction(res.vel), pos);
	}
}
