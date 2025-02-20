using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;
using UnityEditor;

[RequireComponent(typeof(BoxCollider))]
public class Building : MonoBehaviour {

	public Road connected_road;
	
	public static int _counter = 0;
	public static Building create (Building prefab) {
		var building = Instantiate(prefab, g.entities.buildings_go.transform);
		building.name = $"Building #{_counter++}";
		return building;
	}

#if UNITY_EDITOR
	[ContextMenu("Compute Collider")]
	void ComputeCollider () {
		var collider = gameObject.GetComponent<BoxCollider>();

		var lod_group = GetComponent<LODGroup>();
		var lod0_renderer = lod_group.GetLODs().First().renderers.First();
		var bounds = lod0_renderer.bounds;

		collider.center = bounds.center;
		collider.size = bounds.size;

		EditorUtility.SetDirty(gameObject);
	}
#endif
}
