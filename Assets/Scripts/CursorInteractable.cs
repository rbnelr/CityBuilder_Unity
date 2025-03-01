using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

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
	Plane drag_plane;
	Vector3 drag_offs;
	
	public Color base_col;
	public Color hover_col;
	public Color active_col;

	CursorInteractable find_interactable (Ray? ray, out RaycastHit hit) {
		hit = new RaycastHit();
		if (ray.HasValue && Physics.Raycast(ray.Value, out hit, Mathf.Infinity, Controls.INTERACTABLE_LAYER)) {
			if (hit.collider.gameObject.TryGetComponent<CursorInteractable>(out var inter)) {
				return inter;
			}
		}
		return null;
	}

	ButtonControl dragging_button => Mouse.current.leftButton;

	public void Update () {
		Ray? ray = Controls.cursor_ray();
		
		// while dragging, drag object with cursor
		if (dragging) {
			if (dragging_button.isPressed) {
				// actively dragging

				// Physics.Raycast(cursor_ray, out var info, Mathf.Infinity, Controls.GROUND_LAYER)
				if (ray.HasValue && drag_plane.Raycast(ray.Value, out float enter)) {
					Vector3 point = ray.Value.GetPoint(enter);
					// move object along plane with existing offset
					active_obj.transform.position = point - drag_offs;
				}
				else {
					// still dragging, but cursor invalid
				}
			}
			else {
				// stop dragging
				dragging = false;
				active_obj_mat.color = hover_col;

				Debug.Log(">> Stop dragging");
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
					Debug.Log($">> active_obj {active_obj.name} exited");
				}
				
				// new_obj active
				active_obj = new_obj;
				active_obj_mat = active_obj?.GetComponent<MeshRenderer>().material ?? null;

				if (new_obj) {
					// new_obj entered
					active_obj_mat.color = hover_col;
					Debug.Log($">> new_obj {active_obj.name} entered");
				}
			}

			if (active_obj) {
				// new_obj hovered
				
				if (dragging_button.isPressed) {
					// begin dragging
					dragging = true;

					active_obj_mat.color = active_col;

					drag_plane = new Plane(Vector3.up, hit.point);
					drag_offs = (Vector3)hit.point - active_obj.transform.position;

					Debug.Log(">> Begin dragging");
				}
			}
		}
	}
}
