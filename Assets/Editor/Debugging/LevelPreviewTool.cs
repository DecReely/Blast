using System;
using System.IO;
using Blast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Blast.EditorTools
{
    /// <summary>
    /// Renders levels offscreen to PNG files.
    ///
    /// A development aid that makes board layout, sprite sorting, falling and explosions reviewable
    /// at a glance, without having to enter play mode and step through by hand.
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

        /// <summary>Context handed to a capture body.</summary>
        internal sealed class Context
        {
            public LevelSessionController Session;
            public Board Board;
            public SceneTicker Ticker;
            public Action<string> Capture;
        }

        [MenuItem("Dream Games/Debug/Capture Level Previews")]
        public static void CaptureAll()
        {
            var outputFolder = PrepareOutputFolder();

            RunInLevelScene(context =>
            {
                for (var levelNumber = 1; levelNumber <= LevelCount; levelNumber++)
                {
                    context.Session.LoadLevel(levelNumber);

                    var level = context.Session.CurrentLevel;
                    if (level == null)
                    {
                        continue;
                    }

                    context.Capture(Path.Combine(
                        outputFolder,
                        $"level_{levelNumber:00}_{level.Width}x{level.Height}.png"));
                }
            });

            Debug.Log($"[LevelPreview] Previews written to {outputFolder}");
        }

        /// <summary>
        /// Captures the three distinct turn types in sequence on one board: a plain blast, a blast
        /// large enough to create a special, and detonating that special. Exercises the whole tap
        /// pipeline including chain reactions.
        /// </summary>
        [MenuItem("Dream Games/Debug/Capture Turn Simulation")]
        public static void CaptureTurnSimulation()
        {
            var outputFolder = PrepareOutputFolder();

            RunInLevelScene(context =>
            {
                // Level 1: two full rows of stone under a large field of cubes, so a rocket sweeping
                // along the bottom has plenty to clear.
                context.Session.LoadLevel(1);

                var board = context.Board;
                var coordinator = context.Session.Coordinator;

                context.Capture(Path.Combine(outputFolder, "turn_0_start.png"));

                // A big group becomes a special item; capture the cubes mid-flight.
                var cell = FindLargestCubeGroupCell(board, out var groupSize);
                var movesBefore = context.Session.Moves.Remaining;

                coordinator.HandleWorldTap(board.CellToWorld(cell));
                context.Ticker.Advance(4);
                context.Capture(Path.Combine(outputFolder, "turn_1_merging.png"));

                context.Ticker.RunUntilIdle(coordinator);
                context.Capture(Path.Combine(outputFolder, "turn_2_special_created.png"));

                var special = FindSpecial(board, out var specialCell);
                Debug.Log($"[LevelPreview] Blasted {groupSize} cubes at {cell} " +
                          $"(moves {movesBefore} -> {context.Session.Moves.Remaining}); " +
                          $"created {(special == null ? "nothing" : special.GetType().Name)} at {specialCell}.");

                if (special == null)
                {
                    return;
                }

                // Detonating it: capture the sweep in flight, then the aftermath.
                coordinator.HandleWorldTap(board.CellToWorld(specialCell));
                context.Ticker.Advance(5);
                context.Capture(Path.Combine(outputFolder, "turn_3_exploding.png"));

                var frames = context.Ticker.RunUntilIdle(coordinator);
                context.Capture(Path.Combine(outputFolder, "turn_4_settled.png"));

                Debug.Log($"[LevelPreview] Detonation settled after {frames} frames. " +
                          BoardIntegrity.Describe(board));
            });

            Debug.Log($"[LevelPreview] Turn simulation written to {outputFolder}");
        }

        /// <summary>Cell belonging to the biggest cube group, so the tap reliably creates a special.</summary>
        private static Vector2Int FindLargestCubeGroupCell(Board board, out int groupSize)
        {
            var finder = new GroupFinder();
            var buffer = new System.Collections.Generic.List<Vector2Int>();

            var best = Vector2Int.zero;
            groupSize = 0;

            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var size = finder.FindGroupOfCubes(board, cell, buffer);

                    if (size <= groupSize)
                    {
                        continue;
                    }

                    groupSize = size;
                    best = cell;
                }
            }

            return best;
        }

        private static Items.SpecialItem FindSpecial(Board board, out Vector2Int cell)
        {
            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    cell = new Vector2Int(x, y);
                    if (board.GetItem(cell) is Items.SpecialItem special)
                    {
                        return special;
                    }
                }
            }

            cell = Vector2Int.zero;
            return null;
        }

        /// <summary>
        /// Opens LevelScene and hands the caller its gameplay objects, with no rendering set up.
        /// Used by logic-only checks. Spawned items are cleared again on the way out.
        /// </summary>
        internal static void RunHeadless(Action<Context> body)
        {
            Run(body, withRendering: false);
        }

        private static void RunInLevelScene(Action<Context> body)
        {
            Run(body, withRendering: true);
        }

        private static void Run(Action<Context> body, bool withRendering)
        {
            EditorSceneManager.OpenScene(ProjectBootstrapTool.LevelScenePath, OpenSceneMode.Single);

            var session = UnityEngine.Object.FindFirstObjectByType<LevelSessionController>();
            var board = UnityEngine.Object.FindFirstObjectByType<Board>();
            var boardCamera = UnityEngine.Object.FindFirstObjectByType<BoardCamera>();
            var ticker = SceneTicker.FromOpenScene();

            if (session == null || board == null || ticker == null)
            {
                Debug.LogError("[LevelPreview] LevelScene is missing a gameplay object.");
                return;
            }

            var context = new Context
            {
                Session = session,
                Board = board,
                Ticker = ticker,
                Capture = _ => { }
            };

            RenderTexture renderTexture = null;
            RenderTexture previousTarget = null;
            Camera camera = null;

            if (withRendering && boardCamera != null)
            {
                camera = boardCamera.GetComponent<Camera>();
                renderTexture = new RenderTexture(CaptureWidth, CaptureHeight, 24);
                previousTarget = camera.targetTexture;
                camera.targetTexture = renderTexture;

                // Assigning a render texture changes the camera's aspect, so resize after doing so.
                boardCamera.Apply();

                var target = renderTexture;
                var cam = camera;
                context.Capture = path => File.WriteAllBytes(path, Capture(cam, target));
            }

            try
            {
                body(context);
            }
            finally
            {
                if (camera != null)
                {
                    camera.targetTexture = previousTarget;
                }

                if (renderTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }

                // Leave no spawned items behind: if the scene is saved later they would be baked in.
                BoardBuilder.ClearItems(board);
            }
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
            UnityEngine.Object.DestroyImmediate(image);
            return png;
        }
    }
}
