#if UNITY_EDITOR

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using UnityEditor;
using UnityEngine;

namespace Altspace_World_Preserver
{

    [InitializeOnLoad]
    [ExecuteInEditMode]
    public class LoginManager : EditorWindow
    {
        public static readonly string versionString = "v0.0.8";
        public static readonly string ripperRoot = "ripper";

        private static string _login = "";
        private static string _password = "";
        private static userEntryJSON _userEntry = null;
        private static Texture2D freemre = null;
        private static Texture2D vrsocial = null;
        private static Texture2D button = null;

        /// <summary>
        /// ID of the currently logged in user, null if not logged in or unavailable.
        /// </summary>
        public static string userid
        {
            get => _userEntry == null ? null : _userEntry.user_id;
        }

        /// <summary>
        /// Returns the HTTP Client (decorated with credential cookie, if available) if available
        /// </summary>
        /// <returns>Client if present, null otherwise</returns>
        public static HttpClient GetHttpClient() => WebClient.GetHttpClient();

        private OnlineSpaceManager spaceManager = null;

        public static T LoadSingleAltVRItem<T>(string item_id) where T : ITypedAsset, new()
        {
            var sar = new WebClient.SingleAssetRequest<T>(item_id);
            if (!sar.Process()) return default;

            return sar.singleAsset;
        }

        public static void LoadAltVRItems<T>(Action<T> callback) where T : IPaginated, new()
        {
            int currentPage = 0;
            int maxPage = 1;

            while (currentPage < maxPage)
            {
                EditorUtility.DisplayProgressBar("Reading item list", "Loading page... (" + currentPage + "/" + maxPage + ")", currentPage / maxPage);

                currentPage++;

                var par = new WebClient.PagedAssetsRequest<T>(currentPage);
                if (par.Process())
                {
                    maxPage = par.pages;
                    callback(par.pagedAsset);
                }
            }

            EditorUtility.ClearProgressBar();
        }
        public static void LoadSpaceComponents(string space_id, Action<spaceComponentsJson> callback)
        {
            int currentPage = 0;
            int maxPage = 1;

            while (currentPage < maxPage)
            {
                EditorUtility.DisplayProgressBar("Reading item list", "Loading page... (" + currentPage + "/" + maxPage + ")", currentPage / maxPage);

                currentPage++;

                var par = new WebClient.PagedSpaceComponentsRequest(space_id, currentPage);
                if (par.Process())
                {
                    maxPage = par.pages;
                    callback(par.pagedAsset);
                }
            }

            EditorUtility.ClearProgressBar();
        }

        [MenuItem("AWP/Login", false, 0)]
        public static void ShowLogInWindow()
        {
            LoginManager window = GetWindow<LoginManager>();
            window.Show();
        }

        public static GUIStyle getLabelLikeButtonStyle()
        {
            var style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.normal.background = button;
            style.hover.textColor = Color.blue;
            style.hover.background = button;
            style.fontStyle = FontStyle.BoldAndItalic;
            style.alignment = TextAnchor.MiddleCenter;
            return style;
        }

        public static GUIStyle getTermButtonStyle()
        {
            var style = new GUIStyle();
            style.normal.textColor = Color.grey;
            style.normal.background = button;
            style.hover.textColor = Color.red;
            style.hover.background = button;
            style.fontSize = 10;
            style.alignment = TextAnchor.MiddleCenter;
            return style;
        }

        public static GUIStyle getShameListStyle()
        {
            var style = new GUIStyle();
            style.normal.textColor = Color.red;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 15;
            return style;
        }

        private Texture2D MakeBackgroundTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            Texture2D backgroundTexture = new Texture2D(width, height);

            backgroundTexture.SetPixels(pixels);
            backgroundTexture.Apply();

            return backgroundTexture;
        }

        public void OnEnable()
        {
            wantsMouseMove = true;
            freemre = Resources.Load<Texture2D>("freelogo");
            vrsocial = Resources.Load<Texture2D>("vrsocial");
            button = MakeBackgroundTexture(1, 1, new Color32(0, 0, 0, 0));
            if (!File.Exists(Path.Combine(ripperRoot, "AssetRipper.exe")))
            {
                string zipPath = ripperRoot + ".zip";
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    Downloader.Download("https://github.com/AssetRipper/AssetRipper/releases/download/0.3.0.5/AssetRipper_win_x64.zip", zipPath);
                }
                else if (Application.platform == RuntimePlatform.OSXEditor)
                {
                    string[] appleSiliconMacs = new string[] {
                        "Mac14,5","Mac14,9",
                        "Mac14,7",
                        "MacBookPro18,3","MacBookPro18,4",
                        "MacBookPro18,1","MacBookPro18,2",
                        "MacBookPro17,1",
                    };
                    if (appleSiliconMacs.All(SystemInfo.deviceModel.Contains)) {
                        Downloader.Download("https://github.com/AssetRipper/AssetRipper/releases/download/0.3.0.5/AssetRipper_mac_arm64.zip", zipPath);
                    } else {
                        Downloader.Download("https://github.com/AssetRipper/AssetRipper/releases/download/0.3.0.5/AssetRipper_mac_x64.zip", zipPath);
                    }
                }
                ZipFile.ExtractToDirectory(zipPath, ripperRoot);
            }

            TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
        }

        public void OnDestroy()
        {
        }

        public void OnGUI()
        {
            if (Event.current.type == EventType.MouseMove)
                Repaint();
            WebClient.GetHttpClient();
            if (WebClient.IsAuthenticated && _userEntry == null)
            {
                var uidr = new WebClient.UserIDRequest();
                if (uidr.Process())
                    _userEntry = uidr.userEntry;
                else
                    WebClient.ForgetAuthentication();

            }

            // EditorGUILayout.Space(); EditorGUILayout.LabelField(SystemInfo.deviceModel);
            GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });

            Common.DisplayStatus("Login State:", "Logged out", WebClient.IsAuthenticated ? "Logged in" : null);

            if (WebClient.IsAuthenticated)
            {
                if (userid != null)
                {
                    Common.DisplayStatus("ID:", "unknown", userid);
                    Common.DisplayStatus("User handle:", "unknown", _userEntry.username);
                    Common.DisplayStatus("Display name:", "unknown", _userEntry.display_name);
                }
            }

            EditorGUILayout.Space();

            if (!WebClient.IsAuthenticated)
            {
                _login = EditorGUILayout.TextField(new GUIContent("EMail", "The EMail you've registered yourself to Altspace with."), _login);
                _password = EditorGUILayout.PasswordField(new GUIContent("Password", "Your password"), _password);

                if (GUILayout.Button("Log In"))
                    DoLogin();
            }
            else
            {
                if (GUILayout.Button("Log Out"))
                    DoLogout();

            }

            EditorGUILayout.Space();

            if (spaceManager == null)
            {
                spaceManager = ScriptableObject.CreateInstance<OnlineSpaceManager>();
            }
            spaceManager.ManageSpaces();

            // GUILayout.FlexibleSpace();
            // hallOfshame();
            GUILayout.FlexibleSpace();

            // footer
            EditorGUILayout.BeginVertical("HelpBox");
            if (GUILayout.Button("Altspace World Preserver " + versionString, getTermButtonStyle()))
                GetWindow<Terms>().Show();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var style = getLabelLikeButtonStyle();
            if (GUILayout.Button(vrsocial, style, GUILayout.MaxWidth(40), GUILayout.MaxHeight(40)))
                onLogoClick("http://vrsocial.org");
            if (GUILayout.Button("vrsocial.org", style, GUILayout.MaxWidth(90), GUILayout.MaxHeight(40)))
                onLogoClick("http://vrsocial.org");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(freemre, style, GUILayout.MaxWidth(40), GUILayout.MaxHeight(40)))
                onLogoClick("https://freemre.com");
            if (GUILayout.Button("freemre.com", style, GUILayout.MaxWidth(90), GUILayout.MaxHeight(40)))
                onLogoClick("https://freemre.com");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            GUILayout.EndVertical();

        }

        private static void hallOfshame()
        {
            EditorGUILayout.BeginVertical();
            var style = getShameListStyle();
            EditorGUILayout.LabelField("The following \"builders\" forbid you from using their stuff\nBecause their money is more important that your worlds", new GUIStyle()
            {
                normal = new GUIStyleState() { textColor = Color.cyan },
                alignment = TextAnchor.MiddleCenter
            }, GUILayout.MaxHeight(40));
            EditorGUILayout.LabelField("Optic_AltspaceVR", style);
            EditorGUILayout.LabelField("Artsy", style);
            EditorGUILayout.EndVertical();
        }

        private static void onLogoClick(string url)
        {
            Application.OpenURL(url);
        }

        private void DoLogin()
        {
            var req = new WebClient.LoginRequest(_login, _password);
            if (!req.Process())
                ShowNotification(new GUIContent("Login failed"), 5.0f);
        }

        private void DoLogout()
        {
            _userEntry = null;

            var req = new WebClient.LogoutRequest();
            if (!req.Process())
            {
                Debug.LogWarning("Logout failed");
            }

        }
    }
}

#endif // UNITY_EDITOR
