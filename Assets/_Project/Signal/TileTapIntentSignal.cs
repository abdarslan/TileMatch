namespace TileMatch.Signal
{
    /// <summary>
    /// Fired by <see cref="View.InputView"/> when the user taps a tile physical collider.
    /// Consumed by <see cref="Controller.LevelController"/> to validate if the tile is actually reachable (not blocked).
    /// </summary>
    public struct TileTapIntentSignal
    {
        public int TileID;
    }
}
