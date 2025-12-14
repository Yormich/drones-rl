using Agents;
using GridWorld;
using GridWorld.UI;
using GridWorld.Visuals;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GridWorld
{
    public class GridArea : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] GameObject obstacle;
        [SerializeField] GameObject goal;
        [SerializeField] GameObject floor;
        [SerializeField] GameObject wall;
        [SerializeField] GameObject visualCamera;

        private ColorFlashFeedback floorFeedback;
        private Grid2DAgent agent;
        private GameObject currentVisualCamera;

        // Current State
        private float _currentGridSize;
        private float _currentUnitSize;
        private int _currentNumObstacles;

        // Object Tracking
        private readonly List<GameObject> _dynamicObjects = new List<GameObject>(); // Obstacles/Goals
        private readonly List<GameObject> _staticObjects = new List<GameObject>();  // Walls/Floors
        private readonly HashSet<Vector2Int> _cachedObstaclePositions = new HashSet<Vector2Int>();

        private void Start()
        {
            var gridSettings = Grid2DSettings.Instance;

            this.agent = GetComponentInChildren<Grid2DAgent>();
            
            if (gridSettings == null)
            {
                throw new MissingReferenceException("Error happened while instantiating environments, grid settings was not present at scene");
            }

            if (this.agent == null)
            {
                throw new MissingReferenceException("Grid agent was missing in environment during initialization process");
            }

            _currentUnitSize = gridSettings.GetUnitSize();

            ResetArea();
        }

        public void ResetArea()
        {
            if (Grid2DSettings.Instance == null) return;

            float newGridSize = Grid2DSettings.Instance.GetActiveGridSize();
            int newNumObstacles = Grid2DSettings.Instance.GetActiveNumObstacles(newGridSize);

            bool sizeChanged = Mathf.Abs(newGridSize - _currentGridSize) > 0.01f;
            
            _currentGridSize = newGridSize;
            _currentNumObstacles = newNumObstacles;

            if (sizeChanged || _staticObjects.Count == 0)
            {
                RebuildStaticStructure();
            }

            // Place Goal and Obstacles
            PlaceDynamicObjects();
        }

        private void RebuildStaticStructure()
        {
            // Clean old static objects (Walls/Floors)
            foreach (var obj in _staticObjects)
            {
                if (obj != null) DestroyImmediate(obj);
            }
            _staticObjects.Clear();

            float totalSize = _currentGridSize * _currentUnitSize;
            float halfSize = totalSize / 2.0f;

            // Floor
            GameObject floorObj = Instantiate(floor, this.transform);
            floorObj.transform.localPosition = new Vector3(halfSize, 0f, -halfSize);
            const float planeInternalScale = 10.0f;
            floorObj.transform.localScale = new Vector3(totalSize / planeInternalScale, 1f, totalSize / planeInternalScale);
            _staticObjects.Add(floorObj);

            this.floorFeedback = floorObj.GetComponent<ColorFlashFeedback>();
    

            // Walls
            float wallHeight = _currentUnitSize;
            CreateWall(new Vector3(halfSize, wallHeight / 2f, _currentUnitSize / 2f),
                       new Vector3(totalSize, wallHeight, _currentUnitSize)); // North
            CreateWall(new Vector3(halfSize, wallHeight / 2f, -(totalSize + _currentUnitSize / 2f)),
                       new Vector3(totalSize, wallHeight, _currentUnitSize)); // South
            CreateWall(new Vector3(totalSize + _currentUnitSize / 2f, wallHeight / 2f, -halfSize),
                       new Vector3(totalSize + 2f * _currentUnitSize, wallHeight, _currentUnitSize), Quaternion.Euler(0, 90, 0)); // East
            CreateWall(new Vector3(-_currentUnitSize / 2f, wallHeight / 2f, -halfSize),
                       new Vector3(totalSize + 2f * _currentUnitSize, wallHeight, _currentUnitSize), Quaternion.Euler(0, 90, 0)); // West

            // Camera
            GameObject cameraObj = Instantiate(visualCamera, this.transform);
            cameraObj.transform.localPosition = new Vector3(halfSize * _currentUnitSize, totalSize * _currentUnitSize, -(halfSize * _currentUnitSize));
            this.currentVisualCamera = cameraObj;

            // God View Recorder for side channel
            GodViewRecorder recorder = cameraObj.AddComponent<GodViewRecorder>();
            recorder.Initialize();

            IAgent currentObservableAgent = AgentListController.Instance.CurrentSelectedAgent;
            this.currentVisualCamera.gameObject.SetActive(currentObservableAgent != null && this.agent.AgentId == currentObservableAgent.AgentId);
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

            HashSet<int> coordinates = new HashSet<int>();
            int maxCells = (int)Mathf.Pow(_currentGridSize, 2f);

            int actualObstacles = Mathf.Min(_currentNumObstacles, maxCells - 2);

            if (maxCells <= 1)
            {
                return;
            }

            while (coordinates.Count < actualObstacles + 1)
            {
                coordinates.Add(UnityEngine.Random.Range(0, maxCells));
            }

            int[] coordinatesArr = coordinates.ToArray();

            // Spawn Obstacles
            for (int i = 0; i < actualObstacles; i++)
            {
                Vector2Int pos = IndexToGrid(coordinatesArr[i]);
                _cachedObstaclePositions.Add(pos);
                PlaceObjectOnGrid(pos, this.obstacle);
            }

            // Spawn Goal
            Vector2Int goalPos = IndexToGrid(coordinatesArr[^1]);
            PlaceObjectOnGrid(goalPos, this.goal);
        }

        public float CurrentGridSize => _currentGridSize;

        private Vector2Int IndexToGrid(int index)
        {
            return new Vector2Int(index % (int)_currentGridSize, index / (int)_currentGridSize);
        }

        public Vector2Int GetGoalGridPosition()
        {
            if (_dynamicObjects.Count == 0) return Vector2Int.zero;
            
            // Assuming the Goal is always the last object added
            return LocalPositionToGrid(_dynamicObjects[^1].transform.localPosition);
        }

        public bool IsOutOfBounds(Vector2Int gridPos)
        {
            return gridPos.x < 0 || gridPos.x >= _currentGridSize || gridPos.y < 0 || gridPos.y >= _currentGridSize;
        }

        public bool DoesContainObstacle(Vector2Int gridPos) => _cachedObstaclePositions.Contains(gridPos);

        public bool IsPositionFree(Vector2Int gridPos) => !IsOutOfBounds(gridPos) && !DoesContainObstacle(gridPos);


        public Vector3 GridToLocalPosition(Vector2Int gridPos)
        {
            return new Vector3(
                gridPos.x * _currentUnitSize + _currentUnitSize / 2f,
                _currentUnitSize / 2f,
                -(gridPos.y * _currentUnitSize + _currentUnitSize / 2f)
            );
        }

        public Vector2Int LocalPositionToGrid(Vector3 localPos)
        {
            int col = Mathf.FloorToInt(localPos.x / _currentUnitSize);
            int row = Mathf.FloorToInt(Mathf.Abs(localPos.z) / _currentUnitSize);
            return new Vector2Int(col, row);
        }

        private void PlaceObjectOnGrid(Vector2Int coordinates, GameObject baseObject)
        {
            GameObject obj = Instantiate(baseObject, this.transform);
            obj.transform.localPosition = GridToLocalPosition(coordinates);
            _dynamicObjects.Add(obj);
        }

        private void OnEnable()
        {
            AgentListController.OnNewAgentSelected += AgentListController_OnNewAgentSelected;
        }

        private void OnDisable()
        {
            AgentListController.OnNewAgentSelected -= AgentListController_OnNewAgentSelected;
        }

        private void AgentListController_OnNewAgentSelected(IAgent newAgent)
        {
            this.currentVisualCamera.gameObject.SetActive(newAgent.AgentId == this.agent.AgentId);
        }

        public void TriggerSuccess()
        {
            floorFeedback.FlashSuccess();
        }

        public void TriggerFailure()
        {
            floorFeedback.FlashFailure();
        }
    }
}