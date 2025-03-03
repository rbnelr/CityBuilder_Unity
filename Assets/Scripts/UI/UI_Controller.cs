using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class UI_Controller : MonoBehaviour {
	UIDocument doc;

	public UI_Toolshelf root_toolshelf;
	
	VisualElement[] toolshelf_levels;
	

	private void Start () {
		doc = GetComponent<UIDocument>();

		root_toolshelf.gameObject.SetActiveRecursivelyEx(false);
		
		var toolshelf_container = doc.rootVisualElement.Q<VisualElement>("toolshelf_container");

		toolshelf_levels = new VisualElement[8];
		for (int i=0; i<8; i++) {
			var level = new VisualElement();
			level.AddToClassList("ToolshelfContainerHoriz");
			toolshelf_container.Add(level);
			toolshelf_levels[i] = level;
		}

		var dummy_button = root_toolshelf.create_ui(toolshelf_levels, 0);

		root_toolshelf.active = true;
	}

	bool ui_visible => doc.rootVisualElement.style.display != DisplayStyle.None;
	void toggle_ui_visibility () {
		// https://discussions.unity.com/t/does-uidocument-clear-contents-when-disabled/837983/22
		if (Keyboard.current.f1Key.wasPressedThisFrame) {
			doc.rootVisualElement.style.display = ui_visible ? DisplayStyle.None : DisplayStyle.Flex;
		}
	}

	private void Update () {
		if (Keyboard.current.f1Key.wasPressedThisFrame) {
			toggle_ui_visibility();
		}
		
		if (ui_visible)
			root_toolshelf.update();
	}
}
