using TileMatch.Model;

namespace TileMatch.Signal.UI
{
    /// <summary>
    /// Fired by <see cref="Controller.GameplayController"/> whenever the high-level game state changes.
    /// Consumed by UI Views to toggle their visibility and <see cref="View.VisualView"/> to perform cleanup.
    /// </summary>
    public struct GameStateChangedSignal
    {
        public GameState NewState;
    }
}
