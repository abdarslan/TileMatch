using TileMatch.Controller;
using TileMatch.Model;
using TileMatch.Service;
using TileMatch.View;
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
        [SerializeField] private LevelData  _targetLevel;
        [SerializeField] private InputView  _inputView;
        [SerializeField] private VisualView _visualView;

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

            // 4. Start the level
            if (_targetLevel != null)
            {
                _gameplayController.StartLevel(_targetLevel);
            }
            else
            {
                Debug.LogError("[GameplayInstaller] Target Level is missing! Cannot start the game.");
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
