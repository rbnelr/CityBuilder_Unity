using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.InputSystem;
using NaughtyAttributes;
using System;
using static UnityEngine.Rendering.DebugUI.Table;
using Unity.VisualScripting;
using UnityEngine.Rendering.Universal;

// TODO: freeze cursor when pressing look key (hide it? or keep it visible but freeze it?)

[RequireComponent(typeof(Camera))]
public class GameCamera : MonoBehaviour {

	public float3 orbit_pos = 0;
	[ShowNativeProperty]
	public Vector3 cam_pos => GetComponent<Camera>().transform.position;
	
	public float base_speed = 1.0f;

	// CCW  0=north (+Z)  90=east(+X)  (compass)
	[Range(-180, 180)]
	public float azimuth = 0;
	// 0=looking at horizon  90=looking up  (flight HUDs)
	[Range(-90, 90)]
	public float elevation = 0;
	// 0=normal  90=camera rolled towards right
	[Range(-180, 180)]
	public float roll = 0;

	//public SmoothedVar zoom = new SmoothedVar { value = 10, smoothing_time = 0.1f };
	
#region zoom
	float _zoom;
	public float zoom {
		get => _zoom;
		set {
			_zoom = value;
			zoom_target = value;
			zoom_velocity = 0;
		}
	}
	void hard_set_zoom () { zoom = zoom_target; }

	[SerializeField, InspectorName("Zoom"), LogarithmicRange(0.1f, 10000.0f, 2), OnValueChanged("hard_set_zoom")]
	public float zoom_target = 200;
	
	public float zoom_min = 0.1f;
	public float zoom_max = 10000;
	public float zoom_sens = 0.2f;
	public float zoom_smoothing = 0.1f;
	float zoom_velocity;
#endregion

#region FOV
	// smoothed fov while gracefully handling public API and inspector

	// get: real fov
	// set: set real fov without smoothing
	public float fov {
		get => GetComponent<Camera>().fieldOfView;
		set {
			GetComponent<Camera>().fieldOfView = value;
			fov_target = value;
			fov_velocity = 0;
		}
	}
	void hard_set_fov () { fov = fov_target; }

	// serialize fov target (since we can't serialize/inspect properties)
	// on inspector modification don't smooth
	[SerializeField, InspectorName("FOV"), Range(0.1f, 179.0f), OnValueChanged("hard_set_fov")]
	public float fov_target = 70;
	
	public float fov_min = 0.1f;
	public float fov_max = 179;
	public float fov_sens = 0.1f;
	public float fov_smoothing = 0.1f;
	float fov_velocity;
	float default_fov;
#endregion
	
	private void Awake () {
		hard_set_fov();
		hard_set_zoom();
		default_fov = fov; // store fov to allow reset to default
	}

#region controls
	[Header("Controls")]
	public float look_mouse_sensitivity = 1;
	public float look_gamepad_sensitivity = 1;

	public float roll_speed = 45;

	static float2 get_WASD () { // unnormalized
		float2 dir = 0;
		dir.y += Keyboard.current.sKey.isPressed ? -1.0f : 0.0f;
		dir.y += Keyboard.current.wKey.isPressed ? +1.0f : 0.0f;
		dir.x += Keyboard.current.aKey.isPressed ? -1.0f : 0.0f;
		dir.x += Keyboard.current.dKey.isPressed ? +1.0f : 0.0f;
		return dir;
	}
	static float get_QE () {
		float val = 0;
		val += Keyboard.current.qKey.isPressed ? -1.0f : 0.0f;
		val += Keyboard.current.eKey.isPressed ? +1.0f : 0.0f;
		return val;
	}
	static float get_PlusMinus () {
		float val = 0;
		val += Keyboard.current.numpadMinusKey.isPressed ? -1.0f : 0.0f;
		val += Keyboard.current.numpadPlusKey.isPressed ? +1.0f : 0.0f;
		return val;
	}
	
	bool look_button => Mouse.current.middleButton.isPressed;
	bool change_fov => Keyboard.current.fKey.isPressed;
	float scroll_delta => Mouse.current.scroll.ReadValue().y;

	float2 get_mouse_look_delta () {
		float2 look = Mouse.current.delta.ReadValue();
		
		// Apply sensitivity and do _not_ scale by FOV
		// scaling by FOV like in Flycam feels wrong for orbit cameras
		look *= 0.1f * look_mouse_sensitivity; // arbitrary 0.1 degrees / mouse dot
		
		if (look_button) {
			return look;
		}
		return 0;
	}
	
	static float2 get_left_stick () => Gamepad.current?.leftStick.ReadValue() ?? float2(0);
	static float2 get_right_stick () => Gamepad.current?.rightStick.ReadValue() ?? float2(0);
	static float get_gamepad_up_down () {
		float value = 0;
		value += Gamepad.current?.leftShoulder.ReadValue() ?? 0;
		value -= Gamepad.current?.leftTrigger.ReadValue() ?? 0;
		return value;
	}

	float2 get_look_delta () {
		float2 look = get_mouse_look_delta();
		if (look.x == 0 && look.y == 0)
			look = get_right_stick() * look_gamepad_sensitivity * 100 * Time.deltaTime;
		return look;
	}
	float3 get_move3d () {
		float3 move3d = float3(get_WASD(), get_QE());
		move3d = normalizesafe(move3d);

		if (lengthsq(move3d) == 0)
			move3d = float3(get_left_stick(), get_gamepad_up_down()); // stick is analog, up down via buttons is digital, how to normalize, if at all?

		return float3(move3d.x, move3d.z, move3d.y); // swap to unity Y-up
	}

