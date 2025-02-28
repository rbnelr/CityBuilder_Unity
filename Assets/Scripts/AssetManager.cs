using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;
using GLTFast;
using GLTFast_Custom.Utils;
using System.Linq;

// Use GLTFast to load gltf specified in json as prefabs in unity editor
// GLTFast is supposed to either be used at runtime or through standard drag&drop import, so this is kinda scuffed
// But the advantage is that is already automates importing using GLTFast, and should be easily portable to real runtime asset loading later
// I may want to switch to a more custom asset loader later and ditch GLTFast (supposedly mesh loading is the only hard part?)
// but we might be able to keep it especially if GLTFast handles things like bounds in combination with animations, which are not trivial to compute
// The biggest thing I will need to add is to cache meshes and textures of imported assets as binary/dds files so we avoid slow loading times!
[ExecuteInEditMode]
public class AssetManager : MonoBehaviour {
#if UNITY_EDITOR
	public static AssetManager inst;
	private void OnEnable () {
		inst = this;
	}

	public string prefabs_dir => "Assets/Assets/LoadedPrefabs";
	public string prefab_vehicles_dir => prefabs_dir + "/Vehicles";
	public string prefab_buildings_dir => prefabs_dir + "/Buildings";

	public Transform tmp_loc => transform;

	public string content_dir = "Assets/Assets/Import";

	public ImportSettings import_setting = new ImportSettings();

	public Material building_mat;

	class AssetsDefinition {
		public List<VehicleAsset> vehicles;
		public List<BuildingAsset> buildings;
	}

	// delete all existing saved assets
	public void clear_all () {
		// Only works with Assets/* paths, not absolute paths!
		foreach (string guid in AssetDatabase.FindAssets("", new[] { prefab_vehicles_dir, prefab_buildings_dir })) {
			AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
		}

		g.entities.vehicle_assets.HardClear();
		g.entities.building_assets.HardClear();
	}

	[NaughtyAttributes.Button]
	public void Reimport_All () {
		try {
			clear_all();
			Util.DestroyChildren(tmp_loc); // for good measure

			var settings = new JsonSerializerSettings();
			settings.Converters.Add(new VehicleAssetSerializer());
			settings.Converters.Add(new BuildingAssetSerializer());

			string text = File.ReadAllText(content_dir + "/assets.json");
			var def = JsonConvert.DeserializeObject<AssetsDefinition>(text, settings);

			Debug.Log($"All Assets reloaded.");
		}
		catch (Exception e) {
			Debug.LogError($"Failed to load Assets: {e.Message}!");
		}
		finally {
			//Util.DestroyChildren(tmp_loc); // for good measure
		}
	}

	// Currently:
	//  centering since I like to place multiple models side by side in blender
	//  and blender Y-forward -> Unity Z-forward
	void gltf_fix_skinned_transform (GameObject go) {
		// center object itself
		go.transform.localPosition = Vector3.zero;
		go.transform.localRotation = Quaternion.identity;
		go.transform.localScale = Vector3.one;

		// rotate all children
		// GLTFSceneImporter loaded Z-up Y-forward objects as Y-up but Z-backwards for some reason
		// Face the vehicle Z-forward in blender and make the vehicle bones Y-up Z-forward (X-left will be inverted to X-right on import due to RHS->LHS)
		foreach (Transform t in go.transform) {
			if (t.GetComponent<SkinnedMeshRenderer>() == null) { // only flip bones, does not really matter but I like it clean
				var rot = Quaternion.Euler(0, 180, 0);
				t.localPosition = rot * t.localPosition;
				t.localRotation = rot * t.localRotation;
			}
		}
	}

	static int parse_LOD (MeshRenderer obj) {
		var s = obj.name.Split("_LOD");
		if (s.Length == 2) {
			return int.Parse(s[1]);
		}
		return 0;
	}
	static int assign_LOD (List<LOD> lods, MeshRenderer obj) {
		int i = parse_LOD(obj);
		while (lods.Count <= i) lods.Add(new LOD());
		if (lods[i].renderers == null) {
			var l = lods[i];
			l.renderers = new Renderer[] { obj };
			lods[i] = l;
		}
		return i;
	}
	
	void collider_from_LOD_group (LODGroup lod_group) {
		var collider = lod_group.gameObject.AddComponent<BoxCollider>();

		var lod0_renderer = lod_group.GetLODs().First().renderers.First();
		var bounds = lod0_renderer.bounds;

		collider.center = bounds.center;
		collider.size = bounds.size;

		EditorUtility.SetDirty(gameObject);
	}

