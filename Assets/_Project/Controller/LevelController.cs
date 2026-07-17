using TileMatch.Model;
using TileMatch.Service;
using TileMatch.Signal;
using UnityEngine;

namespace TileMatch.Controller
{
    /// <summary>
    /// Owns the runtime board dictionary. Validates whether a tapped tile is
    /// reachable (exists and unblocked), removes it, propagates the unblocking
    /// cascade, then fires <see cref="TileValidatedSignal"/> for downstream
    /// controllers to consume. Has no knowledge of orders or the rack.
    /// </summary>
    public class LevelController
    {
        private readonly RuntimeGameState _state;
        private readonly SignalBus        _signalBus;

        private bool _active;

        public LevelController(RuntimeGameState state, SignalBus signalBus)
        {
            _state     = state;
            _signalBus = signalBus;

            _signalBus.Subscribe<LevelStartedSignal>(OnLevelStarted);
        }

        public void Dispose()
        {
            _signalBus.Unsubscribe<LevelStartedSignal>(OnLevelStarted);
            Deactivate();
        }

        private void OnLevelStarted(LevelStartedSignal signal) => Activate();

        private void Activate()
        {
            if (_active) return;
            _active = true;

            _signalBus.Subscribe<TileTapIntentSignal>(OnTileTapIntent);
            _signalBus.Subscribe<RackFullSignal>(OnGameEnded);
            _signalBus.Subscribe<LevelCompletedSignal>(OnGameEnded);
#if UNITY_EDITOR
            Debug.Log("[LevelController] Activated.");
#endif
        }

        // ─────────────────────────────────────────────────────────────────────
        private void OnTileTapIntent(TileTapIntentSignal signal)
        {
            if (!_active) return;

            if (!_state.RuntimeTiles.TryGetValue(signal.TileID, out TileData tile))
            {
#if UNITY_EDITOR
                Debug.Log($"[LevelController] TileID {signal.TileID} not found on board — ignoring.");
#endif
                return;
            }

            if (tile.blockingTileIDs.Count > 0)
            {
#if UNITY_EDITOR
                Debug.Log($"[LevelController] TileID {signal.TileID} blocked by {tile.blockingTileIDs.Count} tile(s) — ignoring.");
#endif
                return;
            }

            _state.RuntimeTiles.Remove(tile.tileID);
            UnblockDependents(tile.tileID);
#if UNITY_EDITOR
            Debug.Log($"[LevelController] TileID {tile.tileID} (type {tile.typeID}) removed. Remaining: {_state.RuntimeTiles.Count}.");
#endif

            _signalBus.Fire(new TileValidatedSignal { Tile = tile });
        }

        private void UnblockDependents(int removedTileID)
        {
            foreach (TileData tile in _state.RuntimeTiles.Values)
            {
                if (tile.blockingTileIDs.Remove(removedTileID))
                {
                    if (tile.blockingTileIDs.Count == 0)
                    {
                        _signalBus.Fire(new TileUnblockedSignal { TileID = tile.tileID });
                    }
                }
            }
        }

        private void OnGameEnded(RackFullSignal signal)       => Deactivate();
        private void OnGameEnded(LevelCompletedSignal signal) => Deactivate();

        private void Deactivate()
        {
            if (!_active) return;
            _active = false;

            _signalBus.Unsubscribe<TileTapIntentSignal>(OnTileTapIntent);
            _signalBus.Unsubscribe<RackFullSignal>(OnGameEnded);
            _signalBus.Unsubscribe<LevelCompletedSignal>(OnGameEnded);
#if UNITY_EDITOR
            Debug.Log("[LevelController] Deactivated.");
#endif
        }
    }
}
