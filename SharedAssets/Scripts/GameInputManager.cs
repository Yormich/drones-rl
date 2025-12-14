using InputCore;
using UnityEngine;


namespace InputCore
{
    [DefaultExecutionOrder(-50)]
    public class GameInputManager : MonoBehaviour
    {
        public static GameInputManager Instance { get; private set; }

        [Header("Defaults")]
        [SerializeField] private MonoBehaviour defaultEnableScript;

        private IInputControllable _currentController;
        private IInputControllable _mainController;

        private void Awake()
        {
            Instance = this;

            // Verify the camera implements the interface
            if (defaultEnableScript is IInputControllable mainInputControllable)
            {
                _mainController = mainInputControllable;
            }
            else
            {
                Debug.LogError("Main System does not implement IInputControllable");
            }
        }

        private void Start()
        {
            SwitchControlTo(_mainController);
        }

        public void SwitchControlTo(IInputControllable newController)
        {
            if (newController == null)
            {
                return;
            }

            if (_currentController != null)
            {
                _currentController.DisableControl();
            }
            _currentController = newController;

            _currentController?.EnableControl();
        }
    }

}