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
[JsonConverter(typeof(BuildingAssetSerializer))]
#endif
public class BuildingAsset : MonoBehaviour {
	public float spawn_weight = 1;

	public GameObject instance_prefab;
}

#if UNITY_EDITOR
public class BuildingAssetSerializer : JsonConverter {
	public override bool CanConvert (Type objectType) {
		return objectType == typeof(BuildingAsset);
	}

	public override void WriteJson (JsonWriter writer, object value, JsonSerializer serializer) {
		throw new NotImplementedException();
	}

	public override object ReadJson (JsonReader reader, Type objectType, object value, JsonSerializer serializer) {
		BuildingAsset asset = null;
		try {
			asset = new GameObject().AddComponent<BuildingAsset>();
			asset.transform.SetParent(AssetManager.inst.tmp_loc);

			var j = JObject.Load(reader);

			asset.name = j.Value<string>("name");
			j.TryGet("spawn_weight", ref asset.spawn_weight);

			string model_file = j.Value<string>("model_file");
			string texture_files = j.Value<string>("texture_files");

			Debug.Log($"Asset definition loaded for {asset.name}");

			var prefab = AssetManager.inst.load_building_prefab(asset, model_file, texture_files);
			g.entities.building_assets.Add(prefab);

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
