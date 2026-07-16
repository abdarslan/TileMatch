using Cysharp.Threading.Tasks;
using DG.Tweening;
using TileMatch.Controller;
using TileMatch.Service;
using TileMatch.Signal.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TileMatch.View.UI
{
    public class MainMenuView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button _playButton;
        [SerializeField] private Transform _buttonBg;
        private SignalBus _signalBus;

        public void Initialize(SignalBus signalBus)
        {
            _signalBus = signalBus;


            Debug.Log($"[MainMenuView] Initialize called. PlayButton is {(_playButton != null ? "Assigned" : "NULL")}");

            if (_playButton != null)
            {
                _playButton.onClick.RemoveAllListeners();
                _playButton.onClick.AddListener(OnPlayClicked);
                Debug.Log("[MainMenuView] Successfully wired OnPlayClicked listener!");
            }
            else
            {
                Debug.LogError("[MainMenuView] PLAY BUTTON IS NULL! Please assign it in the Inspector!");
            }

            _signalBus.Subscribe<GameStateChangedSignal>(OnGameStateChanged);
        }

        private void OnDestroy()
        {
            if (_signalBus != null)
            {
                _signalBus.Unsubscribe<GameStateChangedSignal>(OnGameStateChanged);
            }
        }

        private void OnGameStateChanged(GameStateChangedSignal signal)
        {
            gameObject.SetActive(signal.NewState == GameplayController.GameState.Menu);
        }

        private async void OnPlayClicked()
        {
            Debug.Log("[MainMenuView] Play Button Clicked! Firing StartGameRequestSignal.");
            await PlayClickAnimation();
            _signalBus.Fire(new StartGameRequestSignal());
        }

        private async UniTask PlayClickAnimation()
        {
            Vector3 startScale = _buttonBg.localScale;
            Vector3 targetScale = startScale * 0.99f;
            var pressSequence = DOTween.Sequence();
            pressSequence.Append(_buttonBg.DOScale(targetScale, 0.1f));
            pressSequence.Join(_buttonBg.DOMoveY(_buttonBg.position.y - 0.01f, 0.1f));
            await pressSequence.AsyncWaitForCompletion().AsUniTask();
            _buttonBg.DOScale(startScale, 0.1f);
            _buttonBg.DOMoveY(_buttonBg.position.y + 0.01f, 0.1f);
        }
    }
}
