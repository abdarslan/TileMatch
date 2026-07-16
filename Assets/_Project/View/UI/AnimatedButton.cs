using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace TileMatch.View.UI
{
    public class AnimatedButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Animation Settings")]
        [Tooltip("The graphic you want to scale/move. Defaults to this object if left null.")]
        [SerializeField] private Transform _targetTransform;
        [SerializeField] private float _pressScale = 0.95f;
        [SerializeField] private float _hoverScale = 1.05f;
        [SerializeField] private float _animationDuration = 0.1f;
        [SerializeField] private float _pressMoveY = -0.03f;

        private Vector3 _originalScale;
        private Vector3 _originalPosition;
        private Tween _currentScaleTween;
        private Tween _currentMoveTween;
        private bool _isInitialized = false;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_isInitialized) return;

            if (_targetTransform == null) 
                _targetTransform = transform;
            
            _originalScale = _targetTransform.localScale;
            _originalPosition = _targetTransform.localPosition;
            
            _isInitialized = true;
        }

        private void OnEnable()
        {
            Initialize();
            // Reset to original state when re-enabled
            if (_targetTransform != null)
            {
                _targetTransform.localScale = _originalScale;
                _targetTransform.localPosition = _originalPosition;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            KillTweens();
            _currentScaleTween = _targetTransform.DOScale(_originalScale * _pressScale, _animationDuration);
            _currentMoveTween = _targetTransform.DOLocalMoveY(_originalPosition.y + _pressMoveY, _animationDuration);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            KillTweens();
            _currentScaleTween = _targetTransform.DOScale(_originalScale, _animationDuration).SetEase(Ease.OutBack);
            _currentMoveTween = _targetTransform.DOLocalMoveY(_originalPosition.y, _animationDuration).SetEase(Ease.OutBack);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            KillTweens();
            _currentScaleTween = _targetTransform.DOScale(_originalScale * _hoverScale, _animationDuration);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            KillTweens();
            _currentScaleTween = _targetTransform.DOScale(_originalScale, _animationDuration);
            _currentMoveTween = _targetTransform.DOLocalMoveY(_originalPosition.y, _animationDuration);
        }

        private void KillTweens()
        {
            _currentScaleTween?.Kill();
            _currentMoveTween?.Kill();
        }

        private void OnDisable()
        {
            KillTweens();
        }
    }
}
