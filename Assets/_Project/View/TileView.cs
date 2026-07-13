using UnityEngine;

namespace TileMatch.View
{
    /// <summary>
    /// Lightweight identity component placed on every pooled tile GameObject.
    /// Holds the logical tileID so InputView's raycast can identify which tile
    /// was tapped without searching any dictionary. Set once by VisualView on spawn.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TileView : MonoBehaviour
    {
        public int TileID { get; private set; }
        public int TypeID { get; private set; }

        [SerializeField] private SpriteRenderer _iconRenderer;

        public void Setup(int tileID, int typeID, Sprite icon)
        {
            TileID = tileID;
            TypeID = typeID;
            _iconRenderer.sprite = icon;
        }

        public void SetSortingOrder(int order)
        {
            var baseRenderer = GetComponent<SpriteRenderer>();
            if (baseRenderer != null) baseRenderer.sortingOrder = order;
            if (_iconRenderer != null) _iconRenderer.sortingOrder = order + 1;
        }
    }
}
