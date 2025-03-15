using UnityEngine;
using UnityEngine.UIElements;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.InputSystem;
using System.Linq;

public class BulldozeTool : UI_ButtonTool {
	
	public void delete_selection () {
		foreach (var obj in g.controls.selection.selection) {
			obj.check?.bulldoze();
		}
		g.controls.selection.clear();
	}

	protected override void activated () {
		delete_selection();
	}
	public override void update () {
		
	}
}
