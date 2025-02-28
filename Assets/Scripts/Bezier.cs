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
}
