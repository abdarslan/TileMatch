using TileMatch.Model;

namespace TileMatch.Signal
{
    public struct TileRoutedToTraySignal
    {
        public TileData Tile;
        public int TargetTrayIndex;
        public int TargetItemIndex;
    }
}
