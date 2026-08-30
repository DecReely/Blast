using UnityEngine;

namespace Blast.Levels
{
    /// <summary>
    /// Local persistence for how far the player has got.
    ///
    /// Deliberately the single place that knows the storage key, so the gameplay code, the main
    /// menu and the editor "set last played level" tool all agree on where the value lives.
    /// </summary>
    public static class LevelProgress
    {
        private const string CurrentLevelKey = "blast.current_level";

        /// <summary>Level the player starts on before any progress is stored.</summary>
        public const int FirstLevel = 1;

        /// <summary>
        /// The level the player should be offered next, clamped to at least
        /// <see cref="FirstLevel"/>. May exceed the level count, which is how "all levels
        /// finished" is represented.
        /// </summary>
        public static int CurrentLevel
        {
            get => Mathf.Max(FirstLevel, PlayerPrefs.GetInt(CurrentLevelKey, FirstLevel));
            set
            {
                PlayerPrefs.SetInt(CurrentLevelKey, Mathf.Max(FirstLevel, value));
                PlayerPrefs.Save();
            }
        }

        /// <summary>Advances progress after a win.</summary>
        public static void AdvanceTo(int nextLevel)
        {
            CurrentLevel = nextLevel;
        }

        /// <summary>True once the player has cleared every level in <paramref name="database"/>.</summary>
        public static bool HasFinishedAllLevels(LevelDatabase database)
        {
            return database != null && CurrentLevel > database.LevelCount;
        }

        public static void Reset()
        {
            CurrentLevel = FirstLevel;
        }
    }
}
