using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityGLTF;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;

public class AssetManager : MonoBehaviour {
	public Transform vehicles_loc;

	public string content_dir => Directory.GetCurrentDirectory() + "/Content"; // TODO: Fix for real application

	public Material car_material;
	public Material bus_material;
	
	class AssetsDefinition {
		public List<VehicleAsset> vehicles;
	}

	[NaughtyAttributes.Button]
	public void ReloadAll () {
		try {
			Util.DestroyChildren(vehicles_loc.transform);
			
			var settings = new JsonSerializerSettings();
			settings.Converters.Add(new VehicleAssetSerializer());

			string text = File.ReadAllText(content_dir + "/assets.json");
			var def = JsonConvert.DeserializeObject<AssetsDefinition>(text, settings);

			vehicles = def.vehicles;

			float3 pos = 0;
			foreach (var vehicle in vehicles) {
				vehicle.transform.position = pos;
				pos.x += 10;
			}

			Debug.Log($"All Assets reloaded.");
		}
		catch (Exception e) {
			Debug.LogError($"Failed to load Assets: {e.Message}!");
		}
	}

	// List of loaded assets
	public List<VehicleAsset> vehicles;

	// Currently:
	//  centering since I like to place multiple models side by side in blender
	//  and blender Y-forward -> Unity Z-forward
	static void fix_transform (GameObject go) {
		// center object itself
		go.transform.localPosition = Vector3.zero;
		go.transform.localRotation = Quaternion.identity;
		go.transform.localScale = Vector3.one;

		// rotate all children
		// GLTFSceneImporter loaded Z-up Y-forward objects as Y-up but Z-backwards for some reason
		// Face the vehicle Z-forward in blender and make the vehicle bones Y-up Z-forward (X-left will be inverted to X-right on import due to RHS->LHS)
		foreach (Transform t in go.transform) {
			if (t.GetComponent<SkinnedMeshRenderer>() == null) { // only flip bones, does not really matter but I like it clean
				var rot = Quaternion.Euler(0,180,0);
				t.localPosition = rot * t.localPosition;
				t.localRotation = rot * t.localRotation;
			}
		}
	}
	
	public async Task<GameObject> load_gltf_model (string name, string filepath, Transform parent) {
		var importer = new GLTFSceneImporter(filepath, new ImportOptions());

		// GLTFSceneImporter builds GO hierarchy in scene, place it under this GO
		importer.SceneParent = parent;

		await importer.LoadSceneAsync();

		// scene (wrapper object)
		var scene = importer.LastLoadedScene;
		// object with the mesh renderer, and children bones for skinned mesh renderers
		var obj = scene.transform.GetChild(0).gameObject;

		importer.LastLoadedScene.name = "gltf scene";

		fix_transform(obj);

		return scene;
	}
}

public class VehicleAssetSerializer : JsonConverter {
	public override bool CanConvert (Type objectType) {
		return objectType == typeof(VehicleAsset);
	}

	public override void WriteJson (JsonWriter writer, object value, JsonSerializer serializer) {
		// Load JSON object into a JObject for structured parsing
		//var vehicle = (Vehicle)value;
		//
		//// Create a JObject to represent the JSON structure
		//var jsonObject = new JObject
		//{
		//    ["vehicle_id"] = vehicle.Id,
		//    ["vehicle_model"] = vehicle.Model,
		//    ["vehicle_speed"] = vehicle.Speed
		//};
		//
		//// Write the JObject to the JSON writer
		//jsonObject.WriteTo(writer);

		throw new NotImplementedException();
	}

	public override object ReadJson (JsonReader reader, Type objectType, object value, JsonSerializer serializer) {
		VehicleAsset asset = null;
		try {
			var j = JObject.Load(reader);
		
			// create empty GO inside g.assets.vehicles_loc and attach VehicleAsset script
			asset = (new GameObject()).AddComponent<VehicleAsset>();
			asset.transform.SetParent(g.assets.vehicles_loc);

			// deserialize it from json
			//serializer.Populate(j.CreateReader(), asset); // works but will allow users to directly write unity properties
			asset.name = j.Value<string>("name");
			j.TryGet("max_speed", ref asset.max_speed);

			j.TryGet("model_file", ref asset.model_file);
			j.TryGet("texture_files", ref asset.texture_files);

			string path = Path.Combine(g.assets.content_dir, asset.model_file);
			var task = g.assets.load_gltf_model(asset.name, path, asset.transform);

			task.GetAwaiter().OnCompleted(() => {
				var obj = task.Result;
				var renderer = obj.GetComponentInChildren<SkinnedMeshRenderer>();
				renderer.sharedMaterial = asset.name == "bus" ? g.assets.bus_material : g.assets.car_material; // TODO: Actually add runtime texture loading!

				Debug.Log($"Asset gltf loaded for {asset.name}");
			});
			
			Debug.Log($"Asset definition loaded for {asset.name}");
			return asset;
		}
		catch (Exception e) {
			Debug.LogError($"Error during Asset loading for {asset?.name ?? "unkown"}!\n  {e.Message}");
			
			if (asset != null) Util.Destroy(asset.transform);
			return null;
		}
	}
}
