namespace TileMatch.Signal
{
    /// <summary>
    /// Fired by <see cref="Controller.LevelController"/> when a tile is no longer blocked by any tiles above it.
    /// Consumed by <see cref="View.VisualView"/> to remove the darkened shade from the newly accessible tile.
    /// </summary>
    public struct TileUnblockedSignal
    {
        public int TileID;
    }
}
