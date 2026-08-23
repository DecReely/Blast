using UnityEngine;

namespace Blast.Gameplay
{
    /// <summary>
    /// Destroys objects in a way that works both in play mode and in the editor.
    ///
    /// <see cref="Object.Destroy"/> is deferred to the end of the frame, which never arrives outside
    /// play mode — so editor tooling that rebuilds or blasts a board would silently stack duplicate
    /// objects. Centralised here so every call site gets the same behaviour.
    /// </summary>
    internal static class ObjectLifetime
    {
        public static void Destroy(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
