using System.Collections.Generic;
using System.Linq;
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
    /// A pure C# companion to VisualView that owns all DOTween animations and 
    /// manages the safe cleanup of Tweens when the board is cleared or destroyed.
    /// Uses UniTask to provide awaitable, asynchronous visual sequences.
    /// </summary>
    public class BoardAnimator
    {
        public struct Settings
        {
            public float TilePickupDuration;
            public float TileRouteDuration;
            public float TilePickupScale;
            public float RackShakeDuration;
            public float WinPunchScale;
            public ParticleSystem ConfettiParticles;
            public Transform BoardRoot;
            public Transform RackParent;
            public Vector3 RackOriginalLocalPos;
            public Sprite[] TypeIcons;
        }

        private const float HexHorizontalOffset = 0.76f;
        private const float HexVerticalMultiplier = 1.08f;
        private const float DropAnimationYOffset = 1500f;
        private const float SpawnOffscreenOffset = 7f;
        private const float LayerAnimationStagger = 0.3f;
        private const int FlightSortingOrder = 30000;

        private readonly Settings _settings;
        private readonly TilePool _pool;
        private readonly HapticService _hapticService;
        private readonly SignalBus _signalBus;
        
        private int _activeTileAnimations = 0;
        private readonly HashSet<Tween> _activeTweens = new HashSet<Tween>();

        public BoardAnimator(Settings settings, TilePool pool, HapticService hapticService, SignalBus signalBus)
        {
            _settings = settings;
            _pool = pool;
            _hapticService = hapticService;
            _signalBus = signalBus;
        }

        public void KillAllTweens()
        {
            var tweens = _activeTweens.ToArray();
            foreach (var tween in tweens)
            {
                if (tween != null && tween.IsActive() && !tween.IsComplete())
                {
                    tween.Kill();
                }
            }
            _activeTweens.Clear();
        }

        private void RegisterTween(Tween tween)
        {
            _activeTweens.Add(tween);
            tween.OnKill(() => _activeTweens.Remove(tween));
        }

        public TileView SpawnTile(TileData data)
        {
            TileView view = _pool.Rent();
            view.transform.SetParent(_settings.BoardRoot);
            
            float x = data.visualPosition.x * _pool.OriginalScale.x * HexHorizontalOffset;
            float y = data.visualPosition.y * _pool.OriginalScale.y * HexHorizontalOffset * HexVerticalMultiplier;
            float z = data.visualPosition.z;

            view.transform.localPosition = new Vector3(x, y, z);
            int sortingOrder = Mathf.RoundToInt(data.visualPosition.z * 1000f) - Mathf.RoundToInt(data.visualPosition.y * 100f);
            view.SetSortingOrder(sortingOrder);
            view.SetBlockedState(data.blockingTileIDs != null && data.blockingTileIDs.Count > 0);
            
            Sprite icon = _settings.TypeIcons != null && data.typeID > 0 && data.typeID < _settings.TypeIcons.Length 
                ? _settings.TypeIcons[data.typeID] 
                : null;
                
            view.Setup(data.tileID, data.typeID, icon);
            view.gameObject.SetActive(true);
            return view;
        }

        public async UniTaskVoid AnimateLevelStartAsync(IReadOnlyList<TileData> activeTiles, Dictionary<int, TileView> activeTileViews, CancellationToken ct)
        {
            Sequence sequence = DOTween.Sequence();
            RegisterTween(sequence);
            
            float gridStartTime = 0.2f;

            var layers = activeTiles
                .GroupBy(t => Mathf.RoundToInt(t.visualPosition.z * 100f))
                .OrderBy(g => g.Key)
                .ToList();

            for (int i = 0; i < layers.Count; i++)
            {
                float direction = (i % 2 == 0) ? -1f : 1f;
                float offscreenOffset = direction * SpawnOffscreenOffset;

                foreach (TileData data in layers[i])
                {
                    TileView view = SpawnTile(data);
                    activeTileViews[data.tileID] = view;
                    Vector3 targetPos = view.transform.localPosition;

                    view.transform.localPosition = targetPos + new Vector3(offscreenOffset, 0, 0);
                    sequence.Insert(gridStartTime + (i * LayerAnimationStagger), view.transform.DOLocalMoveX(targetPos.x, 0.5f).SetEase(Ease.OutBack, overshoot: 1.2f));
                }
            }

            await sequence.Play()
                .AsyncWaitForCompletion()
                .AsUniTask()
                .AttachExternalCancellation(ct)
                .SuppressCancellationThrow();
                
#if UNITY_EDITOR
            Debug.Log($"[BoardAnimator] Animated {activeTiles.Count} tile visuals across {layers.Count} layers.");
#endif
        }

        public async UniTask AnimateTileToDestinationAsync(TileView t, Vector3 destination, bool returnToPool, CancellationToken ct)
        {
            try
            {
                _activeTileAnimations++;
                t.SetSortingOrder(FlightSortingOrder);

                Sequence seq = DOTween.Sequence();
                RegisterTween(seq);
                
                seq.Join(t.transform.DOMove(destination, _settings.TileRouteDuration).SetEase(Ease.InOutQuart));
                seq.Join(t.transform.DOScale(_pool.OriginalScale, _settings.TileRouteDuration).SetEase(Ease.InQuart));
                seq.Join(t.transform.DORotate(new Vector3(0f, 0f, Random.Range(-25f, 25f)), _settings.TileRouteDuration * 0.5f)
                          .SetEase(Ease.OutCubic)
                          .SetLoops(2, LoopType.Yoyo));

                await seq.AsyncWaitForCompletion()
                         .AsUniTask()
                         .AttachExternalCancellation(ct)
                         .SuppressCancellationThrow();

                if (ct.IsCancellationRequested) return;

                if (returnToPool)
                    _pool.Return(t);
            }
            finally
            {
                _activeTileAnimations--;
            }
        }

        public async UniTaskVoid PlayEndGameFeedbackAsync(bool isWin, Dictionary<int, TileView> activeTileViews, CancellationToken ct)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.Log($"[BoardAnimator] PlayEndGameFeedbackAsync started. Active tile animations: {_activeTileAnimations}");
#endif
            while (_activeTileAnimations > 0)
            {
                if (ct.IsCancellationRequested) return;
                await UniTask.Yield(ct).SuppressCancellationThrow();
            }
#if UNITY_EDITOR
            UnityEngine.Debug.Log($"[BoardAnimator] All tile animations cleared! Proceeding with visual win sequence.");
#endif
                         
            if (ct.IsCancellationRequested) return;

            if (isWin)
            {
                if (_settings.ConfettiParticles != null) _settings.ConfettiParticles.Play();
                _hapticService.OnLevelWon(ct);
            }
            else
            {
                if (_settings.RackParent != null)
                {
                    Tween shake = _settings.RackParent.DOShakePosition(_settings.RackShakeDuration, strength: new Vector3(30f, 0, 0), vibrato: 20);
                    RegisterTween(shake);
                }
            }

            await UniTask.WaitForSeconds(0.6f, cancellationToken: ct).SuppressCancellationThrow();
            if (ct.IsCancellationRequested) return;

            if (_settings.RackParent != null)
            {
                Tween drop = _settings.RackParent.DOLocalMoveY(_settings.RackOriginalLocalPos.y - DropAnimationYOffset, 0.5f).SetEase(Ease.InBack);
                RegisterTween(drop);
            }

            foreach (var kvp in activeTileViews)
            {
                _pool.Return(kvp.Value);
            }
            activeTileViews.Clear();
            
            if (!isWin)
                _hapticService.OnRackFull();
                
            _signalBus.Fire(new VisualFeedbackFinishedSignal { IsWin = isWin });
        }
    }
}
