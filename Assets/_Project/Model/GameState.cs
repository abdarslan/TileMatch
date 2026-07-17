namespace TileMatch.Model
{
    /// <summary>
    /// Standalone enum representing the high-level game flow state.
    /// Lives in the Model layer so both the Controller and Signal layers
    /// can reference it without creating a Signal → Controller dependency.
    /// </summary>
    public enum GameState
    {
        Menu,
        Playing,
        Won,
        Failed
    }
}
