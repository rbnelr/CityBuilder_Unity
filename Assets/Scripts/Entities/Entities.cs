using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

public class Entities : MonoBehaviour {
	public static Entities inst { get; private set; }
	void OnEnable () {
		Debug.Assert(inst == null);
		inst = this;
	}

	public GameObject roads_go;
	public GameObject junctions_go;
	public GameObject buildings_go;
	public GameObject vehicles_go;

	public Junction junction_prefab;
	
	public Road medium_road_asym_asset;
	public Road medium_road_asset;
	public Road small_road_asset;
	
	public List<BuildingAsset> building_assets;
	public List<VehicleAsset> vehicle_assets;

	public IEnumerable<Road> roads => transform.GetComponentsInChildren<Road>();
	public IEnumerable<Junction> junctions => transform.GetComponentsInChildren<Junction>();
	
	public void destroy_all () {
		foreach (var road in roads) {
			road.destroy();
		}
		foreach (var road in junctions) {
			road.destroy();
		}

		Util.DestroyChildren(buildings_go.transform);

		destroy_vehicles();
	}
	public void destroy_vehicles () {
		Util.DestroyChildren(vehicles_go.transform);
	}
}
