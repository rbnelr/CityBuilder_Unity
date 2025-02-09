using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.InputSystem;
using NaughtyAttributes;

[RequireComponent(typeof(Camera))]
public class Flycam : MonoBehaviour {

	public float base_speed = 10;

	float max_speed = 1000000.0f;
	float speedup_factor = 2;

	[ShowNativeProperty] public string CurrentSpeed => $"{cur_speed}";

	float cur_speed = 0;

	// (azimuth, elevation, roll)
	// azimuth: CCW  0=north (+Z)  90=east(+X)  (compass)
	// elevation: 0=looking at horizon  90=looking up  (flight HUDs)
	// roll: 0=normal  90=camera rolled towards right
	public float3 angles = 0;

	public bool planar_move = false;

	[Header("Controls")]
	public float look_mouse_sensitivity = 1;
	public float look_gamepad_sensitivity = 1;

	public float roll_speed = 45;
	
	// lock and make cursor invisible
	public bool lock_cursor = false;

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
	
	bool manual_look => Mouse.current.middleButton.isPressed;
	bool change_fov => Keyboard.current.fKey.isPressed;
	float scroll_delta => Mouse.current.scroll.ReadValue().y;

	float2 get_mouse_look_delta () {
		float2 look = Mouse.current.delta.ReadValue();
		
		// Apply sensitivity and scale by FOV
		// scaling by FOV might not be wanted in all situations (180 flick in a shooter would need new muscle memory with other fov)
		// but usually muscle memory for flicks are supposedly based on offsets on screen, which do scale with FOV, so FOV scaled sens seems to be better
		// look_sensitivity is basically  screen heights per 100 mouse dots, where dots are moved mouse distance in inch * mouse dpi
		look *= 0.001f * look_mouse_sensitivity * GetComponent<Camera>().fieldOfView;
		
		if (lock_cursor || manual_look) {
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
	float2 get_move2d () {
		float2 move2d = normalizesafe(get_WASD());
		if (move2d.x == 0 && move2d.y == 0)
			move2d = get_left_stick();
		return move2d;
	}
	float3 get_move3d () {
		float3 move3d = float3(get_WASD(), get_QE());
		move3d = normalizesafe(move3d);

		if (lengthsq(move3d) == 0)
			move3d = float3(get_left_stick(), get_gamepad_up_down()); // stick is analog, up down via buttons is digital, how to normalize, if at all?

		return float3(move3d.x, move3d.z, move3d.y); // swap to unity Y-up
	}

	void update_lock_cursor () {
		bool toggle_lock = Keyboard.current.f2Key.wasPressedThisFrame;

		if (toggle_lock) {
			lock_cursor = !lock_cursor;
		}
		if (lock_cursor && !Application.isFocused)
			lock_cursor = false; // enforce unlock when alt-tab

		if (lock_cursor) {
			Cursor.visible = false;
			Cursor.lockState = CursorLockMode.Locked;
		}
		else {
			Cursor.visible = true;
			if (manual_look) {
				Cursor.lockState = CursorLockMode.Confined;
			}
			else {
				Cursor.lockState = CursorLockMode.None;
			}
		}
	}

	void Update () {
		update_lock_cursor();
		
		//float2 move2d = get_move2d();
		//float roll = get_QE();

		float3 move3d = get_move3d();
		float roll = 0;

		float2 look_delta = get_look_delta();

		{ //// Camera rotation
			angles.xy += look_delta;
			angles.x = (angles.x + 360.0f) % 360.0f;
			angles.y = clamp(angles.y, -85.0f, +85.0f);

			angles.z += roll * roll_speed * Time.unscaledDeltaTime;
			angles.z = (angles.z + 360.0f) % 360.0f;

			transform.rotation = Quaternion.Euler(-angles.y, angles.x, -angles.z);
		}
		
		{ //// movement
			if (lengthsq(move3d) == 0.0f)
				cur_speed = base_speed; // no movement resets speed

			if (Keyboard.current.leftShiftKey.isPressed) {
				cur_speed += base_speed * speedup_factor * Time.unscaledDeltaTime;
			}

			cur_speed = clamp(cur_speed, base_speed, max_speed);

			float3 move_delta = cur_speed * move3d * Time.unscaledDeltaTime;

			if (planar_move) {
				transform.position += Quaternion.AngleAxis(angles.x, Vector3.up) * move_delta;
			}
			else {
				transform.position += transform.TransformDirection(move_delta);
			}
		}

		{ //// speed or fov change with mousewheel
			if (!change_fov) {
				float log = log2(base_speed);
				log += 0.1f * Mouse.current.scroll.ReadValue().y;
				base_speed = clamp(pow(2.0f, log), 0.001f, max_speed);
			} else {
				//float delta_log = -0.1f * I.mouse_wheel_delta;
				//vfov = clamp(powf(2.0f, log2f(vfov) + delta_log), deg(1.0f/10), deg(170));
				//
				//if (I.buttons[binds.modifier].is_down && delta_log != 0)
				//	vfov = default_vfov;
			}
		}
	}
}
