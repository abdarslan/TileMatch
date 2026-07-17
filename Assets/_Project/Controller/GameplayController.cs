using System.Collections.Generic;
using TileMatch.Model;
using TileMatch.Service;
using TileMatch.Signal;
using TileMatch.Signal.UI;
using UnityEngine;

namespace TileMatch.Controller
{
    /// <summary>
    /// Manages the high-level game-state machine (Menu → Playing → Won | Failed).
    /// Owns level initialisation: deep-copies <see cref="LevelData"/> into
    /// <see cref="RuntimeGameState"/> so the ScriptableObject asset is never
    /// mutated at runtime. Promotes the first active orders on start.
    /// Handles sequential progression through the level list.
    /// </summary>
    public class GameplayController
    {
        public enum GameState { Menu, Playing, Won, Failed }

        public GameState CurrentState { get; private set; } = GameState.Menu;

        private readonly RuntimeGameState _state;
        private readonly SignalBus        _signalBus;
        
        private LevelData[] _sequence;
        private int _currentLevelIndex = 0;
        private System.Threading.CancellationTokenSource _cts = new System.Threading.CancellationTokenSource();

        public GameplayController(RuntimeGameState state, SignalBus signalBus)
        {
            _state     = state;
            _signalBus = signalBus;

            _signalBus.Subscribe<RackFullSignal>(OnRackFull);
            _signalBus.Subscribe<LevelCompletedSignal>(OnLevelCompleted);

            // UI Intent subscriptions
            _signalBus.Subscribe<StartGameRequestSignal>(OnStartGameRequest);
            _signalBus.Subscribe<ReturnToMenuRequestSignal>(OnReturnToMenuRequest);
            _signalBus.Subscribe<RestartLevelRequestSignal>(OnRestartLevelRequest);
            _signalBus.Subscribe<NextLevelRequestSignal>(OnNextLevelRequest);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            
            _signalBus.Unsubscribe<RackFullSignal>(OnRackFull);
            _signalBus.Unsubscribe<LevelCompletedSignal>(OnLevelCompleted);

            _signalBus.Unsubscribe<StartGameRequestSignal>(OnStartGameRequest);
            _signalBus.Unsubscribe<ReturnToMenuRequestSignal>(OnReturnToMenuRequest);
            _signalBus.Unsubscribe<RestartLevelRequestSignal>(OnRestartLevelRequest);
            _signalBus.Unsubscribe<NextLevelRequestSignal>(OnNextLevelRequest);
        }

        // ─────────────────────────────────────────────────────────────────────
        public void InitSequence(LevelData[] sequence)
        {
            _sequence = sequence;
            _currentLevelIndex = 0;
            
            SetState(GameState.Menu);
        }

        private void SetState(GameState newState)
        {
            CurrentState = newState;
            _signalBus.Fire(new GameStateChangedSignal { NewState = CurrentState });
        }

        // ─────────────────────────────────────────────────────────────────────
        private void OnStartGameRequest(StartGameRequestSignal signal)
        {
            if (_sequence != null && _sequence.Length > 0)
            {
                if (_currentLevelIndex >= _sequence.Length) _currentLevelIndex = 0;
                StartLevel(_sequence[_currentLevelIndex]);
            }
        }

        private void OnReturnToMenuRequest(ReturnToMenuRequestSignal signal)
        {
            SetState(GameState.Menu);
        }

        private void OnRestartLevelRequest(RestartLevelRequestSignal signal)
        {
            if (_sequence != null && _sequence.Length > _currentLevelIndex)
            {
                StartLevel(_sequence[_currentLevelIndex]);
            }
        }

        private void OnNextLevelRequest(NextLevelRequestSignal signal)
        {
            if (_sequence != null && _sequence.Length > 0)
            {
                if (_currentLevelIndex >= _sequence.Length)
                {
                    Debug.Log("[GameplayController] YOU BEAT ALL LEVELS! Looping back to level 1.");
                    _currentLevelIndex = 0;
                }
                StartLevel(_sequence[_currentLevelIndex]);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Initialises runtime state from the supplied level asset and begins play.
        /// Safe to call multiple times (restarts the level cleanly).
        /// </summary>
        public void StartLevel(LevelData level)
        {
            PopulateRuntimeState(level);
            SetState(GameState.Playing);
            _signalBus.Fire(new LevelStartedSignal { Level = level });

            // Fire OrderPromotedSignal for initial active orders
            for (int i = 0; i < RuntimeGameState.MaxActiveOrders; i++)
            {
                if (_state.ActiveOrders[i] != null)
                {
                    _signalBus.Fire(new OrderPromotedSignal { Order = _state.ActiveOrders[i], TrayIndex = i });
                }
            }

            Debug.Log($"[GameplayController] Level started. " +
                      $"Tiles: {_state.RuntimeTiles.Count}, " +
                      $"Pending orders: {_state.PendingOrders.Count}.");
        }

        // ─────────────────────────────────────────────────────────────────────
        private void PopulateRuntimeState(LevelData level)
        {
            // Clear all mutable runtime collections
            _state.RuntimeTiles.Clear();
            for (int i = 0; i < RuntimeGameState.MaxActiveOrders; i++)
                _state.ActiveOrders[i] = null;
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

            // Pre-fill active orders
            for (int i = 0; i < RuntimeGameState.MaxActiveOrders; i++)
            {
                if (_state.PendingOrders.Count > 0)
                {
                    _state.ActiveOrders[i] = _state.PendingOrders.Dequeue();
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        private void OnRackFull(RackFullSignal signal)
        {
            if (CurrentState != GameState.Playing) return;

            Debug.Log("[GameplayController] Rack full — level FAILED.");
            SetState(GameState.Failed);
        }

        private void OnLevelCompleted(LevelCompletedSignal signal)
        {
            if (CurrentState != GameState.Playing) return;

            Debug.Log("[GameplayController] All orders fulfilled — level WON.");
            
            _currentLevelIndex++;
            if (_sequence != null && _currentLevelIndex >= _sequence.Length)
            {
                Debug.Log("[GameplayController] Finished final level in sequence. Looping back to level 0.");
                _currentLevelIndex = 0;
            }
            
            SetState(GameState.Won);
        }
    }
}
