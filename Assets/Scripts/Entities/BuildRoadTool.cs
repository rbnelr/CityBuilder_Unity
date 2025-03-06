using UnityEngine;
using UnityEngine.UIElements;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.InputSystem;

public class BuildRoadTool : UI_ButtonTool {

	[Header("BuildRoadTool")]
	public Road prefab;

	bool accept_button => Mouse.current.leftButton.wasPressedThisFrame;
	bool back_button => Mouse.current.rightButton.wasPressedThisFrame;

	// temporary objects for previewing, these get deleted eventually
	Road road = null;
	Junction junc0 = null;
	Junction junc1 = null;
	// editing state
	int stage = 0;
	Junction start_junc = null;
	Junction end_junc = null;
	
	protected override void deactivated () {
		if (road) road.destroy();
		if (junc0) junc0.destroy();
		if (junc1) junc1.destroy();
		
		stage = 0;
		start_junc = null;
		end_junc = null;
	}

	bool handle_back_action () {
		// right click undoes current placement stage
		if (back_button) {
			if (stage > 0) {
				stage--;
			}
			else {
				// final right click exits build road tool
				base.update(); // handle right click, TODO: this sucks, why can't we just consume inputs???
				return true;
			}
		}
		return false;
	}
	
	static void make_real (ref Junction new_junc) {
		// generate real name
		new_junc.set_name();
		new_junc.GetComponent<SphereCollider>().enabled = true;
		// avoid deleting from now on
		new_junc = null;
	}
	static void make_real (ref Road new_road) {
		new_road.set_name();
		new_road = null;
	}

	// raycast existing junction
	// or lazyily create new one placed on ground collision layer in new_junc (and move it with cursor)
	Junction pick_new_or_existing_junction (ref Junction new_junc, Junction exclude = null) {
		Junction junc = null;

		// This seems to work reliably, does this have performance impact?
		if (exclude) exclude.GetComponent<SphereCollider>().enabled = false;

		if (Controls.raycast(out var hit, Controls.GROUND_LAYER | Controls.JUNCTIONS_LAYER)) {
			junc = hit.collider.gameObject.GetComponent<Junction>();
			if (junc != null) {
				// connect to existing junction
			}
			else {
				// connect to new junction at hit point
				if (!new_junc) new_junc = Junction.create("BuildRoadTool-junc");

				new_junc.position = hit.point;
				// avoid treating this as existing junction in next frame!
				new_junc.gameObject.SetActive(true);
				// always exclude new junctions
				new_junc.GetComponent<SphereCollider>().enabled = false;

				junc = new_junc;
			}
		}

		if (exclude) exclude.GetComponent<SphereCollider>().enabled = true;

		return junc;
	}

	public override void update () {
		if (handle_back_action()) return;
		
		// delete and recreate road every frame to avoid complication of having to reconnect road dynamically
		if (road) road.destroy(keep_empty_junc: true);

		// preview start point
		if (stage == 0) {
			start_junc = pick_new_or_existing_junction(ref junc0);
			end_junc = null;
			
			// click to remember start point
			if (start_junc && accept_button) {
				stage = 1;
			}
		}
		// preview linear road
		else if (stage == 1) {
			// exclude start_junc, can't connect to itself!
			end_junc = pick_new_or_existing_junction(ref junc1, exclude: start_junc);
			
			if (start_junc && end_junc && start_junc != end_junc) {
				// create linear road with default rules
				road = Road.create(prefab, start_junc, end_junc, "BuildRoadTool-road");
				
				// accept road
				if (accept_button) {
					if (junc0 && junc0 == start_junc) make_real(ref junc0);
					if (junc1 && junc1 == end_junc  ) make_real(ref junc1);
					make_real(ref road);
					
					// start new road at end of previous road
					start_junc = end_junc;
					stage = 1;
				}
			}
		}

		// newly created junctions only visible if not using existing and if raycast succeeds
		if (junc0 && junc0 != start_junc) junc0.gameObject.SetActive(false);
		if (junc1 && junc1 != end_junc  ) junc1.gameObject.SetActive(false);
	}
}
