using System.IO;
using Blast.Gameplay;
using Blast.Items;
using Blast.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Blast.EditorTools
{
    /// <summary>
    /// Renders a chalice box at every count it can hold, from full to empty.
    ///
    /// The shelf layout is computed rather than authored, so the only way to know all eleven states
    /// look deliberate is to look at all eleven. Playing to them by hand would mean setting up a
    /// board and landing the right damage ten times over.
    ///
    /// Framed tightly on the box rather than reusing the level preview's whole-board shot, because
    /// the detail worth reviewing — spacing, the larger chalice in the middle, whether anything
    /// clips the shelf — is a few pixels across at board scale.
    /// </summary>
    public static class ChaliceBoxPreviewTool
    {
        private const int CaptureSize = 512;

        /// <summary>Cells framed by the shot. The box is two, so this leaves a small margin.</summary>
        private const float FramedCells = 2.6f;

        [MenuItem("Dream Games/Debug/Capture Chalice Box States")]
        public static void CaptureStates()
        {
            var catalog = BoardScratchpad.LoadCatalog();
            if (catalog == null)
            {
                return;
            }

            var outputFolder = Path.Combine(Path.GetTempPath(), "blast_previews", "chalice_box");
            Directory.CreateDirectory(outputFolder);

            EditorSceneManager.OpenScene(ProjectPaths.LevelScenePath, OpenSceneMode.Single);

            var session = Object.FindFirstObjectByType<LevelSessionController>();
            var board = Object.FindFirstObjectByType<Board>();
            var camera = Object.FindFirstObjectByType<BoardCamera>()?.GetComponent<Camera>();

            if (session == null || board == null || camera == null)
            {
                Debug.LogError("[ChaliceBoxPreview] LevelScene is missing a gameplay object.");
                return;
            }

            var renderTexture = new RenderTexture(CaptureSize, CaptureSize, 24);
            var previousTarget = camera.targetTexture;
            var previousSize = camera.orthographicSize;
            var previousPosition = camera.transform.position;

            camera.targetTexture = renderTexture;

            try
            {
                session.LoadLevel(1);
                Capture(catalog, board, camera, renderTexture, outputFolder);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.orthographicSize = previousSize;
                camera.transform.position = previousPosition;

                Object.DestroyImmediate(renderTexture);

                // Leave nothing behind: spawned items would be baked in if the scene is saved later.
                BoardBuilder.ClearItems(board);
            }

            Debug.Log($"[ChaliceBoxPreview] States written to {outputFolder}");
        }

        private static void Capture(
            ItemCatalog catalog,
            Board board,
            Camera camera,
            RenderTexture target,
            string outputFolder)
        {
            BoardScratchpad.Clear(board);

            var origin = new Vector2Int(0, 0);
            var box = (ChaliceBox)BoardScratchpad.Place(
                board, catalog, LevelCodes.ChaliceBoxBottomLeft, origin);

            if (box == null)
            {
                return;
            }

            FrameOn(camera, box.transform.position);

            // The doors hide the shelves, so the first shot is the phase the player starts on.
            File.WriteAllBytes(Path.Combine(outputFolder, "doors.png"), Render(camera, target));

            // One explosion opens the doors without taking a chalice.
            box.ApplyDamage(DamageInfo.FromExplosion());

            for (var remaining = box.ChalicesRemaining; remaining >= 0; remaining--)
            {
                File.WriteAllBytes(
                    Path.Combine(outputFolder, $"chalices_{remaining:00}.png"),
                    Render(camera, target));

                if (remaining == 0)
                {
                    break;
                }

                // A fresh blast each time, so the box does not treat it as a repeat of one source.
                box.ApplyDamage(DamageInfo.FromBlast());
            }
        }

        /// <summary>Centres the camera on the box and zooms in until it fills the frame.</summary>
        private static void FrameOn(Camera camera, Vector3 center)
        {
            camera.transform.position = new Vector3(center.x, center.y, camera.transform.position.z);
            camera.orthographicSize = FramedCells * 0.5f;
        }

        private static byte[] Render(Camera camera, RenderTexture target)
        {
            camera.Render();

            var active = RenderTexture.active;
            RenderTexture.active = target;

            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            image.Apply();

            RenderTexture.active = active;

            var png = image.EncodeToPNG();
            Object.DestroyImmediate(image);
            return png;
        }
    }
}
