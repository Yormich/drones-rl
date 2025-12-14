using Agents;
using GridWorld.Metrics;
using System;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using Unity.VisualScripting;
using UnityEngine;


namespace GridWorld
{
    public class Grid3DAgent : Agent, IAgent
    {
        private static int AgentIdGenerator = 0;

        [Header("Settings")]
        [SerializeField] private GridArea3D area;

        [Tooltip("If true, the agent cannot try to walk into walls/obstacles. Speeds up training.")]
        [SerializeField] private bool maskActions = true;

        [SerializeField] private int maxStepStatic = 0;
        [SerializeField] private float breathingAreaModifier = 1.5f;
        [SerializeField] private float baseExistencePenalty = -0.005f;
        [SerializeField] private float goalReward = 1.0f;
        [SerializeField] private float obstacleHitPunishment = -0.005f;
        [SerializeField] private float wallHitPunishment = -0.005f;

        [SerializeField] private float timeBetweenDecisionsAtInference = 0.15f;
        private float _timeSinceDecision;
        private float _previousDistanceToGoal;

        private RayPerceptionSensorComponent3D[] _sensors;
        private Agent3DMetricCollector _metrics;

        private readonly Vector3Int[] actionSet = new Vector3Int[]
        {
            Vector3Int.zero, // Do Nothing
            Vector3Int.forward, // Forward
            Vector3Int.back, // Backward
            Vector3Int.right, // Right
            Vector3Int.left, // Left
            Vector3Int.up, // Up
            Vector3Int.down, // Down
        };


        private Vector3Int _currentEnvironmentPos;
        private Vector3Int _goalGridPos;

        private BehaviorType _behaviorType;

        private int StepCountHeuristic;
        private int MaxStepHeuristic;

        public Vector3Int[] ActionSet => actionSet;

        public GridArea3D Area => area;

        public int AgentId { get; private set; }

        public Agent3DMetricCollector Metrics => _metrics;

        public int GetRelevantStepCount => _behaviorType == BehaviorType.HeuristicOnly ? StepCountHeuristic : StepCount;

        public override void Initialize()
        {
            Grid3DSettings settings = Grid3DSettings.Instance;
            _metrics = GetComponent<Agent3DMetricCollector>(); 
            if (settings == null)
            {
                throw new MissingReferenceException("Grid3D Agent couldn't access environment settings instance of Grid3DSettings class");
            }

            lock (settings)
            {
                this.AgentId = ++AgentIdGenerator;
            }

            if (area == null)
            {
                area = GetComponentInParent<GridArea3D>();

                if (area == null)
                    throw new MissingReferenceException("Grid3DAgent could not find a GridArea3D in parent.");
            }

            _behaviorType = GetComponent<BehaviorParameters>().BehaviorType;
            _sensors = GetComponentsInChildren<RayPerceptionSensorComponent3D>();
            transform.localScale = new Vector3(settings.GetUnitSize(), settings.GetUnitSize(), settings.GetUnitSize());
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var discreteActions = actionsOut.DiscreteActions;
            discreteActions[0] = 0;
            if (this._behaviorType != BehaviorType.HeuristicOnly)
            {
                return;
            }

            if (Input.GetKey(KeyCode.W)) discreteActions[0] = 2;
            if (Input.GetKey(KeyCode.S)) discreteActions[0] = 1;
            if (Input.GetKey(KeyCode.A)) discreteActions[0] = 4;
            if (Input.GetKey(KeyCode.D)) discreteActions[0] = 3;
            if (Input.GetKey(KeyCode.Q)) discreteActions[0] = 5;
            if (Input.GetKey(KeyCode.E)) discreteActions[0] = 6;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            AddDistanceSizeBasedObservations(sensor);

            Vector3 normalizedPos = new Vector3(
                (float)_currentEnvironmentPos.x / area.CurrentEnvironmentSize.x,
                (float)_currentEnvironmentPos.y / area.CurrentEnvironmentSize.y,
                (float)_currentEnvironmentPos.z / area.CurrentEnvironmentSize.z
            );
            sensor.AddObservation(normalizedPos);
        }

        private void AddDistanceSizeBasedObservations(VectorSensor sensor)
        {
            Vector3Int environmentSize = area.CurrentEnvironmentSize;
            Vector3 distanceToGoal = _goalGridPos - _currentEnvironmentPos;

            // Avoid division by zero if size is 1
            float divX = environmentSize.x > 1 ? environmentSize.x - 1f : 1f;
            float divY = environmentSize.y > 1 ? environmentSize.y - 1f : 1f;
            float divZ = environmentSize.z > 1 ? environmentSize.z - 1f : 1f;

            sensor.AddObservation(distanceToGoal.x / divX);
            sensor.AddObservation(distanceToGoal.y / divY);
            sensor.AddObservation(distanceToGoal.z / divZ);
        }

