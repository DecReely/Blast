using System.IO;
using Blast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Blast.EditorTools
{
    /// <summary>
    /// Renders every level offscreen and writes it to a PNG.
    ///
    /// A development aid: it makes board layout, cell alignment, sprite sorting and frame padding
    /// reviewable at a glance for all ten levels at once, without having to enter play mode and
    /// step through them by hand.
    /// </summary>
    public static class LevelPreviewTool
    {
        /// <summary>Portrait 9:16, matching the target aspect ratio.</summary>
        private const int CaptureWidth = 540;

        private const int CaptureHeight = 960;

        [MenuItem("Dream Games/Debug/Capture Level Previews")]
        public static void CaptureAll()
        {
            var outputFolder = Path.Combine(Path.GetTempPath(), "blast_previews");
            Directory.CreateDirectory(outputFolder);

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
            var previous = camera.targetTexture;
            camera.targetTexture = renderTexture;

            // Assigning the render texture changes the camera's aspect, so resize after doing so.
            boardCamera.Apply();

            try
            {
                for (var levelNumber = 1; levelNumber <= 10; levelNumber++)
                {
                    session.LoadLevel(levelNumber);

                    var level = session.CurrentLevel;
                    if (level == null)
                    {
                        continue;
                    }

                    var path = Path.Combine(outputFolder, $"level_{levelNumber:00}_{level.Width}x{level.Height}.png");
                    File.WriteAllBytes(path, Capture(camera, renderTexture));
                }
            }
            finally
            {
                camera.targetTexture = previous;
                Object.DestroyImmediate(renderTexture);
            }

            Debug.Log($"[LevelPreview] Previews written to {outputFolder}");
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
