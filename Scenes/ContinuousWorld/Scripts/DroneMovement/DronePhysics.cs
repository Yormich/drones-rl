using UnityEngine;

namespace DroneMovement
{
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(Rigidbody))]
    public class DronePhysics : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private float targetTWR = 2.0f; // Thrust-to-Weight Ratio
        [SerializeField] private float torqueCoefficient = 0.02f;
        [SerializeField] private float dragCoefficient = 0.5f;


        [Header("Rotors (X-Config)")]
        [SerializeField] private Transform rotorFL; // Clockwise
        [SerializeField] private Transform rotorFR; // Counter-Clockwise
        [SerializeField] private Transform rotorRL; // Counter-Clockwise
        [SerializeField] private Transform rotorRR; // Clockwise

        [Header("Structural Integrity")]
        [Tooltip("The height (in meters) from which a fall would be considered fatal/crashing.")]
        [SerializeField] private float fatalDropHeight = 2.0f;

        private Rigidbody _rb;
        private float _maxMotorForce;
        private float _hoverForcePerMotor;

        private Transform[] _rotors;
        private int[] _rotorDirections; // 1 = CCW, -1 = CW

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            InitializeRotors();
            CalculatePhysicsLimits();
            ConfigureRigidBody();
        }

        private void InitializeRotors()
        {
            _rotors = new[] { rotorFL, rotorFR, rotorRL, rotorRR };
            // Standard X-Config spin directions relative to Up
            _rotorDirections = new[] { -1, 1, 1, -1 };
        }

        private void CalculatePhysicsLimits()
        {
            float gravity = Physics.gravity.magnitude;
            float totalWeight = _rb.mass * gravity;

            _hoverForcePerMotor = totalWeight / 4.0f;
            _maxMotorForce = (totalWeight * targetTWR) / 4.0f;
        }

        private void ConfigureRigidBody()
        {
            _rb.linearDamping = 0.1f;
            _rb.angularDamping = 0.5f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void FixedUpdate()
        {
            ApplyAerodynamicDrag();
        }

        /// <summary>
        /// Main entry point for the controller.
        /// Inputs expected to be Normalized: Throttle [0..1], P/R/Y [-1..1]
        /// </summary>
        public void ApplyMotorForces(float throttleNorm, float pitchNorm, float rollNorm, float yawNorm)
        {
            // 1. Calculate Total Thrust based on Hover-Centric curve
            float requestedThrustTotal;
            float totalMax = _maxMotorForce * 4.0f;
            float totalHover = _hoverForcePerMotor * 4.0f;


            if (throttleNorm <= 0.5f)
            {
                requestedThrustTotal = Mathf.Lerp(0, totalHover, throttleNorm * 2.0f);
            }
            else
            {
                requestedThrustTotal = Mathf.Lerp(totalHover, totalMax, (throttleNorm - 0.5f) * 2.0f);
            }

            float baseThrust = requestedThrustTotal / 4.0f;

            // 2. Mix Forces (X-Configuration)
            // Limit control authority to 25% of max force to ensure headroom
            float authority = _maxMotorForce * 0.25f;


            float pForce = pitchNorm * authority;
            float rForce = rollNorm * authority;
            float yForce = yawNorm * authority;

            // Mixing Matrix
            float fl = baseThrust + rForce - pForce - yForce;
            float fr = baseThrust - rForce - pForce + yForce;
            float rl = baseThrust + rForce + pForce + yForce;
            float rr = baseThrust - rForce + pForce - yForce;

            ApplyForceToProp(0, fl);
            ApplyForceToProp(1, fr);
            ApplyForceToProp(2, rl);
            ApplyForceToProp(3, rr);
        }

        private void ApplyForceToProp(int index, float force)
        {
            force = Mathf.Clamp(force, 0, _maxMotorForce);
            Transform t = _rotors[index];
            int dir = _rotorDirections[index];

            // Linear Thrust
            _rb.AddForceAtPosition(t.up * force, t.position);

            // Angular Torque (Action-Reaction)
            // If prop spins CW (-1), body reacts CCW (+Up)
            float torque = force * torqueCoefficient * -dir;
            _rb.AddRelativeTorque(Vector3.up * torque);
        }

        private void ApplyAerodynamicDrag()
        {
            Vector3 velocity = _rb.linearVelocity;
            float speed = velocity.magnitude;
            if (speed > Mathf.Epsilon)
            {
                // Drag equation: F = -0.5 * rho * v^2 * Cd * A. 
                // Simplified here to: -0.5 * v^2 * Coeff
                Vector3 dragForce = 0.5f * dragCoefficient * speed * speed * -velocity.normalized;
                _rb.AddForce(dragForce);
            }
        }

        public float CalculateTerminalVelocity()
        {
            float totalMaxThrust = _maxMotorForce * 4.0f;

            if (dragCoefficient <= Mathf.Epsilon) return 100f; // Arbitrary high cap

            // Derived from drag formula: F = 0.5 * drag * v^2
            // v^2 = F / (0.5 * drag)
            // v = Sqrt( 2 * F / drag )
            float vSquared = (2.0f * totalMaxThrust) / dragCoefficient;
            return Mathf.Sqrt(vSquared);
        }

        public float EstimateMaxAngularVelocity()
        {
            return 15.0f;
        }

        public float CalculateCrashVelocityThreshold()
        {
            // v = sqrt(2 * g * h)
            float gravity = Physics.gravity.magnitude;
            return Mathf.Sqrt(2f * gravity * fatalDropHeight);
        }

        public void SetRelevantAerodynamicDrag(float dragCoefficient)
        {
            this.dragCoefficient = dragCoefficient;
        }

        public void ResetPhysics()
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }
}