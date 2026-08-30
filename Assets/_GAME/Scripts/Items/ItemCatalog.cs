using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blast.Items
{
    /// <summary>
    /// Maps the level files' item codes to prefabs.
    ///
    /// This is the single registration point for item types: supporting a new item means adding a
    /// prefab and one row here, with no changes to the parser or the board. The cube pool used for
    /// the <c>rand</c> code and for refilling the board is derived from the same rows, so colours
    /// can never be configured in two places and disagree.
    /// </summary>
    [CreateAssetMenu(menuName = "Blast/Item Catalog", fileName = "ItemCatalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            [Tooltip("Item code exactly as it appears in the level files.")]
            [SerializeField] private string _code;

            [SerializeField] private GridItem _prefab;

            public string Code => _code;

            public GridItem Prefab => _prefab;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        private Dictionary<string, GridItem> _prefabsByCode;
        private List<Cube> _cubePrefabs;

        /// <summary>
        /// Prefab for a code, or null if the code is not registered. Callers report the problem in
        /// context, which gives a far more useful message than a failure buried in here.
        /// </summary>
        public GridItem GetPrefab(string code)
        {
            EnsureLookupBuilt();
            return _prefabsByCode.TryGetValue(code, out var prefab) ? prefab : null;
        }

        /// <summary>
        /// A random cube colour, used for the <c>rand</c> code and for cubes spawned above the
        /// board during refill.
        /// </summary>
        public Cube GetRandomCubePrefab()
        {
            EnsureLookupBuilt();

            if (_cubePrefabs.Count == 0)
            {
                Debug.LogError("[ItemCatalog] No cube prefabs registered.", this);
                return null;
            }

            return _cubePrefabs[UnityEngine.Random.Range(0, _cubePrefabs.Count)];
        }

        private void OnDisable()
        {
            // Drop the caches so edits to the asset take effect without a domain reload.
            _prefabsByCode = null;
            _cubePrefabs = null;
        }

        private void EnsureLookupBuilt()
        {
            if (_prefabsByCode != null)
            {
                return;
            }

            _prefabsByCode = new Dictionary<string, GridItem>(_entries.Length);
            _cubePrefabs = new List<Cube>();

            foreach (var entry in _entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Code) || entry.Prefab == null)
                {
                    Debug.LogError("[ItemCatalog] Skipping an entry with a missing code or prefab.", this);
                    continue;
                }

                if (!_prefabsByCode.TryAdd(entry.Code, entry.Prefab))
                {
                    Debug.LogError($"[ItemCatalog] Duplicate code '{entry.Code}'.", this);
                    continue;
                }

                if (entry.Prefab is Cube cube)
                {
                    _cubePrefabs.Add(cube);
                }
            }
        }
    }
}
