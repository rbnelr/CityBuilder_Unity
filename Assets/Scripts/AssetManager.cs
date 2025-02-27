using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using GLTFast;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;

[ExecuteInEditMode]
public class AssetManager : MonoBehaviour {

	public static AssetManager inst;
	private void OnEnable () {
		inst = this;
	}

	public string content_dir => Path.GetFullPath("Assets/Assets/Import/");
	public Transform tmp_loc => transform;

	
	public ImportSettings import_setting = new ImportSettings();

	public Material car_material;
	public Material bus_material;
	
	class AssetsDefinition {
		public List<VehicleAsset> vehicles;
	}
	
	[NaughtyAttributes.Button]
	public void Reimport_All () {
		try {
			//Util.DestroyChildren(vehicles_loc.transform);
			
			var settings = new JsonSerializerSettings();
			settings.Converters.Add(new VehicleAssetSerializer());

			string text = File.ReadAllText(Path.Combine(content_dir, "assets.json"));
			var def = JsonConvert.DeserializeObject<AssetsDefinition>(text, settings);

			//vehicles = def.vehicles;

			//float3 pos = 0;
			//foreach (var vehicle in vehicles) {
			//	vehicle.transform.position = pos;
			//	pos.x += 10;
			//}

			Debug.Log($"All Assets reloaded.");
		}
		catch (Exception e) {
			Debug.LogError($"Failed to load Assets: {e.Message}!");
		}
	}

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
		try {
			var deferAgent = Application.isEditor ? new UninterruptedDeferAgent() : null; // needed to fix exception in editor mode
			var importer = new GltfImport(null, deferAgent);

			bool success = await importer.Load(filepath, import_setting);
			if (!success) throw new Exception("GltfImport.Load error");

			success = await importer.InstantiateMainSceneAsync(parent);
			if (!success) throw new Exception("GltfImport.InstantiateMainSceneAsync error");
			
			// scene (wrapper object)
			var scene = parent.gameObject;

			// object with the mesh renderer, and children bones for skinned mesh renderers
			var obj = scene.transform.GetChild(0).gameObject;
			obj.name = "gltf scene";
			
			fix_transform(obj);
			
			return scene;
		}
		catch (Exception e) {
			Debug.LogError($"Error {e.Message}");
			throw e;
		}
	}
}

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
			var j = JObject.Load(reader);
		
			// create empty GO inside g.assets.vehicles_loc and attach VehicleAsset script
			asset = new GameObject().AddComponent<VehicleAsset>();
			asset.transform.SetParent(AssetManager.inst.tmp_loc);

			// deserialize it from json
			//serializer.Populate(j.CreateReader(), asset); // works but will allow users to directly write unity properties
			asset.name = j.Value<string>("name");
			j.TryGet("max_speed", ref asset.max_speed);

			string model_file = j.TryGet<string>("model_file");
			string texture_files = j.TryGet<string>("texture_files");

			string path = Path.Combine(AssetManager.inst.content_dir, model_file);
			var task = AssetManager.inst.load_gltf_model(asset.name, path, asset.transform);

			task.Wait(1000);
			//task.GetAwaiter().OnCompleted(() => {
				var obj = task.Result;
				var renderer = obj.GetComponentInChildren<SkinnedMeshRenderer>();
				renderer.sharedMaterial = asset.name == "bus" ? AssetManager.inst.bus_material : AssetManager.inst.car_material; // TODO: Actually add runtime texture loading!
				renderer.updateWhenOffscreen = false; // Not really needed in for vehicles

				asset.prefab = obj;
			
				Debug.Log($"Asset gltf loaded for {asset.name}");
			//});
			
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