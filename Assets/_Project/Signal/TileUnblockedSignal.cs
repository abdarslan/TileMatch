namespace TileMatch.Signal
{
    /// <summary>
    /// Fired when a tile is no longer blocked by any tiles above it.
    /// VisualView listens to this to un-shade the tile.
    /// </summary>
    public struct TileUnblockedSignal
    {
        public int TileID;
    }
}
