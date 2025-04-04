using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;
using UnityEditor;
using UnityEngine.Profiling;

[DefaultExecutionOrder(100)]
public class Pathfinding : MonoBehaviour {

	// Pathfinding ignores lanes other than checking if any lane allows the turn to a node being visited
	// Note: lane selection happens later during car path follwing, a few segments into the future
	// TODO: rewrite this with segments as the primary item? Make sure to handle start==dest
	//  and support roads with median, ie no enter or exit buildings with left turn -> which might cause uturns so that segments get visited twice
	//  supporting this might require keeping entries for both directions of segments
	
	int pathing_count = 0;
	double pathing_total_time = 0;
	//int _dijk_iter = 0;
	//int _dijk_iter_dupl = 0;
	//int _dijk_iter_lanes = 0;

	TimedAverage path_avg = new();

	public Road[] _pathfind (Road start, Road dest) {
		// use dijkstra algorithm

		if (start == null || dest == null)
			return null;

		var unvisited = new Utils.PriorityQueue<Junction, float>();
		
		Profiler.BeginSample("prepare");

		// prepare all nodes
		foreach (var node in g.entities.junctions) {
			node._cost = float.PositiveInfinity;
			node._visited = false;
			//node._q_idx = -1;
			node._pred = null;
			node._pred_road = null;
		}

		Profiler.EndSample();

		// FAILSAFE, TODO: fix!
		// Currently if start == dest and forw,backw == true
		//  either pathinding glitches and returns uturn (due to both start nodes being the dest nodes)
		//  or (with _pred_seg != dest) check, both nodes fail, so rather than fail pathing, just arbitrarily restrict direction
		//if (start.seg == dest.seg && start.forw && start.backw)
		//	start.backw = false;

		// handle the two start nodes
		// pretend start point is at center of start segment for now
		// forw/backw can restrict the direction allowed for the start segment

		// Assume start and dest points are in middle of segment, this is wrong!
		// but we might not even know the correct dest segment t, due to parking being chosen when vehicle is close to destination
		// and this should not make a huge difference (only difference is final forw/backw approach, which we can already restrict if needed!)

		//if (start.forw) {
		{
			start.junc1._cost = (start.length_for_pathfinding * 0.5f) / start.asset.speed_limit;
			start.junc1._pred_road = start;
			unvisited.Enqueue(start.junc1, start.junc1._cost);
		}
		//if (start.backw) {
		{
			start.junc0._cost = (start.length_for_pathfinding * 0.5f) / start.asset.speed_limit;
			start.junc0._pred_road = start;
			unvisited.Enqueue(start.junc0, start.junc0._cost);
		}
		
		//net._dijk_iter = 0;
		//net._dijk_iter_dupl = 0;
		//net._dijk_iter_lanes = 0;
	
		for (;;) {
			//net._dijk_iter_dupl++;
		
			// visit node with min cost
			if (!unvisited.TryDequeue(out Junction cur_node, out float _cur_cost)) {
				break;
			}

			if (cur_node._visited) continue;
			cur_node._visited = true;

			//net._dijk_iter++;

			// early out optimization
			if (dest.junc0._visited && dest.junc1._visited)
				break; // shortest path found if both dest segment nodes are visited

			// Get all allowed turns for incoming segment
			//Turns allowed = Turns::NONE;
			//for (auto lane : cur_node->_pred_seg->in_lanes(cur_node)) {
			//	allowed |= lane.get().allowed_turns;
			//}

			float cur_cost = cur_node._cost;
			
			// update neighbours with new minimum cost
			foreach (var road in cur_node.roads) {
				//for (auto lane : seg->in_lanes(cur_node)) { // This is dumb and makes no sense, TODO: fix it!
					var other_node = road.other_junction(cur_node);

					// check if turn to this node is actually allowed
					//auto turn = classify_turn(cur_node, cur_node->_pred_seg, lane.seg);
					//if (!any_set(allowed, turn)) {
					//	// turn not allowed
					//	//assert(false); // currently impossible, only the case for roads with no right turn etc.
					//	continue;
					//}

					float len = road.length_for_pathfinding + road.junc0._radius + road.junc1._radius;
					float cost = len / road.asset.speed_limit;
					Debug.Assert(cost > 0);

					float new_cost = cur_cost + cost;
					if (new_cost < other_node._cost && !other_node._visited) {
						other_node._pred      = cur_node;
						other_node._pred_road = road;
						other_node._cost      = new_cost;
						//assert(!other_node->_visited); // dijstra with positive costs should prevent this

						unvisited.Enqueue(other_node, other_node._cost); // push updated neighbour (duplicate)
					}

					//net._dijk_iter_lanes++;
				//}
			}
		}
	
		//// make path out of dijkstra graph
	
		// additional distances from a and b of the dest segment
		float dist_from_a = 0.5f;
		float dist_from_b = 0.5f;

		Junction end_node = null;
		float a_cost = dest.junc0._cost + dist_from_a / dest.asset.speed_limit;
		float b_cost = dest.junc1._cost + dist_from_b / dest.asset.speed_limit;

		// do not count final node if coming from dest segment, to correctly handle start == dest
		if (dest.junc0._pred_road && dest.junc0._pred_road != dest) {
			end_node = dest.junc0;
		}
		if (dest.junc1._pred_road && dest.junc1._pred_road != dest) {
			// if both nodes count, choose end node that end up fastest
			if (!end_node || b_cost < a_cost) {
				end_node = dest.junc1;
			}
		}

		if (!end_node)
			return null; // no path found
		
		Debug.Assert(end_node._cost < float.PositiveInfinity);

		var path = new List<Road>();
		path.Add(dest);

		Junction cur = end_node;
		while (cur) {
			Debug.Assert(cur._pred_road);
			path.Add(cur._pred_road);
			cur = cur._pred;
		}
		Debug.Assert(path.Count >= 2); // code currently can't handle single segment path
		// TODO: make that possible and then handle make driving into building on other side of road possible? Or should we just have it drive around the block for this?

		path.Reverse();

		return path.ToArray();
	}
	public Road[] pathfind (Road start, Road dest) {
		if (pathing_count >= 200)
			return null; // HACK: artifically fail pathfinding if too many pathfinds per frame, to avoid freezing the unity editor
		
		using (Timer.Start(d => pathing_total_time += d)) {
			Profiler.BeginSample("pathfind");
			
			pathing_count++;
			var res = _pathfind(start, dest);

			Profiler.EndSample();
			return res;
		}
	}

