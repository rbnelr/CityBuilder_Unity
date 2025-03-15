using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

[DefaultExecutionOrder(-450)]
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


	public Material[] _always_include_mat;

	public EntityList<Road> roads { get; private set; } = new();
	public EntityList<Junction> junctions { get; private set; } = new();
	public EntityList<Building> buildings { get; private set; } = new();
	public EntityList<Vehicle> vehicles { get; private set; } = new();
	
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

	float _veh_time = 0;

	private void Update () {
		roads.update();
		junctions.update();
		buildings.update();
		vehicles.update();

		using (Timer.Start(d => _veh_time = d)) {
			foreach (var vehicle in vehicles) {
				vehicle.update();
			}
		}
		
		DebugHUD.Show(
			$"Vehicle update: #{vehicles.Count} "+
			$"total: {_veh_time * 1000.0, 6:0.000}ms "+
			$"avg: {_veh_time/vehicles.Count * 1000000.0, 6:0.000}us");
	}
}

public class EntityList<T> where T : UnityEngine.Object {
	List<T> list = new();

	public void update () {
		list.RemoveAll(x => x == null);
	}

	public void add (T item) {
		list.Add(item);
	}

	public int Count => list.Count;
	public IEnumerator<T> GetEnumerator () => list.GetEnumerator();
}
