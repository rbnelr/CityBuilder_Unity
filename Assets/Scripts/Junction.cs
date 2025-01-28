using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

public class Junction : MonoBehaviour {
	public float3 position {
		get { return transform.position; }
		set { transform.position = value; }
	}

	public Road[] roads { get; private set; } = new Road[0];

	public float _radius;
	
	public static Junction create (Entities e) {
		var junction = Instantiate(e.junction_prefab, e.junctions_go.transform);
		return junction;
	}

	public void connect_road (Road road) {
		Debug.Assert(!roads.Contains(road));

		var list = roads.ToList();
		list.Add(road);
		roads = list.ToArray();
	}
}
