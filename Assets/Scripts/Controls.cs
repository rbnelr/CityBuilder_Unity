using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[DefaultExecutionOrder(-200)]
public class Controls : MonoBehaviour {
	public const int GROUND_LAYER = 1 << 6;
	public const int JUNCTIONS_LAYER = 1 << 7;
	public const int _TMP_LAYER = 1 << 8;

	public GameCamera main_camera;
	public Flycam debug_camera;

	public bool view_debug_camera = false;

	public Camera active_camera => view_debug_camera ? debug_camera.GetComponent<Camera>() : main_camera.GetComponent<Camera>();

	public CursorDragging cursor_dragging;

	public BulldozeTool bulldoze;
	public Selection selection;

	public static Ray? cursor_ray () {
		if (!Mouse.current.enabled)
			return null;
		// Can this ever be invalid? what if cursor out of window?
		var cursor_pos = Mouse.current.position.ReadValue();
		var ray = Camera.main.ScreenPointToRay(float3(cursor_pos, 0));
		return ray;
	}

	public static bool raycast (out RaycastHit hit, int layerMask) {
		hit = default;
		var ray = cursor_ray();
		return ray.HasValue && Physics.Raycast(ray.Value, out hit, INFINITY, layerMask);
	}
	public static bool raycast_ground (out RaycastHit hit) {
		hit = default;
		var ray = cursor_ray();
		return ray.HasValue && Physics.Raycast(ray.Value, out hit, INFINITY, GROUND_LAYER);
	}

	void camera_controls () {
		if (Keyboard.current.f2Key.wasPressedThisFrame) {
			view_debug_camera = !view_debug_camera;
		}
		if (Keyboard.current.altKey.isPressed && Keyboard.current.enterKey.wasPressedThisFrame) {
			Screen.fullScreen = !Screen.fullScreen;
		}

		if (debug_camera.gameObject.activeInHierarchy != view_debug_camera) { // if changed through button or through inspector

			// toggle camera active state
			main_camera.gameObject.SetActive(!view_debug_camera);
			debug_camera.gameObject.SetActive(view_debug_camera);

			// move debug camera to main (act as if debug camera was spawned)
			if (view_debug_camera) {
				debug_camera.transform.position = main_camera.cam_pos;

				debug_camera.azimuth   = main_camera.azimuth;
				debug_camera.elevation = main_camera.elevation;
				debug_camera.roll      = main_camera.roll;
			}
		}
	}

	private void Update () {
		camera_controls();

		cursor_dragging.Update();

		selection.update_cursor_select();

		if (Keyboard.current.deleteKey.wasPressedThisFrame) {
			bulldoze.toggle();
		}
	}
}

