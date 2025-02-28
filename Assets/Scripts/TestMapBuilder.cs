using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using NaughtyAttributes;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

[DefaultExecutionOrder(-500)]
public class TestMapBuilder : MonoBehaviour {

	[Range(1, 500)]
	public int grid = 10;
	public float spacing = 60;
	
	[Range(0, 10000)]
	public int num_vehicles = 2;

	public float intersection_radius = 0.0f;

	[Range(0.0f, 1.0f)]
	public float connection_chance = 0.7f;

	Random rand = new Random(1);
	
	void Start () {
		recreate_map();
	}

	void Update () {
		adjust_vehicle_count();
	}
	
	Road road_type (int2 pos, int axis, out bool flip) {
		bool type1 = (pos[axis^1]-5) % 10 == 0;

		bool type2_0 = (pos[axis]-5) % 10 <= 1;
		bool type2_1 = (pos[axis]-5) % 10 >= 8;

		//bool at_edge = pos.x <= (axis ? 4 : 4) || pos.y <= (axis ? 4 : 4) ||
		//	pos.x >= _grid_n-(axis ? 4 : 4) || pos.y >= _grid_n-(axis ? 4 : 4);
		bool at_edge = false;

		flip = false;
		if (type1 && !at_edge) {
			if (type2_0 || type2_1) {
				flip = type2_0;
				return g.entities.medium_road_asym_asset;
			}
			return g.entities.medium_road_asset;
		}
		return g.entities.small_road_asset;
	}
	void create_segment (Road prefab, Junction node_a, Junction node_b, bool flip) {
		Debug.Assert(node_a && node_b && node_a != node_b);
		if (flip) (node_a, node_b) = (node_b, node_a);

		var road = Road.create(prefab, node_a, node_b);
		
		node_a._radius = max(node_a._radius, prefab.width/2);
		node_b._radius = max(node_b._radius, prefab.width/2);
	}
	
	
	[Button("Destroy All")]
	private void destroy_all () {
		g.entities.destroy_all();
	}

	[Button("Recreate Map")]
	private void recreate_map () {
		g.entities.destroy_all();

		var junctions = new Dictionary<int2, Junction>();

		var base_pos = float3(0);
		
		// create path nodes grid
		for (int y=0; y<grid+1; ++y)
		for (int x=0; x<grid+1; ++x) {
			var junc = Junction.create();
			junc.position = base_pos + float3(x, 0, y) * float3(spacing, 0, spacing);
			junc._radius = 0;
		
			bool big_intersec = (x-5) % 10 == 0 && (y-5) % 10 == 0;
			//node->_fully_dedicated_turns = big_intersec;

			junctions.Add(int2(x,y), junc);
		}
		
		
		// create x paths
		for (int y=0; y<grid+1; ++y)
		for (int x=0; x<grid; ++x) {
			var asset = road_type(int2(x,y), 0, out bool flip);

			var a = junctions[int2(x, y)];
			var b = junctions[int2(x+1, y)];
			create_segment(asset, a, b, flip);
		}
		// create y paths
		for (int y=0; y<grid; ++y)
		for (int x=0; x<grid+1; ++x) {
			var asset = road_type(int2(x,y), 1, out bool flip);

			if (asset != g.entities.small_road_asset || rand.Chance(connection_chance)) {
				var a = junctions[int2(x, y)];
				var b = junctions[int2(x, y+1)];
				create_segment(asset, a, b, flip);
			}
		}

		//foreach (var junc in network.junctions) {
		//	junc.update_cached(intersection_radius);
		//	junc.set_defaults();
		//}
		foreach (var road in g.entities.roads_go.GetComponentsInChildren<Road>()) {
			road.refresh(reset: true); // update road end positions
		}

		
		for (int y=0; y<grid+1; ++y)
		for (int x=0; x<grid; ++x) {
			Road conn_seg = null;
			{
				var a = junctions[int2(x, y)];
				var b = junctions[int2(x+1, y)];
				foreach (var road in a.roads) {
					var other = road.other_junction(a);
					if (other == b) {
						// found path in front of building
						conn_seg = road;
						break;
					}
				}
			}

			float3 road_center = (float3(x,0,y) + float3(0.5f,0,0)) * float3(spacing,0,spacing);
			float roadL = conn_seg.edgeL;
			float roadR = conn_seg.edgeR;
			
			float3 building_size = float3(16, 16, 7); // TODO

			{
				var building = Building.create(rand.Pick(g.entities.building_assets));
				building.transform.position = base_pos + road_center + float3(0, 0, roadR + building_size.z);
				building.transform.rotation = Quaternion.Euler(0,180,0);
				building.connected_road = conn_seg;
			}
			{
				var building = Building.create(rand.Pick(g.entities.building_assets));
				building.transform.position = base_pos + road_center - float3(0, 0, -roadL + building_size.z);
				building.transform.rotation = Quaternion.Euler(0,0,0);
				building.connected_road = conn_seg;
			}
		}

		respawn_vehicles();
	}
	
	[Button("Respawn Vehicles")]
	void respawn_vehicles () {
		g.entities.destroy_vehicles();
		
		adjust_vehicle_count();
	}
	void adjust_vehicle_count () {
		if (g.entities.buildings_go.transform.childCount <= 0) return;

		while (g.entities.vehicles_go.transform.childCount < num_vehicles) {
			spawn_vehicle();
		}
		if (g.entities.vehicles_go.transform.childCount > num_vehicles) {
			int to_destroy = g.entities.vehicles_go.transform.childCount - num_vehicles;
			for (int i=0; i<to_destroy; ++i) {
				var c = g.entities.vehicles_go.transform.GetChild(g.entities.vehicles_go.transform.childCount-1-i);
				Destroy(c.gameObject);
			}
		}
	}


	void spawn_vehicle () {
		var asset = rand.Pick(g.entities.vehicle_assets);
		var vehicle = Vehicle.create(asset);

		vehicle.cur_building = rand.Pick(g.entities.buildings_go.GetComponentsInChildren<Building>());
	}
}
