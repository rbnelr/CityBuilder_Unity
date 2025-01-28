using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

public static class MathExt {
	public static bool Chance (this ref Random rand, float probabilty) {
		return rand.NextFloat(0, 1) < probabilty;
	}

	public static T Pick<T> (this ref Random rand, IReadOnlyList<T> choices) {
		Debug.Assert(choices.Any());
		return choices[ rand.NextInt(0, choices.Count()) ];
	}
}
