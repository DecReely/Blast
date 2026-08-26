using Blast.Effects;
using Blast.Gameplay;
using Blast.Items;
using Blast.Levels;
using Blast.Motion;
using Blast.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Blast.EditorTools
{
    /// <summary>
    /// One-shot project setup: rebuilds the two scene skeletons the case study asks for and applies
    /// the portrait player/build settings.
    ///
    /// Kept as a re-runnable tool rather than hand-edited scene files so the layout stays
    /// reproducible, reviewable and easy to regenerate if a scene is ever damaged.
    /// </summary>
    public static class ProjectBootstrapTool
    {
        public const string MainScenePath = "Assets/Scenes/MainScene.unity";
        public const string LevelScenePath = "Assets/Scenes/LevelScene.unity";

        private const string MenuBackgroundPath = "Assets/Art/Menu/background.png";
        private const string BoardFramePath = "Assets/Art/UI/Gameplay/grid_background.png";

        /// <summary>Portrait 9:16 reference resolution used by every Canvas scaler.</summary>
        private static readonly Vector2 ReferenceResolution = new(1080f, 1920f);

        /// <summary>Board items render above this, so the frame sits well behind them.</summary>
        private const int BoardFrameSortingOrder = -100;

        /// <summary>
        /// Routine setup. Safe to re-run at any time: it only touches import settings, generated
        /// assets and project settings, and deliberately leaves the scenes alone so hand-made scene
        /// edits are never clobbered. Use <see cref="RebuildScenes"/> explicitly to regenerate those.
        /// </summary>
        [MenuItem("Dream Games/Setup/Run Project Setup")]
        public static void RunAll()
        {
            ArtImportSettingsTool.Apply();
            GameAssetsBootstrapTool.CreateAll();
            ConfigurePlayerSettings();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Project setup complete.");
        }

        /// <summary>
        /// Regenerates both scenes from scratch. Destructive: anything added to a scene by hand is
        /// lost, which is why it is kept out of <see cref="RunAll"/>.
        /// </summary>
        [MenuItem("Dream Games/Setup/Rebuild Scenes (destructive)")]
        public static void RebuildScenes()
        {
            // Import settings first: the scenes reference sprites whose 9-slice borders and
            // pixels-per-unit decide how they lay out, so building against stale settings would
            // silently produce a scene that looks wrong.
            ArtImportSettingsTool.Apply();

            // Prefabs and data assets must exist next, because LevelScene is wired to reference them.
            GameAssetsBootstrapTool.CreateAll();

            BuildMainScene();
            BuildLevelScene();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            Debug.Log("Scenes rebuilt.");
        }

        [MenuItem("Dream Games/Setup/Configure Player Settings")]
        public static void ConfigurePlayerSettings()
        {
            // The case study requires portrait 9:16 only.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Makes the editor's default standalone window portrait too.
            PlayerSettings.defaultScreenWidth = (int)ReferenceResolution.x;
            PlayerSettings.defaultScreenHeight = (int)ReferenceResolution.y;
            PlayerSettings.runInBackground = true;

            Debug.Log($"Player settings: portrait only, {ReferenceResolution.x}x{ReferenceResolution.y}.");
        }

        [MenuItem("Dream Games/Setup/Configure Build Settings")]
        public static void ConfigureBuildSettings()
        {
            // MainScene must be index 0 so a build boots into the level select.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true),
                new EditorBuildSettingsScene(LevelScenePath, true)
            };

            Debug.Log($"Build settings: 0={MainScenePath}, 1={LevelScenePath}");
        }

        [MenuItem("Dream Games/Setup/Rebuild MainScene (destructive)")]
        public static void BuildMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera(new Color(0.05f, 0.05f, 0.08f, 1f));

            var canvas = CreateCanvas("UI");
            CreateBackground(canvas);
            UiBootstrapTool.BuildMainMenu(canvas);
            CreateEventSystem();

            SaveScene(scene, MainScenePath);
        }

        [MenuItem("Dream Games/Setup/Rebuild LevelScene (destructive)")]
        public static void BuildLevelScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = CreateCamera(new Color(0.16f, 0.09f, 0.24f, 1f));
            var boardCamera = camera.gameObject.AddComponent<BoardCamera>();
            Wire(boardCamera, ("_camera", camera));

            // Input lives on the camera because a tap is only meaningful once unprojected through it.
            var boardInput = camera.gameObject.AddComponent<BoardInput>();
            Wire(boardInput, ("_camera", camera));

            var (board, boardFrame, boardMask) = CreateBoardRoot();

            var effectPool = new GameObject("Effects").AddComponent<ParticleEffectPool>();
            var fallAnimator = new GameObject("FallAnimator").AddComponent<FallAnimator>();
            var mergeAnimator = new GameObject("MergeAnimator").AddComponent<MergeAnimator>();
            var actionRunner = new GameObject("BoardActions").AddComponent<BoardActionRunner>();

            var specialVisuals = CreateSpecialItemVisuals();

            var canvas = CreateCanvas("UI");
            CreateEventSystem();

            var session = CreateLevelSession(
                board, boardFrame, boardMask, boardInput, effectPool,
                fallAnimator, mergeAnimator, actionRunner, specialVisuals);

            CreateLevelFlow(session, UiBootstrapTool.BuildLevelUi(canvas), camera);

            SaveScene(scene, LevelScenePath);
        }

        private static Camera CreateCamera(Color background)
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener))
            {
                tag = "MainCamera"
            };

            var camera = go.GetComponent<Camera>();
            camera.orthographic = true;

            // Starting value only. In LevelScene, BoardCamera recomputes this from the aspect ratio
            // so a cell is always the same size on screen regardless of the level's dimensions.
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            go.transform.position = new Vector3(0f, 0f, -10f);

            return camera;
        }

        /// <summary>
        /// The board root owns the <see cref="Grid"/> that acts as the single coordinate authority:
        /// every system converts between cells and world space through it rather than doing its own
        /// cell-size arithmetic.
        /// </summary>
        private static (Board board, BoardFrame frame, BoardMask mask) CreateBoardRoot()
        {
            var boardGo = new GameObject("Board", typeof(Grid));

            var grid = boardGo.GetComponent<Grid>();

            // Art is imported at the cell's pixel size, so one cell is exactly one world unit.
            grid.cellSize = new Vector3(1f, 1f, 0f);
            grid.cellGap = Vector3.zero;

            var frameGo = new GameObject("Frame", typeof(SpriteRenderer));
            frameGo.transform.SetParent(boardGo.transform, false);

            var frameRenderer = frameGo.GetComponent<SpriteRenderer>();
            frameRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BoardFramePath);

            // Sliced so the rounded frame art stretches to any grid size without distorting.
            frameRenderer.drawMode = SpriteDrawMode.Sliced;
            frameRenderer.size = new Vector2(6f, 6f);
            frameRenderer.sortingOrder = BoardFrameSortingOrder;

            var boardFrame = frameGo.AddComponent<BoardFrame>();
            Wire(boardFrame, ("_spriteRenderer", frameRenderer));

            // Item instances are parented here so the board hierarchy stays readable at runtime.
            var itemsGo = new GameObject("Items");
            itemsGo.transform.SetParent(boardGo.transform, false);

            var board = boardGo.AddComponent<Board>();
            Wire(board, ("_itemsRoot", itemsGo.transform));

            // Clips items to the playfield so refilled cubes are not seen above the board.
            // BoardMask supplies its own square sprite; see that class for why it cannot be rounded.
            var maskGo = new GameObject("Mask", typeof(SpriteMask));
            maskGo.transform.SetParent(boardGo.transform, false);

            var boardMask = maskGo.AddComponent<BoardMask>();

            return (board, boardFrame, boardMask);
        }

        /// <summary>
        /// Owns the rocket-half artwork. The sprites live here rather than on the rocket prefab
        /// because combos fire rockets that were never board items and need the same visuals.
        /// </summary>
        private static SpecialItemVisuals CreateSpecialItemVisuals()
        {
            var go = new GameObject("SpecialItemVisuals");
            var visuals = go.AddComponent<SpecialItemVisuals>();

            var halfPrefab = AssetDatabase
                .LoadAssetAtPath<GameObject>(GameAssetsBootstrapTool.RocketHalfPrefabPath)
                .GetComponent<SpriteRenderer>();

            const string folder = GameAssetsBootstrapTool.RocketFolder;

            Wire(visuals,
                ("_rocketHalfPrefab", halfPrefab),
                ("_horizontalLeft", LoadSprite($"{folder}/horizontal_rocket_part_left.png")),
                ("_horizontalRight", LoadSprite($"{folder}/horizontal_rocket_part_right.png")),
                ("_verticalDown", LoadSprite($"{folder}/vertical_rocket_part_bottom.png")),
                ("_verticalUp", LoadSprite($"{folder}/vertical_rocket_part_top.png")));

            return visuals;
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError($"[ProjectBootstrap] Missing sprite: {path}");
            }

            return sprite;
        }

        /// <summary>
        /// The one object that knows about all the others. Kept separate from the board so the board
        /// itself has no opinion about which level is loaded or where levels come from.
        /// </summary>
        private static LevelSessionController CreateLevelSession(
            Board board,
            BoardFrame boardFrame,
            BoardMask boardMask,
            BoardInput boardInput,
            ParticleEffectPool effectPool,
            FallAnimator fallAnimator,
            MergeAnimator mergeAnimator,
            BoardActionRunner actionRunner,
            SpecialItemVisuals specialItemVisuals)
        {
            var go = new GameObject("LevelSession");
            var controller = go.AddComponent<LevelSessionController>();

            Wire(controller,
                ("_levelDatabase", AssetDatabase.LoadAssetAtPath<LevelDatabase>(GameAssetsBootstrapTool.LevelDatabasePath)),
                ("_itemCatalog", AssetDatabase.LoadAssetAtPath<ItemCatalog>(GameAssetsBootstrapTool.ItemCatalogPath)),
                ("_board", board),
                ("_boardFrame", boardFrame),
                ("_boardMask", boardMask),
                ("_boardInput", boardInput),
                ("_effectPool", effectPool),
                ("_fallAnimator", fallAnimator),
                ("_mergeAnimator", mergeAnimator),
                ("_actionRunner", actionRunner),
                ("_specialItemVisuals", specialItemVisuals));

            return controller;
        }

        /// <summary>
        /// The bridge between the session and its interface. Kept as its own object so the session
        /// stays unaware of the UI and the UI stays unaware of how a level is built.
        /// </summary>
        private static void CreateLevelFlow(
            LevelSessionController session, UiBootstrapTool.LevelUi ui, Camera boardCamera)
        {
            var go = new GameObject("LevelFlow");
            var flow = go.AddComponent<LevelFlowController>();

            Wire(flow,
                ("_session", session),
                ("_topBar", ui.TopBar),
                ("_failPopup", ui.FailPopup),
                ("_winCelebration", ui.WinCelebration),
                ("_chaliceFlight", ui.ChaliceFlight),
                ("_boardCamera", boardCamera));
        }

        /// <summary>
        /// Assigns private serialized fields through <see cref="SerializedObject"/>, which is the
        /// supported way to set them from editor code without widening their access for runtime.
        /// </summary>
        internal static void Wire(Object target, params (string property, Object value)[] references)
        {
            var serialized = new SerializedObject(target);

            foreach (var (property, value) in references)
            {
                var field = serialized.FindProperty(property);
                if (field == null)
                {
                    Debug.LogError($"[ProjectBootstrap] {target.GetType().Name} has no serialized field '{property}'.");
                    continue;
                }

                if (value == null)
                {
                    Debug.LogError($"[ProjectBootstrap] Nothing to assign to {target.GetType().Name}.{property}.");
                    continue;
                }

                field.objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Canvas CreateCanvas(string name)
        {
            var root = new GameObject(name);

            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(root.transform, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            // Match width: in portrait the width is the constrained axis, so UI keeps a consistent
            // horizontal layout and taller screens simply reveal more vertical space.
            scaler.matchWidthOrHeight = 0f;

            return canvas;
        }

        /// <summary>
        /// Full-bleed area image for the main menu. <see cref="AspectRatioFitter"/> in
        /// <see cref="AspectRatioFitter.AspectMode.EnvelopeParent"/> mode gives "cover" behaviour, so
        /// the artwork fills any aspect ratio without being stretched.
        /// </summary>
        private static void CreateBackground(Canvas canvas)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MenuBackgroundPath);

            var go = new GameObject("Background", typeof(Image), typeof(AspectRatioFitter));
            go.transform.SetParent(canvas.transform, false);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var fitter = go.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite != null
                ? sprite.rect.width / sprite.rect.height
                : ReferenceResolution.x / ReferenceResolution.y;
        }

        /// <summary>
        /// The project is set to the Input System package only, so the UI module has to be the
        /// Input System one rather than the legacy <c>StandaloneInputModule</c>.
        /// </summary>
        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static void SaveScene(Scene scene, string path)
        {
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                Debug.LogError($"Failed to save scene: {path}");
                return;
            }

            Debug.Log($"Rebuilt scene: {path}");
        }
    }
}
