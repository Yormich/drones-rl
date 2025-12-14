using UnityEngine;

namespace DroneMovement
{
    [RequireComponent(typeof(DronePhysics))]
    public class DroneController : MonoBehaviour
    {
        [Header("Stabilization")]
        [SerializeField] private PIDController pitchPID;
        [SerializeField] private PIDController rollPID;
        [SerializeField] private PIDController yawPID;

        [Header("Control Behavior")]
        [SerializeField] private float maxPitchAngle = 45f;
        [SerializeField] private float maxYawRate = 5.0f;

        private DronePhysics _physics;
        private float _inputThrottle;
        private float _inputPitch;
        private float _inputRoll;
        private float _inputYaw;

        private void Awake()
        {
            _physics = GetComponent<DronePhysics>();
        }

        /// <summary>
        /// Updates the target inputs.
        /// </summary>
        public void SetControlInputs(float throttle, float pitch, float roll, float yaw)
        {
            _inputThrottle = Mathf.Clamp01(throttle);
            _inputPitch = Mathf.Clamp(pitch, -1f, 1f);
            _inputRoll = Mathf.Clamp(roll, -1f, 1f);
            _inputYaw = Mathf.Clamp(yaw, -1f, 1f);
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            // 1. Get Current State (Rotation)
            Vector3 currentEuler = transform.localEulerAngles;

            // 2. PID Targets
            // We use DeltaAngle to calculate error. This handles the 0/360 wrap automatically.
            // Target is simply 0 + input offset.
            float targetPitch = _inputPitch * maxPitchAngle;
            float pitchError = Mathf.DeltaAngle(currentEuler.x, targetPitch);

            // Roll is inverted in Unity frame for standard drone controls usually, 
            // but we calculate error: Target - Current.
            float targetRoll = -_inputRoll * maxPitchAngle;
            float rollError = Mathf.DeltaAngle(currentEuler.z, targetRoll);


            // Yaw is Rate-based (Acro style)
            float currentYawRate = transform.InverseTransformDirection(GetComponent<Rigidbody>().angularVelocity).y;
            float targetYawRate = _inputYaw * maxYawRate;
            float yawError = targetYawRate - currentYawRate;

            // 3. Compute PID
            float pitchCmd = pitchPID.Update(pitchError, dt);
            float rollCmd = rollPID.Update(rollError, dt);
            float yawCmd = yawPID.Update(yawError, dt);


            // 4. Send to Physics (Normalize PID output -100 to -1..1 range)
            _physics.ApplyMotorForces(
                _inputThrottle,
                pitchCmd / pitchPID.MaxOutput,
                -rollCmd / rollPID.MaxOutput, // Invert output to match physics direction if needed
                yawCmd / yawPID.MaxOutput
            );
        }

        public void ResetController()
        {
            pitchPID.Reset();
            rollPID.Reset();
            yawPID.Reset();
            _physics.ResetPhysics();
        }
    }
}