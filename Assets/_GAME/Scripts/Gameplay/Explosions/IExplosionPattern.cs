using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// The shape of a single explosion, centred on one cell.
    ///
    /// Kept separate from the item that produced it so a combo can substitute its own shape wholesale
    /// instead of the members exploding individually — which is exactly what the case study asks for.
    /// A lone rocket, a lone TNT and each of the three combos are all just different patterns, so
    /// adding another combo means adding a class and a selection rule and nothing else.
    /// </summary>
    public interface IExplosionPattern
    {
        /// <summary>Applies the explosion around <paramref name="center"/>.</summary>
        void Detonate(Vector2Int center, ExplosionSystem explosions);
    }
}
