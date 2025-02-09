using System.Collections.Generic;
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

public class MyMath {
	public static float smooth_var (float dt, float cur, float target, float smooth_fac, float smooth_const=1) {
		if (smooth_fac <= 0.0f)
			return target;

		// smoothed zoom via animating towards zoom_target
		float delta = target - cur;
		float dir = sign(delta);
		delta = abs(delta);
		float vel = delta * smooth_fac + 1.0f; // proportional velocity + small constant to actually reach target

		cur += dir * min(delta, vel * dt); // min(delta, vel) to never overshoot target
		return cur;
	}
}