	void gltf_fix_and_setup_non_skinned_LOD (GameObject go, string prefab_path) {
		go.transform.localPosition = Vector3.zero;
		go.transform.localRotation = Quaternion.identity;
		go.transform.localScale = Vector3.one;

		var lod_group = go.AddComponent<LODGroup>();

		var lods = new List<LOD>();

		foreach (MeshRenderer renderer in go.GetComponentsInChildren<MeshRenderer>()) {
			int lod = assign_LOD(lods, renderer);

			renderer.transform.localPosition = Vector3.zero;
			renderer.transform.localRotation = Quaternion.Euler(0, 180, 0);
			renderer.transform.localScale = Vector3.one;

			renderer.sharedMaterial = building_mat;
			
			var mesh_filter = renderer.gameObject.GetComponent<MeshFilter>();
			AssetDatabase.CreateAsset(mesh_filter.sharedMesh, prefab_path+$"_LOD{lod:00}.mesh");
		}

		for (int i=0; i<lods.Count; ++i) {
			//float transition = 1.0f / (i + 2);
			float transition = 1.0f / (Mathf.Pow(i+1, 4.0f) + 3);
			lods[i] = new LOD(transition, lods[i].renderers);
		}
		
		lod_group.SetLODs(lods.ToArray());
		lod_group.RecalculateBounds();

		collider_from_LOD_group(lod_group);
	}

	// load mesh from .gltf and place it under parent (skinned mesh renderer turns into bone hierarchy etc)
	public GameObject gltf_load_editor (string filepath, Transform parent) {
		try {
			Debug.Assert(Application.isEditor);
			var importer = new GltfImport(null, new UninterruptedDeferAgent());

			bool success = AsyncHelpers.RunSync(() => importer.Load(filepath, import_setting));
			if (!success) throw new Exception("GltfImport.Load error");

			success = AsyncHelpers.RunSync(() => importer.InstantiateMainSceneAsync(parent));
			if (!success) throw new Exception("GltfImport.InstantiateMainSceneAsync error");

			// scene (wrapper object)
			var scene = parent.gameObject;

			// object with the mesh renderer, and children bones for skinned mesh renderers
			var obj = scene.transform.GetChild(0).gameObject;
			obj.name = "gltf scene";

			return obj;
		}
		catch (Exception e) {
			Debug.LogError($"Error {e.Message}");
			throw e;
		}
	}

	// create new material for assset, but assume textures already exist as assets for now
	public Material load_vehicle_material (string texture_files) {
		var mat = new Material(Shader.Find("Shader Graphs/VehicleLit"));

		var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Assets/"+texture_files.Replace("%", ""));
		Texture2D norm = null;
		var pbr    = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Assets/"+texture_files.Replace("%", ".pbr"));
		var glow   = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Assets/"+texture_files.Replace("%", ".glow"));

		mat.SetTexture("_Albedo", albedo);
		mat.SetTexture("_Normal", norm);
		mat.SetTexture("_PBR", pbr);
		mat.SetTexture("_GlowTex", glow);
		mat.SetVector("_GlowValue", new Vector4(100, 100, 100, 100));

		mat.enableInstancing = true;

		return mat;
	}

	public VehicleAsset load_vehicle_prefab (VehicleAsset asset, string model_file, string texture_files) {
		// load gltf file as gameobject hierarchy
		string src_path = Path.Combine(Path.GetFullPath(content_dir), model_file);
		var obj = gltf_load_editor(src_path, asset.transform);

		// add vehicle script
		var vehicle = obj.AddComponent<Vehicle>();
		vehicle.asset = asset; // vehicle references its asset
		
		// turn material, mesh and gameobject hierarchy into prefab
		var prefab_path = $"{prefab_vehicles_dir}/{asset.name}";

		asset.instance_prefab = obj;
		
		gltf_fix_skinned_transform(obj);

		// store (copy?) loaded mesh as asset
		var renderer = obj.GetComponentInChildren<SkinnedMeshRenderer>();
		renderer.updateWhenOffscreen = false; // Not really needed for vehicles
		renderer.sharedMaterial = load_vehicle_material(texture_files);

		AssetDatabase.CreateAsset(renderer.sharedMesh, prefab_path+".mesh");
		AssetDatabase.CreateAsset(renderer.sharedMaterial, prefab_path+".mat");
		var prefab = PrefabUtility.SaveAsPrefabAsset(asset.gameObject, prefab_path+".prefab").GetComponent<VehicleAsset>();

		Debug.Log($"Asset gltf loaded for {asset.name}");

		return prefab;
	}

	public BuildingAsset load_building_prefab (BuildingAsset asset, string model_file, string texture_files) {
		// load gltf file as gameobject hierarchy
		string src_path = Path.Combine(Path.GetFullPath(content_dir), model_file);
		var obj = gltf_load_editor(src_path, asset.transform);

		// add vehicle script
		var building = obj.AddComponent<Building>();
		building.asset = asset; // vehicle references its asset
		
		// turn material, mesh and gameobject hierarchy into prefab
		var prefab_path = $"{prefab_buildings_dir}/{asset.name}";

		asset.instance_prefab = obj;
		
		gltf_fix_and_setup_non_skinned_LOD(obj, prefab_path);

		var prefab = PrefabUtility.SaveAsPrefabAsset(asset.gameObject, prefab_path+".prefab").GetComponent<BuildingAsset>();

		Debug.Log($"Asset gltf loaded for {asset.name}");

		return prefab;
	}
#endif
}
