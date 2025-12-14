using InputActions;
using InputCore;
using UnityEngine;

namespace DroneMovement
{
    public class DroneInputs : MonoBehaviour, IInputControllable
    {
        [Header("Settings")]
        [SerializeField] private float throttleSensitivity = 1.0f;

        [SerializeField] private bool directControl = true;

        [Header("Drone Controller Reference")]
        [SerializeField] private DroneController _droneController;
        
        private DroneInputActions _inputActions;
        private float _currentThrottle, _currentPitch, _currentRoll, _currentYaw;

        [Header("Input Sensitivity")]
        public float throttleSpeed = 1.0f;


        public bool DirectControl
        {
            get => directControl;
            set => directControl = value;
        }

        private void Awake()
        {
            if (_droneController == null)
            {
                _droneController = GetComponent<DroneController>();
            }

            _inputActions = new DroneInputActions();
            GameInputManager.Instance.SwitchControlTo(this);
        }

        private void Update()
        {
            ReadInputs();

            if (directControl && _droneController != null)
            {
                _droneController.SetControlInputs(
                    _currentThrottle,
                    _currentPitch,
                    _currentRoll,
                    _currentYaw
                );
            }
        }

        private void ReadInputs()
        {
            float throttleDelta = _inputActions.Flight.Throttle.ReadValue<float>();

            if (Mathf.Abs(throttleDelta) > 0.01f)
            {
                _currentThrottle += throttleDelta * throttleSensitivity * Time.deltaTime;
            }
            _currentThrottle = Mathf.Clamp01(_currentThrottle);

            Vector2 moveInput = _inputActions.Flight.RollPitch.ReadValue<Vector2>();
            _currentPitch = moveInput.y;
            _currentRoll = moveInput.x;
            _currentYaw = _inputActions.Flight.Yaw.ReadValue<float>();
        }

        public void GetCurrentInputs(out float t, out float p, out float r, out float y)
        {
            t = _currentThrottle;
            p = _currentPitch;
            r = _currentRoll;
            y = _currentYaw;
        }

        public void EnableControl()
        {
            _inputActions.Flight.Enable();
        }

        public void DisableControl()
        {
            _inputActions.Flight.Disable();
        }
    }
}