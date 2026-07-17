using TileMatch.Model;

namespace TileMatch.Signal
{
    /// <summary>
    /// Fired by <see cref="Controller.GameplayController"/> when a level begins.
    /// Consumed by all downstream Controllers and Views to reset their local state
    /// and begin gameplay.
    /// </summary>
    public struct LevelStartedSignal
    {
        public LevelData Level;
    }
}