        private void AddDistanceRelativeObservations(VectorSensor sensor)
        {
            Vector3 distanceToGoal = _goalGridPos - _currentEnvironmentPos;
            

            float maxDist = Mathf.Max(Mathf.Abs(distanceToGoal.x), Mathf.Abs(distanceToGoal.y), Mathf.Abs(distanceToGoal.z));

            // safety check to prevent division by zero
            if (maxDist < 0.0001f)
            {
                maxDist = 1.0f;
            }

            // observations are in range [-1.0f, 1.0f]
            sensor.AddObservation(distanceToGoal.x / maxDist);
            sensor.AddObservation(distanceToGoal.y / maxDist);
            sensor.AddObservation(distanceToGoal.z / maxDist);
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            if (!this.maskActions)
            {
                return;
            }

            for (int i = 1; i < actionSet.Length; i++)
            {
                Vector3Int targetPos = _currentEnvironmentPos + actionSet[i];

                if (!area.IsPositionFree(targetPos))
                {
                    actionMask.SetActionEnabled(0, i, false);
                }
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            bool isTimeout = (MaxStep > 0 && StepCount >= MaxStep - 1);


            if (_behaviorType == BehaviorType.HeuristicOnly && this.StepCountHeuristic >= this.MaxStepHeuristic)
            {
                isTimeout = true;
            }

            if (isTimeout)
            {
                area.TriggerFailure();
            }

            Vector3Int prevPos = _currentEnvironmentPos;

            float currentStepReward = 0f;
            
            currentStepReward += baseExistencePenalty;


            // one possible branch responsible for movement 
            int actionIndex = actions.DiscreteActions[0];
            this.StepCountHeuristic++;

            if (_behaviorType == BehaviorType.HeuristicOnly && this.StepCountHeuristic >= this.MaxStepHeuristic)
            {
                EndEpisode();
                return;
            }

            // do nothing
            if (actionIndex == 0)
            {
                SetReward(currentStepReward);
                _metrics.RegisterAction(GetRelevantStepCount, 0, prevPos, _currentEnvironmentPos, currentStepReward);
                _metrics.UpdateStats(GetRelevantStepCount);
                return;
            }

            Vector3Int intendedDir = actionSet[actionIndex];
            Vector3Int targetPos = _currentEnvironmentPos + intendedDir;

            if (area.IsPositionFree(targetPos))
            {
                _currentEnvironmentPos = targetPos;
                transform.localPosition = area.CellCoordinatesToLocalPosition(_currentEnvironmentPos);

                float currentDistance = Vector3.Distance(_currentEnvironmentPos, _goalGridPos);
                float distChange = _previousDistanceToGoal - currentDistance;
                currentStepReward += (distChange * 0.05f);
                _previousDistanceToGoal = currentDistance;

                if (_currentEnvironmentPos == _goalGridPos)
                {
                    currentStepReward += goalReward;
                    SetReward(currentStepReward);
                    area.TriggerSuccess();

                    _metrics.RegisterAction(GetRelevantStepCount, actionIndex, prevPos, _currentEnvironmentPos, currentStepReward);
                    _metrics.UpdateStats(GetRelevantStepCount);

                    EndEpisode();
                    return;
                }
            }
            // we punish agent (works only with masking off)
            else
            {
                if (area.IsOutOfBounds(targetPos))
                {
                    currentStepReward += wallHitPunishment;
                }

                if (area.DoesContainObstacle(targetPos))
                {
                    currentStepReward += obstacleHitPunishment;
                }

                area.TriggerFailure();
                SetReward(currentStepReward);

                _metrics.RegisterAction(GetRelevantStepCount, actionIndex, prevPos, _currentEnvironmentPos, currentStepReward);
                _metrics.UpdateStats(GetRelevantStepCount);

                EndEpisode();
            }

            SetReward(currentStepReward);
            _metrics.RegisterAction(GetRelevantStepCount, actionIndex, prevPos, _currentEnvironmentPos, currentStepReward);
            _metrics.UpdateStats(GetRelevantStepCount);
        }

        public override void OnEpisodeBegin()
        {
            StepCountHeuristic = 0;

            area.ResetArea();

            Vector3Int envSize = area.CurrentEnvironmentSize;
            float environmentVolume = envSize.x * envSize.y * envSize.z;

            int maxStep = maxStepStatic != 0 ? (int)(maxStepStatic * this.breathingAreaModifier) 
                : Mathf.FloorToInt(environmentVolume * this.breathingAreaModifier);



            if (_behaviorType == BehaviorType.HeuristicOnly)
            {
                this.MaxStep = 0;
                this.MaxStepHeuristic = maxStep;
            }
            else
            {
                this.MaxStep = maxStep;
            }

            _goalGridPos = area.GetGoalEnvironmentPosition();
            _currentEnvironmentPos = area.LocalPositionToCellCoordinates(transform.localPosition);
            _previousDistanceToGoal = Vector3.Distance(_currentEnvironmentPos, _goalGridPos);

            baseExistencePenalty = CalculateExistencePenalty();

            ConfigureSensors();
            _metrics.ResetHistory();
        }

        private void ConfigureSensors()
        {
            const float radiusStandardOffset = 0.15f;
            float unitSize = Grid3DSettings.Instance.UnitSize;
            float rayLength = 50f;
            Array.ForEach(_sensors, (sensor) =>
            {
                sensor.RayLength = rayLength;
                sensor.SphereCastRadius = unitSize / 2.0f - radiusStandardOffset;
            });
        }

        private float CalculateExistencePenalty()
        {
            return baseExistencePenalty;
        }


        // as a default engine of mlagents, we are using this method for inference and training only
        public void FixedUpdate()
        {
            if (_behaviorType != BehaviorType.HeuristicOnly)
            {
                WaitTimeInference();
            }
            else
            {
                RequestDecision();
            }
        }

        public string GetAgentName()
        {
            return $"{gameObject.name} (ID: {this.AgentId}) Type: Grid3DAgent";
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

        private void WaitTimeInference()
        {
            // if we are training model, then we request decision as a default behavior
            if (Academy.Instance.IsCommunicatorOn)
            {
                RequestDecision();
            }
            // one last mode is inference, where we run our trained model
            // here we are just demonstrating learning capabilities of our model
            // so default time between decisions is used
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
        public void SetAgentLocalPosition(Vector3 position)
        {
            transform.localPosition = position;
        }

        public Vector3 GetEnvironmentPosition()
        {
            return this.Area.transform.position;
        }
    }
}