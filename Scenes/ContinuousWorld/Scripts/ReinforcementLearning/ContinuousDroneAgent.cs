using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using DroneMovement;
using Agents;
using Unity.MLAgents.Policies;
using GridWorld;
using UnityEngine.InputSystem;
using UnityEditor;
using GridWorld.Visuals;

namespace ContinuousWorld
{
    public class ContinuousDroneAgent : Agent, IAgent
    {
        private static int AgentIdGenerator;

        [Header("References")]
        private ContinuousArea _area;
        private Rigidbody _rb;
        private DroneInputs _droneInputs;
        private DroneController _droneController;
        private DronePhysics _dronePhysics;

        [Header("Normalization")]
        private float _maxLinearVelocity;
        private float _maxAngularVelocity;

        private float _crashVelocityThreshold;

        [Header("Rewards & Penalties")]
        [SerializeField] private float goalReward = 2.0f;
        [SerializeField] private float collisionPenaltyLight = -0.1f; // Just a bump
        [SerializeField] private float collisionPenaltyCrash = -1.0f; // High speed impact
        [SerializeField] private float collisionStayPenalty = -0.01f; // Penalty per physics step while touching
        [SerializeField] private float boundaryHitPenalty = -1.0f;
        [SerializeField] private float existencePenalty = -0.001f;
        [SerializeField] private float spinPenaltyCoefficient = 0.001f;

        [Header("Settings")]
        [SerializeField] private float timeBetweenDecisionsAtInference = 0.15f;

        [Header("Safety Checks")]
        [SerializeField] private float minUprightDotProduct = 0.25f;

        private float _timeSinceDecision;
        private float _previousDistanceToGoal;

        private int _currentCollisionCount = 0;

        private BehaviorType _behaviorType;

        private ColorFlashFeedback[] _colorFlashFeedbacks;

        public int AgentId { get; private set; }
        public ContinuousArea Area => _area;

        public int GetRelevantStepCount => StepCount;

        public override void Initialize()
        {
            var settings = ContinuousWorldSettings.Instance;

            if (settings == null)
            {
                throw new MissingReferenceException("ContinuousWorldSettings instance not found in scene.");
            }
            _rb = GetComponent<Rigidbody>();
            _droneInputs = GetComponentInChildren<DroneInputs>(true);
            _droneController = GetComponent<DroneController>();
            _dronePhysics = GetComponent<DronePhysics>();
            _area = GetComponentInParent<ContinuousArea>();

            _colorFlashFeedbacks = GetComponentsInChildren<ColorFlashFeedback>();

            if (_area == null)
            {
                throw new MissingReferenceException("ContinuousAgent could not find a ContinuousWorld in parent.");
            }

            _maxAngularVelocity = _dronePhysics.EstimateMaxAngularVelocity();
            _crashVelocityThreshold = _dronePhysics.CalculateCrashVelocityThreshold();

            lock (settings) { AgentId = ++AgentIdGenerator; }

            _behaviorType = GetComponent<BehaviorParameters>().BehaviorType;
            transform.localScale = new Vector3(settings.GetUnitSize(), settings.GetUnitSize(), settings.GetUnitSize());

            if (_droneInputs != null)
            {
                // Disable direct control if agent is present in scene
                // Disabling it here removes the responsibility of passing read inputs to the DroneController from the DroneInputs script
                _droneInputs.DirectControl = false;

                // We still need the script active so it can read hardware inputs if we are playing scene in Heuristic mode
                _droneInputs.gameObject.SetActive(_behaviorType == BehaviorType.HeuristicOnly);
            }
        }

