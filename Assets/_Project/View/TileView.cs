using UnityEngine;
using DG.Tweening;
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
        public bool IsGhost { get; private set; }

        [SerializeField] private SpriteRenderer _iconRenderer;
        [SerializeField] private Transform _tileBg;

        [SerializeField] private ParticleSystem _particleSystem;
        [SerializeField] private float _blockedBrightness = 0.4f;

        public void Setup(int tileID, int typeID, Sprite icon, bool isGhost = false)
        {
            ResetVisuals();
            TileID = tileID;
            TypeID = typeID;
            IsGhost = isGhost;
            _iconRenderer.sprite = icon;
        }

        public int SortingOrder { get; private set; }

        public void SetSortingOrder(int order)
        {
            SortingOrder = order;
            var baseRenderer = _tileBg.GetComponent<SpriteRenderer>();
            if (baseRenderer != null) baseRenderer.sortingOrder = order;
            if (_iconRenderer != null) _iconRenderer.sortingOrder = order + 1;
            
            if (_particleSystem != null)
            {
                var particleRenderer = _particleSystem.GetComponent<ParticleSystemRenderer>();
                if (particleRenderer != null)
                {
                    particleRenderer.sortingOrder = order + 2;
                }
            }
        }
        public void ResetVisuals() {
            _tileBg.gameObject.SetActive(true);
            _tileBg.localScale = Vector3.one;
        }

        public void PlayDisappearAnimation()
        {
            _particleSystem.Play();
            _tileBg.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack).OnComplete(() => 
            {
                DisableBg();
            });
        }

        public void SetBlockedState(bool isBlocked)
        {
            float alpha = isBlocked ? _blockedBrightness : 1f;
            Color c = new Color(alpha, alpha, alpha, 1f);
            
            var baseRenderer = _tileBg.GetComponent<SpriteRenderer>();
            if (baseRenderer != null) baseRenderer.color = c;
            if (_iconRenderer != null) _iconRenderer.color = c;
        }
        public void DisableBg()
        {
            _tileBg.gameObject.SetActive(false);
        }

        public Vector3 GetIconWorldOffset()
        {
            if (_iconRenderer == null) return Vector3.zero;
            return _iconRenderer.transform.position - transform.position;
        }
    }
}
