using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Altspace_World_Preserver
{
    public interface IPaginated
    {
        paginationJSON pages { get; }
        // string assetPluralType { get; }
        void iterator<U>(Action<U> callback);
    }

    public interface ITypedAsset
    {
        // string assetPluralType { get; }
        string assetId { get; }
        string assetName { get; }
    }

    [Serializable]
    public class userPwCredentialsJSON
    {
        public string email;
        public string password;
    }

    [Serializable]
    public class userLoginJSON
    {
        public userPwCredentialsJSON user = new userPwCredentialsJSON();
    }

    /// <summary>
    /// Part of a user entry
    /// </summary>
    [Serializable]
    public class userEntryJSON
    {
        public List<string> roles = new List<string>();
        public List<string> platform_roles = new List<string>();
        public string username;
        public string display_name;
        public string email;
        public string user_id;
    }

    [Serializable]
    public class userListJSON
    {
        public List<userEntryJSON> users = new List<userEntryJSON>();
    }

    /// <summary>
    /// Current page, number of pages
    /// </summary>
    [Serializable]
    public class paginationJSON
    {
        public int page = 0;
        public int pages = 0;
        public int count = 0;
    }

    /// <summary>
    /// An Asset Bundle inside a kit or template.
    /// </summary>
    [Serializable]
    public class assetBundleJSON
    {
        public string game_engine;
        public int game_engine_version;
        public string platform;
        public string color_space;
        public string created_at;
        public string updated_at;
        public string url;
        public string asset_bundle_id;
    }

    /// <summary>
    /// Collection of AssetBundles, coined to a specific user, inside a template.
    /// </summary>
    [Serializable]
    public class assetBundleSceneJSON
    {
        public string user_id = null;
        public List<assetBundleJSON> asset_bundles = new List<assetBundleJSON>();
    }

    public abstract class AltspaceListItem
    {
        private string _itemPath = null;

        public string itemName = null;      // Name of the online Altspace item, raw version. Null if no selection
        public string id = null;            // ID. Null if no selection

        public List<assetBundleJSON> asset_bundles = null;  // (Unity Assets) Asset Bundles connected to the Altspace Item
        public string item_url = null;                      // (Flat File Assets) URL to download the asset from

        public string imageFile = null;
        public string description = null;
        public string tag_list = null;

        /// <summary>
        /// get and set itemPath. Triggers a save into the association list when needed
        /// </summary>
        public string itemPath
        {
            get { return _itemPath; }
            set
            {
                // Safe to call Update since it only enacts a write if we did change something.
                // So assignments during load won't trigger a save.
                _itemPath = value;
            }
        }

        /// <summary>
        /// True if we're online and had selected an item from the kit or template list
        /// </summary>
        public bool isSelected => !string.IsNullOrEmpty(itemName);

        /// <summary>
        /// True if an asset path is set, regardless of validity
        /// </summary>
        public abstract bool isSet { get; }

        /// <summary>
        /// Suggests the asset path if we find assets in the project that could belong to the selected Altspace item
        /// </summary>
        public abstract string suggestedAssetPath { get; }

        public abstract void importAltVRItem<U>(U json);

        /// <summary>
        /// type: "kit" or "space_template"
        /// </summary>
        public abstract string type { get; }

        /// <summary>
        /// friendly name of the type, for display in UI
        /// </summary>
        public abstract string friendlyName { get; }        // "kit" or "template"

        /// <summary>
        /// plural of the type name
        /// </summary>
        public abstract string pluralType { get; }          // "kits" or "space_templates"

        /// <summary>
        /// Describe yourself in the GUI, with all details
        /// </summary>
        public abstract void showSelf();

        public virtual bool isAssetBundleItem { get => false; }

    }

    public class AltspaceSpaceItem : AltspaceListItem
    {

        public skyboxJson skybox = null;
        public string spaceSceneName => Path.GetFileNameWithoutExtension(itemPath);

        public override string suggestedAssetPath
        {
            get
            {
                string fileName = id + "_" + Common.SanitizeFileName(itemName);
                string fullName = Path.Combine("Assets", "Scenes", fileName + ".unity");
                return (File.Exists(fullName)) ? fullName : null;
            }
        }

        public override void importAltVRItem<U>(U _json)
        {
            spaceJSON json = _json as spaceJSON;
            itemName = json.name;
            id = json.space_id;
            description = json.description;
            tag_list = null;
            skybox = json.skybox;
            if (json.asset_bundle_scenes.Count > 0)
            {
                asset_bundles = json.asset_bundle_scenes[0].asset_bundles;
            }
        }

        public override void showSelf() => OnlineSpaceManager.ShowSpace(this);

        public override string type => "space";

        public override string friendlyName => "world";

        public override string pluralType => "spaces";

        public override bool isSet => !string.IsNullOrEmpty(id);

        public override bool isAssetBundleItem { get => true; }
    }

    [Serializable]
    public class spaceJSON : ITypedAsset
    {
        public string space_sid = null;    // Long sid
        public string activity_name = null;         // friendly name
        public string description = null;
        public string space_id = null;     // ID used in URLs
        public string space_template_id = null;     // ID used in URLs
        public string user_id = null;
        public List<assetBundleSceneJSON> asset_bundle_scenes = new List<assetBundleSceneJSON>(); // asset Bundles coined to different users? Strange.
        public string name = null;                  // friendly name (again)
        public skyboxJson skybox = new skyboxJson();

        public static string assetPluralType { get => "world"; }
        public string assetId { get => space_id; }
        public string assetName { get => name; }

    }

    [Serializable]
    public class spacesJSON : IPaginated
    {
        public List<spaceJSON> spaces = new List<spaceJSON>();
        public paginationJSON pagination = new paginationJSON();

        public paginationJSON pages { get => pagination; }

        public static string assetPluralType { get => "worlds"; }

        public void iterator<U>(Action<U> callback)
        {
            foreach (spaceJSON item in spaces)
            {
                if (item.asset_bundle_scenes.Count > 0 && item.user_id == LoginManager.userid)
                {
                    (callback as Action<spaceJSON>)(item);
                }
            }
        }
    }

    [Serializable]
    public class kitJson : ITypedAsset
    {
        public string id = null;
        public string kit_id = null;
        public string name = null;
        public string[] prefabPaths = null;

        public static string assetPluralType { get => "kits"; }
        public List<assetBundleJSON> asset_bundles = new List<assetBundleJSON>(); // asset Bundles coined to different users? Strange.
        public string assetId { get => id; }
        public string assetName { get => name; }
    }

    [Serializable]
    public class artifactJson : ITypedAsset
    {
        public string id = null;
        public string kit_id { get => kit.kit_id; }
        public string artifact_id = null;
        public string photo_id = null;
        public string name = null;
        public string prefab_name = null;
        public kitJson kit = null;

        public static string assetPluralType { get => "artifacts"; }
        public string assetId { get => id; }
        public string assetName { get => name; }

    }
    [Serializable]
    public class spaceComponentJson : ITypedAsset
    {
        public string id = null;
        public string artifact_id = null;
        public string photo_id = null;
        public string kit_id = null;
        public string component_type = null;
        public string name = null;

        public string color = null;

        public Vector3 position = new Vector3(0, 0, 0);
        public Vector3 rotation = new Vector3(0, 0, 0);
        public Vector3 scale = new Vector3(1, 1, 1);

        public static string assetPluralType { get => "object"; }
        public string assetId { get => id; }
        public string assetName { get => name; }

    }

    [Serializable]
    public class spaceComponentsJson : IPaginated
    {
        public List<spaceComponentJson> space_components = new List<spaceComponentJson>();
        public paginationJSON pagination = new paginationJSON();

        public paginationJSON pages { get => pagination; }

        public static string assetPluralType { get => "objects"; }

        public void iterator<U>(Action<U> callback)
        {
            foreach (spaceComponentJson item in space_components)
            {
                (callback as Action<spaceComponentJson>)(item);
            }
        }
    }

    [Serializable]
    public class photosJson : IPaginated
    {
        public List<photoJson> photos = new List<photoJson>();
        public paginationJSON pagination = new paginationJSON();

        public paginationJSON pages { get => pagination; }

        public static string assetPluralType { get => "photos"; }

        public void iterator<U>(Action<U> callback)
        {
            foreach (photoJson item in photos)
            {
                (callback as Action<photoJson>)(item);
            }
        }
    }
    [Serializable]
    public class photoJson : ITypedAsset
    {
        public string id = null;
        public string name = null;

        public string image_original = null;

        public static string assetPluralType { get => "photos"; }
        public string assetId { get => id; }
        public string assetName { get => name; }

    }

    [Serializable]
    public class skyboxJson : ITypedAsset
    {
        public string id = null;
        public string name = null;
        public string three_sixty_image = null;
        public string audio_url = null;
        public string prefab_name = null;
        public List<assetBundleJSON> asset_bundles = new List<assetBundleJSON>(); // asset Bundles coined to different users? Strange.

        public static string assetPluralType { get => "skyboxes"; }
        public string assetId { get => id; }
        public string assetName { get => name; }

    }

    [System.Serializable]
    public class JsonableListWrapper<T, U, V, W>
        where T : spaceComponentJson, new()
        where U : artifactJson, new()
        where V : kitJson, new()
        where W : photoJson, new()
    {
        public List<T> space_components;
        public List<U> artifacts;
        public List<V> kits;
        public List<W> photos;
        public JsonableListWrapper(List<T> s, List<U> a, List<V> k, List<W> p)
        {
            this.space_components = s;
            this.artifacts = a;
            this.kits = k;
            this.photos = p;
        }
    }

    [System.Serializable]
    public class JsonableListWrapper<T>
        where T : photoJson, new()
    {
        public List<T> photos;
        public JsonableListWrapper(List<T> p)
        {
            this.photos = p;
        }
    }
}
