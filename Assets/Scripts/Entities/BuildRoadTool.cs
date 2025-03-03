using UnityEngine;
using UnityEngine.UIElements;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine.InputSystem;

public class BuildRoadTool : UI_ButtonTool {

	[Header("BuildRoadTool")]
	public RoadTest prefab;

	RoadTest preview = null;
	int stage = 0;

	bool place_button => Mouse.current.leftButton.wasPressedThisFrame;
	bool undo_button => Mouse.current.rightButton.wasPressedThisFrame;
	
	//protected override void activated () {
	//	Debug.Log($"BuildRoadTool deactivated {name}");
	//}
	protected override void deactivated () {
		//Debug.Log($"BuildRoadTool deactivated {name}");
		Destroy(preview.gameObject);
		preview = null;
		stage = 0;
	}

	public override void update () {

		if (Controls.raycast_ground(out var hit)) {
			if (preview == null) {
				preview = Instantiate(prefab);
				preview.name = "BuildRoadTool-preview";
				preview.set_controls_active(false);

				foreach (var mat in preview.materials) {
					mat.mat.SetColor("_AlbedoColor", new Color(1,1,1, 0.9f));
				}
				stage = 0;
			}

			if (stage == 0) {
				Bezier bez = Bezier.from_line(hit.point, (float3)hit.point + float3(preview.width, 0, 0));
				preview.set_bez(bez);

				if (place_button)
					stage = 1;
			}
			else if (stage == 1) {
				Bezier bez = preview.get_bez();
				bez = Bezier.from_line(bez.a, hit.point);
				preview.set_bez(bez);

				if (place_button) {
					var placed = Instantiate(prefab);
					placed.set_controls_active(false);
					placed.set_bez( preview.get_bez() );
					placed.refresh();

					Destroy(preview.gameObject);
					preview = null;
					stage = 0;
				}
			}
		}

		preview?.refresh();

		if (undo_button) {
			if (stage > 0) {
				stage--;
			}
			else {
				base.update(); // handle right click, TODO: this sucks, why can't we just consume inputs???
			}
		}
	}
}
