using UnityEngine;
using UnityEngine.UIElements;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.InputSystem;
using System.Linq;

public class BulldozeTool : UI_ButtonTool {
	
	public bool delete_selection () {
		if (g.controls.selection.selection.Count > 0) {
			foreach (var obj in g.controls.selection.selection) {
				obj.check?.bulldoze();
			}
			g.controls.selection.clear();
			return true;
		}
		return false;
	}

	// delete selection when tool becomes active
	protected override void activated () {
		delete_selection();
	}
	// TODO: implement single click delete
	// TODO: implement click-drag multi-delete
	// TODO: implement click-drag road delete (via pathfinding)
	public override void update () {
		// Can reuse selection class here, use a local copy

		// single click delete: left click went down: replace select hovered
		// single click delete: left click is down && selection and current hover is road/junction: pathfind between junctions and replace sel with all roads in path

		// multi-delete: add select hovered

		// highlight selection

		// left click release: accept delete selection
	}
}
