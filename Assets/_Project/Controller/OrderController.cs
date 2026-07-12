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

        public OrderController(RuntimeGameState state, SignalBus signalBus)
        {
            _state     = state;
            _signalBus = signalBus;

            _signalBus.Subscribe<TileValidatedSignal>(OnTileValidated);
        }

        public void Dispose()
        {
            _signalBus.Unsubscribe<TileValidatedSignal>(OnTileValidated);
        }

        // ─────────────────────────────────────────────────────────────────────
        private void OnTileValidated(TileValidatedSignal signal)
        {
            TileSaveData tile = signal.Tile;

            for (int i = 0; i < _state.ActiveOrders.Count; i++)
            {
                OrderData order = _state.ActiveOrders[i];

                if (order.currentItemIndex >= order.requiredTypeIDs.Count) continue;
                if (order.requiredTypeIDs[order.currentItemIndex] != tile.typeID) continue;

                order.currentItemIndex++;
                _signalBus.Fire(new TileRoutedToTraySignal { Tile = tile, TargetTrayIndex = i });

                Debug.Log($"[OrderController] TileID {tile.tileID} (type {tile.typeID}) matched order[{i}] slot {order.currentItemIndex - 1}.");

                if (order.currentItemIndex >= order.requiredTypeIDs.Count)
                    CompleteOrder(i);

                return;
            }

            Debug.Log($"[OrderController] TileID {tile.tileID} (type {tile.typeID}) matched no active order — firing TileUnmatchedSignal.");
            _signalBus.Fire(new TileUnmatchedSignal { Tile = tile });
        }

        // ─────────────────────────────────────────────────────────────────────
        private void CompleteOrder(int orderIndex)
        {
            _state.ActiveOrders.RemoveAt(orderIndex);
            Debug.Log($"[OrderController] Order {orderIndex} completed. Active: {_state.ActiveOrders.Count}, Pending: {_state.PendingOrders.Count}.");

            PromoteNextOrder();

            if (_state.ActiveOrders.Count == 0 && _state.PendingOrders.Count == 0)
            {
                Debug.Log("[OrderController] All orders fulfilled — firing LevelCompletedSignal.");
                _signalBus.Fire(new LevelCompletedSignal());
            }
        }

        private void PromoteNextOrder()
        {
            while (_state.ActiveOrders.Count < RuntimeGameState.MaxActiveOrders
                   && _state.PendingOrders.Count > 0)
            {
                _state.ActiveOrders.Add(_state.PendingOrders.Dequeue());
                Debug.Log("[OrderController] Promoted next pending order to active.");
            }
        }
    }
}
