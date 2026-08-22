using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Blast.EditorTools
{
    /// <summary>
    /// Applies this project's sprite import conventions to everything under <c>Assets/Art</c>.
    ///
    /// The supplied art is authored on a 140 px grid: a one-cell item occupies the bottom
    /// 140x140 px of its canvas and any extra height is the 3D top bevel that is meant to
    /// overhang into the cell above (a cube is 142x162, a 2x2 chalice box is 300x300 = 280 px
    /// of footprint plus the same 20 px bevel).
    ///
    /// Importing every board sprite at 140 pixels-per-unit therefore makes one grid cell exactly
    /// one world unit, and giving each sprite a pivot on the centre of its <em>footprint</em>
    /// (rather than the centre of its canvas) means an item can always be positioned with a plain
    /// <c>Grid.GetCellCenterWorld</c> call with no per-prefab offsets to maintain.
    /// </summary>
    public static class ArtImportSettingsTool
    {
        /// <summary>Pixel size of a single grid cell in the supplied art.</summary>
        public const int CellPixelSize = 140;

        private const string ArtRoot = "Assets/Art";

        private enum PivotRule
        {
            /// <summary>Canvas centre. Used for particles and Canvas-space UI art.</summary>
            CanvasCenter,

            /// <summary>
            /// Centre of the item's cell footprint, which is bottom-aligned on the canvas.
            /// Lets the bevel overhang upwards while the pivot still lands on the cell centre.
            /// </summary>
            FootprintCenter
        }

        private sealed class Convention
        {
            public string PathContains;
            public int PixelsPerUnit = CellPixelSize;
            public PivotRule Pivot = PivotRule.CanvasCenter;

            /// <summary>Height of the item's footprint in cells. Only used by <see cref="PivotRule.FootprintCenter"/>.</summary>
            public int FootprintCellsY = 1;

            /// <summary>Non-zero enables 9-slicing (left, bottom, right, top in pixels).</summary>
            public Vector4 Border = Vector4.zero;
        }

        /// <summary>
        /// Ordered longest-prefix-first: the first entry whose path fragment matches wins, so
        /// nested "Particles" folders must be listed before their parent category.
        /// </summary>
        private static readonly Convention[] Conventions =
        {
            // The board frame is a rounded rect stretched around the whole grid, so it is 9-sliced.
            new Convention
            {
                PathContains = "Art/UI/Gameplay/grid_background",
                Border = new Vector4(24, 24, 24, 24)
            },

            // Particle textures are driven by ParticleSystem modules, so they stay canvas-centred.
            new Convention { PathContains = "/Particles/" },

            // The chalice box is the only multi-cell item: a 2x2 footprint.
            new Convention
            {
                PathContains = "Art/Obstacles/ChaliceBox/ChaliceBoxBg",
                Pivot = PivotRule.FootprintCenter,
                FootprintCellsY = 2
            },
            new Convention
            {
                PathContains = "Art/Obstacles/ChaliceBox/ChaliceBoxDoors",
                Pivot = PivotRule.FootprintCenter,
                FootprintCellsY = 2
            },

            // A collected chalice flies to the goal UI, so it is never cell-aligned.
            new Convention { PathContains = "Art/Obstacles/ChaliceBox/Chalice" },

            // Single-cell board items. Square art (rockets, TNT) resolves to a centred pivot
            // through the same rule, so it needs no special case.
            new Convention { PathContains = "Art/Cubes/", Pivot = PivotRule.FootprintCenter },
            new Convention { PathContains = "Art/Obstacles/", Pivot = PivotRule.FootprintCenter },
            new Convention { PathContains = "Art/SpecialItems/", Pivot = PivotRule.FootprintCenter },

            // Canvas-space art is sized by its RectTransform, so pixels-per-unit is irrelevant.
            new Convention { PathContains = "Art/UI/", PixelsPerUnit = 100 },
            new Convention { PathContains = "Art/Menu/", PixelsPerUnit = 100 }
        };

        [MenuItem("Dream Games/Setup/Apply Art Import Settings")]
        public static void Apply()
        {
            var log = new StringBuilder("Art import settings\n");
            var changed = new List<string>();

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot });

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    {
                        continue;
                    }

                    var convention = ResolveConvention(path);
                    if (ApplyTo(importer, path, convention, log))
                    {
                        changed.Add(path);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            foreach (var path in changed)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            log.Insert(0, $"Inspected {guids.Length} textures, reimported {changed.Count}.\n");
            Debug.Log(log.ToString());
        }

        private static Convention ResolveConvention(string assetPath)
        {
            foreach (var convention in Conventions)
            {
                if (assetPath.Contains(convention.PathContains))
                {
                    return convention;
                }
            }

            return new Convention { Pivot = PivotRule.CanvasCenter };
        }

        private static bool ApplyTo(TextureImporter importer, string assetPath, Convention convention, StringBuilder log)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            var pivot = new Vector2(0.5f, 0.5f);
            var alignment = SpriteAlignment.Center;

            if (convention.Pivot == PivotRule.FootprintCenter)
            {
                if (!TryReadPngSize(assetPath, out _, out var pixelHeight))
                {
                    log.AppendLine($"  ! could not read PNG header, left as-is: {assetPath}");
                    return false;
                }

                // The footprint is bottom-aligned on the canvas, so the centre of the cell block
                // sits half a footprint above the bottom edge of the sprite.
                var footprintPixels = convention.FootprintCellsY * CellPixelSize;
                pivot = new Vector2(0.5f, footprintPixels * 0.5f / pixelHeight);
                alignment = SpriteAlignment.Custom;
            }

            var before = Describe(settings, importer);

            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteAlignment = (int)alignment;
            settings.spritePivot = pivot;
            settings.spriteBorder = convention.Border;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.mipmapEnabled = false;
            settings.alphaIsTransparency = true;
            settings.filterMode = FilterMode.Bilinear;
            settings.wrapMode = TextureWrapMode.Clamp;
            importer.SetTextureSettings(settings);

            importer.spritePixelsPerUnit = convention.PixelsPerUnit;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 4096;

            var after = Describe(settings, importer);
            if (before == after)
            {
                return false;
            }

            log.AppendLine($"  {assetPath}\n      {after}");
            return true;
        }

        private static string Describe(TextureImporterSettings settings, TextureImporter importer)
        {
            return $"ppu={importer.spritePixelsPerUnit} pivot={settings.spritePivot} " +
                   $"align={settings.spriteAlignment} border={settings.spriteBorder} " +
                   $"mesh={settings.spriteMeshType} mips={settings.mipmapEnabled} " +
                   $"compression={importer.textureCompression} max={importer.maxTextureSize}";
        }

        /// <summary>
        /// Reads width/height straight out of the PNG IHDR chunk.
        /// Deliberately avoids <c>Texture2D.width</c>, which reports the <em>imported</em> size and
        /// would feed a downscaled height back into the pivot maths.
        /// </summary>
        private static bool TryReadPngSize(string assetPath, out int width, out int height)
        {
            width = 0;
            height = 0;

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!File.Exists(fullPath) || Path.GetExtension(fullPath).ToLowerInvariant() != ".png")
            {
                return false;
            }

            var header = new byte[24];
            using (var stream = File.OpenRead(fullPath))
            {
                if (stream.Read(header, 0, header.Length) != header.Length)
                {
                    return false;
                }
            }

            // 8 byte signature, then a chunk length, then "IHDR", then big-endian width/height.
            if (header[12] != 'I' || header[13] != 'H' || header[14] != 'D' || header[15] != 'R')
            {
                return false;
            }

            width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            return width > 0 && height > 0;
        }
    }
}
