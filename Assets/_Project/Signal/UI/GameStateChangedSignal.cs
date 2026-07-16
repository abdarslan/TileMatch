using TileMatch.Controller;

namespace TileMatch.Signal.UI
{
    public struct GameStateChangedSignal
    {
        public GameplayController.GameState NewState;
    }
}
