using System.Reflection;
using UnityEditor;
using UnityEngine;

// Convenient class to allow automatically smoothed values like camera zoom
[System.Serializable]
public struct SmoothedVar : ISerializationCallbackReceiver {
	float _real_value;
	[InspectorName("value"), NaughtyAttributes.OnValueChanged("InstantlySetToTarget")]
	public float target_value; // get/set target value, essentially chaning value while allowing it to be smoothed
	float _velocity;
	public float smoothing_time;

	// get: read real value
	// set: instantly set real value
	public float value {
		get => _real_value;
		set {
			_real_value = value;
			target_value = value;
			_velocity = 0;
			//Debug.Log($"Value set to: {value}");
		}
	}
	void InstantlySetToTarget () {
		value = target_value;
	}
	
	public void Update (float dt){
		_real_value = Mathf.SmoothDamp(_real_value, target_value, ref _velocity, smoothing_time, float.PositiveInfinity, dt);
	}

	public void OnBeforeSerialize () {
		
	}
	public void OnAfterDeserialize () {
		InstantlySetToTarget();
	}
}

#if false
// Unity does not support properties in the insepctor, need to create CustomPropertyDrawer and manually implement it
[CustomPropertyDrawer(typeof(SmoothedVar))]
public class SmoothedVarDrawer : PropertyDrawer {

	public override void OnGUI (Rect position, SerializedProperty property, GUIContent label) {
		if (fieldInfo.GetValue<SmoothedVar>(property.serializedObject.targetObject) is  smoothed_var) {
		
			var range = fieldInfo.GetCustomAttribute<SmoothedVar.RangeAttribute>();

			EditorGUI.BeginProperty(position, label, property);
		
			float new_value;
			if (range != null) new_value = EditorGUI.Slider(position, label, smoothed_var.value, range.min, range.max);
			else               new_value = EditorGUI.FloatField(position, label, smoothed_var.value);

			EditorGUI.EndProperty();
		
			//EditorGUI.PropertyField(position, property, label, true);
		
			if (!Mathf.Approximately(new_value, smoothed_var.value)) {
				smoothed_var.value = new_value; // trigger property setter om change
			
				EditorUtility.SetDirty(property.serializedObject.targetObject);
			}
		}
	}
}
#endif
