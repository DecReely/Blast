using UnityEngine.SceneManagement;

namespace Blast.UI
{
    /// <summary>
    /// The two scenes and the transitions between them.
    ///
    /// Centralised so the scene names appear once rather than as strings scattered through the UI,
    /// and so every path into a level goes through the same call.
    /// </summary>
    public static class SceneLoader
    {
        public const string MainScene = "MainScene";
        public const string LevelScene = "LevelScene";

        public static void GoToMainMenu()
        {
            SceneManager.LoadScene(MainScene);
        }

        /// <summary>Starts, or restarts, the level the player's progress points at.</summary>
        public static void GoToLevel()
        {
            SceneManager.LoadScene(LevelScene);
        }
    }
}
