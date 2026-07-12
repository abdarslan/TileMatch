using System.Collections.Generic;
using TileMatch.Model;
using TileMatch.Service;
using TileMatch.Signal;
using UnityEngine;

namespace TileMatch.Controller
{
    /// <summary>
    /// Owns the runtime board dictionary. Validates whether a tapped tile is
    /// reachable (unblocked), removes it, propagates the unblocking cascade to
    /// child tiles, then passes the tile to <see cref="OrderController"/>.
    /// </summary>
    public class LevelController
    {
        private readonly RuntimeGameState _state;
        private readonly OrderController  _orderController;
        private readonly SignalBus        _signalBus;

        private bool _active;

        public LevelController(RuntimeGameState state, OrderController orderController, SignalBus signalBus)
        {
            _state           = state;
            _orderController = orderController;
            _signalBus       = signalBus;

            _signalBus.Subscribe<TileTapIntentSignal>(OnTileTapIntent);
            _signalBus.Subscribe<RackFullSignal>(OnGameEnded);
            _signalBus.Subscribe<LevelCompletedSignal>(OnGameEnded);

            _active = true;
        }

        public void Dispose()
        {
            Deactivate();
        }

        // ─────────────────────────────────────────────────────────────────────
        private void OnTileTapIntent(TileTapIntentSignal signal)
        {
            if (!_active) return;

            if (!_state.RuntimeTiles.TryGetValue(signal.TileID, out TileSaveData tile))
            {
                Debug.Log($"[LevelController] TileID {signal.TileID} not found on board — ignoring.");
                return;
            }

            if (tile.blockingTileIDs.Count > 0)
            {
                Debug.Log($"[LevelController] TileID {signal.TileID} is blocked by {tile.blockingTileIDs.Count} tile(s) — ignoring.");
                return;
            }

            _state.RuntimeTiles.Remove(tile.tileID);
            UnblockDependents(tile.tileID);

            Debug.Log($"[LevelController] TileID {tile.tileID} (type {tile.typeID}) removed from board. Remaining: {_state.RuntimeTiles.Count}.");

            _orderController.RouteTile(tile);
        }

        /// <summary>
        /// Removes <paramref name="removedTileID"/> from every remaining tile's
        /// blockingTileIDs list. Runs in O(N) over the remaining board tiles.
        /// </summary>
        private void UnblockDependents(int removedTileID)
        {
            foreach (TileSaveData tile in _state.RuntimeTiles.Values)
                tile.blockingTileIDs.Remove(removedTileID);
        }

        private void OnGameEnded(RackFullSignal signal)      => Deactivate();
        private void OnGameEnded(LevelCompletedSignal signal) => Deactivate();

        private void Deactivate()
        {
            if (!_active) return;
            _active = false;

            _signalBus.Unsubscribe<TileTapIntentSignal>(OnTileTapIntent);
            _signalBus.Unsubscribe<RackFullSignal>(OnGameEnded);
            _signalBus.Unsubscribe<LevelCompletedSignal>(OnGameEnded);

            Debug.Log("[LevelController] Deactivated — no longer processing tile taps.");
        }
    }
}
