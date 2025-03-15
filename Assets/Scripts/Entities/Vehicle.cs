using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

public class Vehicle : MonoBehaviour {

	public VehicleAsset asset;
	public Color color = Color.white;

	public Building cur_building = null;
	public Building target = null;

	static Random rand = new Random(1);
	static float stay_time => rand.NextFloat(2, 5);
	float timer = stay_time;

	public static int _counter = 0;
	public static Vehicle create (VehicleAsset asset) {
		var vehicle = Instantiate(asset.instance_prefab, g.entities.vehicles_go.transform).GetComponent<Vehicle>();
		vehicle.name = $"Vehicle #{_counter++}";

		if (asset.color_set != null)
			vehicle.color = rand.Weighted(asset.color_set.colors, x => x.weight).color;

		vehicle.GetComponentInChildren<SkinnedMeshRenderer>().material.SetColor("_Tint", vehicle.color);
		
		g.entities.vehicles.add(vehicle);
		return vehicle;
	}

	// TODO: probably need to turn generator function into manual state machine again, because:
	// -not efficient, does a heap alloc per IEnumerator, likely forces Motion to exists twice (stored internally, but also by me if I want to mutate it
	// -can't ask about total length of path afterwards unless I also manually store it
	// so at the end I only really benefit by avoiding manually having a 'progress' counter, and having slightly more readable code
	// it's also easier to use more memory than you need to, since any local var in the function might be stored permanently

	// TODO: make the current travel state be serializable
	// -> Serialize all of the path, target_building, motion etc.
	//     or simply make sure pathfinding is deterministic and just store current motion, then allow 'repathing', which I want to support anyway
	//     repathing to the same spot should then produce identical movement as if repathing did not happen, this way storing the path can be avoided entirely
	//     if pathing takes into account current traffic, it would _not_ be identical anymore and loading a savegame might introduce visible repathing
	//     not sure what the alternative is, if vehicles repath regularily anyway (because storing hundreds of future path nodes is expensive at runtime too)
	//     lets say vehicles follow 20 nodes, then always repath with new traffic data (and constantly update the next few lanes)

	struct Motion {
		public OffsetBezier bezier;
		public float bez_length;
		public float cur_dist;
	};

	Road[] path;
	Building start_building = null;
	Building target_building = null;

	IEnumerator<Motion> motion_enumer;
	Motion motion;
	int _path_idx;

	RoadDir get_road_dir (int path_idx) {
		Road cur_road = path[path_idx];
		if (path_idx == 0) {
			var next_junc = Junction.between(cur_road, path[path_idx + 1]);
			return next_junc != cur_road.junc0 ? RoadDir.Forward : RoadDir.Backward;
		}
		else {
			var prev_junc = Junction.between(path[path_idx - 1], cur_road);
			return prev_junc == cur_road.junc0 ? RoadDir.Forward : RoadDir.Backward;
		}
	}
	// TODO: split lanes by direction by default so this becomes unneeded
	static RoadLane pick_lane (Road road, RoadDir dir) => rand.Pick(road.lanes_in_dir(dir).ToArray());

	static float3 parking_spot (Building b) => b.transform.TransformPoint(float3(0, 0, 8));

	IEnumerable<Motion> follow_path () {
		Debug.Assert(path.Length >= 2);

		var cur_lane = pick_lane(path[0], get_road_dir(0));

		{ // building -> first lane
			float3 point = cur_lane.road.calc_path(cur_lane).eval(0).pos;
			// avoid the need for bezier here? simply have vehicle drive to target point?
			var bezier = Bezier.from_line(parking_spot(start_building), point);
			yield return new Motion {
				bezier = bezier.offset(0),
				bez_length = bezier.approx_len(),
				cur_dist = 0
			};
		}

		for (_path_idx = 0; _path_idx < path.Length; _path_idx++) {
			{ //// Road
				var bezier = cur_lane.road.calc_path(cur_lane);

				yield return new Motion {
					bezier = bezier,
					bez_length = bezier.approx_len(),
					cur_dist = 0
				};
			}

			if (_path_idx == path.Length - 1)
				break; // No junction at end

			RoadLane next_lane = pick_lane(path[_path_idx + 1], get_road_dir(_path_idx + 1));

			{ //// Junction

				var junc = Junction.between(cur_lane.road, next_lane.road);
				var bezier = RoadGeometry.calc_curve(junc, cur_lane, next_lane);

				yield return new Motion {
					bezier = bezier.offset(0),
					bez_length = bezier.approx_len(),
					cur_dist = 0
				};
			}

			cur_lane = next_lane;
		}

		{ // last lane -> building
			float3 point = cur_lane.road.calc_path(cur_lane).eval(1).pos;

			var bezier = Bezier.from_line(point, parking_spot(target_building));
			yield return new Motion {
				bezier = bezier.offset(0),
				bez_length = bezier.approx_len(),
				cur_dist = 0
			};
		}
	}

	// TODO: Could track progress in kilometer and or time and ETA in minutes
	[NaughtyAttributes.ShowNativeProperty]
	public string TripProgress {
		get {
			if (cur_building == null && target_building == null) return "";

			if (cur_building != null) {
				return $"parked at {cur_building}";
			}
			return $"{_path_idx}/{path.Length}";
		}
	}

	bool start_trip () {
		var targ = rand.Pick(g.entities.buildings_go.GetComponentsInChildren<Building>());
		if (targ == cur_building)
			return false; // not supported

		path = g.pathfinding.pathfind(cur_building.connected_road, targ.connected_road);
		if (path == null)
			return false; // pathfinding failed

		start_building = cur_building;
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

		start_building = null;
		cur_building = target_building;
		target_building = null;
		path = null;
		motion_enumer = null;
	}

	public void update () {
		if (g.game_time.paused) return;

		if (cur_building == null && target_building == null || (cur_building == null && motion_enumer == null)) {
			Destroy(this.gameObject);
			return; // handle vehicle spawned while no buildings exist gracefully
		}

		if (cur_building) {
			transform.position = parking_spot(cur_building);

			timer -= Time.deltaTime;

			if (timer <= 0) {
				bool success = start_trip(); // if fail try again in stay_time

				timer = stay_time;
			}
		}

		if (!cur_building) {
			float bez_t = motion.cur_dist / motion.bez_length;
			var bez = motion.bezier.eval(bez_t);

			transform.position = bez.pos;
			transform.rotation = Quaternion.LookRotation(bez.dir);

			float step = asset.max_speed * g.game_time.dt;
			motion.cur_dist += step;

			if (motion.cur_dist > motion.bez_length) {
				if (!motion_enumer.MoveNext()) {
					end_trip();
					return;
				}
				motion = motion_enumer.Current;
			}
		}
	}
}
