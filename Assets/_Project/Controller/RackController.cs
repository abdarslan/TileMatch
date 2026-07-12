using TileMatch.Model;
using TileMatch.Service;
using TileMatch.Signal;
using UnityEngine;

namespace TileMatch.Controller
{
    /// <summary>
    /// Subscribes to <see cref="TileUnmatchedSignal"/>. Owns the 6-slot rack
    /// array. Allocates tiles that matched no active order, fires
    /// <see cref="TileRoutedToRackSignal"/> so the VisualView can animate the
    /// tile into its slot, and fires <see cref="RackFullSignal"/> immediately
    /// when all 6 slots are occupied. Has no knowledge of any other controller.
    /// </summary>
    public class RackController
    {
        private readonly RuntimeGameState _state;
        private readonly SignalBus        _signalBus;

        public RackController(RuntimeGameState state, SignalBus signalBus)
        {
            _state     = state;
            _signalBus = signalBus;

            _signalBus.Subscribe<TileUnmatchedSignal>(OnTileUnmatched);
        }

        public void Dispose()
        {
            _signalBus.Unsubscribe<TileUnmatchedSignal>(OnTileUnmatched);
        }

        // ─────────────────────────────────────────────────────────────────────
        private void OnTileUnmatched(TileUnmatchedSignal signal)
        {
            TileSaveData tile = signal.Tile;
            int slot = FindFreeSlot();

            if (slot < 0)
            {
                Debug.Log("[RackController] No free slot — overflow guard triggered. Firing RackFullSignal.");
                _signalBus.Fire(new RackFullSignal());
                return;
            }

            _state.Rack[slot] = tile.typeID;
            _signalBus.Fire(new TileRoutedToRackSignal { Tile = tile, TargetRackIndex = slot });

            int occupied = CountOccupied();
            Debug.Log($"[RackController] TileID {tile.tileID} (type {tile.typeID}) → rack[{slot}]. Occupied: {occupied}/{RuntimeGameState.RackCapacity}.");

            if (occupied >= RuntimeGameState.RackCapacity)
            {
                Debug.Log("[RackController] Rack full — firing RackFullSignal.");
                _signalBus.Fire(new RackFullSignal());
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        private int FindFreeSlot()
        {
            for (int i = 0; i < _state.Rack.Length; i++)
                if (_state.Rack[i] == 0) return i;

            return -1;
        }

        private int CountOccupied()
        {
            int count = 0;
            for (int i = 0; i < _state.Rack.Length; i++)
                if (_state.Rack[i] != 0) count++;

            return count;
        }
    }
}
