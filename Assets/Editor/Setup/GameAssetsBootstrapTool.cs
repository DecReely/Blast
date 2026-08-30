using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blast.Gameplay;
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

        /// <summary>Self-contained visual used for each half of a splitting rocket.</summary>
        public const string RocketHalfPrefabPath = "Assets/Prefabs/Effects/RocketHalf.prefab";

        /// <summary>Burst at the centre of a combo, whose members never explode individually.</summary>
        public const string ComboEffectPrefabPath = "Assets/Prefabs/Effects/ComboBlast.prefab";

        /// <summary>Puff a newly created special item arrives in.</summary>
        public const string SpecialCreatedEffectPrefabPath = "Assets/Prefabs/Effects/SpecialCreated.prefab";

        private const string PrefabFolder = "Assets/Prefabs/Items";
        private const string EffectFolder = "Assets/Prefabs/Effects";
        private const string LevelFolder = "Assets/Data/Levels";
        private const string CubeParticleFolder = "Assets/Art/Cubes/Particles";

        /// <summary>Debris chunks thrown out when a cube is blasted.</summary>
        private const int CubeDebrisCount = 7;

        private const string CubeDefaultFolder = "Assets/Art/Cubes/DefaultState";
        private const string CubeRocketFolder = "Assets/Art/Cubes/RocketState";
        private const string CubeTntFolder = "Assets/Art/Cubes/TntState";
        public const string RocketFolder = "Assets/Art/SpecialItems/Rocket";
        private const string ObstacleFolder = "Assets/Art/Obstacles";
        private const string TntFolder = "Assets/Art/SpecialItems/TNT";

        /// <summary>
        /// A board cell is exactly one world unit, because every board sprite is imported at the
        /// cell's pixel size. Special items are drawn to this rather than to their own artwork size.
        /// </summary>
        private static readonly Vector2 CellSize = Vector2.one;

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
            EnsureFolder(EffectFolder);
            EnsureFolder($"{EffectFolder}/Materials");

            var entries = new List<(string code, GridItem prefab)>();

            foreach (var (color, code, spriteName) in Cubes)
            {
                // Each colour gets its own debris burst, built before the cube so the cube prefab can
                // reference it.
                var blastEffect = EffectPrefabFactory.CreateDebrisBurst(
                    $"CubeBlast_{color}",
                    $"{CubeParticleFolder}/particle_{spriteName}.png",
                    CubeDebrisCount);

                entries.Add((code, CreateCube(color, spriteName, blastEffect)));
            }

            entries.Add((LevelCodes.HorizontalRocket, CreateRocket(Rocket.Axis.Horizontal)));
            entries.Add((LevelCodes.VerticalRocket, CreateRocket(Rocket.Axis.Vertical)));
            entries.Add((LevelCodes.Tnt, CreateTnt()));
            entries.Add(("s", CreateStone()));
            entries.Add(("v", CreateVase()));
            entries.Add((LevelCodes.ChaliceBoxBottomLeft, CreateChaliceBox()));

            CreateRocketHalfPrefab();
            CreateMomentEffects();

            CreateItemCatalog(entries);
            CreateLevelDatabase();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Game assets rebuilt: {entries.Count} item prefabs, catalog and level database.");
        }

        // ---------------------------------------------------------------- items

        private static GridItem CreateCube(CubeColor color, string spriteName, ParticleSystem blastEffect)
        {
            var root = NewItemRoot($"Cube_{color}", out var spriteRenderer);

            var defaultSprite = LoadSprite($"{CubeDefaultFolder}/{spriteName}.png");
            spriteRenderer.sprite = defaultSprite;

            var cube = root.AddComponent<Cube>();
            var serialized = new SerializedObject(cube);
            serialized.FindProperty("_clearEffectPrefab").objectReferenceValue = blastEffect;
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
            FitToCell(spriteRenderer);

            var rocket = root.AddComponent<Rocket>();
            var serialized = new SerializedObject(rocket);
            serialized.FindProperty("_clearEffectPrefab").objectReferenceValue =
                EffectPrefabFactory.CreateDebrisBurst(
                    "RocketBurst", $"{RocketFolder}/Particles/particle_star.png", 8);
            serialized.FindProperty("_axis").enumValueIndex = (int)axis;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab<Rocket>(root, name);
        }

        private static GridItem CreateTnt()
        {
            var root = NewItemRoot("Tnt", out var spriteRenderer);
            spriteRenderer.sprite = LoadSprite($"{TntFolder}/TNT.png");
            FitToCell(spriteRenderer);

            var tnt = root.AddComponent<Tnt>();
            var serialized = new SerializedObject(tnt);
            serialized.FindProperty("_clearEffectPrefab").objectReferenceValue =
                EffectPrefabFactory.CreateDebrisBurst(
                    "TntBlast", $"{TntFolder}/Particles/particle_tnt_01.png", 12);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab<Tnt>(root, "Tnt");
        }

        /// <summary>
        /// The two halves a rocket splits into are not board items — they occupy no cell — so this is
        /// a plain visual rather than a <see cref="GridItem"/>.
        ///
        /// It carries all four direction sprites and its own exhaust trail, so which way a half is
        /// facing is the only thing the sweep has to tell it.
        /// </summary>
        private static void CreateRocketHalfPrefab()
        {
            var root = new GameObject("RocketHalf");
            var renderer = root.AddComponent<SpriteRenderer>();

            // Above every board row. Clipping to the board is applied when a half is spawned rather
            // than baked in here; see NewItemRoot for why.
            renderer.sortingOrder = 50;

            // A half is the same size as the rocket it came from.
            renderer.sprite = LoadSprite($"{RocketFolder}/horizontal_rocket_part_right.png");
            FitToCell(renderer);

            var trail = EffectPrefabFactory.CreateTrail(
                root, "Trail", $"{RocketFolder}/Particles/particle_smoke.png");

            var view = root.AddComponent<RocketHalfView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_spriteRenderer").objectReferenceValue = renderer;
            serialized.FindProperty("_horizontalLeft").objectReferenceValue =
                LoadSprite($"{RocketFolder}/horizontal_rocket_part_left.png");
            serialized.FindProperty("_horizontalRight").objectReferenceValue =
                LoadSprite($"{RocketFolder}/horizontal_rocket_part_right.png");
            serialized.FindProperty("_verticalDown").objectReferenceValue =
                LoadSprite($"{RocketFolder}/vertical_rocket_part_bottom.png");
            serialized.FindProperty("_verticalUp").objectReferenceValue =
                LoadSprite($"{RocketFolder}/vertical_rocket_part_top.png");
            serialized.FindProperty("_trail").objectReferenceValue = trail;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, RocketHalfPrefabPath);
            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Bursts that belong to a moment rather than to an item: a combo going off, and a blast
        /// collapsing into a new special. Neither has an item to hang off, so they are standalone
        /// prefabs the scene wires into <see cref="Blast.Gameplay.SpecialItemVisuals"/>.
        /// </summary>
        private static void CreateMomentEffects()
        {
            EffectPrefabFactory.CreateDebrisBurst(
                "ComboBlast", $"{TntFolder}/Particles/particle_tnt_02.png", 20);

            EffectPrefabFactory.CreateDebrisBurst(
                "SpecialCreated", $"{RocketFolder}/Particles/particle_star.png", 10);
        }

        private static GridItem CreateStone()
        {
            var root = NewItemRoot("Stone", out var spriteRenderer);
            spriteRenderer.sprite = LoadSprite($"{ObstacleFolder}/Stone/stone.png");

            var stone = root.AddComponent<Stone>();
            var serialized = new SerializedObject(stone);
            serialized.FindProperty("_clearEffectPrefab").objectReferenceValue =
                EffectPrefabFactory.CreateDebrisBurst(
                    "StoneBreak", $"{ObstacleFolder}/Stone/Particles/particle_stone_01.png", 8);
            serialized.ApplyModifiedPropertiesWithoutUndo();

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
            serialized.FindProperty("_clearEffectPrefab").objectReferenceValue =
                EffectPrefabFactory.CreateDebrisBurst(
                    "VaseBreak", $"{ObstacleFolder}/Vase/Particles/particle_vase_01.png", 9);

            // A vase is the one obstacle with a surviving damaged state, so it is also the one that
            // needs a smaller burst for the hit that only cracks it.
            serialized.FindProperty("_damageEffectPrefab").objectReferenceValue =
                EffectPrefabFactory.CreateDebrisBurst(
                    "VaseCrack", $"{ObstacleFolder}/Vase/Particles/particle_vase_02.png", 4);

            // Ordered most healthy first: index 0 is undamaged, index 1 is one hit taken.
            var states = serialized.FindProperty("_healthStateSprites");
            states.arraySize = 2;
            states.GetArrayElementAtIndex(0).objectReferenceValue = healthy;
            states.GetArrayElementAtIndex(1).objectReferenceValue = cracked;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab<Vase>(root, "Vase");
        }

        /// <summary>
        /// The chalice box is the one item built from several renderers, so they live on children and
        /// the root only carries the <see cref="SortingGroup"/> that keeps them together.
        ///
        /// The layering is what makes the two phases work without any extra state: the cabinet at the
        /// back, the chalices in front of it, and the doors in front of everything, hiding the
        /// chalices until they break.
        /// </summary>
        private static GridItem CreateChaliceBox()
        {
            const string boxFolder = ObstacleFolder + "/ChaliceBox";

            var root = new GameObject("ChaliceBox");
            root.AddComponent<SortingGroup>();

            var box = CreateChildRenderer(root, "Box", $"{boxFolder}/ChaliceBoxBg.png", 0);

            var shelves = CreateChaliceShelves(root);

            // Comfortably above the shelf renderers, which raise the middle of each row to fake depth.
            var doors = CreateChildRenderer(root, "Doors", $"{boxFolder}/ChaliceBoxDoors.png", 50);

            var chaliceBox = root.AddComponent<ChaliceBox>();
            var serialized = new SerializedObject(chaliceBox);
            serialized.FindProperty("_boxRenderer").objectReferenceValue = box;
            serialized.FindProperty("_doorsRenderer").objectReferenceValue = doors;
            serialized.FindProperty("_shelfView").objectReferenceValue = shelves;

            // Three distinct bursts: splinters when the doors give way, a small one each time
            // chalices are taken, and the box breaking apart when the last one goes.
            serialized.FindProperty("_doorBreakEffectPrefab").objectReferenceValue =
                EffectPrefabFactory.CreateDebrisBurst(
                    "ChaliceBoxDoorBreak",
                    $"{boxFolder}/Particles/DoorParticles/chalice_box_door_particle_01.png",
                    10);

            serialized.FindProperty("_chaliceCollectEffectPrefab").objectReferenceValue =
                EffectPrefabFactory.CreateDebrisBurst(
                    "ChaliceCollect",
                    $"{boxFolder}/Particles/BaseParticles/chalice_box_particle_02.png",
                    5);

            serialized.FindProperty("_clearEffectPrefab").objectReferenceValue =
                EffectPrefabFactory.CreateDebrisBurst(
                    "ChaliceBoxBreak",
                    $"{boxFolder}/Particles/BaseParticles/chalice_box_particle_01.png",
                    12);

            serialized.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab<ChaliceBox>(root, "ChaliceBox");
        }

        /// <summary>
        /// The child that draws the box's remaining chalices. Only the artwork is wired here; the
        /// layout defaults live on the component, measured against the cabinet art.
        /// </summary>
        private static ChaliceShelfView CreateChaliceShelves(GameObject parent)
        {
            var child = new GameObject("Chalices");
            child.transform.SetParent(parent.transform, false);

            var view = child.AddComponent<ChaliceShelfView>();

            ProjectBootstrapTool.Wire(view,
                ("_chaliceSprite", LoadSprite($"{ObstacleFolder}/ChaliceBox/Chalice.png")));

            return view;
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
        ///
        /// Deliberately left unmasked. Clipping to the board is applied by
        /// <see cref="Blast.Gameplay.Board.Place"/> instead, because a masked renderer draws nothing
        /// where no <see cref="SpriteMask"/> exists — including the scene Unity renders asset
        /// thumbnails in, which is what previously left every generated prefab with a blank icon in
        /// the project window, object pickers and prefab mode.
        /// </summary>
        private static GameObject NewItemRoot(string name, out SpriteRenderer spriteRenderer)
        {
            var root = new GameObject(name);
            root.AddComponent<SortingGroup>();

            spriteRenderer = root.AddComponent<SpriteRenderer>();
            return root;
        }

        /// <summary>
        /// Makes a renderer draw its sprite at exactly one grid cell, whatever size the artwork
        /// happens to be.
        ///
        /// The supplied special item art is smaller than a cell (a rocket is 140x140 in a 150 px
        /// cell) and would otherwise sit undersized on the board. Sizing the renderer rather than
        /// scaling the transform keeps every prefab at scale one, so nothing downstream has to
        /// account for a scale factor — and both fields stay editable on the prefab.
        ///
        /// Sliced draw mode is what makes <see cref="SpriteRenderer.size"/> apply at all. It is valid
        /// here because <see cref="ArtImportSettingsTool"/> imports every sprite with a full rect
        /// mesh, which is Unity's requirement for it.
        ///
        /// Deliberately not used for cubes: their art is taller than a cell on purpose, and that
        /// overhang is what draws the shadow line between rows.
        /// </summary>
        private static void FitToCell(SpriteRenderer renderer)
        {
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = CellSize;
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
