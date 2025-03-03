using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

public class GUI_Document : MonoBehaviour {

	public UIDocument doc;

	HashSet<BaseButtonTool> tools;

	private void Start () {
		tools = new HashSet<BaseButtonTool>();
		doc = GetComponent<UIDocument>();

		//var test1 = doc.rootVisualElement.Q<Label>("MoveTool2");
		//var test = doc.rootVisualElement.Q<Label>("TestTool");
		//
		////test.clicked += () => do_test();
		//
		//all_tools.Add( new CustomButtonTool(test1) );
		//all_tools.Add( new TestTool(test) );

		create_tool_from_ui_doc();
	}

	void create_tool_from_ui_doc () {
		var toolshelf_root = doc.rootVisualElement.Q<VisualElement>("toolshelf_root");
		foreach (var toolshelf in toolshelf_root.Children()) {
			foreach (Label tool in toolshelf.Children().OfType<Label>()) {
				create_tool_from_ui_element(tool);
			}
		}
	}
	void create_tool_from_ui_element (Label elem) {
		string tool_name = elem.name;
		Type type = Type.GetType(tool_name, false);
		if (type == null || !typeof(BaseButtonTool).IsAssignableFrom(type)) {
			Debug.LogError($"GUI: Unknown or invlid class {tool_name} to create for UI button!");
			return;
		}

		dynamic instance = ScriptableObject.CreateInstance(type);
		//var instance = (BaseButtonTool)Activator.CreateInstance(type, elem);

		instance.init(elem);
		tools.Add((BaseButtonTool)instance);
	}

	public VehicleAsset prefab;
}
