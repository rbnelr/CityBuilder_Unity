using UnityEngine;
using UnityEngine.UIElements;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.InputSystem;
using System.Linq;

public class BuildRoadTool : UI_ButtonTool {
	BuildToolshelf build => GetComponentInParent<BuildToolshelf>();

	[Header("BuildRoadTool")]
	public Road road_prefab;

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

	GameObject junc0_mesh = null;

	float3? control0 = 0;
	
	protected override void deactivated () {
		if (road) road.destroy();
		if (junc0) junc0.destroy();
		if (junc1) junc1.destroy();
		if (junc0_mesh) Destroy(junc0_mesh);
		
		stage = 0;
		start_junc = null;
		end_junc = null;
	}

	bool handle_back_action () {
		if (Keyboard.current.escapeKey.wasPressedThisFrame) {
			base.update();
			return true;
		}

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
		new_road.tint = new Color(0,0,0,0);
		new_road.Refresh(refresh_junc: true);
		// avoid deleting from now on
		new_road = null;
	}

	Junction create_junc_at (ref Junction new_junc, float3 pos) {
		// connect to new junction at hit point
		if (!new_junc) new_junc = Junction.create("BuildRoadTool-junc");

		new_junc.position = pos;
		// avoid treating this as existing junction in next frame!
		new_junc.gameObject.SetActive(true);
		// always exclude new junctions
		new_junc.GetComponent<SphereCollider>().enabled = false;

		return new_junc;
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
				junc = create_junc_at(ref new_junc, hit.point);
			}
		}

		if (exclude) exclude.GetComponent<SphereCollider>().enabled = true;

		return junc;
	}
	float3? pick_control_point () {
		if (Controls.raycast(out var hit, Controls.GROUND_LAYER)) {
			return hit.point;
		}
		return null;
	}
	
	void preview_start_point () {
		start_junc = pick_new_or_existing_junction(ref junc0);
		end_junc = null;
			
		// click to remember start point
		if (start_junc && accept_button) {
			stage = 1;
		}
	}
	
	void preview_straight_road (Junction start, Junction end) {
		float min_dist = max(road_prefab.asset.width * 0.5f, 1.0f);
		if (  start && end && start != end &&
			  distance(start.position, end.position) > min_dist) {
			// create linear road with default rules
			road = Road.create(road_prefab, start, end, "BuildRoadTool-road", build.preview_tint);
		}
	}
	void preview_curved_road (Junction start, Junction end) {
		float min_dist = max(road_prefab.asset.width * 0.5f, 1.0f);
		if (  start && end && start != end &&
			  distance(control0.Value, end.position) > min_dist) {
			
			road = Road.create(road_prefab, start, end, "BuildRoadTool-road", build.preview_tint);
			var bez = road.bezier;
			bez.b = lerp(bez.a, control0.Value, 0.5539f);
			bez.c = lerp(bez.d, control0.Value, 0.5539f);
			road.bezier = bez;

			road.Refresh(refresh_junc: true);
		}
		else {
			// Fallback
			end_junc = create_junc_at(ref junc1, control0.Value);
			preview_straight_road(start, end_junc);
		}
	}

	void accept_road () {
		if (junc0 && junc0 == start_junc) make_real(ref junc0);
		if (junc1 && junc1 == end_junc  ) make_real(ref junc1);
		make_real(ref road);
		
		// start new road at end of previous road
		start_junc = end_junc;
		stage = 1;
	}

	void build_straight () {
		if (stage == 0) {
			preview_start_point();
		}
		else if (stage == 1) {
			// exclude start_junc, can't connect to itself!
			end_junc = pick_new_or_existing_junction(ref junc1, exclude: start_junc);
		
			preview_straight_road(start_junc, end_junc);
			
			if (road != null && accept_button) {
				accept_road();
			}
		}
	}
	void build_curved () {
		if (stage == 0) {
			preview_start_point();
		}
		else if (stage == 1) {
			// exclude start_junc, can't connect to itself!
			control0 = pick_control_point();
			if (control0.HasValue) {
				end_junc = create_junc_at(ref junc1, control0.Value);
				
				preview_straight_road(start_junc, end_junc);
			
				if (accept_button) {
					stage = 2;
				}
			}
		}
		else if (stage == 2) {
			end_junc = pick_new_or_existing_junction(ref junc1, exclude: start_junc);

			preview_curved_road(start_junc, end_junc);
			
			if (road != null && accept_button) {
				accept_road();
			}
		}
	}

	public override void update () {
		if (handle_back_action()) return;
		
		// delete and recreate road every frame to avoid complication of having to reconnect road dynamically
		if (road) road.destroy(keep_empty_junc: true);
		if (junc0_mesh) Destroy(junc0_mesh);
		road = null;

		switch (build.cur_mode) {
			case BuildToolshelf.BuildMode.STRAIGHT: {
				build_straight();
			} break;
			case BuildToolshelf.BuildMode.CURVED: {
				build_curved();
			} break;
		}

		if (start_junc?.roads.Length == 0) {
			// Draw lone junction mesh TODO: could probably draw a road with 0 length instead if we didn't have to manage gameobjects
			var mesh = road_prefab.GetComponent<MeshFilter>().sharedMesh;
			var mats0 = RoadGeometry.refresh_junc_mesh(start_junc, road_prefab, +1, build.preview_tint);
			var mats1 = RoadGeometry.refresh_junc_mesh(start_junc, road_prefab, -1, build.preview_tint);

			for (int i=0; i<mesh.subMeshCount; ++i)
				Graphics.DrawMesh(mesh, Matrix4x4.identity, mats0[i], 0, null, i);
			for (int i=0; i<mesh.subMeshCount; ++i)
				Graphics.DrawMesh(mesh, Matrix4x4.identity, mats1[i], 0, null, i);
		}

		// newly created junctions only visible if not using existing and if raycast succeeds
		if (junc0 && junc0 != start_junc) junc0.gameObject.SetActive(false);
		if (junc1 && junc1 != end_junc  ) junc1.gameObject.SetActive(false);
	}
}
