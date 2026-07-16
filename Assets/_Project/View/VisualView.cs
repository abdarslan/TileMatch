using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TileMatch.Model;
using TileMatch.Service;
using TileMatch.Signal;
using TileMatch.Signal.UI;
using TileMatch.Controller;
using UnityEngine;

namespace TileMatch.View
{
    /// <summary>
    /// Owns the tile GameObject pool. Listens to signal bus routing events to
    /// animate tiles from the board to their target (tray or rack slot).
    /// Never references any Controller. Reads LevelData (Model) directly on
    /// level start to spawn the initial tile visuals.
    ///
    /// Animation rules:
    ///   - All DOTween sequences are awaited via UniTask.
    ///   - Every await ends with .SuppressCancellationThrow() per the rulebook.
    ///   - Animations run fire-and-forget (UniTaskVoid) — game logic is never
    ///     blocked waiting for a visual.
    /// </summary>
    public class VisualView : MonoBehaviour
    {
        // ── Inspector wiring ─────────────────────────────────────────────────
        [SerializeField] private TileView   _tilePrefab;
        [SerializeField] private Transform  _poolParent;

        [Header("Board")]
        [SerializeField] private Transform  _boardRoot;

        [Header("Order Trays — one slot per active order (index 0 and 1)")]
        [SerializeField] private OrderTrayView[] _traySlots;

        [Header("Rack — exactly 6 slots")]
        [SerializeField] private RackSlotView[] _rackSlots;

        [Header("Icons — element index == typeID (index 0 unused, typeIDs start at 1)")]
        [SerializeField] private Sprite[] _typeIcons;
        // ── Animation tuning ─────────────────────────────────────────────────
        [Header("Animation Durations")]
        [SerializeField] private float TilePickupDuration  = 0.10f;
        [SerializeField] private float TileRouteDuration   = 0.50f;
        [SerializeField] private float TilePickupScale     = 1.20f;
        [SerializeField] private float RackShakeDuration   = 0.40f;
        [SerializeField] private float WinPunchScale       = 1.30f;

        // ── Runtime state ─────────────────────────────────────────────────────
        private SignalBus                   _signalBus;
        private HapticService               _hapticService;
        private readonly List<TileView>     _pool               = new List<TileView>();
        private readonly Dictionary<int, TileView> _activeTiles = new Dictionary<int, TileView>();
        private CancellationTokenSource     _levelCts           = new CancellationTokenSource();
        private Vector3                     _originalTileScale  = Vector3.one;

        // ─────────────────────────────────────────────────────────────────────
        public void Initialize(SignalBus signalBus, HapticService hapticService, int prewarmCount = 64)
        {
            if (_tilePrefab != null) _originalTileScale = _tilePrefab.transform.localScale;

            _signalBus     = signalBus;
            _hapticService = hapticService;

            PrewarmPool(prewarmCount);

            _signalBus.Subscribe<LevelStartedSignal>(OnLevelStarted);
            _signalBus.Subscribe<OrderPromotedSignal>(OnOrderPromoted);
            _signalBus.Subscribe<OrderCompletedSignal>(OnOrderCompleted);
            _signalBus.Subscribe<TileRoutedToTraySignal>(OnTileRoutedToTray);
            _signalBus.Subscribe<TileRoutedToRackSignal>(OnTileRoutedToRack);
            _signalBus.Subscribe<TileRoutedFromRackToTraySignal>(OnTileRoutedFromRackToTray);
            _signalBus.Subscribe<RackFullSignal>(OnRackFull);
            _signalBus.Subscribe<LevelCompletedSignal>(OnLevelCompleted);
            _signalBus.Subscribe<TileUnblockedSignal>(OnTileUnblocked);
            _signalBus.Subscribe<GameStateChangedSignal>(OnGameStateChanged);
        }

        private void OnDestroy()
        {
            _levelCts.Cancel();
            _levelCts.Dispose();

            if (_signalBus == null) return;
            _signalBus.Unsubscribe<LevelStartedSignal>(OnLevelStarted);
            _signalBus.Unsubscribe<OrderPromotedSignal>(OnOrderPromoted);
            _signalBus.Unsubscribe<OrderCompletedSignal>(OnOrderCompleted);
            _signalBus.Unsubscribe<TileRoutedToTraySignal>(OnTileRoutedToTray);
            _signalBus.Unsubscribe<TileRoutedToRackSignal>(OnTileRoutedToRack);
            _signalBus.Unsubscribe<TileRoutedFromRackToTraySignal>(OnTileRoutedFromRackToTray);
            _signalBus.Unsubscribe<RackFullSignal>(OnRackFull);
            _signalBus.Unsubscribe<LevelCompletedSignal>(OnLevelCompleted);
            _signalBus.Unsubscribe<TileUnblockedSignal>(OnTileUnblocked);
            _signalBus.Unsubscribe<GameStateChangedSignal>(OnGameStateChanged);
        }

