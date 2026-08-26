using System;
using System.IO;
using Blast.Gameplay;
using Blast.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Blast.EditorTools
{
    /// <summary>
    /// Renders the interfaces offscreen to PNG files, the way <see cref="LevelPreviewTool"/> does for
    /// the board.
    ///
    /// The canvases run in Screen Space Overlay, which by definition is composited straight to the
    /// display and never appears in a camera's render texture. To capture them the canvas is put into
    /// Screen Space Camera mode for the duration of the shot and restored afterwards, which changes
    /// nothing about how the UI is laid out.
    ///
    /// Nothing here runs in play mode, so no <c>Awake</c> has fired: state that a component would
    /// normally set up for itself is driven explicitly below.
    /// </summary>
    public static class UiPreviewTool
    {
        private const int CaptureWidth = 540;
        private const int CaptureHeight = 960;

        [MenuItem("Dream Games/Debug/Capture UI Previews")]
        public static void CaptureAll()
        {
            var folder = Path.Combine(Path.GetTempPath(), "blast_previews");
            Directory.CreateDirectory(folder);

            CaptureMainMenu(folder);
            CaptureLevel(folder);

            Debug.Log($"[UiPreview] UI previews written to {folder}");
        }

        private static void CaptureMainMenu(string folder)
        {
            EditorSceneManager.OpenScene(ProjectBootstrapTool.MainScenePath, OpenSceneMode.Single);

            // OnEnable has not run outside play mode, so the label still shows its authored
            // placeholder until the controller is refreshed by hand.
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            menu?.Refresh();

            WithCameraCanvas(shoot => shoot(Path.Combine(folder, "ui_main_menu.png")));
        }

        private static void CaptureLevel(string folder)
        {
            EditorSceneManager.OpenScene(ProjectBootstrapTool.LevelScenePath, OpenSceneMode.Single);

            var session = Object.FindFirstObjectByType<LevelSessionController>();
            var board = Object.FindFirstObjectByType<Board>();
            var topBar = FindIncludingInactive<TopBarView>();
            var failPopup = FindIncludingInactive<FailPopup>();
            var celebration = FindIncludingInactive<WinCelebration>();

            if (session == null || board == null)
            {
                Debug.LogError("[UiPreview] LevelScene is missing a gameplay object.");
                return;
            }

            try
            {
                SetActive(failPopup, false);
                SetActive(celebration, false);

                // Level 1 is stones only, so its single goal should fill the panel.
                Load(session, topBar, 1);
                WithCameraCanvas(shoot => shoot(Path.Combine(folder, "ui_top_bar_one_goal.png")));

                // Level 7 has stones, vases and chalice boxes: every slot at once.
                Load(session, topBar, 7);
                WithCameraCanvas(shoot => shoot(Path.Combine(folder, "ui_top_bar_three_goals.png")));

                // Clearing one obstacle type swaps its count for a tick.
                ClearVases(board);
                session.Goals.Refresh(board);
                WithCameraCanvas(shoot => shoot(Path.Combine(folder, "ui_top_bar_goal_met.png")));

                SetActive(failPopup, true);
                WithCameraCanvas(shoot => shoot(Path.Combine(folder, "ui_fail_popup.png")));
                SetActive(failPopup, false);

                SetActive(celebration, true);
                WithCameraCanvas(shoot => shoot(Path.Combine(folder, "ui_win_celebration.png")));
                SetActive(celebration, false);
            }
            finally
            {
                // Same rule as the board previews: never leave spawned items in the scene asset.
                BoardBuilder.ClearItems(board);
            }
        }

        /// <summary>
        /// Loads a level and binds the bar to it. Outside play mode nothing subscribes to the
        /// session's events, so the wiring the flow controller normally does is done here.
        /// </summary>
        private static void Load(LevelSessionController session, TopBarView topBar, int levelNumber)
        {
            session.LoadLevel(levelNumber);
            topBar?.Bind(session.Moves, session.Goals);
        }

        private static void ClearVases(Board board)
        {
            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    if (board.GetItem(new Vector2Int(x, y)) is not Items.Vase vase)
                    {
                        continue;
                    }

                    board.Remove(vase);
                    Object.DestroyImmediate(vase.gameObject);
                }
            }
        }

        private static T FindIncludingInactive<T>() where T : Object
        {
            var found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return found.Length > 0 ? found[0] : null;
        }

        private static void SetActive(Component component, bool active)
        {
            if (component != null)
            {
                component.gameObject.SetActive(active);
            }
        }

        /// <summary>
        /// Points the canvas at the scene camera, renders, then puts everything back. The camera is
        /// shared with the board, so a level shot composites the UI over the real playfield.
        /// </summary>
        private static void WithCameraCanvas(Action<Action<string>> body)
        {
            var camera = Camera.main;
            var canvas = FindIncludingInactive<Canvas>();

            if (camera == null || canvas == null)
            {
                Debug.LogError("[UiPreview] Scene has no camera or canvas.");
                return;
            }

            var previousMode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            var previousDistance = canvas.planeDistance;
            var previousOrder = canvas.sortingOrder;
            var previousTarget = camera.targetTexture;

            var renderTexture = new RenderTexture(CaptureWidth, CaptureHeight, 24);

            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;

                // Comfortably inside the near plane, so the UI is always in front of the board.
                canvas.planeDistance = 1f;

                // Board items sort by row, so the canvas has to out-sort the topmost of them. Overlay
                // mode ignores this, which is why the real scene does not need it.
                canvas.sortingOrder = 1000;

                camera.targetTexture = renderTexture;

                // The render texture defines the viewport, so the board camera must resize to it and
                // the canvas must re-run its layout at the new reference size.
                var boardCamera = camera.GetComponent<BoardCamera>();
                boardCamera?.Apply();

                Canvas.ForceUpdateCanvases();
                RebuildLayout(canvas.transform as RectTransform);

                body(path => File.WriteAllBytes(path, Capture(camera, renderTexture)));
            }
            finally
            {
                camera.targetTexture = previousTarget;
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousDistance;
                canvas.sortingOrder = previousOrder;

                Object.DestroyImmediate(renderTexture);
            }
        }

        private static void RebuildLayout(RectTransform root)
        {
            if (root == null)
            {
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(root);

            for (var i = 0; i < root.childCount; i++)
            {
                RebuildLayout(root.GetChild(i) as RectTransform);
            }
        }

        private static byte[] Capture(Camera camera, RenderTexture renderTexture)
        {
            camera.Render();

            var active = RenderTexture.active;
            RenderTexture.active = renderTexture;

            var image = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0);
            image.Apply();

            RenderTexture.active = active;

            var png = image.EncodeToPNG();
            Object.DestroyImmediate(image);
            return png;
        }
    }
}
