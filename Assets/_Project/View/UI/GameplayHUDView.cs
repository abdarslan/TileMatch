using TileMatch.Model;
using TileMatch.Service;
using TileMatch.Signal;
using TileMatch.Signal.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TileMatch.View.UI
{
    public class GameplayHUDView : MonoBehaviour
    {
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _menuButton;
        
        private SignalBus _signalBus;
        private HapticService _hapticService;

        public void Initialize(SignalBus signalBus, HapticService hapticService)
        {
            _signalBus = signalBus;
            _hapticService = hapticService;
            
            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveAllListeners();
                _restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (_menuButton != null)
            {
                _menuButton.onClick.RemoveAllListeners();
                _menuButton.onClick.AddListener(OnMenuClicked);
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
            if (signal.NewState == GameState.Playing)
            {
                gameObject.SetActive(true);
            }
            else if (signal.NewState == GameState.Menu)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnVisualFeedbackFinished(VisualFeedbackFinishedSignal signal)
        {
            gameObject.SetActive(false);
        }

        private void OnRestartClicked()
        {
            _hapticService?.OnUIButtonTapped();
            _signalBus.Fire(new RestartLevelRequestSignal());
        }

        private void OnMenuClicked()
        {
            _hapticService?.OnUIButtonTapped();
            _signalBus.Fire(new ReturnToMenuRequestSignal());
        }
    }
}
