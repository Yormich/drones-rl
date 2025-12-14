using Agents;
using GridWorld.Metrics;
using System;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;


namespace GridWorld
{
    //[RequireComponent(typeof(DecisionRequester))]
    [RequireComponent(typeof(Agent2DMetricCollector))]
    public class Grid2DAgent : Agent, IAgent
    {
        private const int ObservationVectorSize = 6;
        private static int AgentIdGenerator = 0;

        [Header("Settings")]
        [SerializeField] private GridArea area;
        
        [Tooltip("If true, the agent cannot try to walk into walls/obstacles. Speeds up training.")]
        [SerializeField] private bool maskActions = true;
        
        [SerializeField] private float breathingAreaModifier = 1.0f;
        [SerializeField] private float existencePenalty = -0.001f;
        [SerializeField] private float goalReward = 1.0f;
        [SerializeField] private float obstacleHitPunishment = -0.5f;
        [SerializeField] private float wallHitPunishment = -0.5f;

        [SerializeField] private float timeBetweenDecisionsAtInference;

        private float _timeSinceDecision;
        
        private Vector2Int _currentGridPos;
        private Vector2Int _goalGridPos;

        private BehaviorType _behaviorType;

        private int StepCountHeuristic;
        private int MaxStepHeuristic;

        private readonly Vector2Int[] _directions = new Vector2Int[]
        {
            new Vector2Int(0, 0),  // 0: Do Nothing
            new Vector2Int(0, -1), // 1: Up
            new Vector2Int(0, 1),  // 2: Down
            new Vector2Int(-1, 0), // 3: Left
            new Vector2Int(1, 0)   // 4: Right
        };

        private Agent2DMetricCollector _metrics;


        public GridArea Area => area;

        public int AgentId { get; private set; }

        public int GetRelevantStepCount => _behaviorType == BehaviorType.HeuristicOnly ? StepCountHeuristic : StepCount;

        public Agent2DMetricCollector Metrics => _metrics;

        public override void Initialize()
        {
            _metrics = GetComponent<Agent2DMetricCollector>();
            Grid2DSettings settings = Grid2DSettings.Instance;

            lock (settings)
            {
                this.AgentId = ++AgentIdGenerator;
            }

            if (area == null)
            {
                area = GetComponentInParent<GridArea>();

                if (area == null)
                    throw new MissingReferenceException("Grid2DAgent could not find a GridArea in parent.");
            }
            
            // reassure that size of vector observation is set as needed
            GetComponent<BehaviorParameters>()!.BrainParameters.VectorObservationSize = ObservationVectorSize;

            _behaviorType = GetComponent<BehaviorParameters>().BehaviorType;


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

            if (Input.GetKey(KeyCode.W)) discreteActions[0] = 1;
            if (Input.GetKey(KeyCode.S)) discreteActions[0] = 2;
            if (Input.GetKey(KeyCode.A)) discreteActions[0] = 3;
            if (Input.GetKey(KeyCode.D)) discreteActions[0] = 4;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            // use 4 cardinal directions and cached obstacle positions to check content of adjacent cells
            for (int i = 1; i < _directions.Length; i++)
            {
                Vector2Int direction = _directions[i];

                bool isBlockedOrOutOfBounds = !area.IsPositionFree(_currentGridPos + direction);

                // observation is in range [0, 1.0f]
                sensor.AddObservation(isBlockedOrOutOfBounds ? 1.0f : 0.0f);
            }

            AddDistanceGridBasedObservations(sensor);
        }

        private void AddDistanceGridBasedObservations(VectorSensor sensor)
        {
            float gridSize = area.CurrentGridSize;
            float distX = _goalGridPos.x - _currentGridPos.x;
            float distY = _goalGridPos.y - _currentGridPos.y;

            // observations are in range [-1.0f, 1.0f]
            sensor.AddObservation(distX / (gridSize - 1.0f));
            sensor.AddObservation(distY / (gridSize - 1.0f));
        }

