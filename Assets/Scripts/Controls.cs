using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-200)]
public class Controls : MonoBehaviour {
	public GameCamera main_camera;
	public Flycam debug_camera;

	public bool view_debug_camera = false;


	private void Update () {
		if (Keyboard.current.pKey.wasPressedThisFrame) {
			view_debug_camera = !view_debug_camera;
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
}
