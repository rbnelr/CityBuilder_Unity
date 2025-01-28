using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

public class Building : MonoBehaviour {

	public static Building create (Entities e, Building prefab) {
		var building = Instantiate(prefab, e.buildings_go.transform);
		return building;
	}
}
