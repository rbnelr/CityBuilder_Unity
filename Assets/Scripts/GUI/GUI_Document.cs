using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class GUI_Document : MonoBehaviour {

	public UIDocument doc;

	HashSet<object> all_tools;

	private void Start () {
		all_tools = new HashSet<object>();
		doc = GetComponent<UIDocument>();

		var test1 = doc.rootVisualElement.Q<Label>("MoveTool2");
		var test = doc.rootVisualElement.Q<Button>("TestTool");

		//test.clicked += () => do_test();

		all_tools.Add( new CustomButtonTool(test1) );
		all_tools.Add( new TestTool(test) );
	}

	public VehicleAsset prefab;

	private void do_test () {
		var ray = Controls.cursor_ray();
		if (ray.HasValue && Physics.Raycast(ray.Value, out var hit, Mathf.Infinity, Controls.GROUND_LAYER)) {

			var vehicle = Instantiate(prefab.instance_prefab, hit.point, Quaternion.identity).GetComponent<Vehicle>();
			vehicle.enabled = false; // avoid disappearing
		}
	}
}
