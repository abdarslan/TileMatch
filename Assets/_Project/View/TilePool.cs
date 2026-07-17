using System.Collections.Generic;
using UnityEngine;

namespace TileMatch.View
{
    /// <summary>
    /// A simple object pool for <see cref="TileView"/> instances. Prevents heavy 
    /// instantiation/destruction overhead during gameplay and level transitions.
    /// </summary>
    public class TilePool
    {
        private readonly TileView _prefab;
        private readonly Transform _parent;
        private readonly List<TileView> _pool = new List<TileView>();
        public Vector3 OriginalScale { get; private set; } = Vector3.one;

        public TilePool(TileView prefab, Transform parent, int prewarmCount)
        {
            _prefab = prefab;
            _parent = parent;
            
            if (_prefab != null)
                OriginalScale = _prefab.transform.localScale;

            PrewarmPool(prewarmCount);
        }

        private void PrewarmPool(int count)
        {
            if (_pool.Capacity < count) _pool.Capacity = count;
            
            for (int i = 0; i < count; i++)
                _pool.Add(CreatePooledTile());
        }

        private TileView CreatePooledTile()
        {
            TileView t = Object.Instantiate(_prefab, _parent);
            t.gameObject.SetActive(false);
            return t;
        }

        public TileView Rent()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].gameObject.activeSelf)
                    return _pool[i];
            }
            TileView fresh = CreatePooledTile();
            _pool.Add(fresh);
            return fresh;
        }

        public void Return(TileView tile)
        {
            if (tile == null) return;
            tile.transform.SetParent(_parent);
            tile.transform.localScale = OriginalScale;
            tile.gameObject.SetActive(false);
        }
    }
}
