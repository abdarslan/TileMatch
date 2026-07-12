using TileMatch.Model;
using TileMatch.Service;
using TileMatch.Signal;
using UnityEngine;

namespace TileMatch.Controller
{
    /// <summary>
    /// Owns the 6-slot rack array. Allocates tiles that matched no active order.
    /// Fires <see cref="TileRoutedToRackSignal"/> on every allocation so the
    /// VisualView can animate the tile into its slot.
    /// Fires <see cref="RackFullSignal"/> immediately when all 6 slots are occupied.
    /// </summary>
    public class RackController
    {
        private readonly RuntimeGameState _state;
        private readonly SignalBus        _signalBus;

        public RackController(RuntimeGameState state, SignalBus signalBus)
        {
            _state     = state;
            _signalBus = signalBus;
        }

        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Allocates <paramref name="tile"/> to the first free rack slot.
        /// If no slot is free (shouldn't occur under normal flow) the rack-full
        /// guard fires <see cref="RackFullSignal"/> defensively.
        /// </summary>
        public void AllocateTile(TileSaveData tile)
        {
            int slot = FindFreeSlot();

            if (slot < 0)
            {
                Debug.Log("[RackController] No free slot — rack overflow guard triggered. Firing RackFullSignal.");
                _signalBus.Fire(new RackFullSignal());
                return;
            }

            _state.Rack[slot] = tile.typeID;
            _signalBus.Fire(new TileRoutedToRackSignal { Tile = tile, TargetRackIndex = slot });

            int occupied = CountOccupied();
            Debug.Log($"[RackController] TileID {tile.tileID} (type {tile.typeID}) → rack[{slot}]. Occupied: {occupied}/{RuntimeGameState.RackCapacity}.");

            if (occupied >= RuntimeGameState.RackCapacity)
            {
                Debug.Log("[RackController] Rack is full. Firing RackFullSignal.");
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
