using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class UI_Toolshelf : UI_ButtonTool {
	protected VisualElement ui_shelf;

	[NonSerialized] public List<UI_ButtonTool> subtools;

	public override VisualElement create_ui (VisualElement[] toolshelf_levels, int level) {
		base.create_ui();

		if (level < toolshelf_levels.Length) {

			ui_shelf = new VisualElement();
			ui_shelf.AddToClassList("Toolshelf");
			if (!active) ui_shelf.style.display = DisplayStyle.None;
			toolshelf_levels[level].Add(ui_shelf);

			subtools = new List<UI_ButtonTool>();
			foreach (Transform child in transform) {
				var tool = child.GetComponent<UI_ButtonTool>();
				if (tool) {
					ui_shelf.Add( tool.create_ui(toolshelf_levels, level+1) );

					subtools.Add(tool);
					tool.parent = this;
				}
			}
		}
		return ui_button;
	}
	
	// deactivate exclusive sibling tools
	public void deactivate_nonexclusive_siblings (UI_ButtonTool child) {
		foreach (var tool in subtools) {
			if (tool != child && tool.exclusive)
				tool.active = false;
		}
	}
	void deactivate_subtools () {
		foreach (var tool in subtools) {
			tool.active = false;
		}
	}

	protected override void on_activated () {
		//Debug.Log($"Toolshelf on_activated {name}");

		ui_shelf.style.display = DisplayStyle.Flex;

		base.on_activated();
	}
	protected override void on_deactivated () {
		//Debug.Log($"Toolshelf on_deactivated {name}");

		deactivate_subtools();
		ui_shelf.style.display = DisplayStyle.None;

		base.on_deactivated();
	}

	public override void update () {
		// RMB and ESC auto deactivates top-level active and exclusive tool
		bool should_deactivate = parent != null && exclusive && subtools.All(x => !x.active);
		if (should_deactivate && deactivate_pressed)
			active = false;

		foreach (var tool in subtools) {
			if (tool.active) {
				tool.update();
			}
		}
	}
}