        private void AddDistanceRelativeObservations(VectorSensor sensor)
        {
            float distX = _goalGridPos.x - _currentGridPos.x;
            float distY = _goalGridPos.y - _currentGridPos.y;

            float maxDist = Mathf.Max(Mathf.Abs(distX), Mathf.Abs(distY));

            // safety check to prevent division by zero
            if (maxDist < 0.0001f)
            {
                maxDist = 1.0f;
            }

            // observations are in range [-1.0f, 1.0f]
            sensor.AddObservation(distX / maxDist);
            sensor.AddObservation(distY / maxDist);
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            if (!this.maskActions)
            {
                return;
            }

            for (int i = 1; i < _directions.Length; i++)
            {
                Vector2Int targetPos = _currentGridPos + _directions[i];

                if (!area.IsPositionFree(targetPos))
                {
                    // agent has only one action branch related to movement with starting index of 0
                    // we can't go into the obstacle or wall, and using cached coordinated inside area
                    // is more performance optimized rather than throwing colliders and comparing tags
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

            Vector2Int prevPos = _currentGridPos;

            float currentStepReward = 0f;

            currentStepReward += existencePenalty;

            
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
                _metrics.RegisterAction(StepCount, 0, prevPos, _currentGridPos, currentStepReward);
                _metrics.UpdateStats(StepCount);
                return;
            }

            Vector2Int intendedDir = _directions[actionIndex];
            Vector2Int targetPos = _currentGridPos + intendedDir;

            // even with masking enabled, it is important to safe-check position 
            // position is free, move agent towards position
            if (area.IsPositionFree(targetPos))
            {
                _currentGridPos = targetPos;
                transform.localPosition = area.GridToLocalPosition(_currentGridPos);

                if (_currentGridPos == _goalGridPos)
                {
                    currentStepReward += goalReward;
                    SetReward(currentStepReward);

                    _metrics.RegisterAction(StepCount, actionIndex, prevPos, _currentGridPos, currentStepReward);
                    _metrics.UpdateStats(StepCount);
                    area.TriggerSuccess();
                    EndEpisode();
                    return;
                }
            }
            // we punish agent (works only with masking off)
            else
            {
                // use this structure because punishments for hitting a wall or obstacle may vary in the future
                if (area.IsOutOfBounds(targetPos))
                {
                    currentStepReward += wallHitPunishment;
                }

                if (area.DoesContainObstacle(targetPos))
                {
                    currentStepReward += obstacleHitPunishment;
                }
            }

            SetReward(currentStepReward);
            _metrics.RegisterAction(StepCount, actionIndex, prevPos, _currentGridPos, currentStepReward);
            _metrics.UpdateStats(StepCount);
        }

        public override void OnEpisodeBegin()
        {
            StepCountHeuristic = 0;

            area.ResetArea();

            float currentGridSize = area.CurrentGridSize;
            int maxStepDynamic = Mathf.FloorToInt(Mathf.Pow(currentGridSize, 2.0f) * this.breathingAreaModifier);

            if (_behaviorType == BehaviorType.HeuristicOnly)
            {
                this.MaxStep = 0;
                this.MaxStepHeuristic = maxStepDynamic;
            }
            else
            {
                this.MaxStep = maxStepDynamic;
            }

            existencePenalty = CalculateExistencePenalty(currentGridSize);
            _goalGridPos = area.GetGoalGridPosition();
            
            SpawnAgent(currentGridSize);

            _metrics.ResetHistory();
        }

        private void SpawnAgent(float floatGridSize)
        {
            bool validPositionFound = false;
            int safetyCounter = 0;
            int maxAttempts = 1000;

            int gridSize = (int)floatGridSize;

            while (!validPositionFound && safetyCounter < maxAttempts)
            {
                int x = UnityEngine.Random.Range(0, gridSize);
                int y = UnityEngine.Random.Range(0, gridSize);
                Vector2Int potentialPos = new Vector2Int(x, y);

                // Check against obstacles and ensure we don't spawn ON the goal
                if (area.IsPositionFree(potentialPos) && potentialPos != _goalGridPos)
                {
                    _currentGridPos = potentialPos;
                    transform.localPosition = area.GridToLocalPosition(_currentGridPos);
                    validPositionFound = true;
                }
                safetyCounter++;
            }

            if (!validPositionFound)
            {
                Debug.LogError("Grid2DAgent: Could not find a free spot to spawn agent.");
            }
        }

        private static float CalculateExistencePenalty(float gridSize)
        {
            float longestPath = ((gridSize - 1) * gridSize / 2) + gridSize;
            return -1.0f / longestPath;
        }

        // use this for heuristic decision process
        private void Update()
        {
            //this method remains empty until heuristic decision process implementation
        }


        // as a default engine of mlagents, we are using this method for inference and training only
        public void FixedUpdate()
        {
            if (_behaviorType != BehaviorType.HeuristicOnly)
            {
                WaitTimeInference();
            }
        }

        public string GetAgentName()
        {
            // You can customize this or use gameObject.name
            return $"{gameObject.name} (ID: {this.AgentId}) Type: Grid2DAgent";
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

        public Vector3 GetEnvironmentPosition()
        {
            return this.Area.transform.position;
        }
    }

}