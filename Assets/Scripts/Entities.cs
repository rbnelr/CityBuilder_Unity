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
	
	public Building[] building_assets;
	public Vehicle[] vehicle_assets;

	public IEnumerable<Road> roads => transform.GetComponentsInChildren<Road>();
	public IEnumerable<Junction> junctions => transform.GetComponentsInChildren<Junction>();
	
	void destroy_children (GameObject go) {
		if (Application.isEditor) {
			for (int i=go.transform.childCount-1; i>=0; i--) {
				DestroyImmediate(go.transform.GetChild(i).gameObject);
			}
		}
		else {
			foreach (Transform c in go.transform) {
				Destroy(c.gameObject);
			}
		}
	}

	public void destroy_all () {
		destroy_children(roads_go);
		destroy_children(junctions_go);
		destroy_children(buildings_go);

		destroy_vehicles();
	}
	public void destroy_vehicles () {
		destroy_children(vehicles_go);
	}
}
