using TMPro;
using TileMatch.Controller;
using TileMatch.Service;
using TileMatch.Signal.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TileMatch.View.UI
{
    public class ResultScreenView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _menuButton;
        
        private SignalBus _signalBus;

        public void Initialize(SignalBus signalBus)
        {
            _signalBus = signalBus;
            
            if (_nextButton != null)
            {
                _nextButton.onClick.RemoveAllListeners();
                _nextButton.onClick.AddListener(OnNextClicked);
            }

            if (_retryButton != null)
            {
                _retryButton.onClick.RemoveAllListeners();
                _retryButton.onClick.AddListener(OnRetryClicked);
            }

            if (_menuButton != null)
            {
                _menuButton.onClick.RemoveAllListeners();
                _menuButton.onClick.AddListener(OnMenuClicked);
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
                if (_titleText != null) _titleText.text = "Victory!";
                if (_nextButton != null) _nextButton.gameObject.SetActive(true);
                if (_retryButton != null) _retryButton.gameObject.SetActive(false);
            }
            else if (signal.NewState == GameplayController.GameState.Failed)
            {
                gameObject.SetActive(true);
                if (_titleText != null) _titleText.text = "Game Over!";
                if (_nextButton != null) _nextButton.gameObject.SetActive(false);
                if (_retryButton != null) _retryButton.gameObject.SetActive(true);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void OnNextClicked()
        {
            _signalBus.Fire(new NextLevelRequestSignal());
        }

        private void OnRetryClicked()
        {
            _signalBus.Fire(new RestartLevelRequestSignal());
        }

        private void OnMenuClicked()
        {
            _signalBus.Fire(new ReturnToMenuRequestSignal());
        }
    }
}