	// TODO: is it reasonably possible to keep cursor in place (and visible) while rotating?
	//  Mouse.current.WarpCursorPosition() can move cursor every frame, but might be dodgy
	void update_lock_cursor () {

		if (look_button) {
			//Cursor.visible = false;
			//Cursor.lockState = CursorLockMode.Locked; // Ends up resetting the cursor to the center, which sucks
			Cursor.lockState = CursorLockMode.Confined;
		}
		else {
			//Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;
			
		}
	}
	#endregion

	void camera_zoom_or_fov () {
		if (!change_fov) { // scroll changes zoom
			float delta = get_PlusMinus() * 16 * Time.unscaledDeltaTime;
			delta += scroll_delta;

			float log = log2(zoom_target);
			log -= zoom_sens * delta;
			zoom_target = clamp(pow(2.0f, log), zoom_min, zoom_max);
		}
		else { // F+scroll changes fov
			float log = log2(fov_target);
			log -= fov_sens * scroll_delta;
			fov_target = clamp(pow(2.0f, log), fov_min, fov_max);

			if (Keyboard.current.leftShiftKey.isPressed && scroll_delta != 0) // shift+F+scroll resets fov
				fov = default_fov;
		}

		// smooth zoom to fov_target (in log space)
		_zoom = pow(2.0f, Mathf.SmoothDamp(log2(_zoom), log2(zoom_target), ref zoom_velocity, zoom_smoothing, float.PositiveInfinity, Time.unscaledDeltaTime));
		// smooth fov to fov_target
		GetComponent<Camera>().fieldOfView = Mathf.SmoothDamp(fov, fov_target, ref fov_velocity, fov_smoothing, float.PositiveInfinity, Time.unscaledDeltaTime);
	}
	
	void camera_rotation () {
		float roll_dir = 0; // could bind to keys, but roll control via UI slider is good enough
		float2 look_delta = get_look_delta();

		azimuth += look_delta.x;
		azimuth = (azimuth + 180.0f) % 360.0f - 180.0f;

		elevation += look_delta.y;
		elevation = clamp(elevation, -90.0f, +90.0f);

		roll += roll_dir * roll_speed * Time.unscaledDeltaTime;
		roll = (roll + 180.0f) % 360.0f - 180.0f;

		transform.rotation = Quaternion.Euler(-elevation, azimuth, -roll);
	}

	bool dragging = false;
	float3 dragging_grab_point = 0;

	// TODO: FIXTHIS: while holding, the screen jitters subtly (at least in build)
	// precision issue?

	// Solution: ditch this unstable and unintuitive method and just compute 'screen with based on orbit distance and fov
	//  -> orbit_pos.xz += clac_width_for_fov_and_zoom() [approx visible circle at orbit center] * drag_delta (px) / screen_width (px)
	// could also try to see if we can freeze cursor here just like we wanted for camera look
	void move_camera_with_cursor () {
		if (Mouse.current.leftButton.isPressed) {
			var cursor_pos = Mouse.current.position.ReadValue();
			var camera = GetComponent<Camera>();
			var ray = camera.ScreenPointToRay(float3(cursor_pos, 0));

			if (Mouse.current.leftButton.wasPressedThisFrame) {
				if (Physics.Raycast(ray, out RaycastHit hit)) {
					//// begin dragging
					Debug.Log($"Hit on transform: {hit.transform.name} {hit.point} {hit.distance}");
					// remember worldspace cursor hit
					dragging = true;
					dragging_grab_point = hit.point;
				}
				else {
					// No valid raycast hit, simply don't start dragging
				}
			}

			if (dragging) {
				//// while dragging
				// compute needed orbit_pos such that dragging_grab_point is still under moved cursor
				// problem: distince can be variable, but keeping distance constant feels bad because it moves camera vertially
				// instead do a little math to keep the camera height and zoom parameter constant and only adjust orbit_pos.xz

				// current camera position (NOTE: depends on camera_position_from_orbit_pos() having run to avoid off by one frame wobble)
				float3 camera_pos = transform.position;

				// dist to grab point needed to keep camera height constant
				float dist = (dragging_grab_point.y - camera_pos.y) / ray.direction.y;
				// new camera position
				camera_pos.xz = dragging_grab_point.xz - ((float3)ray.direction).xz * dist;
				// compute new orbit_pos to get camera_pos, reverse of camera_position_from_orbit_pos()
				orbit_pos.xz = camera_pos.xz + ((float3)transform.forward).xz * zoom;


				// Bad version where grab distance stays constant
				//float3 camera_pos = dragging_grab_point - (float3)ray.direction * dragging_grab_dist;
				//orbit_pos.xz = camera_pos.xz + ((float3)transform.forward).xz * zoom;
			}
		}
		else {
			dragging = false;
		}
	}

	void camera_position_from_orbit_pos () {
		float3 collided_pos = orbit_pos - (float3)transform.forward * zoom;

		collided_pos.y = max(collided_pos.y, 0.01f);

		transform.position = collided_pos;
	}

	void camera_movement () {
		float cur_speed = base_speed;
		if (Keyboard.current.shiftKey.isPressed)
			cur_speed *= 2.0f;

		float fov_scale = fov / 55.0f; // slow down when zooming in
		float3 move_delta = cur_speed * zoom * fov_scale * get_move3d() * Time.unscaledDeltaTime;

		orbit_pos += (float3)(Quaternion.AngleAxis(azimuth, Vector3.up) * move_delta);
		
		// needed for move_camera_with_cursor() to work correctly (otherwise it wobbled vertically when zooming)
		camera_position_from_orbit_pos();

		//move_camera_with_cursor(); // TODO: integrate with other draggable objects? or switch button?
		
		// compute final camera position
		camera_position_from_orbit_pos();
	}

	void LateUpdate () {
		update_lock_cursor();

		camera_zoom_or_fov();

		camera_rotation();

		camera_movement();
	}
}
