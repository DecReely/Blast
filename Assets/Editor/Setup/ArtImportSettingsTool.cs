using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Blast.EditorTools
{
    /// <summary>
    /// Applies this project's sprite import conventions to everything under <c>Assets/Art</c>.
    ///
    /// The art is authored on a <b>150 px square cell</b>, measured from two independent sources: the
    /// 2x2 chalice box is exactly 300x300 px, and the reference screenshots in the case study have an
    /// identical row and column pitch, confirming the cells really are square.
    ///
    /// Items are drawn smaller than their cell and centred in it. A cube is 140x160 px, so it leaves
    /// a 5 px gap on each side horizontally and overhangs 5 px above and below. That overhang is the
    /// block's highlight and shadow edge, and overlapping it with the neighbouring rows is what
    /// produces the shadow line between them.
    ///
    /// Importing every board sprite at 150 pixels-per-unit makes one cell exactly one world unit, and
    /// a plain centred pivot then places any item — including the 2x2 chalice box, whose centre is the
    /// point shared by its four cells — with no per-prefab offsets.
    /// </summary>
    public static class ArtImportSettingsTool
    {
        /// <summary>Pixel size of one square grid cell in the supplied art.</summary>
        public const int CellPixelSize = 150;

        /// <summary>Canvas-space art is sized by its RectTransform, so its scale is arbitrary.</summary>
        private const int UiPixelsPerUnit = 100;

        private const string ArtRoot = "Assets/Art";

        private sealed class Convention
        {
            public string PathContains;
            public int PixelsPerUnit = CellPixelSize;

            /// <summary>Non-zero enables 9-slicing (left, bottom, right, top in pixels).</summary>
            public Vector4 Border = Vector4.zero;
        }

        /// <summary>Most specific first: the first entry whose path fragment matches wins.</summary>
        private static readonly Convention[] Conventions =
        {
            // The board frame is a rounded rect stretched around the whole grid, so it is 9-sliced.
            new Convention
            {
                PathContains = "Art/UI/Gameplay/grid_background",
                Border = new Vector4(24, 24, 24, 24)
            },

            new Convention { PathContains = "Art/UI/", PixelsPerUnit = UiPixelsPerUnit },
            new Convention { PathContains = "Art/Menu/", PixelsPerUnit = UiPixelsPerUnit }
        };

        [MenuItem("Dream Games/Setup/Apply Art Import Settings")]
        public static void Apply()
        {
            var log = new StringBuilder();
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

                    if (ApplyTo(importer, ResolveConvention(path), log, path))
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

            Debug.Log($"Art import settings: inspected {guids.Length} textures, reimported {changed.Count}.\n{log}");
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

            // Everything else is board art and shares the cell-sized convention.
            return new Convention();
        }

        private static bool ApplyTo(TextureImporter importer, Convention convention, StringBuilder log, string assetPath)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            var before = Describe(settings, importer);

            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;

            // Every sprite is centred in its cell, so no custom pivots are needed anywhere.
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);

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
    }
}
