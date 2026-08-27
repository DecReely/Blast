using Blast.Gameplay;
using UnityEngine;

namespace Blast.Items
{
    /// <summary>
    /// Base class for rockets and TNTs.
    ///
    /// Special items are created by blasting a group of four or more cubes, can be triggered by a
    /// tap or by another explosion, and combine into a single combo when tapped while adjacent to
    /// other special items. The explosion patterns themselves live outside the item so that a combo
    /// can replace them wholesale — see the explosion pattern strategies.
    /// </summary>
    public abstract class SpecialItem : GridItem
    {
        /// <summary>Special items obey gravity just like cubes.</summary>
        public override bool CanFall => true;

        /// <summary>
        /// The explosion this item produces on its own.
        ///
        /// Returned as a pattern rather than executed here so that a combo can discard it entirely
        /// and fire one shared pattern instead — which is how "individual explosions of special items
        /// are ignored" is satisfied.
        /// </summary>
        public abstract IExplosionPattern CreateExplosionPattern();

        /// <summary>
        /// Never reached in practice: the explosion system detects a special in a damaged cell and
        /// detonates it instead of damaging it, which is how a chain reaction propagates. Reported
        /// as ignored so that a caller which bypassed that path cannot silently delete a special
        /// without triggering it, and warned about because reaching here means the chain would not
        /// have propagated.
        /// </summary>
        public override DamageResult ApplyDamage(DamageInfo damage)
        {
            Debug.LogWarning(
                $"[{name}] A special item was damaged directly instead of being detonated. " +
                "Route damage through ExplosionSystem so the chain reaction propagates.",
                this);

            return DamageResult.Ignored;
        }
    }
}
