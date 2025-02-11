using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;
using UnityEditor;

public class Pathfinding : MonoBehaviour {

	// Pathfinding ignores lanes other than checking if any lane allows the turn to a node being visited
	// Note: lane selection happens later during car path follwing, a few segments into the future
	// TODO: rewrite this with segments as the primary item? Make sure to handle start==dest
	//  and support roads with median, ie no enter or exit buildings with left turn -> which might cause uturns so that segments get visited twice
	//  supporting this might require keeping entries for both directions of segments
	
	public Road[] pathfind (Road start, Road dest) {
		// use dijkstra algorithm

		if (start == null || dest == null)
			return null;

		var unvisited = new Utils.PriorityQueue<Junction, float>();

		// prepare all nodes
		foreach (var node in g.entities.junctions) {
			node._cost = float.PositiveInfinity;
			node._visited = false;
			//node._q_idx = -1;
			node._pred = null;
			node._pred_road = null;
		}

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
			start.junc_b._cost = (start.length * 0.5f) / start.speed_limit;
			start.junc_b._pred_road = start;
			unvisited.Enqueue(start.junc_b, start.junc_b._cost);
		}
		//if (start.backw) {
		{
			start.junc_a._cost = (start.length * 0.5f) / start.speed_limit;
			start.junc_a._pred_road = start;
			unvisited.Enqueue(start.junc_a, start.junc_a._cost);
		}
		
		//net.pathing_count++;
		//
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
			if (dest.junc_a._visited && dest.junc_b._visited)
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

					float len = road.length + road.junc_a._radius + road.junc_b._radius;
					float cost = len / road.speed_limit;
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
		float a_cost = dest.junc_a._cost + dist_from_a / dest.speed_limit;
		float b_cost = dest.junc_b._cost + dist_from_b / dest.speed_limit;

		// do not count final node if coming from dest segment, to correctly handle start == dest
		if (dest.junc_a._pred_road && dest.junc_a._pred_road != dest) {
			end_node = dest.junc_a;
		}
		if (dest.junc_b._pred_road && dest.junc_b._pred_road != dest) {
			// if both nodes count, choose end node that end up fastest
			if (!end_node || b_cost < a_cost) {
				end_node = dest.junc_b;
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

	public bool visualize_last_pathfind = false;

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
					float3 pos = (node._pred_road.pos_a + node._pred_road.pos_b) * 0.5f;
					
					Gizmos.color = Color.cyan;
					Gizmos.DrawRay(pos, node.position - pos);
				}
			}
		}
	}
}