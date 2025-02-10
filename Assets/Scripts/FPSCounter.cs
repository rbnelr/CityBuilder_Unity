using UnityEngine;
using System.Collections;

public class Fps : MonoBehaviour {
	public float update_frequency = 10;
	float update_period => 1.0f / update_frequency;

	float display_fps = 0;
	float timer = 0;

	void Update () {
		GUI.depth = 2;
		
		// update <update_frequency> times a second
		timer += Time.unscaledDeltaTime;
		if (timer > update_period) {
			// measure fps
			display_fps = 1f / Time.unscaledDeltaTime;

			// accurately keep track of periods
			timer -= update_period;
			// if unscaledDeltaTime was really high for some reason, just reset timer
			if (timer > update_period) timer = 0;
		}

		DebugHUD.Show($"FPS: {Mathf.Round(display_fps)}");
	}
}
