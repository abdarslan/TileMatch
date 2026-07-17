namespace TileMatch.Signal
{
    /// <summary>
    /// Fired by <see cref="View.VisualView"/> when the physical board win/fail animation sequence fully completes.
    /// Consumed by <see cref="View.UI.ResultScreenView"/> to safely display the UI without covering the animation.
    /// </summary>
    public struct VisualFeedbackFinishedSignal
    {
        public bool IsWin;
    }
}
