﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

#nullable enable

public static class Extensions {
	public static bool Chance (this ref Random rand, float probabilty) {
		return rand.NextFloat(0, 1) < probabilty;
	}
	
	public static T? Pick<T> (this ref Random rand, IReadOnlyList<T> choices) {
		//Debug.Assert(choices.Any());
		if (choices.Any())
			return choices[ rand.NextInt(0, choices.Count()) ];
		return default;
	}

	public static void GizmosDrawArrow (Vector3 pos, Vector3 direction, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20.0f) {
		Gizmos.DrawRay(pos, direction);
		
		Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0,180+arrowHeadAngle,0) * new Vector3(0,0,1);
		Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0,180-arrowHeadAngle,0) * new Vector3(0,0,1);
		Gizmos.DrawRay(pos + direction, right * arrowHeadLength);
		Gizmos.DrawRay(pos + direction, left * arrowHeadLength);
	}
}

//public struct Bezier<T> {
//	T a, b, c, d;
//
//	public struct PosVel {
//		public T pos;
//		public T vel; // velocity (delta position / delta bezier t)
//	}
//	PosVel eval<T> (float t) where T: INumber<T> { // ughhhhhh
//		T c0 = a;                   // a
//		T c1 = 3 * (b - a);         // (-3a +3b)t
//		T c2 = 3 * (a + c) - 6*b;   // (3a -6b +3c)t^2
//		T c3 = 3 * (b - c) - a + d; // (-a +3b -3c +d)t^3
//
//		float t2 = t*t;
//		float t3 = t2*t;
//		
//		T value = c3*t3     + c2*t2    + c1*t + c0; // f(t)
//		T deriv = c3*(t2*3) + c2*(t*2) + c1;        // f'(t)
//		
//		return new PosVel { pos=value, vel=deriv };
//	}
//}