        public override void OnEpisodeBegin()
        {
            _maxLinearVelocity = _dronePhysics.CalculateTerminalVelocity();

            _droneController.ResetController();
            _rb.isKinematic = true;
            _area.ResetArea(); // Area handles teleporting agent and goal
            _rb.isKinematic = false;

            _previousDistanceToGoal = Vector3.Distance(transform.position, _area.GoalPosition);
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            // 1. Local Orientation (6 floats)
            sensor.AddObservation(transform.forward);
            sensor.AddObservation(transform.up);

            Vector3 distanceToGoal = _area.GoalPosition - transform.position;

            Vector3 areaBoundaries = _area.GetAreaBoundsHalfSize();

            // 2. Relative Position to Goal (3 floats)
            // Normalized by Area Bounds Half Size (from Area script)
            sensor.AddObservation(new Vector3(
                distanceToGoal.x / areaBoundaries.x,
                distanceToGoal.y / areaBoundaries.y,
                distanceToGoal.z / areaBoundaries.z));

            // 3. Velocities (6 floats)
            // Normalized by calculated Physics limits
            Vector3 normVel = _rb.linearVelocity / _maxLinearVelocity;

            sensor.AddObservation(Vector3.ClampMagnitude(normVel, 1.0f));

            Vector3 normAngVel = _rb.angularVelocity / _maxAngularVelocity;
            sensor.AddObservation(Vector3.ClampMagnitude(normAngVel, 1f));
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            CheckStructuralIntegrity();

            bool isTimeout = (MaxStep > 0 && StepCount >= MaxStep - 1);
            if (isTimeout)
            {
                _area.TriggerFailure();
                EndEpisode();
                return;
            }

            var continuousActions = actions.ContinuousActions;

            float throttle = (Mathf.Clamp(continuousActions[0], -1f, 1f) + 1f) / 2f;
            float pitch = Mathf.Clamp(continuousActions[1], -1f, 1f);
            float roll = Mathf.Clamp(continuousActions[2], -1f, 1f);
            float yaw = Mathf.Clamp(continuousActions[3], -1f, 1f);

           _droneController.SetControlInputs(throttle, pitch, roll, yaw);

            // Frequent Rewards
            AddReward(existencePenalty);

            // Progress Signal (Hotter/Colder)
            float currentDistance = Vector3.Distance(transform.position, _area.GoalPosition);
            float distanceChange = _previousDistanceToGoal - currentDistance;
            AddReward(distanceChange * 0.5f);

            // Stability Penalty
            float spinMagnitude = _rb.angularVelocity.magnitude;
            if (spinMagnitude > 1.0f)
            {
                AddReward(-spinMagnitude * spinPenaltyCoefficient);
            }

            _previousDistanceToGoal = currentDistance;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Goal"))
            {   
                AddReward(goalReward);
                _area.TriggerSuccess();
                EndEpisode();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            bool isBadObject = IsBadCollisionObject(collision.gameObject);

            if (!isBadObject) return;

            _currentCollisionCount++;

            // If this is the first object we are touching, start the visual loop
            if (_currentCollisionCount == 1)
            {
                foreach (var feedback in _colorFlashFeedbacks)
                {
                    feedback.StartContinuousFailure();
                }
            }

            // Logic for Hit Penalties (Instant)
            if (collision.gameObject.CompareTag("Boundary"))
            {
                AddReward(boundaryHitPenalty);
                _area.TriggerFailure();
                EndEpisode();
            }
            else if (collision.gameObject.CompareTag("Obstacle") || collision.gameObject.CompareTag("Untagged"))
            {
                float impactForce = collision.relativeVelocity.magnitude;

                if (impactForce > _crashVelocityThreshold)
                {
                    // Case: CRASH
                    AddReward(collisionPenaltyCrash);
                    _area.TriggerFailure();
                    EndEpisode();
                }
                else
                {
                    // Case: BUMP
                    AddReward(collisionPenaltyLight);
                }
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            // 1. Identify if the object is something we should be penalized for touching
            bool isBadObject = IsBadCollisionObject(collision.gameObject);
            
            if (isBadObject)
            {
                // 2. Apply a small continuous penalty
                // This encourages the drone to not just "accept" the crash, 
                // but to actively fly away from the surface.
                AddReward(collisionStayPenalty);
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            // Determine if the object exiting is "Bad"
            bool isBadObject = IsBadCollisionObject(collision.gameObject);

            if (isBadObject)
            {
                _currentCollisionCount--;

                // Safety clamp to prevent negative counts if physics goes weird
                if (_currentCollisionCount < 0) _currentCollisionCount = 0;

                // Only stop flashing if we are no longer touching ANY bad objects
                if (_currentCollisionCount == 0)
                {
                    foreach (var feedback in _colorFlashFeedbacks)
                    {
                        feedback.StopContinuousFailure();
                    }
                }
            }
        }

        private void CheckStructuralIntegrity()
        {
            // Compare Drone Up with World Up
            float tilt = Vector3.Dot(transform.up, Vector3.up);

            if (tilt < minUprightDotProduct)
            {
                Debug.LogWarning("Drone rotated too far and crashed. Threshold product: " + minUprightDotProduct);
                AddReward(collisionPenaltyCrash);
                _area.TriggerFailure();
                EndEpisode();
            }
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var continuousActions = actionsOut.ContinuousActions;

            _droneInputs.GetCurrentInputs(out float t, out float p, out float r, out float y);

            // This array will be passed straight into OnActionReceived Method

            // Index 0: Throttle. 
            // Input is [0, 1]. Action Buffer expects [-1, 1].
            continuousActions[0] = (t * 2.0f) - 1.0f;

            // Index 1, 2, 3: Pitch, Roll, Yaw
            // Input is [-1, 1]. Action Buffer expects [-1, 1].
            continuousActions[1] = p;
            continuousActions[2] = r;
            continuousActions[3] = y;
        }

        public void FixedUpdate()
        {
            if (_behaviorType != BehaviorType.HeuristicOnly)
            {
                WaitTimeInference();
            }
            else
            {
                if (Input.anyKeyDown)
                {
                    RequestDecision();
                }
            }
        }


        public void SetTransformGlobalPosition(Vector3 position)
        {
            this.transform.position = position;
        }

        public string GetAgentName()
        {
            return $"{gameObject.name} (ID: {this.AgentId}) Type: ContinuousDroneAgent";
        }

        private void WaitTimeInference()
        {
            if (Academy.Instance.IsCommunicatorOn)
            {
                RequestDecision();
            }
            else
            {
                if (_timeSinceDecision >= timeBetweenDecisionsAtInference)
                {
                    _timeSinceDecision = 0f;
                    RequestDecision();
                }
                else
                {
                    _timeSinceDecision += Time.fixedDeltaTime;
                }
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            AgentEvents.Register(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            AgentEvents.Unregister(this);
        }

        public Vector3 GetEnvironmentPosition()
        {
            return _area.GetAreaCenteredPosition();
        }

        private static bool IsBadCollisionObject(GameObject obj)
        {
            return obj.CompareTag("Boundary") ||
                   obj.CompareTag("Obstacle") ||
                   obj.CompareTag("Untagged");
        }
    }
}