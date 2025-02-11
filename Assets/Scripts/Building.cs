using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

public class Building : MonoBehaviour {

	public Road connected_road;

	public static Building create (Building prefab) {
		var building = Instantiate(prefab, g.entities.buildings_go.transform);
		return building;
	}
}
