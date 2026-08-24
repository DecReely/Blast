namespace Blast.Gameplay
{
    /// <summary>
    /// A piece of board activity that plays out over several frames, such as a rocket half sweeping
    /// across the grid and damaging cells as it goes.
    ///
    /// Actions exist because some effects are inherently temporal: the case study requires a rocket
    /// to damage cells "one by one as they pass over them", which cannot be expressed as an instant
    /// set of cells. A turn is not over until every action has finished, and an action may spawn
    /// further actions — that is how a chain reaction propagates.
    /// </summary>
    public interface IBoardAction
    {
        /// <summary>Advances the action. Returns false once it is finished.</summary>
        bool Tick(float deltaTime);
    }
}
