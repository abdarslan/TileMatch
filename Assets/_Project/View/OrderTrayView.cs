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

        private int _pendingAnimations = 0;

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
