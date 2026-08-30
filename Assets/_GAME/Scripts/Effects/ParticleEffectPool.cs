using System.Collections.Generic;
using UnityEngine;

namespace Blast.Effects
{
    /// <summary>
    /// Plays one-shot particle effects, reusing instances per prefab.
    ///
    /// A single blast can destroy a dozen items at once and a combo far more, so instantiating and
    /// destroying a <see cref="ParticleSystem"/> per item would churn badly. Instances are kept
    /// parented here and handed out again once their particles have died.
    /// </summary>
    public sealed class ParticleEffectPool : MonoBehaviour
    {
        private readonly Dictionary<ParticleSystem, List<ParticleSystem>> _poolsByPrefab = new();

        /// <summary>Plays <paramref name="prefab"/> at a world position. Null prefabs are ignored.</summary>
        public void Play(ParticleSystem prefab, Vector3 position)
        {
            if (prefab == null)
            {
                return;
            }

            var instance = Rent(prefab);
            instance.transform.position = position;

            // Clear first so a recycled instance never shows leftover particles from its last use.
            instance.Clear(true);
            instance.Play(true);
        }

        private ParticleSystem Rent(ParticleSystem prefab)
        {
            if (!_poolsByPrefab.TryGetValue(prefab, out var pool))
            {
                pool = new List<ParticleSystem>();
                _poolsByPrefab.Add(prefab, pool);
            }

            foreach (var candidate in pool)
            {
                if (candidate != null && !candidate.IsAlive(true))
                {
                    return candidate;
                }
            }

            var created = Instantiate(prefab, transform);
            created.gameObject.name = prefab.name;
            pool.Add(created);
            return created;
        }
    }
}
