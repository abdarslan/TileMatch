using TileMatch.Controller;
using TileMatch.Service;
using TileMatch.Signal.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TileMatch.View.UI
{
    public class MainMenuView : MonoBehaviour
    {
        [SerializeField] private Button _playButton;
        
        private SignalBus _signalBus;

        public void Initialize(SignalBus signalBus)
        {
            _signalBus = signalBus;
            
            if (_playButton != null)
            {
                _playButton.onClick.RemoveAllListeners();
                _playButton.onClick.AddListener(OnPlayClicked);
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

        private void OnPlayClicked()
        {
            _signalBus.Fire(new StartGameRequestSignal());
        }
    }
}
