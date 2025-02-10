using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.InputSystem;
using NaughtyAttributes;
using UnityEditor;
using System.Reflection;

[RequireComponent(typeof(Camera))]
public class GameCamera : MonoBehaviour {

	public float3 orbit_pos = 0;
	[ShowNativeProperty]
	public Vector3 cam_pos => GetComponent<Camera>().transform.position;

	// CCW  0=north (+Z)  90=east(+X)  (compass)
	[Range(-180, 180)]
	public float azimuth = 0;
	// 0=looking at horizon  90=looking up  (flight HUDs)
	[Range(-90, 90)]
	public float elevation = 0;
	// 0=normal  90=camera rolled towards right
	[Range(-180, 180)]
	public float roll = 0;

	public SmoothedVar zoom = new SmoothedVar { value = 10, smoothing_time = 0.1f };
	
	public SmoothedVar fov = new SmoothedVar { value = 70, smoothing_time = 0.1f };
	float default_vfov = 70;
	
	private void Awake () {
		default_vfov = fov.value;
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
	
	bool look_button => Mouse.current.middleButton.isPressed;
	bool change_fov => Keyboard.current.fKey.isPressed;
	float scroll_delta => Mouse.current.scroll.ReadValue().y;

	float2 get_mouse_look_delta () {
		float2 look = Mouse.current.delta.ReadValue();
		
		// Apply sensitivity and scale by FOV
		// scaling by FOV might not be wanted in all situations (180 flick in a shooter would need new muscle memory with other fov)
		// but usually muscle memory for flicks are supposedly based on offsets on screen, which do scale with FOV, so FOV scaled sens seems to be better
		// look_sensitivity is basically  screen heights per 100 mouse dots, where dots are moved mouse distance in inch * mouse dpi
		look *= 0.001f * look_mouse_sensitivity * fov.value;
		
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
#endregion

	void LateUpdate () {
		float3 move3d = get_move3d();
		float roll_dir = 0; // could bind to keys, but roll control via UI slider is good enough

		float2 look_delta = get_look_delta();

		{ //// zoom or fov change with mousewheel
			if (!change_fov) { // scroll changes zoom
				//float log = log2(base_speed);
				//log += 0.1f * scroll_delta;
				//base_speed = clamp(pow(2.0f, log), 0.001f, max_speed);
			}
			else { // F+scroll changes fov
				float log = log2(fov.target_value);
				log -= 0.1f * scroll_delta;
				fov.target_value = clamp(pow(2.0f, log), 1.0f/10, 170.0f);
				
				if (Keyboard.current.leftShiftKey.isPressed && scroll_delta != 0) // shift+F+scroll resets fov
					fov.value = default_vfov;
			}

			fov.Update(Time.unscaledDeltaTime);
			GetComponent<Camera>().fieldOfView = fov.value;
		}

		{ //// Camera rotation
			azimuth += look_delta.x;
			azimuth = (azimuth + 180.0f) % 360.0f - 180.0f;

			elevation += look_delta.y;
			elevation = clamp(elevation, -90.0f, +90.0f);

			roll += roll_dir * roll_speed * Time.unscaledDeltaTime;
			roll = (roll + 180.0f) % 360.0f - 180.0f;

			transform.rotation = Quaternion.Euler(-elevation, azimuth, -roll);
		}
		
		//{ //// movement
		//	if (lengthsq(move3d) == 0.0f)
		//		cur_speed = base_speed; // no movement resets speed
		//
		//	if (Keyboard.current.leftShiftKey.isPressed) {
		//		cur_speed += base_speed * speedup_factor * Time.unscaledDeltaTime;
		//	}
		//
		//	cur_speed = clamp(cur_speed, base_speed, max_speed);
		//
		//	float3 move_delta = cur_speed * move3d * Time.unscaledDeltaTime;
		//
		//	orbit_pos += Quaternion.AngleAxis(azimuth, Vector3.up) * move_delta;
		//
		//	transform.position = orbit_pos;
		//}
	}
}
