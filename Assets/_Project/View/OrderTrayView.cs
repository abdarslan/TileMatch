using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace TileMatch.View
{
    public class OrderTrayView : MonoBehaviour
    {
        [SerializeField] private Image[] _itemImages;
        [SerializeField] private Transform _orderTray;
        private int _pendingAnimations = 0;
        private Vector3 _originalLocalPosition;
        private bool _isPositionCached = false;

        public bool HasVisualTiles
        {
            get
            {
                foreach (var img in _itemImages)
                {
                    if (img.enabled) return true;
                }
                return false;
            }
        }

        public bool IsFree => _pendingAnimations == 0;

        private void CachePositionIfNeeded()
        {
            if (!_isPositionCached && _orderTray != null)
            {
                _originalLocalPosition = _orderTray.localPosition;
                _isPositionCached = true;
            }
        }

        public void OnAnimationStarted() => _pendingAnimations++;
        public void OnAnimationFinished() => _pendingAnimations--;

        public async UniTask WaitUntilFreeAsync(CancellationToken ct)
        {
            bool wasWaiting = false;
            while (_pendingAnimations > 0)
            {
                wasWaiting = true;
                bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow();
                if (isCanceled) return;
            }

            if (wasWaiting)
            {
                // Wait just a tiny bit so the user can see the fully completed order
                // before it gets replaced by the new one.
                await UniTask.Delay(300, cancellationToken: ct).SuppressCancellationThrow();
            }
        }
        public void Initialize(Model.OrderData order, Sprite[] typeIcons)
        {
            Clear();
            for (int i = 0; i < order.requiredTypeIDs.Count && i < _itemImages.Length; i++)
            {
                int typeID = order.requiredTypeIDs[i];
                if (typeID > 0 && typeID < typeIcons.Length)
                {
                    _itemImages[i].sprite = typeIcons[typeID];
                    _itemImages[i].enabled = true;
                    // faded initially
                    _itemImages[i].color = new Color(1f, 1f, 1f, 0.4f);
                }
            }
        }

        public void AddTile(int index)
        {
            if (index >= 0 && index < _itemImages.Length)
            {
                _itemImages[index].color = new Color(1f, 1f, 1f, 1f); // full opacity
                _itemImages[index].transform.DOPunchScale(Vector3.one * 0.2f, 0.2f, 5, 1f);
            }
        }

        public Vector3 GetSlotWorldPosition(int index)
        {
            if (index >= 0 && index < _itemImages.Length)
                return _itemImages[index].transform.position;
            return transform.position;
        }
        public async UniTask PlayCompletionAnimationAsync(CancellationToken ct)
        {
            if (_orderTray == null) return;
            CachePositionIfNeeded();
            OnAnimationStarted();

            // 1. Jump up a bit to celebrate (using localPosition for UI)
            await _orderTray.DOLocalJump(_originalLocalPosition, 50f, 1, 0.5f)
                .SetEase(Ease.OutQuad)
                .AsyncWaitForCompletion()
                .AsUniTask()
                .AttachExternalCancellation(ct)
                .SuppressCancellationThrow();

            if (ct.IsCancellationRequested) { OnAnimationFinished(); return; }

            // 2. After jumping, exit the screen sliding up (1500 pixels up is safely off-screen)
            await _orderTray.DOLocalMoveY(_originalLocalPosition.y + 1500f, 0.5f)
                .SetEase(Ease.InQuad)
                .AsyncWaitForCompletion()
                .AsUniTask()
                .AttachExternalCancellation(ct)
                .SuppressCancellationThrow();

            OnAnimationFinished();
        }

        public async UniTask SlideInAnimationAsync(CancellationToken ct, int orderIndex)
        {
            if (_orderTray == null) return;
            CachePositionIfNeeded();
            OnAnimationStarted();

            // Set the starting position off-screen top (positive Y)
            float startOffsetY = 1500f; 
            
            // Move off-screen on the Y axis, keeping the original X and Z
            _orderTray.localPosition = _originalLocalPosition + new Vector3(0, startOffsetY, 0);

            // Slide down into its original local position
            await _orderTray.DOLocalMoveY(_originalLocalPosition.y, 0.5f)
                .SetEase(Ease.OutBack)
                .AsyncWaitForCompletion()
                .AsUniTask()
                .AttachExternalCancellation(ct)
                .SuppressCancellationThrow();

            OnAnimationFinished();
        }
        public void Clear()
        {
            for (int i = 0; i < _itemImages.Length; i++)
            {
                _itemImages[i].sprite = null;
                _itemImages[i].enabled = false;
            }
        }
    }
}