	public bool visualize_last_pathfind = false;

	private void Update () {
		if (pathing_count > 0) {
			path_avg.push((float)pathing_total_time/pathing_count);
		}
		path_avg.update();

		DebugHUD.Show(
			$"Pathing Count: {path_avg.cur_result.mean * 1000000.0:0.000}us -- {pathing_count} "+
			$"total: {pathing_total_time * 1000.0, 6:0.000}ms "+
			$"avg: {pathing_total_time/pathing_count * 1000000.0, 6:0.000}us");

		pathing_count = 0;
		pathing_total_time = 0;
	}

	private void OnDrawGizmos () {
		if (!visualize_last_pathfind) return;
	
		float max_cost = 0;
		foreach (var node in g.entities.junctions) {
			if (node._visited) {
				max_cost = max(max_cost, node._cost);
			}
		}

		foreach (var node in g.entities.junctions) {
			if (node._visited) {
				Color col = Color.Lerp(Color.magenta, Color.red, node._cost / max_cost);
				
				Gizmos.color = col;
				Gizmos.DrawWireSphere(node.position, node._radius);

				//Handles.Label(node.position, node._cost.ToString("0."));
				if (node._pred_road) {
					float3 pos = (node._pred_road.pos0 + node._pred_road.pos1) * 0.5f;
					
					Gizmos.color = Color.cyan;
					Gizmos.DrawRay(pos, node.position - pos);
				}
			}
		}
	}
}