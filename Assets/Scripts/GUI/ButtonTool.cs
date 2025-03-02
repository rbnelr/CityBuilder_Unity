using UnityEngine;
using UnityEngine.UIElements;

public abstract class ButtonTool {
	Label ui;
	
	bool _active = false;
	//public bool active {
	//	get => _active;
	//	set {
	//		if (value != _active) {
	//			_active = value;
	//			refresh_style();
	//			if (_active) do_trigger();
	//		}
	//	}
	//}

	public ButtonTool (Label ui) {
		this.ui = ui;
		//ui.clicked += press;
		ui.RegisterCallback<PointerDownEvent>(press);
		ui.RegisterCallback<PointerUpEvent>(release);
		ui.RegisterCallback<PointerEnterEvent>(evt => ui.AddToClassList("ToolButton-hovered"));
		ui.RegisterCallback<PointerLeaveEvent>(evt => ui.RemoveFromClassList("ToolButton-hovered"));
	}

	void release (PointerUpEvent evt) {
		_active = false;
		refresh_style();
	}
	void press (PointerDownEvent evt) {
		//ui.CapturePointer(0);

		_active = true;
		refresh_style();

		trigger();
	}

	void refresh_style () {
		if (_active) ui.AddToClassList("ToolButton-active");
		else         ui.RemoveFromClassList("ToolButton-active");
	}

	public abstract void trigger ();
}

public class CustomButtonTool : ButtonTool {

	public CustomButtonTool (Label ui) : base(ui) {}
	
	public override void trigger () {
		Debug.Log($"CustomButtonTool trigger!");
	}
}

public abstract class ToggleTool {
	Button ui;

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

	void refresh_style () {
		if (_active) ui.AddToClassList("ToolButton-active");
		else         ui.RemoveFromClassList("ToolButton-active");
	}

	public virtual void toggle () {
		active = !active;
	}
	protected abstract void activated ();
	protected abstract void deactivated ();

	public ToggleTool (Button ui) {
		this.ui = ui;
		ui.clicked += toggle;
	}
}

public class TestTool : ToggleTool {

	public TestTool (Button ui) : base(ui) {}
	
	protected override void activated () {
		Debug.Log($"TestTool active!");
	}
	protected override void deactivated () {
		Debug.Log($"TestTool inactive!");
	}
}
