using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blast.Items;
using Blast.Levels;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Blast.EditorTools
{
    /// <summary>
    /// Generates the item prefabs and the two data assets the game needs.
    ///
    /// Doing this in code rather than by hand keeps the prefab set consistent (every item gets the
    /// same component layout and sorting setup) and makes it obvious which sprite belongs to which
    /// item. Re-running overwrites the generated assets in place, so existing references survive.
    /// </summary>
    public static class GameAssetsBootstrapTool
    {
        public const string ItemCatalogPath = "Assets/Data/ItemCatalog.asset";
        public const string LevelDatabasePath = "Assets/Data/LevelDatabase.asset";

        private const string PrefabFolder = "Assets/Prefabs/Items";
        private const string LevelFolder = "Assets/Data/Levels";

        private const string CubeDefaultFolder = "Assets/Art/Cubes/DefaultState";
        private const string CubeRocketFolder = "Assets/Art/Cubes/RocketState";
        private const string CubeTntFolder = "Assets/Art/Cubes/TntState";
        private const string RocketFolder = "Assets/Art/SpecialItems/Rocket";
        private const string ObstacleFolder = "Assets/Art/Obstacles";

        /// <summary>Sprite file stem per cube colour, matching the supplied art.</summary>
        private static readonly (CubeColor color, string code, string spriteName)[] Cubes =
        {
            (CubeColor.Red, "r", "red"),
            (CubeColor.Green, "g", "green"),
            (CubeColor.Blue, "b", "blue"),
            (CubeColor.Yellow, "y", "yellow")
        };

        [MenuItem("Dream Games/Setup/Rebuild Game Assets")]
        public static void CreateAll()
        {
            EnsureFolder(PrefabFolder);

            var entries = new List<(string code, GridItem prefab)>();

            foreach (var (color, code, spriteName) in Cubes)
            {
                entries.Add((code, CreateCube(color, spriteName)));
            }

            entries.Add(("hro", CreateRocket(Rocket.Axis.Horizontal)));
            entries.Add(("vro", CreateRocket(Rocket.Axis.Vertical)));
            entries.Add(("t", CreateTnt()));
            entries.Add(("s", CreateStone()));
            entries.Add(("v", CreateVase()));
            entries.Add((LevelCodes.ChaliceBoxBottomLeft, CreateChaliceBox()));

            CreateItemCatalog(entries);
            CreateLevelDatabase();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Game assets rebuilt: {entries.Count} item prefabs, catalog and level database.");
        }

        // ---------------------------------------------------------------- items

        private static GridItem CreateCube(CubeColor color, string spriteName)
        {
            var root = NewItemRoot($"Cube_{color}", out var spriteRenderer);

            var defaultSprite = LoadSprite($"{CubeDefaultFolder}/{spriteName}.png");
            spriteRenderer.sprite = defaultSprite;

            var cube = root.AddComponent<Cube>();
            var serialized = new SerializedObject(cube);
            serialized.FindProperty("_color").enumValueIndex = (int)color;
            serialized.FindProperty("_spriteRenderer").objectReferenceValue = spriteRenderer;
            serialized.FindProperty("_defaultSprite").objectReferenceValue = defaultSprite;
            serialized.FindProperty("_rocketHintSprite").objectReferenceValue =
                LoadSprite($"{CubeRocketFolder}/{spriteName}_rocket.png");
            serialized.FindProperty("_tntHintSprite").objectReferenceValue =
                LoadSprite($"{CubeTntFolder}/{spriteName}_tnt.png");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab<Cube>(root, $"Cube_{color}");
        }

        private static GridItem CreateRocket(Rocket.Axis axis)
        {
            var horizontal = axis == Rocket.Axis.Horizontal;
            var name = $"Rocket_{axis}";
            var root = NewItemRoot(name, out var spriteRenderer);

            var stem = horizontal ? "horizontal_rocket" : "vertical_rocket";
            spriteRenderer.sprite = LoadSprite($"{RocketFolder}/{stem}.png");

            var rocket = root.AddComponent<Rocket>();
            var serialized = new SerializedObject(rocket);
            serialized.FindProperty("_axis").enumValueIndex = (int)axis;
            serialized.FindProperty("_negativeHalfSprite").objectReferenceValue =
                LoadSprite($"{RocketFolder}/{stem}_part_{(horizontal ? "left" : "bottom")}.png");
            serialized.FindProperty("_positiveHalfSprite").objectReferenceValue =
                LoadSprite($"{RocketFolder}/{stem}_part_{(horizontal ? "right" : "top")}.png");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab<Rocket>(root, name);
        }

        private static GridItem CreateTnt()
        {
            var root = NewItemRoot("Tnt", out var spriteRenderer);
            spriteRenderer.sprite = LoadSprite("Assets/Art/SpecialItems/TNT/TNT.png");
            root.AddComponent<Tnt>();

            return SavePrefab<Tnt>(root, "Tnt");
        }

        private static GridItem CreateStone()
        {
            var root = NewItemRoot("Stone", out var spriteRenderer);
            spriteRenderer.sprite = LoadSprite($"{ObstacleFolder}/Stone/stone.png");
            root.AddComponent<Stone>();

            return SavePrefab<Stone>(root, "Stone");
        }

        private static GridItem CreateVase()
        {
            var root = NewItemRoot("Vase", out var spriteRenderer);

            var healthy = LoadSprite($"{ObstacleFolder}/Vase/vase_01.png");
            var cracked = LoadSprite($"{ObstacleFolder}/Vase/vase_02.png");
            spriteRenderer.sprite = healthy;

            var vase = root.AddComponent<Vase>();
            var serialized = new SerializedObject(vase);
            serialized.FindProperty("_spriteRenderer").objectReferenceValue = spriteRenderer;

            // Ordered most healthy first: index 0 is undamaged, index 1 is one hit taken.
            var states = serialized.FindProperty("_healthStateSprites");
            states.arraySize = 2;
            states.GetArrayElementAtIndex(0).objectReferenceValue = healthy;
            states.GetArrayElementAtIndex(1).objectReferenceValue = cracked;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab<Vase>(root, "Vase");
        }

        /// <summary>
        /// The chalice box is the one item built from two sprites, so its renderers live on children
        /// and the root only carries the <see cref="SortingGroup"/> that keeps them together.
        /// </summary>
        private static GridItem CreateChaliceBox()
        {
            var root = new GameObject("ChaliceBox");
            root.AddComponent<SortingGroup>();

            var box = CreateChildRenderer(root, "Box", $"{ObstacleFolder}/ChaliceBox/ChaliceBoxBg.png", 0);
            var doors = CreateChildRenderer(root, "Doors", $"{ObstacleFolder}/ChaliceBox/ChaliceBoxDoors.png", 1);

            var chaliceBox = root.AddComponent<ChaliceBox>();
            var serialized = new SerializedObject(chaliceBox);
            serialized.FindProperty("_boxRenderer").objectReferenceValue = box;
            serialized.FindProperty("_doorsRenderer").objectReferenceValue = doors;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab<ChaliceBox>(root, "ChaliceBox");
        }

        // ---------------------------------------------------------------- data assets

        private static void CreateItemCatalog(List<(string code, GridItem prefab)> entries)
        {
            EnsureFolder("Assets/Data");

            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ItemCatalog>();
                AssetDatabase.CreateAsset(catalog, ItemCatalogPath);
            }

            // Written through SerializedObject so the runtime asset needs no editor-only API.
            var serialized = new SerializedObject(catalog);
            var rows = serialized.FindProperty("_entries");
            rows.arraySize = entries.Count;

            for (var i = 0; i < entries.Count; i++)
            {
                var row = rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("_code").stringValue = entries[i].code;
                row.FindPropertyRelative("_prefab").objectReferenceValue = entries[i].prefab;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        /// <summary>
        /// Populates the level database from the level folder, ordered by file name so
        /// <c>level_01</c>..<c>level_10</c> line up with level numbers 1..10.
        /// </summary>
        private static void CreateLevelDatabase()
        {
            var files = AssetDatabase.FindAssets("t:TextAsset", new[] { LevelFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".json"))
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<TextAsset>)
                .Where(asset => asset != null)
                .ToArray();

            if (files.Length == 0)
            {
                Debug.LogError($"[GameAssetsBootstrap] No level files found in {LevelFolder}.");
                return;
            }

            var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(LevelDatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<LevelDatabase>();
                AssetDatabase.CreateAsset(database, LevelDatabasePath);
            }

            var serialized = new SerializedObject(database);
            var list = serialized.FindProperty("_levelFiles");
            list.arraySize = files.Length;

            for (var i = 0; i < files.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = files[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);

            Debug.Log($"[GameAssetsBootstrap] Level database populated with {files.Length} levels: " +
                      string.Join(", ", files.Select(f => f.name)));
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Every single-sprite item shares the same layout: a sorting group so multi-renderer items
        /// behave the same way, plus one sprite renderer.
        /// </summary>
        private static GameObject NewItemRoot(string name, out SpriteRenderer spriteRenderer)
        {
            var root = new GameObject(name);
            root.AddComponent<SortingGroup>();
            spriteRenderer = root.AddComponent<SpriteRenderer>();
            return root;
        }

        private static SpriteRenderer CreateChildRenderer(GameObject parent, string name, string spritePath, int order)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);

            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadSprite(spritePath);

            // Ordering within the sorting group, not against the rest of the board.
            renderer.sortingOrder = order;
            return renderer;
        }

        private static GridItem SavePrefab<T>(GameObject root, string name) where T : GridItem
        {
            var path = $"{PrefabFolder}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            if (prefab == null)
            {
                Debug.LogError($"[GameAssetsBootstrap] Failed to save prefab {path}.");
                return null;
            }

            return prefab.GetComponent<T>();
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError($"[GameAssetsBootstrap] Missing sprite: {path}");
            }

            return sprite;
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), assetFolder));
            AssetDatabase.Refresh();
        }
    }
}
