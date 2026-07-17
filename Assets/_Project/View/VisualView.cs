using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TileMatch.Model;
using TileMatch.Service;
using TileMatch.Signal;
using TileMatch.Signal.UI;
using UnityEngine;

namespace TileMatch.View
{
    /// <summary>
    /// The primary View component for the gameplay scene. Listens to routing signals
    /// from the Controller layer and delegates physical animations to the <see cref="BoardAnimator"/>
    /// and <see cref="OrderTrayView"/>. Maintains the logical-to-visual mapping of all spawned tiles.
    /// </summary>
    public class VisualView : MonoBehaviour
    {
        [SerializeField] private TileView _tilePrefab;
        [SerializeField] private Transform _poolParent;
        [SerializeField] private ParticleSystem _confettiParticles;
        [Header("Board")]
        [SerializeField] private Transform _boardRoot;
        private const int FlightSortingOrder = 30000;

        [Header("Order Trays")]
        [SerializeField] private OrderTrayView[] _traySlots;
        [Header("Rack")]
        [SerializeField] private RackSlotView[] _rackSlots;
        [Header("Icons")]
        [SerializeField] private Sprite[] _typeIcons;
        
        [Header("Animation Durations")]
        [SerializeField] private float _tilePickupDuration = 0.10f;
        [SerializeField] private float _tileRouteDuration = 0.50f;
        [SerializeField] private float _tilePickupScale = 1.20f;
        [SerializeField] private float _rackShakeDuration = 0.40f;
        [SerializeField] private float _winPunchScale = 1.30f;

        private SignalBus _signalBus;
        private HapticService _hapticService;
        private TilePool _pool;
        private BoardAnimator _animator;
        private CancellationTokenSource _levelCts = new CancellationTokenSource();
        private Dictionary<int, TileView> _activeTiles = new Dictionary<int, TileView>();
        
        private Transform _rackParent;
        private Vector3 _rackOriginalLocalPos;
        private Vector3[] _trayOriginalLocalPositions;
        private int _activeRoutings = 0;
        private bool[] _rackLogicalState;

