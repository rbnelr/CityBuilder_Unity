using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class UI_ButtonTool : MonoBehaviour {
	protected Label ui_button;

	public bool exclusive = true;
	public bool toggable = true;

	public string[] custom_ui_class_list = new string[0];

	[NonSerialized] public UI_Toolshelf parent = null;

	protected bool deactivate_pressed => Mouse.current.rightButton.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame;

	public bool active {
		get => gameObject.activeSelf;
		set {
			if (value != gameObject.activeSelf) {
				gameObject.SetActive(value);

				refresh_style();

				if (gameObject.activeSelf) on_activated();
				else                       on_deactivated();
			}
		}
	}
	public void toggle () {
		active = !active;
	}

	public virtual VisualElement create_ui (VisualElement[] toolshelf_levels=null, int level=-1) {
		ui_button = new Label(name);
		ui_button.AddToClassList("ToolButton");
		foreach (var c in custom_ui_class_list)
			ui_button.AddToClassList(c);

		ui_button.RegisterCallback<PointerDownEvent>(on_press);
		ui_button.RegisterCallback<PointerUpEvent>(on_release);
		ui_button.RegisterCallback<PointerEnterEvent>(evt => ui_button.AddToClassList("ToolButton-hovered"));
		ui_button.RegisterCallback<PointerLeaveEvent>(evt => ui_button.RemoveFromClassList("ToolButton-hovered"));

		refresh_style();
		return ui_button;
	}
	
	void on_press (PointerDownEvent evt) {
		if (toggable) {
			toggle();
		} else {
			active = true;
		}
	}
	void on_release (PointerUpEvent evt) {
		if (toggable) {
			
		} else {
			active = false;
		}
	}
		
	void refresh_style () {
		if (active) ui_button.AddToClassList("ToolButton-active");
		else        ui_button.RemoveFromClassList("ToolButton-active");
	}
	
	protected virtual void on_activated () {
		if (exclusive) {
			if (parent) parent.deactivate_nonexclusive_siblings(this);
		}

		//Debug.Log($"ButtonTool on_activated {name}");

		activated();
	}
	protected virtual void on_deactivated () {
		//Debug.Log($"ButtonTool on_deactivated {name}");
		deactivated();
	}

	protected virtual void activated () {}
	protected virtual void deactivated () {}

	public virtual void update () {
		// RMB and ESC auto deactivates top-level active and exclusive tool
		bool should_deactivate = parent != null && exclusive;
		if (should_deactivate && deactivate_pressed)
			active = false;
	}
}
