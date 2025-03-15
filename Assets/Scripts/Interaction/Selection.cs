using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.InputSystem;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.InputSystem.Controls;

[System.Serializable]
public class Selection {

	public Color highlighted_tint;
	public Color selected_tint;
	public Color selected_highl_tint;

	ButtonControl select_button => Mouse.current.leftButton;
	bool unselect_button => Mouse.current.rightButton.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame;
	bool add2select_button => Keyboard.current.shiftKey.isPressed;

	public static ISelectable raycast_hover () {
		if (Controls.raycast(out var hit, Controls.JUNCTIONS_LAYER)) {
			return hit.collider.gameObject.GetComponent<ISelectable>();
		}
		return null;
	}

	ISelectable hover = null;

	public HashSet<ISelectable> selection = new HashSet<ISelectable>();

	public void clear () {
		selection = new HashSet<ISelectable>();
	}

	public void update_cursor_select () {
		foreach (var obj in selection) {
			Debug.Assert(obj.unity_null || obj != null);
			if (obj.unity_null) selection.Remove(obj);
			else obj.highlight(false, new Color(0,0,0,0));
		}

		hover?.check?.highlight(false, new Color(0,0,0,0));

		hover = raycast_hover();

		if (unselect_button) {
			clear();
		}
		if (select_button.wasPressedThisFrame) {
			if (add2select_button) {
				if (hover != null) {
					if (selection.Contains(hover)) selection.Remove(hover);
					else                           selection.Add(hover);
				}
			}
			else {
				clear();
				if (hover != null) selection.Add(hover);
			}
		}
		
		foreach (var obj in selection) {
			if (obj != null) obj.highlight(true, selected_tint);
		}
		
		if (hover != null)
			hover.highlight(true, selection.Contains(hover) ? selected_highl_tint : highlighted_tint);
	}
}

public interface ISelectable {
	public bool unity_null => this is UnityEngine.Object obj && obj == null;
	public ISelectable check => unity_null ? null : this;
	public void bulldoze ();
	public void highlight (bool is_highlighted, Color tint);
}
