#if UNITY_EDITOR

using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System;
using UnityEngine.Networking;

namespace Altspace_World_Preserver
{

    [InitializeOnLoad]
    [ExecuteInEditMode]
    public class PhotosManager : EditorWindow
    {
        private string directory = "Assets/Photos";
        private List<photoJson> photosList = new List<photoJson>();

        private int downloaded = 0;

        [MenuItem("AWP/Photos", false, 0)]
        public static void ShowPhotosWindow()
        {
            PhotosManager window = GetWindow<PhotosManager>();
            window.Show();
        }

        public void OnEnable()
        {
        }

        public void OnDestroy()
        {
        }

        public void OnGUI()
        {
            GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Destination", new GUIStyle { normal = new GUIStyleState() { textColor = Color.green } }, GUILayout.MaxWidth(70), GUILayout.MaxWidth(70));
            EditorGUILayout.LabelField(Path.GetFullPath(directory));
            if (GUILayout.Button("...", GUILayout.MaxWidth(40)))
            {
                string dir = EditorUtility.OpenFolderPanel("Destination", "", "");
                if (!string.IsNullOrEmpty(dir))
                {
                    directory = dir;
                }
            }
            GUILayout.EndHorizontal();
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            EditorGUILayout.Space();
            WebClient.GetHttpClient();
            if (!WebClient.IsAuthenticated)
            {
                EditorGUILayout.LabelField("You must login first");
            }
            else
            {
                if (GUILayout.Button("Start Download"))
                    DownloadPhotos();
            }

            EditorGUILayout.Space(20);
            GUILayout.EndVertical();
        }

        private void EnterItemData(photoJson photo)
        {
            this.photosList.Add(photo);
        }

        private void GetPhotosList()
        {
            LoadPhotos((photosJson content) => content.iterator<photoJson>(EnterItemData));
        }

        public void LoadPhotos(Action<photosJson> callback)
        {
            int currentPage = 0;
            int maxPage = 1;

            while (currentPage < maxPage)
            {
                EditorUtility.DisplayProgressBar("Reading item list", "Loading page... (" + currentPage + "/" + maxPage + ")", currentPage / maxPage);

                currentPage++;

                var par = new WebClient.PagedPhotosRequest(currentPage);
                if (par.Process())
                {
                    maxPage = par.pages;
                    callback(par.pagedAsset);
                }
            }

            EditorUtility.ClearProgressBar();
        }
        private void DownloadPhotos()
        {
            downloaded = 0;
            // get photos list
            string savefilePath = Path.Combine(directory, "photos.json");
            if (!File.Exists(savefilePath))
            {
                this.GetPhotosList();
                this.SavePhotosList();
            }

            string text = File.ReadAllText(savefilePath);
            JsonableListWrapper<photoJson> json = JsonUtility.FromJson<JsonableListWrapper<photoJson>>(text);
            this.photosList = json.photos;

            // download photos list
            foreach (var photo in this.photosList)
            {
                DownloadPhoto(photo);
                downloaded++;
            }
        }

        private void SavePhotosList()
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string json = JsonUtility.ToJson(new JsonableListWrapper<photoJson>(photosList), true);
            string savefilePath = Path.Combine(directory, "photos.json");
            File.WriteAllText(savefilePath, json);
            AssetDatabase.Refresh();
        }

        public void DownloadPhoto(photoJson photo)
        {
            string url = photo.image_original;
            string suffix = url.Split('.')[url.Split('.').Length - 1];
            string destination = Path.Combine(directory, photo.id + "." + suffix);
            if (File.Exists(destination))
            {
                return;
            }
            UnityWebRequest www = UnityWebRequest.Get(url);
            www.SendWebRequest();
            while (!www.isDone)
            {
                EditorUtility.DisplayProgressBar("Downloading photo " + photo.id + " (" + downloaded + "/" + this.photosList.Count + ")", string.Format("bytes downloaded far: {0:n0}", www.downloadedBytes), www.downloadProgress);
            }
            if (www.error == null)
            {
                string tempPath = destination;
                FileStream filestream = new FileStream(destination, FileMode.Create);
                filestream.Write(www.downloadHandler.data, 0, www.downloadHandler.data.Length);
                filestream.Close();
            }
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
        }
    }
}

#endif // UNITY_EDITOR
