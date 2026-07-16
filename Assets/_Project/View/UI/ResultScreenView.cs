using TMPro;
using TileMatch.Controller;
using TileMatch.Service;
using TileMatch.Signal.UI;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace TileMatch.View.UI
{
    public class ResultScreenView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _resultText;
        
        [Header("Containers")]
        [SerializeField] private GameObject _winButtons;
        [SerializeField] private GameObject _loseButtons;

        [Header("Buttons")]
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _giveUpButton;

        private SignalBus _signalBus;

        public void Initialize(SignalBus signalBus)
        {
            _signalBus = signalBus;
            
            if (_continueButton != null)
            {
                _continueButton.onClick.RemoveAllListeners();
                _continueButton.onClick.AddListener(OnContinueClicked);
            }

            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveAllListeners();
                _restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (_giveUpButton != null)
            {
                _giveUpButton.onClick.RemoveAllListeners();
                _giveUpButton.onClick.AddListener(OnGiveUpClicked);
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
            if (signal.NewState == GameplayController.GameState.Won)
            {
                gameObject.SetActive(true);
                if (_resultText != null) _resultText.text = "You Won!";
                if (_winButtons != null) _winButtons.SetActive(true);
                if (_loseButtons != null) _loseButtons.SetActive(false);
            }
            else if (signal.NewState == GameplayController.GameState.Failed)
            {
                gameObject.SetActive(true);
                if (_resultText != null) _resultText.text = "You Lost!";
                if (_winButtons != null) _winButtons.SetActive(false);
                if (_loseButtons != null) _loseButtons.SetActive(true);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private async void OnContinueClicked()
        {
            if (_continueButton != null)
            {
                _continueButton.interactable = false;
                await PlayClickAnimation(_continueButton.transform);
            }
            _signalBus.Fire(new ReturnToMenuRequestSignal());
        }

        private async void OnRestartClicked()
        {
            if (_restartButton != null)
            {
                _restartButton.interactable = false;
                await PlayClickAnimation(_restartButton.transform);
            }
            _signalBus.Fire(new RestartLevelRequestSignal());
        }

        private async void OnGiveUpClicked()
        {
            if (_giveUpButton != null)
            {
                _giveUpButton.interactable = false;
                await PlayClickAnimation(_giveUpButton.transform);
            }
            _signalBus.Fire(new ReturnToMenuRequestSignal());
        }

        private async UniTask PlayClickAnimation(Transform target)
        {
            Vector3 startScale = target.localScale;
            Vector3 targetScale = startScale * 0.99f;
            var pressSequence = DOTween.Sequence();
            pressSequence.Append(target.DOScale(targetScale, 0.1f));
            pressSequence.Join(target.DOMoveY(target.position.y - 0.01f, 0.1f));
            await pressSequence.AsyncWaitForCompletion().AsUniTask();
            
            if (target != null)
            {
                target.DOScale(startScale, 0.1f);
                target.DOMoveY(target.position.y + 0.01f, 0.1f);
            }
        }
    }
}
