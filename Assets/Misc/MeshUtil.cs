using UnityEngine;

public class MeshUtil : MonoBehaviour {
	
	[NaughtyAttributes.Button]
	void RecalculateTangents () {
		var mesh = GetComponent<MeshFilter>().sharedMesh;
		mesh.RecalculateTangents();
	}
}
