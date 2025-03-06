using UnityEngine;
using UnityEngine.Rendering.Universal;

public class DecalTest : MonoBehaviour {

	public Color color;

	DecalProjector projector;
	void Start () {
		projector = GetComponent<DecalProjector>();
	}

	void Update () {
		projector.material.color = color;
	}
}
