using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;
using UnityEditor;
using Newtonsoft.Json.Linq;

#nullable enable

public static class Util {
	public static void HardClear<T> (this List<T> list) {
		list.Clear();
		list.Capacity = 0;
	}

	public static bool Chance (this ref Random rand, float probabilty) {
		return rand.NextFloat(0, 1) < probabilty;
	}
	
	public static T? Pick<T> (this ref Random rand, IReadOnlyList<T> choices) {
		//Debug.Assert(choices.Any());
		if (choices.Any())
			return choices[ rand.NextInt(0, choices.Count()) ];
		return default;
	}

	public static T? TryGet<T> (this JObject jobj, string key) {
		if (jobj.TryGetValue(key, out JToken? value)) return value.Value<T>();
		return default;
	}
	public static bool TryGet<T> (this JObject jobj, string key, ref T value) {
		if (jobj.TryGetValue(key, out JToken? val)) {
			value = val.Value<T>()!;
			return true;
		}
		return false;
	}

	public static void GizmosDrawArrow (Vector3 pos, Vector3 direction, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20.0f) {
		Gizmos.DrawRay(pos, direction);
		
		Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0,180+arrowHeadAngle,0) * new Vector3(0,0,1);
		Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0,180-arrowHeadAngle,0) * new Vector3(0,0,1);
		Gizmos.DrawRay(pos + direction, right * arrowHeadLength);
		Gizmos.DrawRay(pos + direction, left * arrowHeadLength);
	}

	public static GameObject? FindChildGameObjectByName (GameObject parent, string name) {
		for (int i=0; i<parent.transform.childCount; i++) {
			var child = parent.transform.GetChild(i).gameObject!;

			if (child.name == name) {
				return child;
			}

			GameObject? res = FindChildGameObjectByName(child, name);
			if (res != null)
				return res;
		}

		return null;
	}
	
	public static void Destroy (Transform transform) {
		if (Application.isEditor) {
			MonoBehaviour.DestroyImmediate(transform.gameObject);
		}
		else {
			MonoBehaviour.Destroy(transform.gameObject);
		}
	}
	public static void DestroyChildren (Transform transform) {
		if (Application.isEditor) {
			for (int i=transform.childCount-1; i>=0; i--) {
				MonoBehaviour.DestroyImmediate(transform.GetChild(i).gameObject);
			}
		}
		else {
			foreach (Transform c in transform) {
				MonoBehaviour.Destroy(c.gameObject);
			}
		}
	}
}

public class MyMath {
	// Rotate vector by 90 degrees in CW
	public static float2 rotate90_right (float2 v) {
		return float2(v.y, -v.x);
	}
	// Rotate vector by 90 degrees in CW around Y
	public static float3 rotate90_right (float3 v) {
		return float3(v.z, v.y, -v.x);
	}

	
	public static bool line_line_intersect (float2 a, float2 ab, float2 c, float2 cd, out float2 out_point) {
		out_point = 0;

		// https://en.wikipedia.org/wiki/Line%E2%80%93line_intersection
		float2 ac = c - a;
		float numer = ac.x * cd.y - ac.y * cd.x;
		float denom = ab.x * cd.y - ab.y * cd.x;
		if (denom == 0)
			return false; // parallel, either overlapping (numer == 0) or not

		float u = numer / denom;
		out_point = a + u*ab;
		return true; // always intersect for now
	}
}

// https://discussions.unity.com/t/logarithmic-slider/563185
#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(LogarithmicRangeAttribute))]
public class LogarithmicRangeDrawer : PropertyDrawer {
	public override void OnGUI (Rect position, SerializedProperty property, GUIContent label) {
		LogarithmicRangeAttribute attribute = (LogarithmicRangeAttribute)this.attribute;
		if (property.propertyType != SerializedPropertyType.Float) {
			EditorGUI.LabelField(position, label.text, "Use LogarithmicRange with float.");
			return;
		}

		Slider(position, property, attribute.min, attribute.max, attribute.power, label);
	}

	public static void Slider (
		Rect position, SerializedProperty property,
		float leftValue, float rightValue, float power, GUIContent label) {
		label = EditorGUI.BeginProperty(position, label, property);
		EditorGUI.BeginChangeCheck();
		float num = PowerSlider(position, label, property.floatValue, leftValue, rightValue, power);

		if (EditorGUI.EndChangeCheck())
			property.floatValue = num;
		EditorGUI.EndProperty();
	}

	public static float PowerSlider (Rect position, GUIContent label, float value, float leftValue, float rightValue, float power) {
		var editorGuiType = typeof(EditorGUI);
		var methodInfo = editorGuiType.GetMethod(
			"PowerSlider",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
			null,
			new[] { typeof(Rect), typeof(GUIContent), typeof(float), typeof(float), typeof(float), typeof(float) },
			null);
		if (methodInfo != null) {
			return (float)methodInfo.Invoke(null, new object[] { position, label, value, leftValue, rightValue, power });
		}
		return leftValue;
	}
}
#endif

[AttributeUsage(AttributeTargets.Field)]
public class LogarithmicRangeAttribute : PropertyAttribute {
	public readonly float min = 1e-3f;
	public readonly float max = 1e3f;
	public readonly float power = 2;
	public LogarithmicRangeAttribute (float min, float max, float power) {
		if (min <= 0) {
			min = 1e-4f;
		}
		this.min = min;
		this.max = max;
		this.power = power;
	}
}

public struct Bezier {
	public float3 a, b, c, d;
	
	public Bezier (float3 a, float3 b, float3 c, float3 d) {
		this.a = a;
		this.b = b;
		this.c = c;
		this.d = d;
	}

	public static Bezier from_line (float3 a, float3 b) {
		return new Bezier(a, lerp(a,b,0.333333f), lerp(a,b,0.666667f), b);
	}

	public struct PosVel {
		public float3 pos;
		public float3 vel; // velocity (delta position / delta bezier t)
	}
	public PosVel eval (float t) {
		float3 c0 = a;                   // a
		float3 c1 = 3 * (b - a);         // (-3a +3b)t
		float3 c2 = 3 * (a + c) - 6*b;   // (3a -6b +3c)t^2
		float3 c3 = 3 * (b - c) - a + d; // (-a +3b -3c +d)t^3

		float t2 = t*t;
		float t3 = t2*t;
		
		float3 value = c3*t3     + c2*t2    + c1*t + c0; // f(t)
		float3 deriv = c3*(t2*3) + c2*(t*2) + c1;        // f'(t)
		
		return new PosVel { pos=value, vel=deriv };
	}
	
	public void debugdraw (int res=10) {
		float3 prev = a;

		for (int i=0; i<res; i++) {
			float t = (float)(i+1) * (1.0f / res);
			float3 pos = eval(t).pos;

			if (i < res-1) Gizmos.DrawLine(prev, pos);
			else           Util.GizmosDrawArrow(prev, pos-prev, 1);

			prev = pos;
		}
	}

	public Bezier reverse () {
		return new Bezier(d,c,b,a);
	}
	
	public float approx_len (int res=10) {
		float3 prev = a;

		float len = 0;
		for (int i=0; i<res; ++i) {
			float t = (float)(i+1) * (1.0f / res);
			float3 pos = eval(t).pos;

			len += length(pos - prev);

			prev = pos;
		}

		return len;
	}
}
