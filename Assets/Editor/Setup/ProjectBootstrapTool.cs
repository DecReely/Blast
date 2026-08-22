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

        /// <summary>Entry point used by both the menu item and the -executeMethod batch run.</summary>
        [MenuItem("Dream Games/Setup/Run Project Setup")]
        public static void RunAll()
        {
            ArtImportSettingsTool.Apply();
            ConfigurePlayerSettings();
            BuildMainScene();
            BuildLevelScene();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Project setup complete.");
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

        [MenuItem("Dream Games/Setup/Rebuild MainScene")]
        public static void BuildMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera(new Color(0.05f, 0.05f, 0.08f, 1f));

            var canvas = CreateCanvas("UI");
            CreateBackground(canvas);
            CreateEventSystem();

            SaveScene(scene, MainScenePath);
        }

        [MenuItem("Dream Games/Setup/Rebuild LevelScene")]
        public static void BuildLevelScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera(new Color(0.16f, 0.09f, 0.24f, 1f));
            CreateBoardRoot();

            CreateCanvas("UI");
            CreateEventSystem();

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

            // Placeholder framing. The level camera is fitted to the board at runtime.
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
        private static void CreateBoardRoot()
        {
            var boardGo = new GameObject("Board", typeof(Grid));

            var grid = boardGo.GetComponent<Grid>();

            // Art is imported at 140 pixels-per-unit, so one cell is exactly one world unit.
            grid.cellSize = new Vector3(1f, 1f, 0f);
            grid.cellGap = Vector3.zero;

            var frameGo = new GameObject("Frame", typeof(SpriteRenderer));
            frameGo.transform.SetParent(boardGo.transform, false);

            var frame = frameGo.GetComponent<SpriteRenderer>();
            frame.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BoardFramePath);
            frame.drawMode = SpriteDrawMode.Sliced;
            frame.size = new Vector3(6f, 6f, 0f);
            frame.sortingOrder = BoardFrameSortingOrder;

            // Item instances are parented here so the board hierarchy stays readable at runtime.
            var itemsGo = new GameObject("Items");
            itemsGo.transform.SetParent(boardGo.transform, false);
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
