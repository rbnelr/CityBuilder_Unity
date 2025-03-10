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

	public static void SetActiveRecursivelyEx (this GameObject go, bool active) {
		go.SetActive (active);
		foreach (Transform child in go.transform) {
			SetActiveRecursivelyEx(child.gameObject, active);
		}
	}

}

public static class Extensions {
	
//// Collections
	public static void HardClear<T> (this List<T> list) {
		list.Clear();
		list.Capacity = 0;
	}
	
//// Random
	public static bool Chance (this ref Random rand, float probabilty) {
		return rand.NextFloat(0, 1) < probabilty;
	}
	
	public static T? Pick<T> (this ref Random rand, IReadOnlyList<T> choices) {
		//Debug.Assert(choices.Any());
		if (choices.Any())
			return choices[ rand.NextInt(0, choices.Count()) ];
		return default;
	}

//// Serialization
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
}

public static class MyMath {
//// General
	public static float3 largest_axis (float3 d) {
		d = abs(d);
		return d.x >= d.y && d.x >= d.z ? float3(1,0,0) :
			( d.y >= d.z ? float3(0,1,0) : float3(0,0,1) );
	}

	// Rotate vector by 90 degrees in CW
	public static float2 rotate90_right (float2 v) {
		return float2(v.y, -v.x);
	}
	// Rotate vector by 90 degrees in CW around Y
	public static float3 rotate90_right (float3 v) {
		return float3(v.z, v.y, -v.x);
	}

	// wrap x into range [0,range)
	// negative x wrap back to +range unlike c++ % operator
	// negative range supported
	public static int wrap (int x, int range) {
		int modded = x % range;
		if (range > 0) {
			if (modded < 0) modded += range;
		} else {
			if (modded > 0) modded += range;
		}
		return modded;
	}
	
	// wrap x into [a,b) range
	public static int wrap (int x, int a, int b) {
		x -= a;
		int range = b -a;
		
		int modulo = wrap(x, range);
		
		return modulo + a;
	}

//// Intersection
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


public class WeightedChoice {
	// TODO: could be optimized using a binary search

	public static int Get (IReadOnlyList<float> weights, float rand01) {
		float total = 0;
		for (int i=0; i<weights.Count; ++i) {
			total += weights[i];
		}

		if (total <= 0) return -1;

		rand01 *= total;

		float accum = 0;
		for (int i=0; i<weights.Count; ++i) {
			accum += weights[i];
			if (rand01 < accum)
				return i;
		}

		return weights.Count-1;
	}
}
public static class WeightedChoiceExt {
	public static int Weighted (this ref Random rand, IReadOnlyList<float> weights) {
		return WeightedChoice.Get(weights, rand.NextFloat());
	}
	
	public static T? Weighted<T> (this ref Random rand, IReadOnlyList<T> items, Func<T, float> get_weight) {
		var weights = items.Select(get_weight).ToArray(); // Not sure how to avoid alloc here
		var idx = WeightedChoice.Get(weights, rand.NextFloat());
		return idx >= 0 ? items[idx] : default;
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

public class DebugVis {
	public static void DebugCurvedMeshNormals (Bezier bez, Mesh mesh, Transform transform) {

		DebugMeshNormals.DrawOnGizmos(mesh, transform.localToWorldMatrix, 0.25f,
			v => {
				var ret = new DebugMeshNormals.Vertex();
				bez.curve_mesh(v.position, v.normal, v.tangent,
					out ret.position, out ret.normal, out ret.tangent);
				return ret;
			});
	}
}