using System.IO;
using Blast.Gameplay;
using Blast.Motion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Blast.EditorTools
{
    /// <summary>
    /// Renders levels offscreen to PNG files.
    ///
    /// A development aid that makes board layout, cell alignment, sprite sorting and blast behaviour
    /// reviewable at a glance, without having to enter play mode and step through by hand.
    ///
    /// Everything it spawns is torn down again before it returns. An earlier version left the items
    /// it built sitting in <c>LevelScene</c>, which then got saved into the scene asset — so cleanup
    /// runs in a <c>finally</c> block and is not optional.
    /// </summary>
    public static class LevelPreviewTool
    {
        /// <summary>Portrait 9:16, matching the target aspect ratio.</summary>
        private const int CaptureWidth = 540;

        private const int CaptureHeight = 960;

        private const int LevelCount = 10;

        /// <summary>Fixed time step used to drive animation outside play mode, where there is no Update.</summary>
        internal const float FrameStep = 1f / 60f;

        [MenuItem("Dream Games/Debug/Capture Level Previews")]
        public static void CaptureAll()
        {
            var outputFolder = PrepareOutputFolder();

            RunInLevelScene((session, camera, capture) =>
            {
                for (var levelNumber = 1; levelNumber <= LevelCount; levelNumber++)
                {
                    session.LoadLevel(levelNumber);

                    var level = session.CurrentLevel;
                    if (level == null)
                    {
                        continue;
                    }

                    capture(Path.Combine(
                        outputFolder,
                        $"level_{levelNumber:00}_{level.Width}x{level.Height}.png"));
                }
            });

            Debug.Log($"[LevelPreview] Previews written to {outputFolder}");
        }

        /// <summary>
        /// Loads a level, taps a cell and captures before/after, which exercises the whole tap
        /// pipeline — screen-to-cell conversion, group finding, the move counter and hint refresh.
        /// </summary>
        [MenuItem("Dream Games/Debug/Capture Blast Simulation")]
        public static void CaptureBlastSimulation()
        {
            var outputFolder = PrepareOutputFolder();

            // Level 1 has a solid block of six blue cubes in its third row, which is large enough to
            // show both a TNT hint beforehand and an obvious hole afterwards.
            const int levelNumber = 1;
            var tappedCell = new Vector2Int(2, 2);

            RunInLevelScene((session, camera, capture) =>
            {
                session.LoadLevel(levelNumber);

                var board = Object.FindFirstObjectByType<Board>();
                var coordinator = session.Coordinator;
                if (board == null || coordinator == null)
                {
                    Debug.LogError("[LevelPreview] Level session did not start.");
                    return;
                }

                capture(Path.Combine(outputFolder, "blast_0_before.png"));

                var animator = Object.FindFirstObjectByType<FallAnimator>();
                var movesBefore = session.Moves.Remaining;

                coordinator.HandleWorldTap(board.CellToWorld(tappedCell));

                // A few frames in, so the capture shows items genuinely in mid-air.
                Advance(animator, 5);
                capture(Path.Combine(outputFolder, "blast_1_midfall.png"));

                var frames = RunUntilSettled(animator, coordinator);
                capture(Path.Combine(outputFolder, "blast_2_settled.png"));

                Debug.Log($"[LevelPreview] Tapped {tappedCell}: moves {movesBefore} -> {session.Moves.Remaining}, " +
                          $"settled after {frames} frames.");
                Debug.Log("[LevelPreview] " + BoardIntegrity.Describe(board));
            });

            Debug.Log($"[LevelPreview] Blast simulation written to {outputFolder}");
        }

        /// <summary>
        /// Opens LevelScene, hands the caller a configured camera plus a capture callback, and
        /// guarantees the scene is emptied of spawned items again afterwards.
        /// </summary>
        private static void RunInLevelScene(System.Action<LevelSessionController, Camera, System.Action<string>> body)
        {
            EditorSceneManager.OpenScene(ProjectBootstrapTool.LevelScenePath, OpenSceneMode.Single);

            var session = Object.FindFirstObjectByType<LevelSessionController>();
            var boardCamera = Object.FindFirstObjectByType<BoardCamera>();
            var camera = boardCamera != null ? boardCamera.GetComponent<Camera>() : null;

            if (session == null || camera == null)
            {
                Debug.LogError("[LevelPreview] LevelScene is missing its session controller or camera.");
                return;
            }

            var renderTexture = new RenderTexture(CaptureWidth, CaptureHeight, 24);
            var previousTarget = camera.targetTexture;
            camera.targetTexture = renderTexture;

            // Assigning a render texture changes the camera's aspect, so resize after doing so.
            boardCamera.Apply();

            try
            {
                body(session, camera, path => File.WriteAllBytes(path, Capture(camera, renderTexture)));
            }
            finally
            {
                camera.targetTexture = previousTarget;
                Object.DestroyImmediate(renderTexture);

                // Leave no spawned items behind: if the scene is saved later they would be baked in.
                var board = Object.FindFirstObjectByType<Board>();
                if (board != null)
                {
                    BoardBuilder.ClearItems(board);
                }
            }
        }

        /// <summary>
        /// Opens LevelScene and hands the caller its gameplay objects, with no rendering set up.
        /// Used by logic-only checks. Spawned items are cleared again on the way out.
        /// </summary>
        internal static void RunHeadless(System.Action<LevelSessionController, Board, FallAnimator> body)
        {
            EditorSceneManager.OpenScene(ProjectBootstrapTool.LevelScenePath, OpenSceneMode.Single);

            var session = Object.FindFirstObjectByType<LevelSessionController>();
            var board = Object.FindFirstObjectByType<Board>();
            var animator = Object.FindFirstObjectByType<FallAnimator>();

            if (session == null || board == null || animator == null)
            {
                Debug.LogError("[LevelPreview] LevelScene is missing a gameplay object.");
                return;
            }

            try
            {
                body(session, board, animator);
            }
            finally
            {
                BoardBuilder.ClearItems(board);
            }
        }

        /// <summary>Steps the fall animation on by a fixed number of frames.</summary>
        internal static void Advance(FallAnimator animator, int frames)
        {
            if (animator == null)
            {
                return;
            }

            for (var i = 0; i < frames; i++)
            {
                animator.Tick(FrameStep);
            }
        }

        /// <summary>
        /// Runs a settle to completion at a fixed time step. Capped so a stuck animation reports an
        /// error instead of hanging the editor.
        /// </summary>
        internal static int RunUntilSettled(FallAnimator animator, BoardCoordinator coordinator)
        {
            const int maxFrames = 900;
            var frames = 0;

            while (coordinator.IsResolving && frames < maxFrames)
            {
                animator.Tick(FrameStep);
                frames++;
            }

            if (coordinator.IsResolving)
            {
                Debug.LogError($"[LevelPreview] Board never settled after {maxFrames} frames.");
            }

            return frames;
        }

        private static string PrepareOutputFolder()
        {
            var folder = Path.Combine(Path.GetTempPath(), "blast_previews");
            Directory.CreateDirectory(folder);
            return folder;
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
