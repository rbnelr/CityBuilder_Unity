using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public abstract class CursorInteractable : MonoBehaviour {
	//public abstract void on_cursor_over (Ray cursor_ray);
	//public abstract void on_cursor_enter (Ray cursor_ray);
	//public abstract void on_cursor_exit ();
	//public abstract void on_cursor_drag (Ray cursor_ray);
}

[System.Serializable]
public class CursorDragging {
	CursorInteractable active_obj = null;
	Material active_obj_mat = null;

	bool dragging = false;
	float3 drag_origin; // worlds[
	float3 drag_offs;
	
	public Color base_col;
	public Color hover_col;
	public Color active_col;

	ButtonControl dragging_button => Mouse.current.leftButton;

	CursorInteractable find_interactable (Ray? ray, out RaycastHit hit) {
		hit = new RaycastHit();
		if (ray.HasValue && Physics.Raycast(ray.Value, out hit, Mathf.Infinity, Controls.INTERACTABLE_LAYER)) {
			if (hit.collider.gameObject.TryGetComponent<CursorInteractable>(out var inter)) {
				return inter;
			}
		}
		return null;
	}

	public void Update () {
		Ray? ray = Controls.cursor_ray();
		
		// while dragging, drag object with cursor
		if (dragging) {
			if (dragging_button.isPressed) {
				// actively dragging

				// Physics.Raycast(cursor_ray, out var info, Mathf.Infinity, Controls.GROUND_LAYER)
				if (ray.HasValue && drag_logic(ray.Value, out float3 target)) {
					// move to target point while keeping original drag point on object
					active_obj.transform.position = target - drag_offs;
				}
				else {
					// still dragging, but cursor invalid
				}
			}
			else {
				// stop dragging
				dragging = false;
				active_obj_mat.color = hover_col;

				//Debug.Log(">> Stop dragging");
			}
		}

		if (!dragging) {
			// find CursorInteractable under cursor if any
			CursorInteractable new_obj = find_interactable(ray, out var hit);

			// check if object has changed since last frame
			if (new_obj != active_obj) {
				if (active_obj) {
					// active_obj exited
					active_obj_mat.color = base_col;
					//Debug.Log($">> active_obj {active_obj.name} exited");
				}
				
				// new_obj active
				active_obj = new_obj;
				active_obj_mat = active_obj?.GetComponent<MeshRenderer>().material ?? null;

				if (new_obj) {
					// new_obj entered
					active_obj_mat.color = hover_col;
					//Debug.Log($">> new_obj {active_obj.name} entered");
				}
			}

			if (active_obj) {
				// new_obj hovered
				
				if (dragging_button.isPressed) {
					// begin dragging
					dragging = true;

					active_obj_mat.color = active_col;

					drag_origin = hit.point;
					drag_offs = hit.point - active_obj.transform.position;

					//Debug.Log(">> Begin dragging");
				}
			}
		}
	}

	bool drag_logic (Ray ray, out float3 target) {
		// Otherwise: Drag along horizontal plane
		float3 plane_norm = float3(0,1,0);

		bool move_vertical = Keyboard.current.ctrlKey.isPressed;
		if (move_vertical) {
			//// [CTRL] Drag along axis aligned plane, snap on to camera view
			//plane_norm = largest_axis((float3)ray.origin - drag_origin);
			
			// [CTRL] Drag along plane rotated on Y towards camera view (Move vertical or along view left/right)
			float3 to_cam = (float3)ray.origin - drag_origin;
			plane_norm = lengthsq(to_cam) > 0.0f ? normalize(float3(to_cam.x, 0, to_cam.z)) : float3(0,1,0);
		}

		var plane = new Plane(plane_norm, drag_origin);
		if (!plane.Raycast(ray, out float t)) {
			target = 0;
			return false; // raycast fail, eg. cursor above horizontal plane
		}

		target = ray.GetPoint(t);

		// [CTRL + ALT] snap to ground?
		if (!move_vertical && Keyboard.current.altKey.isPressed) {
			// [ALT] Drag along single largest offset axis
			float3 offs = target - drag_origin;
			float3 snap_axis = MyMath.largest_axis(offs);
			offs *= snap_axis; // snap movement to largest movement axis
			target = offs + drag_origin;
		}

		return true;
	}
}
