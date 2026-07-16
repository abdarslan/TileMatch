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
        private HapticService _hapticService;
        private bool _hasClickedPlay;

        public void Initialize(SignalBus signalBus, HapticService hapticService)
        {
            _signalBus = signalBus;
            _hapticService = hapticService;

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
            if (signal.NewState == GameplayController.GameState.Menu)
            {
                _hasClickedPlay = false;
                if (_playButton != null) _playButton.interactable = true;
            }
        }

        private void OnPlayClicked()
        {
            if (_hasClickedPlay) return; // Prevent double-clicks
            _hasClickedPlay = true;

            _hapticService?.OnUIButtonTapped();

            Debug.Log("[MainMenuView] Play Button Clicked! Firing StartGameRequestSignal.");
            // Disable button interaction immediately so player can't click again
            _playButton.interactable = false;
            _signalBus.Fire(new StartGameRequestSignal());
        }
    }
}
