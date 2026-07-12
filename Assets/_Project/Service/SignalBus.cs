using System;
using System.Collections.Generic;

namespace TileMatch.Service
{
    /// <summary>
    /// Lightweight generic event bus. Decouples signal producers from consumers
    /// across architectural layers. Iterates a snapshot on Fire so handlers may
    /// safely subscribe/unsubscribe during callback execution.
    /// </summary>
    public class SignalBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            Type key = typeof(T);
            if (!_handlers.ContainsKey(key))
                _handlers[key] = new List<Delegate>();

            _handlers[key].Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (_handlers.TryGetValue(typeof(T), out List<Delegate> list))
                list.Remove(handler);
        }

        public void Fire<T>(T signal) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out List<Delegate> list)) return;

            // Snapshot prevents issues when a handler modifies subscriptions mid-dispatch
            Delegate[] snapshot = list.ToArray();
            foreach (Delegate handler in snapshot)
                ((Action<T>)handler).Invoke(signal);
        }
    }
}
