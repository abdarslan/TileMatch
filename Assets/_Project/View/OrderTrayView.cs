using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TileMatch.View
{
    /// <summary>
    /// Owns all visual sequencing for one order tray.
    ///
    /// Two separate mechanisms work together:
    ///   1. _chainHead  – strictly sequential chain for tray lifecycle (slideout, slidein).
    ///   2. _readyTcs   – a UniTaskCompletionSource that acts as a "gate".
    ///                    Resolved   = tray is in final position, tiles may fly.
    ///                    Unresolved = tray is transitioning; tiles await the gate.
    ///
    /// VisualView only calls EnqueueTileFlight / EnqueueCompletion / EnqueuePromotion.
    /// </summary>
    public class OrderTrayView : MonoBehaviour
    {
        [SerializeField] private Image[] _itemImages;
        [SerializeField] private Transform _orderTray;

        private Vector3 _originalLocalPosition;
        private bool _isPositionCached;

        [Header("Animation Settings")]
        [SerializeField] private float _slideYOffset = 1000f;

        private void Awake()
        {
            if (_orderTray != null)
            {
                _originalLocalPosition = _orderTray.localPosition;
                _isPositionCached = true;
            }
        }

        // ── Chain (sequential tray lifecycle) ─────────────────────────────────────
        private UniTask _chainHead = UniTask.CompletedTask;

        // ── Readiness gate (multi-awaitable, designed for it) ─────────────────────
        private UniTaskCompletionSource _readyTcs;

        // ── Tracks active flights for current order phase ─────────────────────────
        private readonly List<UniTask> _runningFlights = new();

        // ── Public API ────────────────────────────────────────────────────────────

        public bool HasVisualTiles
        {
            get
            {
                foreach (var img in _itemImages)
                    if (img.enabled) return true;
                return false;
            }
        }

        /// <summary>Call at level start to reset all state.</summary>
        public void ResetChain()
        {
            _chainHead = UniTask.CompletedTask;
            _runningFlights.Clear();
            _readyTcs = new UniTaskCompletionSource();
            _readyTcs.TrySetResult(); // tray starts READY (already on screen)
        }

        /// <summary>
        /// Starts a tile flight immediately. If the tray is mid-transition,
        /// the flight internally awaits the readiness gate before animating.
        /// ct is needed so a level-end cancellation unblocks waiting flights.
        /// </summary>
        public void EnqueueTileFlight(Func<UniTask> flightFactory, CancellationToken ct)
        {
            // Capture the gate at tap-time. For current-order tiles this is the
            // already-resolved TCS -> flight proceeds instantly (zero wait).
            // For next-order tiles tapped during a transition, this is the new,
            // unresolved TCS -> flight waits until slide-in completes.
            UniTask gateTask = _readyTcs.Task;
            _runningFlights.Add(RunFlightWhenReadyAsync(gateTask, flightFactory, ct));
        }

        /// <summary>
        /// Marks tray as TRANSITIONING, snapshots running flights, and queues:
        /// WhenAll(flights) -> completion bounce -> slide-out.
        /// </summary>
        public void EnqueueCompletion(CancellationToken ct)
        {
            var flights = new List<UniTask>(_runningFlights);
            _runningFlights.Clear();

            // Open a new gate immediately. Any tile tapped after this line
            // will wait until EnqueuePromotion resolves the new gate.
            _readyTcs = new UniTaskCompletionSource();

            var prev = _chainHead;
            _chainHead = RunCompletionAsync(prev, flights, ct);
        }

        /// <summary>
        /// Queues: Initialize -> slide-in -> resolve gate.
        /// The gate resolution is the exact moment tiles may start flying.
        /// </summary>
        public void EnqueuePromotion(Model.OrderData order, Sprite[] icons, CancellationToken ct)
        {
            // Capture the gate that was just created by EnqueueCompletion.
            var tcsToResolve = _readyTcs;
            var prev = _chainHead;
            _chainHead = RunPromotionAsync(prev, order, icons, tcsToResolve, ct);
        }

        // ── Chain internals ───────────────────────────────────────────────────────

        private static async UniTask RunFlightWhenReadyAsync(
            UniTask gateTask, Func<UniTask> factory, CancellationToken ct)
        {
            // AttachExternalCancellation ensures this doesn't hang forever if level ends.
            await gateTask.AttachExternalCancellation(ct).SuppressCancellationThrow();
            if (ct.IsCancellationRequested) return;
            await factory(); // factory handles its own ct internally
        }

        private async UniTask RunCompletionAsync(
            UniTask prev, List<UniTask> flights, CancellationToken ct)
        {
            await prev.SuppressCancellationThrow();
            if (ct.IsCancellationRequested) return;

            if (flights.Count > 0)
            {
                // Wait for every tile of this order to land.
                await UniTask.WhenAll(flights).SuppressCancellationThrow();
                // Brief pause so the player can see the completed order.
                //await UniTask.Delay(100, cancellationToken: ct).SuppressCancellationThrow();
            }

            if (ct.IsCancellationRequested) return;
            await PlayCompletionAnimationAsync(ct);
        }

        private async UniTask RunPromotionAsync(
            UniTask prev, Model.OrderData order, Sprite[] icons,
            UniTaskCompletionSource tcsToResolve, CancellationToken ct)
        {
            await prev.SuppressCancellationThrow();

            if (ct.IsCancellationRequested)
            {
                tcsToResolve.TrySetResult(); // never leave tiles waiting forever
                return;
            }

            Initialize(order, icons);
            await SlideInAnimationAsync(ct);

            // Gate opens NOW: tray is at its final resting position.
            // All tile flights that were waiting will resume here.
            tcsToResolve.TrySetResult();
        }

        // ── Visual operations ─────────────────────────────────────────────────────

        private void Initialize(Model.OrderData order, Sprite[] typeIcons)
        {
            Clear();
            for (int i = 0; i < order.requiredTypeIDs.Count && i < _itemImages.Length; i++)
            {
                int typeID = order.requiredTypeIDs[i];
                if (typeID > 0 && typeID < typeIcons.Length)
                {
                    _itemImages[i].sprite = typeIcons[typeID];
                    _itemImages[i].enabled = true;
                    _itemImages[i].color = new Color(1f, 1f, 1f, 0.4f);
                }
            }
        }

        public void AddTile(int index)
        {
            if (index >= 0 && index < _itemImages.Length)
            {
                _itemImages[index].color = Color.white;
                _itemImages[index].transform.DOPunchScale(Vector3.one * 0.2f, 0.2f, 5, 1f);
            }
        }

        public Vector3 GetSlotWorldPosition(int index)
        {
            if (index >= 0 && index < _itemImages.Length)
                return _itemImages[index].transform.position;
            return transform.position;
        }
        [SerializeField] private float _punchScale = 0.2f;
        [SerializeField] private int _punchVibra = 5;
        [SerializeField] private float _punchDuration = 0.2f;

        [SerializeField] private float _punchElasticity = 1f;
        private async UniTask PlayCompletionAnimationAsync(CancellationToken ct)
        {
            if (_orderTray == null) return;
            CachePositionIfNeeded();
            // ordertray animates with last order reah scaling up and down back
            _orderTray.DOPunchScale(Vector3.one * _punchScale, _punchDuration, _punchVibra, _punchElasticity);



            await UniTask.Delay(80, cancellationToken: ct).SuppressCancellationThrow();
            await _orderTray.DOLocalMoveY(_originalLocalPosition.y + _slideYOffset, 0.35f)
                .SetEase(Ease.InQuad)
                .AsyncWaitForCompletion().AsUniTask()
                .AttachExternalCancellation(ct).SuppressCancellationThrow();

            Clear();
        }

        private async UniTask SlideInAnimationAsync(CancellationToken ct)
        {
            if (_orderTray == null) return;
            CachePositionIfNeeded();

            _orderTray.localPosition = _originalLocalPosition + new Vector3(0, _slideYOffset, 0);

            await _orderTray.DOLocalMoveY(_originalLocalPosition.y, 0.35f)
                .SetEase(Ease.OutBack, overshoot: 1.2f)
                .AsyncWaitForCompletion().AsUniTask()
                .AttachExternalCancellation(ct).SuppressCancellationThrow();
        }

        public void Clear()
        {
            for (int i = 0; i < _itemImages.Length; i++)
            {
                _itemImages[i].sprite = null;
                _itemImages[i].enabled = false;
            }
        }

        private void CachePositionIfNeeded()
        {
            if (!_isPositionCached && _orderTray != null)
            {
                _originalLocalPosition = _orderTray.localPosition;
                _isPositionCached = true;
            }
        }
    }
}
