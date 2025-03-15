using UnityEngine;
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
	
	// NOTE: for quarter circle turns k=0.5539 results in almost exactly a quarter circle!
	// https://pomax.github.io/bezierinfo/#circles_cubic
	public const float quarter_circle_k = 0.5539f;
	public const float half_circle_k = 1.3333333f;

	public Bezier reverse () {
		return new Bezier(d,c,b,a);
	}
	public static Bezier from_line (float3 a, float3 b) {
		return new Bezier(a, lerp(a,b,0.333333f), lerp(a,b,0.666667f), b);
	}
	public static Bezier from_quarter_circle (float3 start, float3 forw, float3 center) {
		float3 center2start = start - center;
		float radius = length(center2start);

		float3 a = start;
		float3 d = center + forw * radius;
		float3 p = start + forw * radius;

		float3 b = lerp(a, p, quarter_circle_k);
		float3 c = lerp(d, p, quarter_circle_k);
		return new Bezier(a,b,c,d);
	}
	public static Bezier from_half_circle (float3 start, float3 forw, float3 center) {
		float3 center2start = start - center;
		float radius = length(center2start);

		float3 a = start;
		float3 d = start - center2start*2;
		float3 b = a + forw * radius * half_circle_k;
		float3 c = d + forw * radius * half_circle_k;
		return new Bezier(a,b,c,d);
	}

	public (Bezier first, Bezier last) subdiv (float t) {
		float3 ab = lerp(a,b, t);
		float3 bc = lerp(b,c, t);
		float3 cd = lerp(c,d, t);
		float3 abc = lerp(ab,bc, t);
		float3 bcd = lerp(bc,cd, t);
		float3 abcd = lerp(abc,bcd, t);

		return (
			first: new Bezier(a, ab, abc, abcd),
			last:  new Bezier(abcd, bcd, cd, d)
		);
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
		//float3 accel = c3*(t*6)  + c2*2;                 // f''(t)
		
		//float denom = deriv.x*deriv.x + deriv.y*deriv.y;
		//float curv = 0;
		//if (denom >= 0.0001f) // curv not defined if deriv=0, which happens if a==b for example
		//	curv = (deriv.x*accel.y - accel.x*deriv.y) / (denom * sqrt(denom)); // denom^(3/2)
		
		return new PosVel { pos=value, vel=deriv };
	}

	public PointDir eval_offset (float t, float3 offset) {
		var res = eval(t);
		var mat = MyMath.rotate_to_direction(res.vel);

		return new PointDir {
			pos = res.pos + mul(mat, offset),
			dir = mul(mat, float3(0,0,1))
		};
	}

	public OffsetBezier offset (float3 offset) {
		return new OffsetBezier { bez = this, offset = offset };
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
	public float approx_len (float3 offset, int res=10) {
		float3 prev = eval_offset(0, offset).pos;

		float len = 0;
		for (int i=0; i<res; ++i) {
			float t = (float)(i+1) * (1.0f / res);
			float3 pos = eval_offset(t, offset).pos;

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
			var mat = MyMath.rotate_to_direction(bez_res.vel);
			var p0 = bez_res.pos + mul(mat, float3(x0, 0,0));
			var p1 = bez_res.pos + mul(mat, float3(x1, 0,0));

			lo = min(lo, min(p0, p1));
			hi = max(hi, max(p0, p1));
		}

		lo.y += h0;
		hi.y += h1;

		if (any(!isfinite(lo)) || any(!isfinite(hi))) {
			// handle NaN due to singularity bezier?
			lo = -10000000;
			hi = +10000000;
		}

		var bounds = new Bounds();
		bounds.SetMinMax(lo, hi);
		return bounds;
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
	
		float3x3 rotate_to_bezier = MyMath.rotate_to_direction(res.vel);
	
		pos_out = res.pos + mul(rotate_to_bezier, float3(pos_obj.xy,0));
		norm_out = mul(rotate_to_bezier, norm_obj);
		tang_out = mul(rotate_to_bezier, tang_obj);
	}
	
	public void debugdraw (Color col, int res=10) {
		float3 prev = a;

		for (int i=0; i<res; i++) {
			float t = (float)(i+1) * (1.0f / res);
			float3 pos = eval(t).pos;

			if (i < res-1) Debug.DrawLine(prev, pos, col);
			else           Util.DebugDrawArrow(prev, pos-prev, col, 1);

			prev = pos;
		}

		//Debug.DrawRay(a, float3(0,1,0));
		//Debug.DrawRay(b, float3(0,1,0));
		//Debug.DrawRay(c, float3(0,1,0));
		//Debug.DrawRay(d, float3(0,1,0));
	}

	public void debugdraw (float3 offset, Color col, int res=10) {
		float3 prev = eval_offset(0, offset).pos;

		for (int i=0; i<res; i++) {
			float t = (float)(i+1) * (1.0f / res);
			float3 pos = eval_offset(t, offset).pos;

			if (i < res-1) Debug.DrawLine(prev, pos, col);
			else           Util.DebugDrawArrow(prev, pos-prev, col, 1);

			prev = pos;
		}
	}
	public void debugdraw (float3 offset0, float3 offset1, Color col, int res=10) {
		float3 prev = eval_offset(0, offset0).pos;

		for (int i=0; i<res; i++) {
			float t = (float)(i+1) * (1.0f / res);
			float3 offs = lerp(offset0, offset1, t);
			float3 pos = eval_offset(t, offs).pos;

			if (i < res-1) Debug.DrawLine(prev, pos, col);
			else           Util.DebugDrawArrow(prev, pos-prev, col, 1);

			prev = pos;
		}
	}
}
public struct OffsetBezier {
	public Bezier bez;
	public float3 offset;

	public PointDir eval (float t) {
		return bez.eval_offset(t, offset);
	}
	
	// reverse bezier and offset X (right) & Z (forward)
	public OffsetBezier reverse () {
		return new OffsetBezier {
			bez = new Bezier(bez.d,bez.c,bez.b,bez.a),
			offset = float3(-offset.x, offset.y, -offset.z)
		};
	}

	public float approx_len (int res=10) {
		return bez.approx_len(offset, res);
	}

	public void debugdraw (Color col, int res=10) {
		bez.debugdraw(offset, col, res);
	}
}
