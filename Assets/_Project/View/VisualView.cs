using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TileMatch.Model;
using TileMatch.Service;
using TileMatch.Signal;
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
        [SerializeField] private Transform[] _traySlots;

        [Header("Rack — exactly 6 slots")]
        [SerializeField] private Transform[] _rackSlots;

        [Header("Icons — element index == typeID (index 0 unused, typeIDs start at 1)")]
        [SerializeField] private Sprite[] _typeIcons;

        // ── Animation tuning ─────────────────────────────────────────────────
        private const float TilePickupDuration  = 0.10f;
        private const float TileRouteDuration   = 0.32f;
        private const float TilePickupScale     = 1.15f;
        private const float RackShakeDuration   = 0.40f;
        private const float WinPunchScale       = 1.30f;

        // ── Runtime state ─────────────────────────────────────────────────────
        private SignalBus                   _signalBus;
        private HapticService               _hapticService;
        private readonly List<TileView>     _pool               = new List<TileView>();
        private readonly Dictionary<int, TileView> _activeTiles = new Dictionary<int, TileView>();
        private CancellationTokenSource     _levelCts           = new CancellationTokenSource();

        // ─────────────────────────────────────────────────────────────────────
        public void Initialize(SignalBus signalBus, HapticService hapticService, int prewarmCount = 64)
        {
            _signalBus     = signalBus;
            _hapticService = hapticService;

            PrewarmPool(prewarmCount);

            _signalBus.Subscribe<LevelStartedSignal>(OnLevelStarted);
            _signalBus.Subscribe<TileRoutedToTraySignal>(OnTileRoutedToTray);
            _signalBus.Subscribe<TileRoutedToRackSignal>(OnTileRoutedToRack);
            _signalBus.Subscribe<RackFullSignal>(OnRackFull);
            _signalBus.Subscribe<LevelCompletedSignal>(OnLevelCompleted);
        }

        private void OnDestroy()
        {
            _levelCts.Cancel();
            _levelCts.Dispose();

            if (_signalBus == null) return;
            _signalBus.Unsubscribe<LevelStartedSignal>(OnLevelStarted);
            _signalBus.Unsubscribe<TileRoutedToTraySignal>(OnTileRoutedToTray);
            _signalBus.Unsubscribe<TileRoutedToRackSignal>(OnTileRoutedToRack);
            _signalBus.Unsubscribe<RackFullSignal>(OnRackFull);
            _signalBus.Unsubscribe<LevelCompletedSignal>(OnLevelCompleted);
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
            tile.transform.localScale = Vector3.one;
            tile.gameObject.SetActive(false);
        }

        // ── Signal handlers ───────────────────────────────────────────────────
        private void OnLevelStarted(LevelStartedSignal signal)
        {
            ReturnAllToPool();

            _levelCts.Cancel();
            _levelCts.Dispose();
            _levelCts = new CancellationTokenSource();

            foreach (TileSaveData data in signal.Level.activeTiles)
                SpawnTile(data);

            Debug.Log($"[VisualView] Spawned {_activeTiles.Count} tile visuals.");
        }

        private void OnTileRoutedToTray(TileRoutedToTraySignal signal)
        {
            if (!_activeTiles.TryGetValue(signal.Tile.tileID, out TileView view)) return;
            _activeTiles.Remove(signal.Tile.tileID);

            int   trayIndex = Mathf.Clamp(signal.TargetTrayIndex, 0, _traySlots.Length - 1);
            AnimateTileToDestinationAsync(view, _traySlots[trayIndex].position, returnToPool: true, _levelCts.Token).Forget();

            _hapticService.OnTileMatchedOrder();
        }

        private void OnTileRoutedToRack(TileRoutedToRackSignal signal)
        {
            if (!_activeTiles.TryGetValue(signal.Tile.tileID, out TileView view)) return;
            _activeTiles.Remove(signal.Tile.tileID);

            int   slot = Mathf.Clamp(signal.TargetRackIndex, 0, _rackSlots.Length - 1);
            AnimateTileToDestinationAsync(view, _rackSlots[slot].position, returnToPool: false, _levelCts.Token).Forget();

            _hapticService.OnTileSentToRack();
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

        // ── Spawn ─────────────────────────────────────────────────────────────
        private void SpawnTile(TileSaveData data)
        {
            Sprite icon  = GetIcon(data.typeID);
            TileView view = Rent();

            view.transform.SetParent(_boardRoot);
            view.transform.position   = data.visualPosition;
            view.transform.localScale = Vector3.one;
            view.gameObject.SetActive(true);
            view.Setup(data.tileID, data.typeID, icon);

            _activeTiles[data.tileID] = view;
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
        private async UniTaskVoid AnimateTileToDestinationAsync(
            TileView tile, Vector3 destination, bool returnToPool, CancellationToken ct)
        {
            Transform t = tile.transform;

            // 1. Pickup pop
            await t.DOScale(TilePickupScale, TilePickupDuration)
                   .SetEase(Ease.OutBack)
                   .AsyncWaitForCompletion()
                   .AsUniTask()
                   .AttachExternalCancellation(ct)
                   .SuppressCancellationThrow();

            if (ct.IsCancellationRequested) { Return(tile); return; }

            // 2. Route to destination with a slight arc (Z-rotation for visual flair)
            Sequence seq = DOTween.Sequence();
            seq.Join(t.DOMove(destination, TileRouteDuration).SetEase(Ease.InOutQuart));
            seq.Join(t.DOScale(1f, TileRouteDuration).SetEase(Ease.InQuart));
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
            t.localScale = Vector3.one;
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

            Transform rackParent = _rackSlots[0].parent;
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
