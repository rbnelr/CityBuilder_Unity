using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#if UNITY_EDITOR
[JsonConverter(typeof(VehicleAssetSerializer))]
#endif
public class VehicleAsset : MonoBehaviour {
	public float max_speed = 50 / 3.6f;
	public float spawn_weight = 1;
	public ColorSet color_set;

	public GameObject instance_prefab;
}

#if UNITY_EDITOR
public class VehicleAssetSerializer : JsonConverter {
	public override bool CanConvert (Type objectType) {
		return objectType == typeof(VehicleAsset);
	}

	public override void WriteJson (JsonWriter writer, object value, JsonSerializer serializer) {
		throw new NotImplementedException();
	}

	public override object ReadJson (JsonReader reader, Type objectType, object value, JsonSerializer serializer) {
		VehicleAsset asset = null;
		try {
			// create temporary gameobject for VehicleAsset
			asset = new GameObject().AddComponent<VehicleAsset>();
			asset.transform.SetParent(AssetManager.inst.tmp_loc);

			// deserialize it from json
			var j = JObject.Load(reader);

			//serializer.Populate(j.CreateReader(), asset); // works but will allow users to directly write unity properties
			asset.name = j.Value<string>("name");
			j.TryGet("max_speed", ref asset.max_speed);
			j.TryGet("spawn_weight", ref asset.spawn_weight);

			string model_file = j.Value<string>("model_file");
			string texture_files = j.Value<string>("texture_files");

			Debug.Log($"Asset definition loaded for {asset.name}");

			// load mesh and script as subobject, then turn everything into prefab
			var prefab = AssetManager.inst.load_vehicle_prefab(asset, model_file, texture_files);
			g.entities.vehicle_assets.Add(prefab);

			return prefab;
		}
		catch (Exception e) {
			Debug.LogError($"Error during Asset loading for {asset?.name ?? "unkown"}!\n  {e.Message}");
			return null;
		}
		finally {
			// destroy temp object
			if (asset != null) GameObject.DestroyImmediate(asset.gameObject);
		}
	}
}
#endif