        // ── Pool ──────────────────────────────────────────────────────────────
        private void PrewarmPool(int count)
        {
            for (int i = 0; i < count; i++)
                _pool.Add(CreatePooledTile());
        }

        private TileView CreatePooledTile()
        {
            TileView t = Instantiate(_tilePrefab, _poolParent);
            t.gameObject.SetActive(false);
            return t;
        }

        private TileView Rent()
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

        private void Return(TileView tile)
        {
            tile.transform.SetParent(_poolParent);
            tile.transform.localScale = _originalTileScale;
            tile.gameObject.SetActive(false);
        }

        // ── Signal handlers ───────────────────────────────────────────────────
        private void OnGameStateChanged(GameStateChangedSignal signal)
        {
            if (signal.NewState == GameplayController.GameState.Menu)
            {
                ClearBoard();
            }
        }

        private void ClearBoard()
        {
            ReturnAllToPool();

            _levelCts.Cancel();
            _levelCts.Dispose();
            _levelCts = new CancellationTokenSource();

            if (_rackSlots != null)
            {
                foreach (var slot in _rackSlots)
                    if (slot != null) slot.Clear();
            }
            if (_traySlots != null)
            {
                foreach (var tray in _traySlots)
                {
                    if (tray != null)
                    {
                        tray.ResetChain();
                        tray.Clear();
                    }
                }
            }
        }

        private void OnLevelStarted(LevelStartedSignal signal)
        {
            ClearBoard();

            // Fire-and-forget the level start animation sequence
            AnimateLevelStartAsync(signal.Level.activeTiles, _levelCts.Token).Forget();
        }

        private async UniTaskVoid AnimateLevelStartAsync(List<TileData> activeTiles, CancellationToken ct)
        {
            // Group tiles by their Z coordinate (which represents the layer).
            // In our sortingOrder math, larger Z means a higher sorting order (on top).
            // Therefore, we want to animate the smallest Z (deepest layer) first.
            var layers = activeTiles
                .GroupBy(t => Mathf.RoundToInt(t.visualPosition.z * 100f))
                .OrderBy(g => g.Key)
                .ToList();
            Sequence layerSequence = DOTween.Sequence();
            for (int i = 0; i < layers.Count; i++)
            {
                // Alternating direction: Layer 0 comes from left (-1), Layer 1 from right (1), etc.
                float direction = (i % 2 == 0) ? -1f : 1f;
                float offscreenOffset = direction * 10f; // 10 units in world space

                // OPTIMIZATION: Using a DOTween Sequence is much better for low-end devices 
                // than creating dozens of individual UniTasks. It batches the animations.
                

                foreach (TileData data in layers[i])
                {
                    TileView view = SpawnTile(data);
                    
                    Vector3 targetPos = view.transform.localPosition;
                    
                    // Start offscreen
                    view.transform.localPosition = targetPos + new Vector3(offscreenOffset, 0, 0);

                    // Insert the tween at time 0 so they all play simultaneously
                    layerSequence.Insert(0f + i*0.3f, view.transform.DOLocalMoveX(targetPos.x, 0.5f).SetEase(Ease.OutBack, overshoot:1.2f));
                }
            }
                // Await the entire sequence as a single task
                await layerSequence.Play()
                    .AsyncWaitForCompletion()
                    .AsUniTask()
                    .AttachExternalCancellation(ct)
                    .SuppressCancellationThrow();


            Debug.Log($"[VisualView] Animated {_activeTiles.Count} tile visuals across {layers.Count} layers.");
        }

        private void OnOrderCompleted(OrderCompletedSignal signal)
        {
            if (_traySlots == null || signal.TrayIndex < 0 || signal.TrayIndex >= _traySlots.Length) return;
            OrderTrayView tray = _traySlots[signal.TrayIndex];
            tray.EnqueueCompletion(_levelCts.Token);
        }

        private void OnOrderPromoted(OrderPromotedSignal signal)
        {
            if (_traySlots == null || signal.TrayIndex < 0 || signal.TrayIndex >= _traySlots.Length) return;
            OrderTrayView tray = _traySlots[signal.TrayIndex];
            tray.EnqueuePromotion(signal.Order, _typeIcons, _levelCts.Token);
        }

