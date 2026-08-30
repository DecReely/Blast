namespace Blast.EditorTools
{
    /// <summary>
    /// The handful of asset paths the editor tooling has to name by hand.
    ///
    /// Runtime code never needs these: the scenes reach their data through serialized references,
    /// which survive a rename where a string does not. Only tooling that has to open a scene or load
    /// an asset before any of that wiring exists has no choice but to spell the path out, so the
    /// spellings are collected here rather than repeated across the tools that need them.
    /// </summary>
    internal static class ProjectPaths
    {
        public const string MainScenePath = "Assets/Scenes/MainScene.unity";
        public const string LevelScenePath = "Assets/Scenes/LevelScene.unity";

        public const string ItemCatalogPath = "Assets/Data/ItemCatalog.asset";
        public const string LevelDatabasePath = "Assets/Data/LevelDatabase.asset";
    }
}
