using UnityEngine;
using UnityEngine.UIElements;

public class BuildRoadTool : UI_ButtonTool {

	[Header("BuildRoadTool")]
	public RoadTest prefab;
	
	//protected override void activated () {
	//	Debug.Log($"BuildRoadTool deactivated {name}");
	//}
	//protected override void deactivated () {
	//	Debug.Log($"BuildRoadTool deactivated {name}");
	//}

	public override void update () {
		base.update();
	}
}
