using TileMatch.Controller;
using TileMatch.Model;
using TileMatch.Service;
using TileMatch.View;
using TileMatch.View.UI;
using UnityEngine;

namespace TileMatch.Installer
{
    /// <summary>
    /// Composition Root. Instantiates core services, controllers, and state.
    /// Wires them together via constructor injection. Initializes the views.
    /// Starts the level.
    /// </summary>
    public class GameplayInstaller : MonoBehaviour
    {
        [SerializeField] private InputView  _inputView;
        [SerializeField] private VisualView _visualView;

        [Header("UI Views")]
        [SerializeField] private MainMenuView     _mainMenuView;
        [SerializeField] private GameplayHUDView  _gameplayHUDView;
        [SerializeField] private ResultScreenView _resultScreenView;

        // Core
        private RuntimeGameState _state;
        private SignalBus        _signalBus;
        private HapticService    _hapticService;

        // Controllers
        private LevelController    _levelController;
        private OrderController    _orderController;
        private RackController     _rackController;
        private GameplayController _gameplayController;

        private void Awake()
        {
            Application.targetFrameRate = 120; // Force 120 FPS for mobile builds
            
            // 1. Data & Services
            _state         = new RuntimeGameState();
            _signalBus     = new SignalBus();
            _hapticService = new HapticService();

            // 2. Controllers (pure C#, no MonoBehaviour lifecycle, wired via constructor)
            _levelController    = new LevelController(_state, _signalBus);
            _orderController    = new OrderController(_state, _signalBus);
            _rackController     = new RackController(_state, _signalBus);
            _gameplayController = new GameplayController(_state, _signalBus);

            // 3. View Initialization (MonoBehaviours already in scene, just need references)
            _inputView.Initialize(_signalBus);
            _visualView.Initialize(_signalBus, _hapticService);

            if (_mainMenuView != null)     _mainMenuView.Initialize(_signalBus);
            if (_gameplayHUDView != null)  _gameplayHUDView.Initialize(_signalBus);
            if (_resultScreenView != null) _resultScreenView.Initialize(_signalBus);

            // 4. Init the level sequence (Starts in Menu state)
            LevelData[] allLevels = Resources.LoadAll<LevelData>("Levels");
            if (allLevels != null && allLevels.Length > 0)
            {
                // Ensure they are ordered correctly by name (LevelData_01, LevelData_02, etc.)
                System.Array.Sort(allLevels, (a, b) => string.Compare(a.name, b.name));
                _gameplayController.InitSequence(allLevels);
            }
            else
            {
                Debug.LogError("[GameplayInstaller] No LevelData assets found in Resources/Levels! Cannot start the game.");
            }
        }

        private void OnDestroy()
        {
            _levelController?.Dispose();
            _orderController?.Dispose();
            _rackController?.Dispose();
            _gameplayController?.Dispose();
        }
    }
}