        private void OnTileRoutedToTray(TileRoutedToTraySignal signal)
        {
            if (!_activeTiles.TryGetValue(signal.Tile.tileID, out TileView view)) return;
            _activeTiles.Remove(signal.Tile.tileID);
            view.SetSortingOrder(30000);
            view.PlayDissappearAnimation(); // Play immediately on tap

            int trayIndex        = Mathf.Clamp(signal.TargetTrayIndex, 0, _traySlots.Length - 1);
            OrderTrayView tray   = _traySlots[trayIndex];
            int           itemIdx = signal.TargetItemIndex;
            CancellationToken ct  = _levelCts.Token;

            // The tray internally manages readiness via its gate (UniTaskCompletionSource).
            // Current-order tiles fly instantly; next-order tiles wait for slide-in.
            tray.EnqueueTileFlight(async () =>
            {
                Vector3 dest = tray.GetSlotWorldPosition(itemIdx);
                await AnimateTileToDestinationAsync(view, dest, returnToPool: true, ct);
                if (!ct.IsCancellationRequested)
                    tray.AddTile(itemIdx);
            }, ct);

            _hapticService.OnTileMatchedOrder();
        }

        private void OnTileRoutedFromRackToTray(TileRoutedFromRackToTraySignal signal)
        {
            int rackIndex        = Mathf.Clamp(signal.SourceRackIndex, 0, _rackSlots.Length - 1);
            RackSlotView rackSlot = _rackSlots[rackIndex];
            int trayIndex        = Mathf.Clamp(signal.TargetTrayIndex, 0, _traySlots.Length - 1);
            OrderTrayView tray   = _traySlots[trayIndex];
            int   itemIdx        = signal.TargetItemIndex;
            int   typeID         = signal.TypeID;
            CancellationToken ct = _levelCts.Token;

            Vector3 startPos = rackSlot.transform.position;
            rackSlot.Clear();

            // Spawn the ghost tile IMMEDIATELY so it replaces the rack slot icon
            // and remains visible while waiting for the tray slide-in.
            TileView ghost = Rent();
            ghost.transform.SetParent(_boardRoot);
            ghost.transform.position   = startPos;
            ghost.transform.localScale  = _originalTileScale;
            ghost.gameObject.SetActive(true);
            ghost.Setup(-1, typeID, GetIcon(typeID));
            ghost.DisableBg();
            ghost.SetSortingOrder(30000);

            tray.EnqueueTileFlight(async () =>
            {
                // The ghost tile will wait here until the gate opens.
                Vector3 dest = tray.GetSlotWorldPosition(itemIdx);
                await AnimateTileToDestinationAsync(ghost, dest, returnToPool: true, ct);
                if (!ct.IsCancellationRequested)
                    tray.AddTile(itemIdx);
            }, ct);
        }

        private void OnTileRoutedToRack(TileRoutedToRackSignal signal)
        {
            if (!_activeTiles.TryGetValue(signal.Tile.tileID, out TileView view)) return;
            _activeTiles.Remove(signal.Tile.tileID);
            
            view.SetSortingOrder(30000);
            view.PlayDissappearAnimation();
            int slotIndex = Mathf.Clamp(signal.TargetRackIndex, 0, _rackSlots.Length - 1);
            RackSlotView slot = _rackSlots[slotIndex];
            Sprite icon = GetIcon(signal.Tile.typeID);

            RouteTileToRackAsync(view, slot, icon, _levelCts.Token).Forget();

            _hapticService.OnTileSentToRack();
        }

        private async UniTaskVoid RouteTileToRackAsync(TileView view, RackSlotView slot, Sprite icon, CancellationToken ct)
        {
            await AnimateTileToDestinationAsync(view, slot.transform.position, returnToPool: true, ct);
            if (!ct.IsCancellationRequested)
            {
                slot.SetIcon(icon);
            }
        }

        private void OnRackFull(RackFullSignal signal)
        {
            _hapticService.OnRackFull();
            PlayRackFullFeedbackAsync(_levelCts.Token).Forget();
        }

        private void OnLevelCompleted(LevelCompletedSignal signal)
        {
            _hapticService.OnLevelWon();
            PlayWinFeedbackAsync(_levelCts.Token).Forget();
        }

        private void OnTileUnblocked(TileUnblockedSignal signal)
        {
            if (_activeTiles.TryGetValue(signal.TileID, out TileView view))
            {
                view.SetBlockedState(false);
            }
        }

