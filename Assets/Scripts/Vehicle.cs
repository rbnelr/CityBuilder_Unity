using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

public class Vehicle : MonoBehaviour {
	public float max_speed = 50 / 3.6f;

	public static Vehicle create (Entities e, Vehicle prefab) {
		var vehicle = Instantiate(prefab, e.vehicles_go.transform);
		return vehicle;
	}
}
