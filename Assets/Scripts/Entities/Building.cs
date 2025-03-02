using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;
using UnityEditor;

public class Building : MonoBehaviour {

	public BuildingAsset asset;

	public Road connected_road;
	
	public static int _counter = 0;
	public static Building create (BuildingAsset asset) {
		var building = Instantiate(asset.instance_prefab, g.entities.buildings_go.transform).GetComponent<Building>();
		building.name = $"Building #{_counter++}";
		return building;
	}
}
