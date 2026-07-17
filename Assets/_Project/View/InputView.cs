using TileMatch.Service;
using TileMatch.Signal;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TileMatch.View
{
    /// <summary>
    /// Thin input handler. Detects tap/click, casts a ray into the scene,
    /// and immediately fires <see cref="TileTapIntentSignal"/> if a
    /// <see cref="TileView"/> is hit. Contains zero domain logic.
    /// Never references any Controller.
    /// </summary>
    public class InputView : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        private SignalBus _signalBus;
        private bool      _active;

        public void Initialize(SignalBus signalBus)
        {
            _signalBus = signalBus;
            _active    = true;
        }

        public void SetActive(bool value) => _active = value;

        // ─────────────────────────────────────────────────────────────────────
        private void Update()
        {
            if (!_active) return;

            if (!WasTapPerformed(out Vector2 screenPoint)) return;

            Vector2 worldPoint = _camera.ScreenToWorldPoint(screenPoint);
            RaycastHit2D[] hits = Physics2D.RaycastAll(worldPoint, Vector2.zero);

            TileView topTile = null;
            int maxSortingOrder = int.MinValue;

            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;

                TileView tile = hit.collider.GetComponent<TileView>();
                if (tile != null && !tile.IsGhost && tile.SortingOrder > maxSortingOrder)
                {
                    maxSortingOrder = tile.SortingOrder;
                    topTile = tile;
                }
            }

            if (topTile == null) return;

#if UNITY_EDITOR
            Debug.Log($"[InputView] Tap detected on TileID {topTile.TileID} with sorting order {maxSortingOrder}.");
#endif
            _signalBus.Fire(new TileTapIntentSignal { TileID = topTile.TileID });
        }

        // ─────────────────────────────────────────────────────────────────────
        private static bool WasTapPerformed(out Vector2 screenPoint)
        {
#if ENABLE_INPUT_SYSTEM
            Touchscreen touch = Touchscreen.current;
            if (touch != null)
            {
                foreach (var finger in touch.touches)
                {
                    if (finger.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
                    {
                        screenPoint = finger.position.ReadValue();
                        return true;
                    }
                }
            }

            // Editor / desktop fallback
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPoint = Mouse.current.position.ReadValue();
                return true;
            }
#else
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                screenPoint = Input.GetTouch(0).position;
                return true;
            }
            if (Input.GetMouseButtonDown(0))
            {
                screenPoint = Input.mousePosition;
                return true;
            }
#endif
            screenPoint = default;
            return false;
        }
    }
}
