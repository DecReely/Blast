using Blast.Levels;
using UnityEditor;
using UnityEngine;

namespace Blast.EditorTools
{
    /// <summary>
    /// Editor window for setting the last played level, which the case study asks for explicitly.
    ///
    /// Writes through <see cref="LevelProgress"/> rather than touching PlayerPrefs directly, so the
    /// editor and the game can never disagree about the storage key.
    /// </summary>
    public sealed class LevelProgressMenu : EditorWindow
    {
        private int _levelNumber = LevelProgress.FirstLevel;

        [MenuItem("Dream Games/Set Last Played Level...")]
        private static void Open()
        {
            var window = GetWindow<LevelProgressMenu>(true, "Set Last Played Level");
            window._levelNumber = LevelProgress.CurrentLevel;
            window.minSize = new Vector2(320f, 130f);
            window.maxSize = new Vector2(480f, 130f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Level shown on the main menu button", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _levelNumber = Mathf.Max(LevelProgress.FirstLevel, EditorGUILayout.IntField("Level", _levelNumber));

            var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(GameAssetsBootstrapTool.LevelDatabasePath);
            if (database != null && _levelNumber > database.LevelCount)
            {
                EditorGUILayout.HelpBox(
                    $"Above the {database.LevelCount} available levels, so the button will read \"Finished\".",
                    MessageType.Info);
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply"))
                {
                    LevelProgress.CurrentLevel = _levelNumber;
                    Debug.Log($"[LevelProgress] Last played level set to {LevelProgress.CurrentLevel}.");
                }

                if (GUILayout.Button("Reset to level 1"))
                {
                    LevelProgress.Reset();
                    _levelNumber = LevelProgress.CurrentLevel;
                    Debug.Log("[LevelProgress] Progress reset.");
                }
            }
        }
    }
}
