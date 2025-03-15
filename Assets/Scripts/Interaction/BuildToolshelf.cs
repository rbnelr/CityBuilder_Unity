using UnityEngine;
using UnityEngine.UIElements;

public class BuildToolshelf : UI_Toolshelf {
	public Color preview_tint;


	public enum BuildMode {
		STRAIGHT, CURVED
	};

	public BuildMode cur_mode = BuildMode.STRAIGHT;

	
	public UI_BuildRoadSettings settings_ui;
	public override VisualElement create_ui (UI_Controller uic, int level) {
		settings_ui.create_ui(this, uic.doc);
		return base.create_ui(uic, level);
	}
	protected override void deactivated () { settings_ui.ui.style.display = DisplayStyle.None; }
	protected override void activated () { settings_ui.ui.style.display = DisplayStyle.Flex; }
}

// Ugh this sucks, I have no idea how to properly use this UI library
[System.Serializable]
public class UI_BuildRoadSettings {
	public VisualElement ui { get; private set; }

	public VisualTreeAsset template;

	// ????
	//UI_ButtonTool s = new UI_ButtonTool();
	//UI_ButtonTool c = new UI_ButtonTool();
	
	public void create_ui (BuildToolshelf cls, UIDocument doc) {
		ui = template.Instantiate();
		if (!cls.active) ui.style.display = DisplayStyle.None;

		//s.create_ui_for_existing(ui.Q<Label>("Straight"));
		//s.create_ui_for_existing(ui.Q<Label>("Curved"));

		doc.rootVisualElement.Q<VisualElement>("settings_left").Add(ui);
	}
}
