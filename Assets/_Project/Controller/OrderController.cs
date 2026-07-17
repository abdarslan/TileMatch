using TileMatch.Model;
using TileMatch.Service;
using TileMatch.Signal;
using UnityEngine;

namespace TileMatch.Controller
{
    /// <summary>
    /// Subscribes to <see cref="TileValidatedSignal"/>. Evaluates whether the
    /// tile satisfies the next slot of any active order. On a match it fires
    /// <see cref="TileRoutedToTraySignal"/>, advances the order, and promotes
    /// the next pending order when one completes. On a miss it fires
    /// <see cref="TileUnmatchedSignal"/> for <see cref="RackController"/>.
    /// Fires <see cref="LevelCompletedSignal"/> when all orders are fulfilled.
    /// Has no knowledge of LevelController or RackController.
    /// </summary>
    public class OrderController
    {
        private readonly RuntimeGameState _state;
        private readonly SignalBus        _signalBus;
        private bool _levelCompleteFired;

        public OrderController(RuntimeGameState state, SignalBus signalBus)
        {
            _state     = state;
            _signalBus = signalBus;

            _signalBus.Subscribe<TileValidatedSignal>(OnTileValidated);
            _signalBus.Subscribe<LevelStartedSignal>(OnLevelStarted);
        }

        public void Dispose()
        {
            _signalBus.Unsubscribe<TileValidatedSignal>(OnTileValidated);
            _signalBus.Unsubscribe<LevelStartedSignal>(OnLevelStarted);
        }

        private void OnLevelStarted(LevelStartedSignal signal)
        {
            _levelCompleteFired = false;
        }

        // ─────────────────────────────────────────────────────────────────────
        private void OnTileValidated(TileValidatedSignal signal)
        {
            TileData tile = signal.Tile;

            for (int i = 0; i < RuntimeGameState.MaxActiveOrders; i++)
            {
                OrderData order = _state.ActiveOrders[i];
                if (order == null) continue;

                if (order.CurrentItemIndex >= order.requiredTypeIDs.Count) continue;
                if (order.requiredTypeIDs[order.CurrentItemIndex] != tile.typeID) continue;

                order.CurrentItemIndex++;
                _signalBus.Fire(new TileRoutedToTraySignal { 
                    Tile = tile, 
                    TargetTrayIndex = i,
                    TargetItemIndex = order.CurrentItemIndex - 1
                });
#if UNITY_EDITOR
                Debug.Log($"[OrderController] TileID {tile.tileID} (type {tile.typeID}) matched order[{i}] slot {order.CurrentItemIndex - 1}.");
#endif

                if (order.CurrentItemIndex >= order.requiredTypeIDs.Count)
                    CompleteOrder(i);

                return;
            }
#if UNITY_EDITOR
            Debug.Log($"[OrderController] TileID {tile.tileID} (type {tile.typeID}) matched no active order — firing TileUnmatchedSignal.");
#endif
            _signalBus.Fire(new TileUnmatchedSignal { Tile = tile });
        }

        // ─────────────────────────────────────────────────────────────────────
        private void CompleteOrder(int orderIndex)
        {
            _state.ActiveOrders[orderIndex] = null;
#if UNITY_EDITOR
            Debug.Log($"[OrderController] Order {orderIndex} completed. Pending: {_state.PendingOrders.Count}.");
#endif

            _signalBus.Fire(new OrderCompletedSignal { TrayIndex = orderIndex });

            PromoteNextOrder(orderIndex);

            bool hasActive = false;
            for(int i = 0; i < RuntimeGameState.MaxActiveOrders; i++)
            {
                if (_state.ActiveOrders[i] != null) hasActive = true;
            }

            if (!hasActive && _state.PendingOrders.Count == 0 && !_levelCompleteFired)
            {
#if UNITY_EDITOR
                Debug.Log("[OrderController] All orders fulfilled - firing LevelCompletedSignal.");
#endif
                _levelCompleteFired = true;
                _signalBus.Fire(new LevelCompletedSignal());
            }
        }

        private void PromoteNextOrder(int trayIndex)
        {
            if (_state.PendingOrders.Count > 0)
            {
                var order = _state.PendingOrders.Dequeue();
                _state.ActiveOrders[trayIndex] = order;
#if UNITY_EDITOR
                Debug.Log($"[OrderController] Promoted next pending order to active at tray {trayIndex}.");
#endif
                _signalBus.Fire(new OrderPromotedSignal { Order = order, TrayIndex = trayIndex });

                TryFulfillFromRack(trayIndex);
            }
        }

        private void TryFulfillFromRack(int orderIndex)
        {
            // The order might have been completed and removed while checking the rack, 
            // so we must ensure it's still active.
            OrderData order = _state.ActiveOrders[orderIndex];
            if (order == null) return;

            bool foundMatch = true;
            while (foundMatch && order.CurrentItemIndex < order.requiredTypeIDs.Count)
            {
                foundMatch = false;
                int neededType = order.requiredTypeIDs[order.CurrentItemIndex];

                for (int r = 0; r < _state.Rack.Length; r++)
                {
                    if (_state.Rack[r] == neededType)
                    {
                        _state.Rack[r] = 0; // Claim it
                        order.CurrentItemIndex++;

                        _signalBus.Fire(new TileRoutedFromRackToTraySignal 
                        { 
                            SourceRackIndex = r,
                            TargetTrayIndex = orderIndex,
                            TargetItemIndex = order.CurrentItemIndex - 1,
                            TypeID = neededType
                        });

                        foundMatch = true;
#if UNITY_EDITOR
                        Debug.Log($"[OrderController] Rack slot {r} matched newly promoted order[{orderIndex}] slot {order.CurrentItemIndex - 1}.");
#endif

                        if (order.CurrentItemIndex >= order.requiredTypeIDs.Count)
                        {
                            CompleteOrder(orderIndex);
                            return; // This order is done
                        }
                        break;
                    }
                }
            }
        }
    }
}
