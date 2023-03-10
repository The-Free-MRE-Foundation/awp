
using UnityEditor;
using UnityEngine;

namespace Altspace_World_Preserver
{
    public class Terms : EditorWindow
    {
        private static Texture2D freemre = null;
        private static Texture2D vrsocial = null;
        public void OnGUI()
        {
            showTerms();
        }
        public void OnEnable()
        {
            freemre = Resources.Load<Texture2D>("freelogo");
            vrsocial = Resources.Load<Texture2D>("vrsocial");
        }

        protected static void showTerms()
        {
            EditorGUILayout.BeginVertical();

            EditorGUILayout.Space(20);
            GUILayout.Label("User Terms", new GUIStyle(GUI.skin.label)
            {
                normal = new GUIStyleState() { textColor = Color.cyan },
                fontStyle = FontStyle.Bold,
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter
            });

            EditorGUILayout.Space(10);
            string terms = "By downloading you agree to not use for any malicious intentions, or in any illegal form, abide by International Copyright laws, not to resell this software, and not in any form profit from any method using this software.\n\nBy agreeing, you agree that you will only use the tool for \"lawful use\" and not use in any account you do not own.\n\nAnyone caught doing so will be banned permanently from all future projects including updates on this one. The creators of Altspace World Preserver will not be responsible for any action performed by any user using this tool.\n\nBy using this tool you agree to use it lawfully and use it accordingly and responsibly for your own worlds. If you planned to use the content for illegal purpose, you shall be liable for any damages not the creators, this is merely a tool for preserving your OWN worlds that you have permission over, for anything that you DO NOT own please contact the creator for permission shall you use it to upload on any other platforms, if you do not agree with this please do not use this software.";
            EditorGUILayout.BeginHorizontal("HelpBox");
            GUILayout.Label(terms, new GUIStyle()
            {
                normal = new GUIStyleState() { textColor = Color.white },
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            });

            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.Label("It's not mandatory to credit the authors, but doing so incentivizes us to make more free tools like this. If you think this tool saves you some time, please consider supporting us by joining our discord server.", new GUIStyle(GUI.skin.label)
            {
                normal = new GUIStyleState() { textColor = new Color(0.7f, 0.7f, 0.7f) },
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            });
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(vrsocial, new GUIStyle(), GUILayout.MaxWidth(30), GUILayout.MaxHeight(30)))
                onLogoClick("http://vrsocial.org");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(freemre, new GUIStyle(), GUILayout.MaxWidth(30), GUILayout.MaxHeight(30)))
                onLogoClick("https://freemre.com");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);
            EditorGUILayout.EndVertical();
        }
        private static void onLogoClick(string url)
        {
            Application.OpenURL(url);
        }
    }
}