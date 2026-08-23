using UnityEditor;
using UnityEngine;

namespace Blast.EditorTools
{
    /// <summary>
    /// Builds the one-shot particle bursts played when an item is cleared.
    ///
    /// Generated in code so every effect shares the same tuning and so the whole set can be
    /// regenerated after an art change. Each burst is a handful of debris chunks thrown outwards and
    /// pulled down by gravity, shrinking and fading as they go.
    /// </summary>
    public static class EffectPrefabFactory
    {
        private const string EffectFolder = "Assets/Prefabs/Effects";
        private const string MaterialFolder = "Assets/Prefabs/Effects/Materials";

        /// <summary>Well above the highest row's order so debris always draws over the board.</summary>
        private const int EffectSortingOrder = 100;

        /// <summary>
        /// Creates (or refreshes) a debris burst prefab that renders <paramref name="texturePath"/>.
        /// </summary>
        public static ParticleSystem CreateDebrisBurst(string prefabName, string texturePath, int particleCount)
        {
            var material = CreateMaterial(prefabName, texturePath);
            if (material == null)
            {
                return null;
            }

            var root = new GameObject(prefabName);
            var particles = root.AddComponent<ParticleSystem>();

            Configure(particles, particleCount);

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = EffectSortingOrder;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{EffectFolder}/{prefabName}.prefab");
            Object.DestroyImmediate(root);

            return prefab != null ? prefab.GetComponent<ParticleSystem>() : null;
        }

        private static void Configure(ParticleSystem particles, int particleCount)
        {
            // Module structs are views onto the system, so mutating a local copy is the intended API.
            var main = particles.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 4f;
            main.maxParticles = particleCount * 2;

            // World space matters because instances are pooled and repositioned: local space would
            // drag live particles along when the instance is reused somewhere else.
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)particleCount) });

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.22f;
            shape.radiusThickness = 1f;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.25f));

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(FadeOutGradient());

            var rotationOverLifetime = particles.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        }

        /// <summary>Opaque until most of the way through, then a quick fade.</summary>
        private static Gradient FadeOutGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });

            return gradient;
        }

        private static Material CreateMaterial(string prefabName, string texturePath)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                Debug.LogError($"[EffectPrefabFactory] Missing particle texture: {texturePath}");
                return null;
            }

            var materialPath = $"{MaterialFolder}/{prefabName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (material == null)
            {
                // Sprites/Default is alpha-blended and multiplies by the particle colour, which is
                // what the size/alpha-over-lifetime curves need.
                material = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.mainTexture = texture;
            EditorUtility.SetDirty(material);

            return material;
        }
    }
}
