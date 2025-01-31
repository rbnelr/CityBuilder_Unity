using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;
using UnityEngine.InputSystem;

public class GameTime : MonoBehaviour {

	public bool paused = false;
	[Range(0, 10)]
	public float speed = 1;

	public float dt => paused ? 0 : speed * Time.deltaTime;
	

	public static GameTime inst;
	void Awake () {
		inst = this;
	}

	void Update () {
		if (Keyboard.current.spaceKey.wasPressedThisFrame)
			paused = !paused;
	}
}
