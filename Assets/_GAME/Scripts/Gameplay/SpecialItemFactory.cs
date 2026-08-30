using Blast.Items;
using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Spawns the special item a blast has earned, at the cell the player tapped.
    ///
    /// Goes through the same item catalog as level loading, so a rocket created by play and a rocket
    /// authored into a level file are the identical prefab and behave identically.
    /// </summary>
    public sealed class SpecialItemFactory
    {
        private readonly ItemCatalog _catalog;

        public SpecialItemFactory(ItemCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>
        /// Creates and places the special for a group of <paramref name="groupSize"/> cubes, or
        /// returns null if that size does not earn one. The cell must already be empty.
        /// </summary>
        public SpecialItem Create(int groupSize, Board board, Vector2Int cell)
        {
            var code = SpecialItemRules.SpecialCodeFor(groupSize);
            if (code == null)
            {
                return null;
            }

            if (_catalog.GetPrefab(code) is not SpecialItem prefab)
            {
                Debug.LogError($"[SpecialItemFactory] Catalog has no special item registered for '{code}'.");
                return null;
            }

            var instance = Object.Instantiate(prefab, board.ItemsRoot);
            instance.name = $"{prefab.name}_{cell.x}_{cell.y}";

            board.Place(instance, cell);
            return instance;
        }
    }
}
