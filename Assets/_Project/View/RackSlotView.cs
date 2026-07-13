using Cysharp.Threading.Tasks.Triggers;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace TileMatch.View
{
    public class RackSlotView : MonoBehaviour
    {
        // RackSlot prefab has a child object Icon. Icon has image. This can be assigned basically
        [SerializeField] private Image _itemImage;
        

        /// <summary>
        /// Called by VisualView the exact moment the 3D flying tile lands in this UI slot.
        /// </summary>
        public void SetIcon(Sprite tileSprite)
        {
            _itemImage.sprite = tileSprite;
            _itemImage.enabled = true; // Turn the child image on

            // A tiny DOTween pop effect to make the tile "snap" into place feel juicy
            _itemImage.transform.DOPunchScale(Vector3.one * 0.2f, 0.2f, 5, 1f);
        }

        /// <summary>
        /// Called by VisualView to empty the slot (e.g., if you add a mechanic to undo, or clear the rack)
        /// </summary>
        public void Clear()
        {
            _itemImage.sprite = null;
            _itemImage.enabled = false; // Hide the child image so only your background remains visible
        }
    }
}