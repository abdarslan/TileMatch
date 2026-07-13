using System.Collections.Generic;
using TileMatch.Model;
using TileMatch.Service;
using TileMatch.Signal;
using UnityEngine;

namespace TileMatch.Controller
{
    /// <summary>
    /// Manages the high-level game-state machine (Idle → Playing → Won | Failed).
    /// Owns level initialisation: deep-copies <see cref="LevelData"/> into
    /// <see cref="RuntimeGameState"/> so the ScriptableObject asset is never
    /// mutated at runtime. Promotes the first active orders on start.
    /// </summary>
    public class GameplayController
    {
        public enum GameState { Idle, Playing, Won, Failed }

        public GameState CurrentState { get; private set; } = GameState.Idle;

        private readonly RuntimeGameState _state;
        private readonly SignalBus        _signalBus;

        public GameplayController(RuntimeGameState state, SignalBus signalBus)
        {
            _state     = state;
            _signalBus = signalBus;

            _signalBus.Subscribe<RackFullSignal>(OnRackFull);
            _signalBus.Subscribe<LevelCompletedSignal>(OnLevelCompleted);
        }

        public void Dispose()
        {
            _signalBus.Unsubscribe<RackFullSignal>(OnRackFull);
            _signalBus.Unsubscribe<LevelCompletedSignal>(OnLevelCompleted);
        }

        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Initialises runtime state from the supplied level asset and begins play.
        /// Safe to call multiple times (restarts the level cleanly).
        /// </summary>
        public void StartLevel(LevelData level)
        {
            PopulateRuntimeState(level);
            CurrentState = GameState.Playing;
            _signalBus.Fire(new LevelStartedSignal { Level = level });

            // Fire OrderPromotedSignal for initial active orders
            for (int i = 0; i < _state.ActiveOrders.Count; i++)
            {
                _signalBus.Fire(new OrderPromotedSignal { Order = _state.ActiveOrders[i], TrayIndex = i });
            }

            Debug.Log($"[GameplayController] Level started. " +
                      $"Tiles: {_state.RuntimeTiles.Count}, " +
                      $"Active orders: {_state.ActiveOrders.Count}, " +
                      $"Pending orders: {_state.PendingOrders.Count}.");
        }

        // ─────────────────────────────────────────────────────────────────────
        private void PopulateRuntimeState(LevelData level)
        {
            // Clear all mutable runtime collections
            _state.RuntimeTiles.Clear();
            _state.ActiveOrders.Clear();
            _state.PendingOrders.Clear();

            for (int i = 0; i < _state.Rack.Length; i++)
                _state.Rack[i] = 0;

            // Deep-copy tiles so runtime mutations never dirty the ScriptableObject
            foreach (TileData source in level.activeTiles)
            {
                _state.RuntimeTiles[source.tileID] = new TileData
                {
                    tileID          = source.tileID,
                    typeID          = source.typeID,
                    visualPosition  = source.visualPosition,
                    blockingTileIDs = new List<int>(source.blockingTileIDs)
                };
            }

            // Deep-copy orders and queue them
            foreach (OrderData source in level.pendingOrders)
            {
                _state.PendingOrders.Enqueue(new OrderData
                {
                    requiredTypeIDs = new List<int>(source.requiredTypeIDs),
                    currentItemIndex = 0
                });
            }

            // Promote the first wave of active orders
            while (_state.ActiveOrders.Count < RuntimeGameState.MaxActiveOrders
                   && _state.PendingOrders.Count > 0)
            {
                _state.ActiveOrders.Add(_state.PendingOrders.Dequeue());
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        private void OnRackFull(RackFullSignal signal)
        {
            if (CurrentState != GameState.Playing) return;

            CurrentState = GameState.Failed;
            Debug.Log("[GameplayController] Rack full — level FAILED.");
        }

        private void OnLevelCompleted(LevelCompletedSignal signal)
        {
            if (CurrentState != GameState.Playing) return;

            CurrentState = GameState.Won;
            Debug.Log("[GameplayController] All orders fulfilled — level WON.");
        }
    }
}
