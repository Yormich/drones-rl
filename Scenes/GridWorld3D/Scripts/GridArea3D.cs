using Agents;
using GridWorld.AI;
using GridWorld.Generation;
using GridWorld.UI;
using GridWorld.Visuals;
using SideChannels;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace GridWorld
{
    public class GridArea3D : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] GameObject wireframe;
        [SerializeField] GameObject obstacle;
        [SerializeField] GameObject goal;
        [SerializeField] GameObject floor;
        [SerializeField] GameObject wall;

        [Header("Visuals")]
        [SerializeField] GameObject godViewCameraPrefab;
        [SerializeField] GameObject droneCameraPrefab;
        [SerializeField] float sensorPositionMultiplier = 0.5f;
        [SerializeField] Vector2Int sensorResolution = new Vector2Int(84, 84);
        [SerializeField] Vector2Int godViewResolution = new Vector2Int(256, 256);

        // Camera References
        private CameraCapture _godCamCapture;
        private readonly Dictionary<Grid3DViewType, CameraCapture> _droneCamCaptures = new Dictionary<Grid3DViewType, CameraCapture>();

        private ColorFlashFeedback[] colorFlashFeedbacks;
        private Grid3DAgent agent;


        private GameObject currentVisualCamera;

        // Current State
        private Vector3Int _currentEnvSize;
        private float _currentUnitSize;
        private float _currentDensity;
        private int _currentSeed;
        private IMapGenerator _currentGenerator;

        private AStarPathfinder _pathfinder;

        // Object Tracking
        private readonly List<GameObject> _dynamicObjects = new List<GameObject>(); // Obstacles/Goals
        private readonly List<GameObject> _staticObjects = new List<GameObject>();  // Walls/Floors/Borders/Cameras
        private HashSet<Vector3Int> _cachedObstaclePositions = new HashSet<Vector3Int>();

        private void Start()
        {
            var gridSettings = Grid3DSettings.Instance;

            this.agent = GetComponentInChildren<Grid3DAgent>(true);

            if (gridSettings == null)
            {
                throw new MissingReferenceException("Error happened while instantiating environments, grid settings was not present at scene");
            }

            if (this.agent == null)
            {
                throw new MissingReferenceException("Grid agent was missing in environment during initialization process");
            }

            _currentUnitSize = gridSettings.UnitSize;
            _pathfinder = new AStarPathfinder(this);
            ResetArea();
        }

        public void ResetArea()
        {
            if (Grid3DSettings.Instance == null) return;

            var settings = Grid3DSettings.Instance;
            Vector3Int newEnvironmentSize = settings.GetActiveGridSize();
            MapGenerationType mapType = settings.GetActiveGenerationType();

            bool sizeChanged = newEnvironmentSize != _currentEnvSize;

            _currentEnvSize = newEnvironmentSize;
            _currentSeed = UnityEngine.Random.Range(0, 10000);
            _currentDensity = settings.GetActiveDensity();
            _currentGenerator = GetGeneratorStrategy(mapType);

            if (sizeChanged || _staticObjects.Count == 0)
            {
                RebuildStaticStructure();
            }

            // Place Goal and Obstacles
            PlaceDynamicObjects();
            AgentEvents.EnvironmentReady(this.agent);
        }

        private void RebuildStaticStructure()
        {
            foreach (var obj in _staticObjects)
            {
                if (obj != null) DestroyImmediate(obj);
            }
            _staticObjects.Clear();

            Vector3 totalSize = _currentEnvSize * (int)_currentUnitSize;
            Vector3 halfSize = totalSize / 2.0f;

            // Floor
            GameObject floorObj = Instantiate(floor, this.transform);
            floorObj.transform.localPosition = new Vector3(halfSize.x, 0f, -halfSize.z);
            const float planeInternalScale = 10.0f;
            floorObj.transform.localScale = new Vector3(totalSize.x / planeInternalScale, 1f, totalSize.z / planeInternalScale);
            _staticObjects.Add(floorObj);

            // Walls
            float wallHeight = _currentUnitSize;
            CreateWall(new Vector3(halfSize.x, wallHeight / 2f, _currentUnitSize / 2f),
                       new Vector3(totalSize.x, wallHeight, _currentUnitSize)); // North
            CreateWall(new Vector3(halfSize.x, wallHeight / 2f, -(totalSize.z + _currentUnitSize / 2f)),
                       new Vector3(totalSize.x, wallHeight, _currentUnitSize)); // South
            CreateWall(new Vector3(totalSize.x + _currentUnitSize / 2f, wallHeight / 2f, -halfSize.z),
                       new Vector3(totalSize.z + 2f * _currentUnitSize, wallHeight, _currentUnitSize), Quaternion.Euler(0, 90, 0)); // East
            CreateWall(new Vector3(-_currentUnitSize / 2f, wallHeight / 2f, -halfSize.z),
                       new Vector3(totalSize.z + 2f * _currentUnitSize, wallHeight, _currentUnitSize), Quaternion.Euler(0, 90, 0)); // West

            // Wireframe
            GameObject wireframeObj = Instantiate(wireframe, this.transform);
            const float offsetToRemoveRenderingArtifacts = 0.001f;

            // add scaling considering wrapping whole environment, counting walls too
            wireframeObj.transform.localScale = new Vector3(
                totalSize.x + offsetToRemoveRenderingArtifacts, 
                totalSize.y + offsetToRemoveRenderingArtifacts, 
                totalSize.z + offsetToRemoveRenderingArtifacts);

            wireframeObj.transform.localPosition = new Vector3(
                halfSize.x, 
                halfSize.y, 
                -(halfSize.z));
            _staticObjects.Add(wireframeObj);

            if (_godCamCapture != null) DestroyImmediate(_godCamCapture.gameObject);

            GameObject gObj = Instantiate(godViewCameraPrefab, this.transform);
            // Position it high above center
            gObj.transform.localPosition = new Vector3(totalSize.x / 2f, totalSize.y * 2f, -totalSize.z / 2f);
            gObj.transform.LookAt(this.transform.position + new Vector3(totalSize.x / 2f, 0, -totalSize.z / 2f));

            _godCamCapture = gObj.AddComponent<CameraCapture>();
            _godCamCapture.Initialize(godViewResolution.x, godViewResolution.y);
            _staticObjects.Add(gObj);


            this.colorFlashFeedbacks = this.GetComponentsInChildren<ColorFlashFeedback>();
            UpdateCameraVisibility();
        }

        private void CreateWall(Vector3 pos, Vector3 scale, Quaternion rot = default)
        {
            GameObject w = Instantiate(this.wall, this.transform);
            w.transform.localPosition = pos;
            w.transform.localScale = scale;

            if (rot != default) w.transform.localRotation = rot;

            _staticObjects.Add(w);
        }

        private void PlaceDynamicObjects()
        {
            foreach (var obj in _dynamicObjects)
            {
                if (obj != null) DestroyImmediate(obj);
            }
            _dynamicObjects.Clear();
            _cachedObstaclePositions.Clear();

            (Vector3Int agentPos, Vector3Int goalPos) = GeneratePositionsWithExistingPath();

            foreach (Vector3Int obstaclePosition in _cachedObstaclePositions)
            {
                PlaceObjectOnGrid(obstaclePosition, obstacle);
            }

            PlaceObjectOnGrid(goalPos, goal);

            agent.SetAgentLocalPosition(CellCoordinatesToLocalPosition(agentPos));
            SetupDroneCameras();
        }

        private void SetupDroneCameras()
        {
            // 1. Cleanup old cameras
            foreach (var cam in _droneCamCaptures.Values)
            {
                if (cam != null) DestroyImmediate(cam.gameObject);
            }
            _droneCamCaptures.Clear();

            // 2. Define directions and types
            var sensors = new (Vector3 dir, Grid3DViewType type)[]
            {
                (Vector3.forward, Grid3DViewType.Front),
                (Vector3.back,    Grid3DViewType.Back),
                (Vector3.left,    Grid3DViewType.Left),
                (Vector3.right,   Grid3DViewType.Right),
                (Vector3.up,      Grid3DViewType.Up),   // Requires special rotation handling
                (Vector3.down,    Grid3DViewType.Down), // Requires special rotation handling
            };

            float offsetDistance = _currentUnitSize * sensorPositionMultiplier;

            foreach (var (dir, type) in sensors)
            {
                GameObject camObj = Instantiate(droneCameraPrefab, agent.transform);
                camObj.name = $"Sensor_{type}";

                // Place at the face of the cube
                camObj.transform.localPosition = dir * offsetDistance;

                // Default "Up" for the camera is World Up
                Vector3 cameraUpReference = Vector3.up;

                // Check if we are looking straight Up or Down (Dot product is 1 or -1)
                if (Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.99f)
                {
                    // If looking Up/Down, align the top of the camera with the Agent's Forward
                    cameraUpReference = Vector3.forward;
                }

                camObj.transform.localRotation = Quaternion.LookRotation(dir, cameraUpReference);

                // Add the capture component
                var capture = camObj.AddComponent<CameraCapture>();
                capture.Initialize(sensorResolution.x, sensorResolution.y);

                _droneCamCaptures.Add(type, capture);
            }

            UpdateCameraVisibility();
        }
        private void UpdateCameraVisibility(IAgent specificAgent = null)
        {
            IAgent activeAgent = specificAgent ?? AgentListController.Instance.CurrentSelectedAgent;

            bool isSelected = activeAgent is Grid3DAgent && agent != null && activeAgent.AgentId == this.agent.AgentId;

            if (_godCamCapture != null) _godCamCapture.gameObject.SetActive(isSelected);

            foreach (var capture in _droneCamCaptures.Values)
            {
                if (capture != null) capture.gameObject.SetActive(isSelected);
            }
        }

        public Vector3Int CurrentEnvironmentSize => _currentEnvSize;
        public float Density => _currentDensity;
        public static MapGenerationType GenerationType => Grid3DSettings.Instance.GetActiveGenerationType();

        public Grid3DAgent Agent => agent;

        private (Vector3Int agentPos, Vector3Int goalPos) GeneratePositionsWithExistingPath()
        {
            int gridVolume = _currentEnvSize.x * _currentEnvSize.y * _currentEnvSize.z;
            Vector3Int goalPos, agentPos;

            const int maxGenerationAttempts = 5000;
            int safetyCheckCounter = 0;
            do
            {
                _cachedObstaclePositions = _currentGenerator.Generate(_currentEnvSize, _currentSeed + safetyCheckCounter, _currentDensity);

                do
                {
                    goalPos = CoordinateToCellPosition(UnityEngine.Random.Range(0, gridVolume));
                } while (!IsPositionFree(goalPos));

                do
                {
                    agentPos = CoordinateToCellPosition(UnityEngine.Random.Range(0, gridVolume));
                } while (!IsPositionFree(agentPos) || agentPos == goalPos);
                Debug.Log($"Agent pos: {agentPos}. goalPos: {goalPos}. Obstacles count: {_cachedObstaclePositions.Count}");
                safetyCheckCounter++;
            }
            while (_pathfinder.FindPath(agentPos, goalPos) == null && safetyCheckCounter < maxGenerationAttempts);

            if (_pathfinder.FindPath(agentPos, goalPos) == null)
            {
                throw new InvalidOperationException($"Current generation method ({_currentGenerator}) " +
                    $"Couldn't provide an environment with possible path between agent and goal with parameters: (size: {_currentEnvSize}, seed: {_currentSeed}, density: {_currentDensity}). " +
                    $"In {maxGenerationAttempts} attempts");
            }

            return (agentPos, goalPos);
        }

        #region Position Conversion Methods
        private Vector3Int CoordinateToCellPosition(int coordinate)
        {
            return new Vector3Int(coordinate % _currentEnvSize.x, 
                coordinate / (_currentEnvSize.x * _currentEnvSize.z),
                (coordinate / _currentEnvSize.x) % _currentEnvSize.z);
        }

        public Vector3Int GetGoalEnvironmentPosition()
        {
            if (_dynamicObjects.Count == 0) return Vector3Int.zero;

            // Assuming the Goal is always the last object added
            return LocalPositionToCellCoordinates(_dynamicObjects[^1].transform.localPosition);
        }

        public bool IsOutOfBounds(Vector3Int cellPos)
        {
            return cellPos.x < 0 || cellPos.x >= _currentEnvSize.x || 
                cellPos.y < 0 || cellPos.y >= _currentEnvSize.y ||
                cellPos.z < 0 || cellPos.z >= _currentEnvSize.z;
        }

        public bool DoesContainObstacle(Vector3Int cellPos) => _cachedObstaclePositions.Contains(cellPos);

        public bool IsPositionFree(Vector3Int cellPos) => !IsOutOfBounds(cellPos) && !DoesContainObstacle(cellPos);


        public Vector3 CellCoordinatesToLocalPosition(Vector3Int cellPos)
        {
            return new Vector3(
                cellPos.x * _currentUnitSize + _currentUnitSize / 2f,
                cellPos.y * _currentUnitSize + _currentUnitSize / 2f,
                -(cellPos.z * _currentUnitSize + _currentUnitSize / 2f)
            );
        }

        public Vector3Int LocalPositionToCellCoordinates(Vector3 localPos)
        {
            int col = Mathf.FloorToInt(localPos.x / _currentUnitSize);
            int layer = Mathf.FloorToInt(localPos.y / _currentUnitSize);
            int row = Mathf.FloorToInt(Mathf.Abs(localPos.z) / _currentUnitSize);
            
            return new Vector3Int(col, layer, row);
        }

        private void PlaceObjectOnGrid(Vector3Int coordinates, GameObject baseObject)
        {
            GameObject obj = Instantiate(baseObject, this.transform);
            obj.transform.localPosition = CellCoordinatesToLocalPosition(coordinates);
            _dynamicObjects.Add(obj);
        }

        #endregion


        #region Camera Methods And Visual Feedbacks
        public void TriggerSuccess()
        {
            foreach (ColorFlashFeedback feedback in colorFlashFeedbacks)
            {
                feedback.FlashSuccess();
            }
        }

        public void TriggerFailure()
        {
            foreach (ColorFlashFeedback feedback in colorFlashFeedbacks)
            {
                feedback.FlashFailure();
            }
        }

        #endregion

        #region Subscribing and Unsubscribing Cameras
        private void OnEnable()
        {
            AgentListController.OnNewAgentSelected += AgentListController_OnNewAgentSelected;

            Grid3DSideChannel.OnRequestFullSnapshot += SendAllObservations;
        }

        private void OnDisable()
        {
            AgentListController.OnNewAgentSelected -= AgentListController_OnNewAgentSelected;
            Grid3DSideChannel.OnRequestFullSnapshot -= SendAllObservations;
        }

        private void SendAllObservations()
        {
            IAgent currentObservableAgent = AgentListController.Instance.CurrentSelectedAgent;
            if (currentObservableAgent == null || this.agent.AgentId != currentObservableAgent.AgentId)
            {
                return;
            }

            if (Grid3DSideChannel.Instance == null)
            {
                return;
            }

            if (_godCamCapture != null)
            {
                byte[] bytes = _godCamCapture.CaptureFrame();
                Grid3DSideChannel.Instance.SendView(Grid3DViewType.GodView, bytes);
            }

            foreach (var kvp in _droneCamCaptures)
            {
                byte[] bytes = kvp.Value.CaptureFrame();
                Grid3DSideChannel.Instance.SendView(kvp.Key, bytes);
            }
        }

        public RenderTexture GetCameraTexture(Grid3DViewType viewType)
        {
            // Handle God View
            if (viewType == Grid3DViewType.GodView)
            {
                return _godCamCapture != null ? _godCamCapture.OutputTexture : null;
            }

            // Handle Drone Sensors
            if (_droneCamCaptures.TryGetValue(viewType, out var capture))
            {
                return capture != null ? capture.OutputTexture : null;
            }

            return null;
        }

        private void AgentListController_OnNewAgentSelected(IAgent agent)
        {
            UpdateCameraVisibility(agent);
        }
        #endregion
        private static IMapGenerator GetGeneratorStrategy(MapGenerationType type)
        {
            return type switch
            {
                MapGenerationType.Cityscape => new CityscapeGenerator(),
                MapGenerationType.CellularAutomata => new CellularAutomataGenerator(),
                MapGenerationType.Simplex => new SimplexMapGenerator(),
                MapGenerationType.Maze => new MazeGenerator(),
                _ => new SimpleRandomGenerator(),
            };
        }
    }
}