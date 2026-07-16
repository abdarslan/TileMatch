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

        private void OnPlayClicked()
        {
            Debug.Log("[MainMenuView] Play Button Clicked! Firing StartGameRequestSignal.");
            _signalBus.Fire(new StartGameRequestSignal());
        }
    }
}
