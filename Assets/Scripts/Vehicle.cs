using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

public class Vehicle : MonoBehaviour {
	
	static Random rand = new Random(1);

	public float max_speed = 50 / 3.6f;

	public Building cur_building = null;

	static float stay_time => rand.NextFloat(2,5);
	float timer = stay_time;

	public Building target = null;

	public static Vehicle create (Entities e, Vehicle prefab) {
		var vehicle = Instantiate(prefab, e.vehicles_go.transform);
		return vehicle;
	}

	static float3 parking_spot (Building b) => b.transform.TransformPoint(float3(0,0,8));

	void Update () {
		if (cur_building) {
			transform.position = parking_spot(cur_building);

			timer -= Time.deltaTime;

			if (timer <= 0) {
				var targ = rand.Pick(Entities.inst.buildings_go.GetComponentsInChildren<Building>());

				if (targ != cur_building) {
					target = targ;
					cur_building = null;
					timer = stay_time;
				}
			}
		}
		
		if (!cur_building) {
			float3 pos = transform.position;
			float3 targ = parking_spot(target);

			float3 diff = targ - pos;
			float dist = length(diff);
			float3 dir = normalizesafe(diff);

			float step = max_speed * Time.deltaTime;
			if (step < dist) {
				transform.position = (float3)transform.position + dir * step;
				transform.rotation = Quaternion.LookRotation(dir);
			}
			else {
				// would overshoot target this frame
				transform.position = targ;
				
				cur_building = target;
				target = null;
			}
		}
	}
}