        private TileView SpawnTile(TileData data)
        {
            Sprite icon  = GetIcon(data.typeID);
            TileView view = Rent();

            Vector3 scaledPos = new Vector3(
                data.visualPosition.x * _originalTileScale.x * 0.76f,
                data.visualPosition.y * _originalTileScale.y * 0.76f * 1.08f,
                data.visualPosition.z
            );

            view.transform.SetParent(_boardRoot);
            view.transform.localPosition = scaledPos;
            view.transform.localScale = _originalTileScale;
            view.gameObject.SetActive(true);
            view.Setup(data.tileID, data.typeID, icon);
            view.SetBlockedState(data.blockingTileIDs.Count > 0);
            
            int sortingOrder = Mathf.RoundToInt(data.visualPosition.z * 1000f) - Mathf.RoundToInt(data.visualPosition.y * 100f);
            view.SetSortingOrder(sortingOrder);

            _activeTiles.Add(data.tileID, view);
            return view;
        }

        private Sprite GetIcon(int typeID)
        {
            if (_typeIcons == null || typeID <= 0 || typeID >= _typeIcons.Length) return null;
            return _typeIcons[typeID];
        }

        private void ReturnAllToPool()
        {
            foreach (var kvp in _activeTiles)
                Return(kvp.Value);
            _activeTiles.Clear();
        }

        // ── Animations (DOTween + UniTask) ────────────────────────────────────

        /// <summary>
        /// Quick pickup scale-up, then arc movement to the destination.
        /// </summary>
        private async UniTask AnimateTileToDestinationAsync(
            TileView tile, Vector3 destination, bool returnToPool, CancellationToken ct)
        {
            Transform t = tile.transform;
            // 1. Pickup pop
            await t.DOScale(_originalTileScale * TilePickupScale, TilePickupDuration)
                   .SetEase(Ease.OutBack)
                   .AsyncWaitForCompletion()
                   .AsUniTask()
                   .AttachExternalCancellation(ct)
                   .SuppressCancellationThrow();

            if (ct.IsCancellationRequested) { Return(tile); return; }

            // 2. Route to destination with a slight arc (Z-rotation for visual flair)
            Sequence seq = DOTween.Sequence();
            seq.Join(t.DOMove(destination, TileRouteDuration).SetEase(Ease.InOutQuart));
            seq.Join(t.DOScale(_originalTileScale, TileRouteDuration).SetEase(Ease.InQuart));
            seq.Join(t.DORotate(new Vector3(0f, 0f, Random.Range(-25f, 25f)), TileRouteDuration * 0.5f)
                      .SetEase(Ease.OutCubic)
                      .SetLoops(2, LoopType.Yoyo));

            await seq.AsyncWaitForCompletion()
                     .AsUniTask()
                     .AttachExternalCancellation(ct)
                     .SuppressCancellationThrow();

            if (ct.IsCancellationRequested) { Return(tile); return; }

            // 3. Snap to exact slot position
            t.position   = destination;
            t.localScale = _originalTileScale;
            t.rotation   = Quaternion.identity;

            if (returnToPool)
                Return(tile);
        }

        /// <summary>
        /// Screen shake substitute: rack parent shakes horizontally.
        /// </summary>
        private async UniTaskVoid PlayRackFullFeedbackAsync(CancellationToken ct)
        {
            if (_rackSlots == null || _rackSlots.Length == 0) return;

            Transform rackParent = _rackSlots[0].transform.parent;
            if (rackParent == null) return;

            await rackParent.DOShakePosition(RackShakeDuration, strength: 0.18f, vibrato: 18, randomness: 45f)
                            .AsyncWaitForCompletion()
                            .AsUniTask()
                            .AttachExternalCancellation(ct)
                            .SuppressCancellationThrow();
        }

        /// <summary>
        /// All active board tiles do a celebratory punch scale on win.
        /// </summary>
        private async UniTaskVoid PlayWinFeedbackAsync(CancellationToken ct)
        {
            float delay = 0f;
            foreach (var kvp in _activeTiles)
            {
                Transform t = kvp.Value.transform;
                float captured = delay;

                DOTween.Sequence()
                       .AppendInterval(captured)
                       .Append(t.DOPunchScale(Vector3.one * (WinPunchScale - 1f), 0.3f, 6, 0.5f));

                delay += 0.04f;
            }

            await UniTask.WaitForSeconds(delay + 0.35f, cancellationToken: ct)
                         .SuppressCancellationThrow();
        }
    }
}
