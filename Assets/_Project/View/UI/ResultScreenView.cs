using TMPro;
using TileMatch.Model;
using TileMatch.Service;
using TileMatch.Signal;
using TileMatch.Signal.UI;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace TileMatch.View.UI
{
    /// <summary>
    /// UI View for the end-game result screen (Win/Lose). Subscribes to 
    /// <see cref="VisualFeedbackFinishedSignal"/> to pop up *after* the board 
    /// has finished animating its win/lose sequences.
    /// </summary>
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
        private HapticService _hapticService;

        public void Initialize(SignalBus signalBus, HapticService hapticService)
        {
            _signalBus = signalBus;
            _hapticService = hapticService;
            
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
            _signalBus.Subscribe<VisualFeedbackFinishedSignal>(OnVisualFeedbackFinished);
        }

        private void OnDestroy()
        {
            if (_signalBus != null)
            {
                _signalBus.Unsubscribe<GameStateChangedSignal>(OnGameStateChanged);
                _signalBus.Unsubscribe<VisualFeedbackFinishedSignal>(OnVisualFeedbackFinished);
            }
        }

        private void OnGameStateChanged(GameStateChangedSignal signal)
        {
            if (signal.NewState == GameState.Menu || signal.NewState == GameState.Playing)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnVisualFeedbackFinished(VisualFeedbackFinishedSignal signal)
        {
            gameObject.SetActive(true);
            
            // Pop-up animation for juiciness
            transform.localScale = Vector3.one * 0.8f;
            transform.DOScale(Vector3.one, 0.4f).SetEase(DG.Tweening.Ease.OutBack);
            
            if (signal.IsWin)
            {
                if (_resultText != null) _resultText.text = "You Won!";
                if (_winButtons != null) _winButtons.SetActive(true);
                if (_loseButtons != null) _loseButtons.SetActive(false);
                
                // Reset button interactability
                if (_continueButton != null) _continueButton.interactable = true;
            }
            else
            {
                if (_resultText != null) _resultText.text = "You Lost!";
                if (_winButtons != null) _winButtons.SetActive(false);
                if (_loseButtons != null) _loseButtons.SetActive(true);
                
                // Reset button interactability
                if (_restartButton != null) _restartButton.interactable = true;
                if (_giveUpButton != null) _giveUpButton.interactable = true;
            }
        }

        private void OnContinueClicked()
        {
            if (_continueButton != null) _continueButton.interactable = false;
            _hapticService?.OnUIButtonTapped();
            _signalBus.Fire(new ReturnToMenuRequestSignal());
        }

        private void OnRestartClicked()
        {
            if (_restartButton != null) _restartButton.interactable = false;
            _hapticService?.OnUIButtonTapped();
            _signalBus.Fire(new RestartLevelRequestSignal());
        }

        private void OnGiveUpClicked()
        {
            if (_giveUpButton != null) _giveUpButton.interactable = false;
            _hapticService?.OnUIButtonTapped();
            _signalBus.Fire(new ReturnToMenuRequestSignal());
        }
    }
}
