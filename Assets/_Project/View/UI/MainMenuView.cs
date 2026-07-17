using Cysharp.Threading.Tasks;
using DG.Tweening;
using TileMatch.Model;
using TileMatch.Service;
using TileMatch.Signal.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TileMatch.View.UI
{
    /// <summary>
    /// UI View for the main menu. Listens for the Play button click to dispatch a
    /// <see cref="StartGameRequestSignal"/> and toggles its own visibility based on <see cref="GameState"/>.
    /// </summary>
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

#if UNITY_EDITOR
            Debug.Log($"[MainMenuView] Initialize called. PlayButton is {(_playButton != null ? "Assigned" : "NULL")}");
#endif

            if (_playButton != null)
            {
                _playButton.onClick.RemoveAllListeners();
                _playButton.onClick.AddListener(OnPlayClicked);
#if UNITY_EDITOR
                Debug.Log("[MainMenuView] Successfully wired OnPlayClicked listener!");
#endif
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
            gameObject.SetActive(signal.NewState == GameState.Menu);
            if (signal.NewState == GameState.Menu)
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
#if UNITY_EDITOR
            Debug.Log("[MainMenuView] Play Button Clicked! Firing StartGameRequestSignal.");
#endif
            // Disable button interaction immediately so player can't click again
            _playButton.interactable = false;
            _signalBus.Fire(new StartGameRequestSignal());
        }
    }
}
