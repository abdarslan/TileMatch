using TileMatch.Controller;
using TileMatch.Service;
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

        public void Initialize(SignalBus signalBus)
        {
            _signalBus = signalBus;
            
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
            gameObject.SetActive(signal.NewState == GameplayController.GameState.Playing);
        }

        private void OnRestartClicked()
        {
            _signalBus.Fire(new RestartLevelRequestSignal());
        }

        private void OnMenuClicked()
        {
            _signalBus.Fire(new ReturnToMenuRequestSignal());
        }
    }
}