        public void Initialize(SignalBus signalBus, HapticService hapticService, int prewarmCount = 64)
        {
            _signalBus = signalBus;
            _hapticService = hapticService;

            if (_rackSlots != null && _rackSlots.Length > 0 && _rackSlots[0] != null)
            {
                _rackParent = _rackSlots[0].transform.parent;
                _rackOriginalLocalPos = _rackParent.localPosition;
                _rackLogicalState = new bool[_rackSlots.Length];
            }

            if (_traySlots != null && _traySlots.Length > 0)
            {
                _trayOriginalLocalPositions = new Vector3[_traySlots.Length];
                for (int i = 0; i < _traySlots.Length; i++)
                {
                    if (_traySlots[i] != null)
                    {
                        _trayOriginalLocalPositions[i] = _traySlots[i].transform.localPosition;
                        _traySlots[i].OnTrayVisualCompletion += HandleTrayVisualCompletion;
                    }
                }
            }

            _pool = new TilePool(_tilePrefab, _poolParent, prewarmCount);
            var settings = new BoardAnimator.Settings
            {
                TilePickupDuration = _tilePickupDuration,
                TileRouteDuration = _tileRouteDuration,
                TilePickupScale = _tilePickupScale,
                RackShakeDuration = _rackShakeDuration,
                WinPunchScale = _winPunchScale,
                ConfettiParticles = _confettiParticles,
                BoardRoot = _boardRoot,
                RackParent = _rackParent,
                RackOriginalLocalPos = _rackOriginalLocalPos,
                TypeIcons = _typeIcons
            };
            
            _animator = new BoardAnimator(settings, _pool, _hapticService, _signalBus);

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
            if (_animator != null) _animator.KillAllTweens();

            if (_traySlots != null)
            {
                for (int i = 0; i < _traySlots.Length; i++)
                {
                    if (_traySlots[i] != null)
                        _traySlots[i].OnTrayVisualCompletion -= HandleTrayVisualCompletion;
                }
            }

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

        private void OnGameStateChanged(GameStateChangedSignal signal)
        {
            if (signal.NewState == GameState.Menu) ClearBoard();
        }

        private void ClearBoard()
        {
            if (_animator != null) _animator.KillAllTweens();
            foreach (var kvp in _activeTiles) _pool.Return(kvp.Value);
            _activeTiles.Clear();

            _levelCts.Cancel();
            _levelCts.Dispose();
            _levelCts = new CancellationTokenSource();
            _activeRoutings = 0;
            if (_rackLogicalState != null)
            {
                for (int i = 0; i < _rackLogicalState.Length; i++) _rackLogicalState[i] = false;
            }

            if (_rackSlots != null)
            {
                foreach (var slot in _rackSlots) if (slot != null) slot.Clear();
            }
            if (_rackParent != null)
            {
                _rackParent.localPosition = _rackOriginalLocalPos;
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
            _animator.AnimateLevelStartAsync(signal.Level.activeTiles, _activeTiles, _levelCts.Token).Forget();
        }

        private void HandleTrayVisualCompletion()
        {
            _hapticService.OnOrderCompleted(_levelCts.Token);
        }

        private void OnOrderCompleted(OrderCompletedSignal signal)
        {
            if (_traySlots == null || signal.TrayIndex < 0 || signal.TrayIndex >= _traySlots.Length) return;
            _traySlots[signal.TrayIndex].EnqueueCompletion(_levelCts.Token);
        }

        private void OnOrderPromoted(OrderPromotedSignal signal)
        {
            if (_traySlots == null || signal.TrayIndex < 0 || signal.TrayIndex >= _traySlots.Length) return;
            _traySlots[signal.TrayIndex].EnqueuePromotion(signal.Order, _typeIcons, _levelCts.Token);
        }

        private void OnTileRoutedToTray(TileRoutedToTraySignal signal)
        {
            if (!_activeTiles.TryGetValue(signal.Tile.tileID, out TileView view)) return;
            _activeTiles.Remove(signal.Tile.tileID);
            view.PlayDisappearAnimation();

            int trayIndex = Mathf.Clamp(signal.TargetTrayIndex, 0, _traySlots.Length - 1);
            OrderTrayView tray = _traySlots[trayIndex];
            int itemIdx = signal.TargetItemIndex;
            CancellationToken ct = _levelCts.Token;

            _activeRoutings++;
            tray.EnqueueTileFlight(async () =>
            {
                view.SetSortingOrder(FlightSortingOrder);
                Vector3 dest = tray.GetSlotWorldPosition(itemIdx);
                await _animator.AnimateTileToDestinationAsync(view, dest, returnToPool: true, ct);
                if (!ct.IsCancellationRequested) tray.AddTile(itemIdx);
                _activeRoutings--;
            }, ct);

            _hapticService.OnTileMatchedOrder();
        }

        private void OnTileRoutedFromRackToTray(TileRoutedFromRackToTraySignal signal)
        {
            int rackIndex = Mathf.Clamp(signal.SourceRackIndex, 0, _rackSlots.Length - 1);
            RackSlotView rackSlot = _rackSlots[rackIndex];
            _rackLogicalState[rackIndex] = false;
            int trayIndex = Mathf.Clamp(signal.TargetTrayIndex, 0, _traySlots.Length - 1);
            OrderTrayView tray = _traySlots[trayIndex];
            int itemIdx = signal.TargetItemIndex;
            int typeID = signal.TypeID;
            CancellationToken ct = _levelCts.Token;

            Vector3 startPos = rackSlot.transform.position;
            rackSlot.Clear();

            TileView ghost = _pool.Rent();
            ghost.transform.SetParent(_boardRoot);
            ghost.transform.position = startPos;
            ghost.transform.localScale = _pool.OriginalScale;
            ghost.gameObject.SetActive(true);
            
            Sprite icon = _typeIcons != null && typeID > 0 && typeID < _typeIcons.Length ? _typeIcons[typeID] : null;
            ghost.Setup(tileID: -1, typeID: typeID, icon: icon, isGhost: true);
            ghost.DisableBg();
            ghost.SetSortingOrder(FlightSortingOrder);

            _activeRoutings++;
            tray.EnqueueTileFlight(async () =>
            {
                Vector3 dest = tray.GetSlotWorldPosition(itemIdx);
                await _animator.AnimateTileToDestinationAsync(ghost, dest, returnToPool: true, ct);
                if (!ct.IsCancellationRequested) tray.AddTile(itemIdx);
                _activeRoutings--;
            }, ct);
        }

        private void OnTileRoutedToRack(TileRoutedToRackSignal signal)
        {
            if (!_activeTiles.TryGetValue(signal.Tile.tileID, out TileView view)) return;
            _activeTiles.Remove(signal.Tile.tileID);
            view.PlayDisappearAnimation();

            int slotIndex = Mathf.Clamp(signal.TargetRackIndex, 0, _rackSlots.Length - 1);
            RackSlotView slot = _rackSlots[slotIndex];
            _rackLogicalState[slotIndex] = true;
            Sprite icon = _typeIcons != null && signal.Tile.typeID > 0 && signal.Tile.typeID < _typeIcons.Length 
                ? _typeIcons[signal.Tile.typeID] 
                : null;

            _activeRoutings++;
            RouteTileToRackAsync(view, slot, slotIndex, icon, _levelCts.Token).Forget();
            _hapticService.OnTileSentToRack();
        }

        private async UniTaskVoid RouteTileToRackAsync(TileView view, RackSlotView slot, int slotIndex, Sprite icon, CancellationToken ct)
        {
            view.SetSortingOrder(FlightSortingOrder);
            await _animator.AnimateTileToDestinationAsync(view, slot.transform.position, returnToPool: true, ct);
            if (!ct.IsCancellationRequested && _rackLogicalState != null && _rackLogicalState[slotIndex]) 
                slot.SetIcon(icon);
            _activeRoutings--;
        }

        private void OnRackFull(RackFullSignal signal)
        {
            PlayEndGameWhenReadyAsync(false, _levelCts.Token).Forget();
        }

        private void OnLevelCompleted(LevelCompletedSignal signal)
        {
            PlayEndGameWhenReadyAsync(true, _levelCts.Token).Forget();
        }

        private async UniTaskVoid PlayEndGameWhenReadyAsync(bool isWin, CancellationToken ct)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.Log($"[VisualView] PlayEndGameWhenReadyAsync started. Active routings: {_activeRoutings}");
#endif
            while (_activeRoutings > 0)
            {
                if (ct.IsCancellationRequested) return;
                await UniTask.Yield(ct).SuppressCancellationThrow();
            }
            if (ct.IsCancellationRequested) return;
            
#if UNITY_EDITOR
            UnityEngine.Debug.Log($"[VisualView] All routings cleared! Proceeding to PlayEndGameFeedbackAsync.");
#endif
            _animator.PlayEndGameFeedbackAsync(isWin, _activeTiles, ct).Forget();
        }

        private void OnTileUnblocked(TileUnblockedSignal signal)
        {
            if (_activeTiles.TryGetValue(signal.TileID, out TileView view))
            {
                view.SetBlockedState(false);
            }
        }
    }
}
