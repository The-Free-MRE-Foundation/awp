#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace Altspace_World_Preserver
{
    public abstract class OnlineManagerBase<T, U, V> : EditorWindow
        where T : AltspaceListItem, new()
        where U : ITypedAsset, new()
        where V : EditorWindow
    {
        protected static Dictionary<string, T> _known_items = new Dictionary<string, T>();
        protected static T _selected_item = new T();
        // private static string search = null;

        private static string ripper = Application.platform == RuntimePlatform.WindowsEditor ? "AssetRipper.exe" : "AssetRipper";

        public void SelectItem(string id)
        {
            _selected_item = _known_items[id];
            this.clearData();
            this.Close();
            GetWindow<LoginManager>().Repaint();
        }

        public static void ResetContents()
        {
            GetWindow<V>().Close();
            _known_items = new Dictionary<string, T>();
            _selected_item = new T();
        }

        protected static void EnterItemData(U itemJSON)
        {
            if (!String.IsNullOrEmpty(itemJSON.assetName))
            {
                _known_items.Remove(itemJSON.assetId);
                T new_item = new T();
                new_item.importAltVRItem(itemJSON);
                _known_items.Add(itemJSON.assetId, new_item);
            }
        }

        protected static bool LoadSingleItem(string item_id)
        {
            U itemJSON = LoginManager.LoadSingleAltVRItem<U>(item_id);
            if (itemJSON != null && !String.IsNullOrEmpty(itemJSON.assetName))
            {
                EnterItemData(itemJSON);
                return true;
            }

            return false;
        }

        protected void LoadItems<W>() where W : IPaginated, new()
        {
            _known_items.Clear();
            _photos = new Dictionary<string, photoJson>();
            LoginManager.LoadAltVRItems((W content) => content.iterator<U>(EnterItemData));


            if (_known_items.Count < 1)
                ShowNotification(new GUIContent("Item list is empty"), 5.0f);
        }

        private static Dictionary<string, spaceComponentJson> _components = new Dictionary<string, spaceComponentJson>();
        private static Dictionary<string, artifactJson> _artifacts = new Dictionary<string, artifactJson>();
        private static Dictionary<string, kitJson> _kits = new Dictionary<string, kitJson>();
        private static Dictionary<string, photoJson> _photos = new Dictionary<string, photoJson>();
        private static Dictionary<string, skyboxJson> _skyboxes = new Dictionary<string, skyboxJson>();
        private static string assetBundleRoot = "AssetBundles";
        private static string kitsRoot = "Assets/Kits";
        private static string templatesRoot = "Assets/Templates";
        private static string sceneRoot = "Assets/Scenes";
        private static string saveRoot = "Assets/Save";
        private static string photosRoot = "Assets/Pictures";
        private static string skyboxesRoot = "Assets/Skyboxes";

        public void clearData()
        {
            _components = new Dictionary<string, spaceComponentJson>();
            _artifacts = new Dictionary<string, artifactJson>();
            _kits = new Dictionary<string, kitJson>();
            _photos = new Dictionary<string, photoJson>();
        }

        private static void fixShaders(string rootPath)
        {
            var materialAssets = AssetDatabase.FindAssets("t:Material", new[] { Path.Combine(rootPath, "Assets") });
            var materialPaths = materialAssets.Select(asset => AssetDatabase.GUIDToAssetPath(asset)).ToArray();
            foreach (var mp in materialPaths)
            {
                Material m = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (
                    m.shader.name.Contains("Universal Render Pipeline/Lit") ||
                    m.shader.name.Contains("Universal Render Pipeline/Simple Lit") ||
                    m.shader.name.Contains("Universal Render Pipeline/Complex Lit"))
                {
                    Color _baseColor = m.GetColor("_BaseColor");
                    float _Smoothness = m.GetFloat("_Smoothness");
                    string tag = m.GetTag("RenderType", false);

                    m.shader = Shader.Find("Standard");
                    if (!string.IsNullOrEmpty(tag))
                    {
                        m.SetOverrideTag("RenderType", tag);
                    }
                    if (_baseColor != null)
                    {
                        m.SetColor("_Color", _baseColor);
                    }
                    m.SetFloat("_Glossiness", _Smoothness);
                    EditorUtility.SetDirty(m);
                    AssetDatabase.SaveAssets();
                }
                else if (
                    m.shader.name.Contains("Universal Render Pipeline_Autodesk Interactive_Autodesk Interactive"))
                {
                    m.shader = Shader.Find("Standard");
                }
                else if (
                    m.shader.name.Contains("Universal Render Pipeline/Unlit"))
                {
                    m.shader = Shader.Find("Unlit/Texture");
                }
            }

            var shaderAssets = AssetDatabase.FindAssets("t:Shader", new[] { Path.Combine(rootPath, "Assets") });
            var shaderPaths = shaderAssets.Select(asset => AssetDatabase.GUIDToAssetPath(asset)).ToArray();
            foreach (var sp in shaderPaths)
            {
                Shader s = AssetDatabase.LoadAssetAtPath<Shader>(sp);
                string[] sl = new string[] {
                    "Universal Render Pipeline/Lit",
                    "Universal Render Pipeline/Simple Lit",
                    "Universal Render Pipeline/Complex Lit",
                    "Universal Render Pipeline/Unlit",
                    "Hidden_Universal Render Pipeline_FallbackError"
                };
                if (Array.Exists<string>(sl, x => s.name.Contains(x)))
                {
                    AssetDatabase.DeleteAsset(sp);
                }
            }
        }

        private static void UnpackAssetBundle(string assetBundlePath, string targetPath)
        {
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo.WorkingDirectory = LoginManager.ripperRoot;
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                proc.StartInfo.FileName = ripper;
                proc.StartInfo.Arguments = Path.Combine("..", assetBundlePath) + " -o " + Path.Combine("..", targetPath) + " -q ";
            }
            else
            {
                proc.StartInfo.FileName = "/bin/bash";
                proc.StartInfo.Arguments = "-c '" + "chmod +x " + ripper + "; ./" + ripper + " " + Path.Combine("..", assetBundlePath) + " -o " + Path.Combine("..", targetPath) + " -q '";
            }
            proc.Start();
            proc.WaitForExit();
            proc.Close();

            string assetFrom = Path.Combine(targetPath, "ExportedProject/Assets");
            FileUtil.MoveFileOrDirectory(assetFrom, Path.Combine(targetPath, "Assets"));
            FileUtil.DeleteFileOrDirectory(Path.Combine(targetPath, "ExportedProject"));
            FileUtil.DeleteFileOrDirectory(Path.Combine(targetPath, "AuxiliaryFiles"));
            FileUtil.DeleteFileOrDirectory(Path.Combine(targetPath, "Assets", "Scripts"));
            AssetDatabase.Refresh();
        }

        private static void LoadKit(string kit_id)
        {
            kitJson kit = _kits[kit_id];

            if (kit == null)
            {
                Debug.LogWarning("Missing kit " + kit_id);
                return;
            }
            // download
            string url = findBestAssetBundle(kit.asset_bundles).url;
            if (!File.Exists(assetBundleRoot))
            {
                Directory.CreateDirectory(assetBundleRoot);
            }

            string assetBundleName = url.Split('/')[url.Split('/').Length - 1];
            string assetBundlePath = Path.Combine(assetBundleRoot, assetBundleName);
            if (!File.Exists(assetBundlePath))
            {
                Downloader.Download(url, assetBundlePath);
            }

            // unpack
            if (!AssetDatabase.IsValidFolder(kitsRoot))
            {
                AssetDatabase.CreateFolder("", kitsRoot);
            }
            string kitPath = Path.Combine(kitsRoot, kit.assetId);
            if (!AssetDatabase.IsValidFolder(kitPath))
            {
                UnpackAssetBundle(assetBundlePath, kitPath);
                fixShaders(kitPath);
            }
        }

        private static void GetTemplate()
        {
            var asset_bundle = findBestAssetBundle(_selected_item.asset_bundles);
            string asset_bundle_id = asset_bundle.asset_bundle_id;
            string url = asset_bundle.url;
            if (!File.Exists(assetBundleRoot))
            {
                Directory.CreateDirectory(assetBundleRoot);
            }

            string assetBundleName = url.Split('/')[url.Split('/').Length - 1];
            string assetBundlePath = Path.Combine(assetBundleRoot, assetBundleName);
            if (!File.Exists(assetBundlePath))
            {
                Downloader.Download(url, assetBundlePath);
            }

            // unpack
            if (!AssetDatabase.IsValidFolder(templatesRoot))
            {
                AssetDatabase.CreateFolder("", templatesRoot);
            }
            string templatePath = Path.Combine(templatesRoot, asset_bundle_id);
            if (!AssetDatabase.IsValidFolder(templatePath))
            {
                UnpackAssetBundle(assetBundlePath, templatePath);
                fixShaders(templatePath);
            }
        }

        private static void GetComponents()
        {
            _components = new Dictionary<string, spaceComponentJson>();
            string savefilePath = Path.Combine(saveRoot, _selected_item.id + ".json");
            if (File.Exists(savefilePath))
            {
                string text = File.ReadAllText(savefilePath);
                JsonableListWrapper<spaceComponentJson, artifactJson, kitJson, photoJson> json = JsonUtility.FromJson<JsonableListWrapper<spaceComponentJson, artifactJson, kitJson, photoJson>>(text);
                foreach (var s in json.space_components)
                {
                    if (!_components.ContainsKey(s.id))
                    {
                        _components.Add(s.id, s);
                    }
                }
                foreach (var a in json.artifacts)
                {
                    if (!_artifacts.ContainsKey(a.id))
                    {
                        _artifacts.Add(a.id, a);
                    }
                }
                foreach (var k in json.kits)
                {
                    if (!_kits.ContainsKey(k.id))
                    {
                        _kits.Add(k.id, k);
                        LoadKit(k.id);
                    }
                }
                foreach (var p in json.photos)
                {
                    if (!_photos.ContainsKey(p.id))
                    {
                        _photos.Add(p.id, p);
                        LoadPhoto(p.id);
                    }
                }
            }
            else
            {
                LoginManager.LoadSpaceComponents(_selected_item.id, (spaceComponentsJson content) => content.iterator<spaceComponentJson>(EnterComponentData));
            }
        }

        private static void LoadPhoto(string photo_id)
        {
            photoJson photo = _photos[photo_id];
            if (!Directory.Exists(photosRoot))
            {
                Directory.CreateDirectory(photosRoot);
            }
            string photoPath = Path.Combine(photosRoot, photo_id + ".png");
            if (!File.Exists(photoPath))
            {
                Downloader.Download(photo.image_original, photoPath);
            }
        }

        private static void EnterComponentData(spaceComponentJson component)
        {
            // if component is a kit object
            if (!string.IsNullOrEmpty(component.artifact_id))
            {
                if (!_components.ContainsKey(component.id))
                {
                    _components.Add(component.id, component);
                }
                if (!_artifacts.ContainsKey(component.artifact_id))
                {
                    artifactJson artifact = LoginManager.LoadSingleAltVRItem<artifactJson>(component.artifact_id);
                    if (artifact != null && !_artifacts.ContainsKey(component.artifact_id))
                    {
                        if (!string.IsNullOrEmpty(artifact.kit_id))
                        {
                            _artifacts.Add(component.artifact_id, artifact);
                            if (!_kits.ContainsKey(artifact.kit_id))
                            {
                                kitJson kit = LoginManager.LoadSingleAltVRItem<kitJson>(artifact.kit_id);
                                if (kit == null)
                                {
                                    kit = artifact.kit;
                                }
                                if (kit != null)
                                {
                                    if (!_kits.ContainsKey(artifact.kit_id))
                                    {
                                        _kits.Add(artifact.kit_id, kit);
                                    }
                                    LoadKit(artifact.kit_id);
                                    var assets = AssetDatabase.FindAssets("t:Prefab", new[] { Path.Combine(kitsRoot, kit.id, "Assets") });
                                    var prefabPaths = assets.Select(asset => AssetDatabase.GUIDToAssetPath(asset)).ToArray();
                                    kit.prefabPaths = prefabPaths;
                                }
                            }
                        }
                    }
                }

                if (_artifacts.ContainsKey(component.artifact_id))
                {
                    artifactJson artifact = _artifacts[component.artifact_id];
                    kitJson kit = _kits[artifact.kit_id];
                    if (kit != null)
                        component.kit_id = kit.kit_id;
                }
            }
            else if (!string.IsNullOrEmpty(component.photo_id))
            {
                if (!_components.ContainsKey(component.id))
                {
                    _components.Add(component.id, component);
                }

                if (!_photos.ContainsKey(component.photo_id))
                {
                    photoJson photo = LoginManager.LoadSingleAltVRItem<photoJson>(component.photo_id);
                    if (photo != null && !_photos.ContainsKey(component.photo_id))
                    {
                        if (!string.IsNullOrEmpty(photo.id))
                        {
                            if (!_photos.ContainsKey(photo.id))
                            {
                                _photos.Add(photo.id, photo);
                            }
                            LoadPhoto(photo.id);
                        }
                    }
                }
            }
            else if (component.component_type == "label")
            {
                if (!_components.ContainsKey(component.id))
                {
                    _components.Add(component.id, component);
                }
            }
        }

        private static void LoadScene()
        {
            if (!AssetDatabase.IsValidFolder(sceneRoot))
            {
                AssetDatabase.CreateFolder("", sceneRoot);
            }
            string newScenePath = Path.Combine(sceneRoot, _selected_item.id + ".unity");
            if (!File.Exists(newScenePath))
            {
                var asset_bundle = findBestAssetBundle(_selected_item.asset_bundles);
                var asset_bundle_id = asset_bundle.asset_bundle_id;
                var assets = AssetDatabase.FindAssets("t:Scene", new[] { Path.Combine(templatesRoot, asset_bundle_id) });
                string templateScenePath = AssetDatabase.GUIDToAssetPath(assets[0]);
                Scene scene = EditorSceneManager.OpenScene(templateScenePath);
                EditorSceneManager.SaveScene(scene, newScenePath);
                Scene newScene = EditorSceneManager.OpenScene(newScenePath);
                foreach (var o in SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    fixPrimitives(o);
                    fixTerrain(o);
                }
                EditorSceneManager.MarkSceneDirty(newScene);
            }
        }

        private static void GetSkybox()
        {
            skyboxJson skybox = (_selected_item as AltspaceSpaceItem).skybox;
            if (skybox == null || string.IsNullOrEmpty(skybox.id))
                return;

            if (!_skyboxes.ContainsKey(_selected_item.id))
            {
                if (!Directory.Exists(skyboxesRoot))
                {
                    Directory.CreateDirectory(skyboxesRoot);
                }
                string skyboxPath = Path.Combine(skyboxesRoot, skybox.id);
                if (!string.IsNullOrEmpty(skybox.three_sixty_image))
                {
                    if (!Directory.Exists(skyboxPath))
                    {
                        Directory.CreateDirectory(skyboxPath);
                    }
                    string url = skybox.three_sixty_image;
                    string skyboxImageName = url.Split('/')[url.Split('/').Length - 1];
                    string skyboxImagePath = Path.Combine(skyboxPath, skyboxImageName);
                    string skyboxMaterialPath = Path.Combine(skyboxPath, skybox.id + ".mat");
                    if (!File.Exists(skyboxImagePath))
                    {
                        Downloader.Download(url, skyboxImagePath);
                        AssetDatabase.Refresh();

                        TextureImporter importer = AssetImporter.GetAtPath(skyboxImagePath) as TextureImporter;
                        TextureImporterSettings settings = new TextureImporterSettings();
                        settings.textureShape = TextureImporterShape.TextureCube;
                        settings.mipmapEnabled = false;
                        importer.SetTextureSettings(settings);
                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();

                        Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(skyboxImagePath);
                        Material material = new Material(Shader.Find("Skybox/Cubemap"));
                        material.SetTexture("_Tex", texture);
                        AssetDatabase.CreateAsset(material, skyboxMaterialPath);
                        AssetDatabase.Refresh();
                    }
                    _skyboxes.Add(_selected_item.id, skybox);
                }
                else if (skybox.asset_bundles != null)
                {
                    if (!Directory.Exists(skyboxPath))
                    {
                        Directory.CreateDirectory(skyboxPath);
                    }
                    string url = findBestAssetBundle(skybox.asset_bundles).url;
                    string assetBundleName = url.Split('/')[url.Split('/').Length - 1];
                    string assetBundlePath = Path.Combine(assetBundleRoot, assetBundleName);
                    if (!File.Exists(assetBundlePath))
                    {
                        Downloader.Download(url, assetBundlePath);
                    }

                    if (!AssetDatabase.IsValidFolder(skyboxPath))
                    {
                        UnpackAssetBundle(assetBundlePath, skyboxPath);
                    }
                    _skyboxes.Add(_selected_item.id, skybox);
                }
            }
        }

        private static void LoadSkybox()
        {
            if (_skyboxes.ContainsKey(_selected_item.id))
            {
                skyboxJson skybox = _skyboxes[_selected_item.id];
                string skyboxPath = Path.Combine(skyboxesRoot, skybox.id);
                string[] materialAssets = AssetDatabase.FindAssets("t:Material", new string[] { skyboxPath });
                string[] materialPaths = materialAssets.Select(asset => AssetDatabase.GUIDToAssetPath(asset)).ToArray();
                if (materialPaths.Length > 0)
                {
                    RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(materialPaths[0]);
                }
            }
        }


        private static Bounds CalculateBounds(GameObject obj)
        {
            Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
            foreach (Renderer renderer in obj.GetComponentsInChildren<Renderer>())
            {
                bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        private static void placeComponents()
        {
            foreach (var component in _components.Values)
            {
                if (!string.IsNullOrEmpty(component.kit_id)) // kit object
                {
                    if (!_kits.ContainsKey(component.kit_id))
                    {
                        continue;
                    }
                    var kit = _kits[component.kit_id];
                    var prefabPaths = kit.prefabPaths;

                    var prefabName = _artifacts[component.artifact_id].prefab_name;
                    prefabName = prefabName.ToLower();
                    var prefabPath = Array.Find<string>(prefabPaths, (p) =>
                    {
                        var n = Regex.Replace(Path.GetFileName(p), ".prefab$", "");
                        return prefabName == n;
                    });
                    if (prefabPath == null)
                    {
                        Debug.LogWarning("Failed to find " + prefabName + ", " + component.artifact_id);
                        continue;
                    }
                    placeKitObject(prefabPath, component);
                }
                else if (!string.IsNullOrEmpty(component.photo_id)) // photo
                {
                    if (!_photos.ContainsKey(component.photo_id))
                    {
                        continue;
                    }
                    placePhoto(component.photo_id, component);
                }
                else if (component.component_type == "label")
                {
                    placeLabel(component);
                }
            }
        }

        private static void placeKitObject(string prefabPath, spaceComponentJson component)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            fixPrimitives(prefab);
            prefab.transform.position = new Vector3(0, 0, 0);
            prefab.transform.eulerAngles = new Vector3(0, 0, 0);

            GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Bounds bounds = CalculateBounds(obj);
            Quaternion q = Quaternion.Euler(component.rotation.x, component.rotation.y, component.rotation.z);
            Vector3 v = q * bounds.center * component.scale.x;

            obj.transform.position = component.position - v;
            obj.transform.eulerAngles = component.rotation;
            obj.transform.localScale = component.scale;

            if (!string.IsNullOrEmpty(component.color))
                colorize(obj, component);
            // debug(obj);
        }

        private static void colorize(GameObject obj, spaceComponentJson component)
        {
            if (string.IsNullOrEmpty(component.color))
                return;

            string kitPath = Path.Combine(kitsRoot, component.kit_id, "Assets");
            var materialAssets = AssetDatabase.FindAssets("t:Material", new[] { kitPath });
            var materialPaths = materialAssets.Select(asset => AssetDatabase.GUIDToAssetPath(asset)).ToArray();
            if (materialPaths.Length <= 0)
                return;
            string materialDirectory = Path.GetDirectoryName(materialPaths[0]);
            string materialName = component.color;
            string materialPath = Path.Combine(materialDirectory, materialName+".mat");

            Material material = new Material(Shader.Find("Skybox/Cubemap"));
            ColorUtility.TryParseHtmlString(materialName, out var color);
            material.shader = Shader.Find("Standard");
            material.SetColor("_Color", color);
            material.SetFloat("_Glossiness", 0);

            Renderer renderer = obj.GetComponentInChildren<Renderer>();
            renderer.material = material;

            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.Refresh();
        }

        private static string GetPrimitiveMeshPath(PrimitiveType primitiveType)
        {
            switch (primitiveType)
            {
                case PrimitiveType.Sphere:
                    return "New-Sphere.fbx";
                case PrimitiveType.Capsule:
                    return "New-Capsule.fbx";
                case PrimitiveType.Cylinder:
                    return "New-Cylinder.fbx";
                case PrimitiveType.Cube:
                    return "Cube.fbx";
                case PrimitiveType.Plane:
                    return "New-Plane.fbx";
                case PrimitiveType.Quad:
                    return "Quad.fbx";
                default:
                    throw new ArgumentOutOfRangeException(nameof(primitiveType), primitiveType, null);
            }
        }

        private static Mesh GetCachedPrimitiveMesh(PrimitiveType primitiveType)
        {
            Mesh primMesh = Resources.GetBuiltinResource<Mesh>(GetPrimitiveMeshPath(primitiveType));
            return primMesh;
        }

        public static Mesh GetUnityPrimitiveMesh(PrimitiveType primitiveType)
        {
            switch (primitiveType)
            {
                case PrimitiveType.Sphere:
                    return GetCachedPrimitiveMesh(primitiveType);
                case PrimitiveType.Capsule:
                    return GetCachedPrimitiveMesh(primitiveType);
                case PrimitiveType.Cylinder:
                    return GetCachedPrimitiveMesh(primitiveType);
                case PrimitiveType.Cube:
                    return GetCachedPrimitiveMesh(primitiveType);
                case PrimitiveType.Plane:
                    return GetCachedPrimitiveMesh(primitiveType);
                case PrimitiveType.Quad:
                    return GetCachedPrimitiveMesh(primitiveType);
                default:
                    throw new ArgumentOutOfRangeException(nameof(primitiveType), primitiveType, null);
            }
        }

        private static void fixPrimitives(GameObject obj)
        {
            if (obj == null)
                return;
            var mf = obj.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh == null)
            {
                mf.sharedMesh = Resources.GetBuiltinResource<Mesh>(GetPrimitiveMeshPath(PrimitiveType.Cube));
            }
            foreach (Transform c in obj.GetComponentInChildren<Transform>())
            {
                fixPrimitives(c.gameObject);
            }
        }

        private static void fixTerrain(GameObject obj)
        {
            var terrain = obj.GetComponent<Terrain>();
            if (terrain != null)
            {
                terrain.materialTemplate.shader = Shader.Find("Nature/Terrain/Standard");
            }
            foreach (Transform c in obj.GetComponentInChildren<Transform>())
            {
                fixTerrain(c.gameObject);
            }
        }

        private static void placePhoto(string photo_id, spaceComponentJson component)
        {
            // transform
            GameObject parent = new GameObject();
            parent.name = "photo_" + photo_id;
            parent.transform.position = component.position;
            parent.transform.eulerAngles = component.rotation;
            parent.transform.localScale = component.scale;

            GameObject front = GameObject.CreatePrimitive(PrimitiveType.Quad);
            front.transform.SetParent(parent.transform);
            front.transform.position = parent.transform.position;
            front.transform.rotation = parent.transform.rotation;
            GameObject back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            back.transform.position = parent.transform.position;
            back.transform.SetParent(parent.transform);
            back.transform.localRotation = Quaternion.Euler(0, 180, 0);

            front.transform.localScale = new Vector3(1, 1, 1);
            back.transform.localScale = new Vector3(1, 1, 1);

            string photoPath = Path.Combine(photosRoot, photo_id + ".png");

            // material
            string frontMaterialPath = Path.Combine(photosRoot, photo_id + ".mat");
            if (!File.Exists(frontMaterialPath))
            {
                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(photoPath);
                Material material = new Material(Shader.Find("Unlit/Texture"));
                material.SetTexture("_MainTex", texture);
                AssetDatabase.CreateAsset(material, frontMaterialPath);
                AssetDatabase.Refresh();
            }
            Material frontMaterial = AssetDatabase.LoadAssetAtPath<Material>(frontMaterialPath);
            front.GetComponent<Renderer>().material = frontMaterial;

            string backMaterialPath = Path.Combine(photosRoot, "back.mat");
            if (!File.Exists(backMaterialPath))
            {
                Material material = new Material(Shader.Find("Unlit/Texture"));
                AssetDatabase.CreateAsset(material, backMaterialPath);
                AssetDatabase.Refresh();
            }
            Material backMaterial = AssetDatabase.LoadAssetAtPath<Material>(backMaterialPath);
            back.GetComponent<Renderer>().material = backMaterial;

            Texture2D image = AssetDatabase.LoadAssetAtPath<Texture2D>(photoPath);
            int width;
            int height;
            GetImageSize(image, out width, out height);
            Vector3 s = parent.transform.localScale;
            if (width < height) // portait
            {
                parent.transform.localScale = new Vector3(s.x * (float)width / height, s.y, s.z);
            }
            else
            {
                parent.transform.localScale = new Vector3(s.x, s.y * (float)height / width, s.z);
            }
        }

        private static void placeLabel(spaceComponentJson component)
        {
            string text = component.name;
            GameObject parent = new GameObject();
            parent.name = "label_" + component.id;
            parent.transform.position = component.position;
            parent.transform.eulerAngles = component.rotation;
            parent.transform.localScale = component.scale;
            TextMeshPro tmp = parent.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.font = Resources.Load("VarelaRound-Regular SDF", typeof(TMP_FontAsset)) as TMP_FontAsset;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.1f;
            tmp.fontSizeMax = 1.6f;

            RectTransform rt = parent.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1.5f, 100);
        }

        public static bool GetImageSize(Texture2D asset, out int width, out int height)
        {
            if (asset != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(asset);
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

                if (importer != null)
                {
                    object[] args = new object[2] { 0, 0 };
                    MethodInfo mi = typeof(TextureImporter).GetMethod("GetWidthAndHeight", BindingFlags.NonPublic | BindingFlags.Instance);
                    mi.Invoke(importer, args);

                    width = (int)args[0];
                    height = (int)args[1];

                    return true;
                }
            }

            height = width = 0;
            return false;
        }

        private static void debug(GameObject obj)
        {
            Bounds bounds = CalculateBounds(obj);
            // debug
            GameObject origin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            origin.transform.position = obj.transform.position;
            origin.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            origin.GetComponent<Renderer>().material = Resources.Load<Material>("origin");
            origin.transform.SetParent(obj.transform);

            GameObject center = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            center.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            center.transform.localPosition = bounds.center;
            center.GetComponent<Renderer>().material = Resources.Load<Material>("center");
            center.transform.SetParent(obj.transform);

            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.localScale = bounds.extents * 2;
            box.transform.localPosition = bounds.center;
            box.GetComponent<Renderer>().material = Resources.Load<Material>("box");
            box.transform.SetParent(obj.transform);
        }

        private void saveWorld()
        {
            if (!Directory.Exists(saveRoot))
            {
                Directory.CreateDirectory(saveRoot);
            }
            List<spaceComponentJson> space_components = new List<spaceComponentJson>(_components.Values);
            List<artifactJson> artifacts = new List<artifactJson>(_artifacts.Values);
            List<kitJson> kits = new List<kitJson>(_kits.Values);
            List<photoJson> photos = new List<photoJson>(_photos.Values);
            string json = JsonUtility.ToJson(new JsonableListWrapper<spaceComponentJson, artifactJson, kitJson, photoJson>(space_components, artifacts, kits, photos), true);
            string savefilePath = Path.Combine(saveRoot, _selected_item.id + ".json");
            File.WriteAllText(savefilePath, json);
            AssetDatabase.Refresh();
        }

        private static assetBundleJSON findBestAssetBundle(List<assetBundleJSON> asset_bundles)
        {
            var al = asset_bundles.Where(b => b.platform == "pc" && b.color_space == "linear").ToArray();
            if (al.Length > 0)
                return al[0];

            al = asset_bundles.Where(b => b.platform == "android" && b.color_space == "linear").ToArray();
            if (al.Length > 0)
                return al[0];

            al = asset_bundles.Where(b => b.platform == "mac" && b.color_space == "linear").ToArray();
            if (al.Length > 0)
                return al[0];

            al = asset_bundles.Where(b => b.platform == "pc").ToArray();
            if (al.Length > 0)
                return al[0];

            return asset_bundles[0];
        }

        protected void PreserveWorld()
        {
            // fetch meta data and download 
            GetTemplate();
            GetComponents();
            GetSkybox();

            // load scene and place components
            LoadScene();
            LoadSkybox();
            placeComponents();

            // save components
            string savefilePath = Path.Combine(saveRoot, _selected_item.id + ".json");
            if (!File.Exists(savefilePath))
            {
                saveWorld();
            }
        }

        protected void ManageItems(string message)
        {
            if (WebClient.IsAuthenticated)
            {
                if (GUILayout.Button("Select " + _selected_item.friendlyName))
                    GetWindow<V>().Show();
            }
            else
                EditorGUILayout.LabelField("Offline mode", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });

            EditorGUILayout.Space(10);

            _selected_item.showSelf();

            EditorGUILayout.BeginHorizontal();

            if (!_selected_item.isSet)
            {
                GUILayout.Label(message, new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });
            }
            else if (GUILayout.Button("Preserve"))
            {
                PreserveWorld();
            }

            EditorGUILayout.EndHorizontal();
        }

    }

    public class AltVRItemWidgets
    {
        public static void BuildSelectorList<T>(Dictionary<string, T>.ValueCollection vals, Action load_fn, Action<string> select_fn, ref Vector2 scrollPosition)
    where T : AltspaceListItem, new()
        {
            string item_type = null;

            GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });

            EditorGUILayout.Space(10);

            // EditorGUILayout.BeginHorizontal(GUILayout.Width(240.0f));
            // EditorGUILayout.LabelField("Search by world name");
            // EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            bool shownOne = false;

            {
                GUIStyle style = new GUIStyle() { fontStyle = FontStyle.Bold };

                // We got at least one item, pick the type from one.
                if (vals.Count > 0)
                    item_type = vals.First().friendlyName;

                scrollPosition = GUILayout.BeginScrollView(scrollPosition);

                // GUILayout.BeginHorizontal();
                // GUILayout.TextField("", GUILayout.MaxWidth(300));
                // GUILayout.Button("Search", GUILayout.MaxWidth(100));
                // GUILayout.EndHorizontal();

                foreach (var item in vals)
                {
                    EditorGUILayout.BeginHorizontal(GUILayout.Width(120.0f));

                    EditorGUILayout.LabelField(item.itemName);
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Select", EditorStyles.miniButton))
                        select_fn(item.id);

                    EditorGUILayout.EndHorizontal();

                    shownOne = true;
                }

                GUILayout.EndScrollView();
            }

            if (!shownOne)
            {
                // We had no item to read the type from, create an empty "blueprint item" to infer it.
                T blp_item = new T();
                item_type = blp_item.friendlyName;
            }

            if (GUILayout.Button("Load " + item_type + "s"))
            {
                load_fn();
            }

            GUILayout.EndVertical();
        }

    }

    [InitializeOnLoad]
    [ExecuteInEditMode]
    public class OnlineSpaceManager : OnlineManagerBase<AltspaceSpaceItem, spaceJSON, OnlineSpaceManager>
    {
        public static void ShowSpace(AltspaceSpaceItem space)
        {
            Common.ShowItem(space);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.EndHorizontal();
        }

        public void ManageSpaces() => ManageItems("You need to select a world first.");


        private Vector2 m_scrollPosition;

        public void OnGUI()
        {
            AltVRItemWidgets.BuildSelectorList(_known_items.Values, LoadItems<spacesJSON>, SelectItem, ref m_scrollPosition);
        }


    }
}

#endif // UNITY_EDITOR
