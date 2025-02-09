using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;
using NaughtyAttributes;

public class Vehicle : MonoBehaviour {
	
	static Random rand = new Random(1);

	public float max_speed = 50 / 3.6f;

	public Building cur_building = null;

	static float stay_time => rand.NextFloat(2,5);
	float timer = stay_time;

	public Building target = null;

	public static Vehicle create (Vehicle prefab) {
		var vehicle = Instantiate(prefab, Entities.inst.vehicles_go.transform);
		return vehicle;
	}

	static float3 parking_spot (Building b) => b.transform.TransformPoint(float3(0,0,8));

	// TODO: probably need to turn generator function into manual state machine again, because:
	// -not efficient, does a heap alloc per IEnumerator, likely forces Motion to exists twice (stored internally, but also by me if I want to mutate it
	// -can't ask about total length of path afterwards unless I also manually store it
	// so at the end I only really benefit by avoiding manually having a 'progress' counter, and having slightly more readable code
	// it's also easier to use more memory than you need to, since any local var in the function might be stored permanently

	struct Motion {
		public Road cur_road;
		public Road.LanePath path;
		public float cur_dist;
	};
	
	Road[] path;
	Building target_building = null;

	IEnumerator<Motion> motion_enumer;
	Motion motion;
	int _path_idx;
	
	IEnumerable<Motion> follow_path () {
		Debug.Assert(path.Length >= 2);

		for (_path_idx=0; _path_idx<path.Length; _path_idx++) {
			int i = _path_idx;
			Road cur_road = path[i];

			RoadDirection dir;
			if (i == 0) {
				var next_junc = Junction.between(cur_road, path[i+1]);
				dir = next_junc != cur_road.junc_a ? RoadDirection.Forward : RoadDirection.Backward;
			}
			else {
				var prev_junc = Junction.between(path[i-1], cur_road);
				dir = prev_junc == cur_road.junc_a ? RoadDirection.Forward : RoadDirection.Backward;
			}

			int lane = rand.Pick(cur_road.lanes.Select((x,i) => new { lane=x, idx=i }).Where(x => x.lane.dir == dir).Select(x => x.idx).ToArray());

			yield return new Motion { cur_road = cur_road, path = cur_road.get_lane_path(cur_road.lanes[lane]), cur_dist = 0 };
		}
	}

	// TODO: Could track progress in kilometer and or time and ETA in minutes
	[ShowNativeProperty]
	public string TripProgress { get {
		if (cur_building == null && target_building == null) return "";
		
		if (cur_building != null) {
			return $"parked at {cur_building}";
		}
		return $"{_path_idx}/{path.Length}";
	}}

	bool start_trip () {
		var targ = rand.Pick(Entities.inst.buildings_go.GetComponentsInChildren<Building>());
		if (targ == cur_building)
			return false; // not supported

		path = Pathfinding.inst.pathfind(cur_building.connected_road, targ.connected_road);
		if (path == null)
			return false; // pathfinding failed

		target_building = targ;
		cur_building = null;

		motion_enumer = follow_path().GetEnumerator();
		bool res = motion_enumer.MoveNext();
		Debug.Assert(res);
		motion = motion_enumer.Current;
		return true;
	}
	void end_trip () {
		transform.position = parking_spot(target_building);
		
		cur_building = target_building;
		target_building = null;
		path = null;
		motion_enumer = null;
	}

	void Update () {
		if (GameTime.inst.paused) return;
		if (cur_building == null && target_building == null) return; // handle vehicle spawned while no buildings exist gracefully

		if (cur_building) {
			transform.position = parking_spot(cur_building);

			timer -= Time.deltaTime;

			if (timer <= 0) {
				start_trip(); // if fail try again in stay_time

				timer = stay_time;
			}
		}
		
		if (!cur_building) {
			float3 pos = motion.path.a;
			float3 dir = normalizesafe(motion.path.b - motion.path.a);
			
			transform.position = pos + dir * motion.cur_dist;
			transform.rotation = Quaternion.LookRotation(dir);

			float step = max_speed * GameTime.inst.dt;
			motion.cur_dist += step;

			if (motion.cur_dist > motion.cur_road.length) {
				if (!motion_enumer.MoveNext()) {
					end_trip();
					return;
				}
				motion = motion_enumer.Current;
			}
		}
	}
}
