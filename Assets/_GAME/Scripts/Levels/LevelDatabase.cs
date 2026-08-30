using UnityEngine;

namespace Blast.Levels
{
    /// <summary>
    /// The ordered set of level files shipped with the game.
    ///
    /// Holds explicit <see cref="TextAsset"/> references rather than loading from a
    /// <c>Resources</c> folder by name: the dependency is visible in the inspector, survives
    /// renames, and cannot silently break at runtime because of a typo'd path.
    /// </summary>
    [CreateAssetMenu(menuName = "Blast/Level Database", fileName = "LevelDatabase")]
    public sealed class LevelDatabase : ScriptableObject
    {
        [Tooltip("Level files in play order. Index 0 is level 1.")]
        [SerializeField] private TextAsset[] _levelFiles;

        /// <summary>Total number of playable levels, used to detect "all levels finished".</summary>
        public int LevelCount => _levelFiles?.Length ?? 0;

        /// <summary>True when <paramref name="levelNumber"/> (1-based) is a playable level.</summary>
        public bool IsValidLevelNumber(int levelNumber)
        {
            return levelNumber >= 1 && levelNumber <= LevelCount;
        }

        /// <summary>
        /// Parses the level file for a 1-based level number.
        /// Returns null and logs once if the level is missing or malformed, so callers can fail
        /// gracefully instead of throwing deep inside board construction.
        /// </summary>
        public LevelDefinition Load(int levelNumber)
        {
            if (!IsValidLevelNumber(levelNumber))
            {
                Debug.LogError($"[LevelDatabase] Level {levelNumber} is out of range (1-{LevelCount}).", this);
                return null;
            }

            var file = _levelFiles[levelNumber - 1];
            if (file == null)
            {
                Debug.LogError($"[LevelDatabase] Level {levelNumber} has no file assigned.", this);
                return null;
            }

            var definition = JsonUtility.FromJson<LevelDefinition>(file.text);
            if (definition == null)
            {
                Debug.LogError($"[LevelDatabase] Could not parse '{file.name}'.", this);
                return null;
            }

            if (!definition.TryValidate(out var error))
            {
                Debug.LogError($"[LevelDatabase] '{file.name}' is invalid: {error}", this);
                return null;
            }

            return definition;
        }
    }
}
