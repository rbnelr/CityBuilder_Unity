using UnityEngine;
using UnityEngine.UIElements;

public abstract class BaseButtonTool : ScriptableObject {
	protected Label ui;
	
	public void init (Label ui) {
		this.ui = ui;
	}
}

public abstract class ButtonTool : BaseButtonTool {
	
	bool _held = false;
	public bool held => _held;

	public void init (Label ui) {
		base.init(ui);
		ui.RegisterCallback<PointerDownEvent>(on_press);
		ui.RegisterCallback<PointerUpEvent>(on_release);
		ui.RegisterCallback<PointerEnterEvent>(evt => ui.AddToClassList("ToolButton-hovered"));
		ui.RegisterCallback<PointerLeaveEvent>(evt => ui.RemoveFromClassList("ToolButton-hovered"));
	}
	
	void on_press (PointerDownEvent evt) {
		_held = true;
		refresh_style();

		trigger();
	}
	void on_release (PointerUpEvent evt) {
		_held = false;
		refresh_style();
	}

	void refresh_style () {
		if (_held) ui.AddToClassList("ToolButton-active");
		else       ui.RemoveFromClassList("ToolButton-active");
	}

	public virtual void trigger () {}
}

public abstract class ToggleTool : BaseButtonTool {

	bool _active = false;
	public bool active {
		get => _active;
		set {
			if (value != _active) {
				_active = value;
				refresh_style();
				if (_active) activated();
				else         deactivated();
			}
		}
	}

	void on_press (PointerDownEvent evt) {
		toggle();
	}

	void refresh_style () {
		if (_active) ui.AddToClassList("ToolButton-active");
		else         ui.RemoveFromClassList("ToolButton-active");
	}

	public void toggle () {
		active = !active;
	}
	protected virtual void activated () {}
	protected virtual void deactivated () {}

	public void init (Label ui) {
		base.init(ui);
		ui.RegisterCallback<PointerDownEvent>(on_press);
		ui.RegisterCallback<PointerEnterEvent>(evt => ui.AddToClassList("ToolButton-hovered"));
		ui.RegisterCallback<PointerLeaveEvent>(evt => ui.RemoveFromClassList("ToolButton-hovered"));
	}
}

public class CustomButtonTool : ButtonTool {
	public CustomButtonTool (Label ui) { base.init(ui); }
	
	public override void trigger () {
		Debug.Log($"CustomButtonTool trigger!");
	}
}

public class CustomToggleTool : ToggleTool {

	public CustomToggleTool (Label ui) { base.init(ui); }
	
	protected override void activated () {
		Debug.Log($"CustomToggleTool active!");
	}
	protected override void deactivated () {
		Debug.Log($"CustomToggleTool inactive!");
	}
}

public class BuildTool : ToggleTool {
	public BuildTool (Label ui) { base.init(ui); }
}
public class TerraformTool : ToggleTool {
	public TerraformTool (Label ui) { base.init(ui); }
}
public class TerraformMoveTool : ToggleTool {
	public TerraformMoveTool (Label ui) { base.init(ui); }
}
public class TerraformFlattenTool : ToggleTool {
	public TerraformFlattenTool (Label ui) { base.init(ui); }
}
public class TerraformSmoothTool : ToggleTool {
	public TerraformSmoothTool (Label ui) { base.init(ui); }
}
