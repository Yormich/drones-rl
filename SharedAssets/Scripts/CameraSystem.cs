using InputActions;
using InputCore;
using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem; // Required for New Input System

namespace Visuals
{
    public class CameraSystem : MonoBehaviour, IInputControllable
    {
        #region References
        [Header("References")]
        [Tooltip("Reference to the Cinemachine Camera component.")]
        [SerializeField] private CinemachineCamera cinemachineCamera;
        #endregion

        #region Movement Settings
        [Header("Movement Settings")]
        [Tooltip("Speed at which the camera moves via keyboard.")]
        [SerializeField] private float moveSpeed = 50f;
        #endregion

        #region Edge Scrolling Settings
        [Header("Edge Scrolling")]
        [Tooltip("Enable moving the camera by pushing the mouse against the screen edge.")]
        [SerializeField] private bool useEdgeScrolling = true;

        [Tooltip("Distance from edge (in pixels) to trigger scrolling.")]
        [SerializeField] private int edgeScrollSize = 20;
        #endregion

        #region Drag Pan Settings
        [Header("Drag Panning")]
        [Tooltip("Enable moving the camera by holding right-click and dragging.")]
        [SerializeField] private bool useDragPan = true;

        [Tooltip("Sensitivity of the drag pan movement.")]
        [SerializeField] private float dragPanSpeed = 2.0f;
        #endregion

        #region Rotation Settings
        [Header("Rotation Settings (Yaw)")]
        [Tooltip("Speed at which the camera rotates horizontally.")]
        [SerializeField] private float rotateSpeedHorizontal = 100f;

        [Header("Rotation Settings (Pitch)")]
        [Tooltip("Speed at which the camera orbits vertically.")]
        [SerializeField] private float rotateVerticalSpeed = 50f;

        [Tooltip("Minimum angle in degrees (0 = horizon, 90 = top down).")]
        [Range(0f, 89f)]
        [SerializeField] private float minVerticalAngle = 10f;

        [Tooltip("Maximum angle in degrees (0 = horizon, 90 = top down).")]
        [Range(1f, 90f)]
        [SerializeField] private float maxVerticalAngle = 85f;
        #endregion

        #region Zoom Settings
        [Header("Zoom Settings")]
        [Tooltip("Current target FOV (Modified by input).")]
        [SerializeField] private float targetFieldOfView = 15f;

        [Tooltip("Maximum Field of View (Zoom Out).")]
        [SerializeField] private float fieldOfViewMax = 80f;

        [Tooltip("Minimum Field of View (Zoom In).")]
        [SerializeField] private float fieldOfViewMin = 20f;

        [Tooltip("Amount the FOV changes per scroll tick.")]
        [SerializeField] private float zoomAmount = 5f;

        [Tooltip("Speed at which the lens interpolates to the target FOV.")]
        [SerializeField] private float zoomSpeed = 10f;

        [Space(10)]
        [Header("Zoom (Physical Move)")]
        [Tooltip("Minimum distance for physical follow offset.")]
        [SerializeField] private float followOffsetMin = 5f;

        [Tooltip("Maximum distance for physical follow offset.")]
        [SerializeField] private float followOffsetMax = 50f;
        #endregion

        // Internal State
        private Vector3 _followOffset;
        private CinemachineFollow _cinemachineFollow;
        private CameraInputActions _inputActions;

        private void Awake()
        {
            if (cinemachineCamera == null)
            {
                cinemachineCamera = GetComponent<CinemachineCamera>();
            }

            _cinemachineFollow = cinemachineCamera.GetComponent<CinemachineFollow>();

            if (_cinemachineFollow == null)
            {
#pragma warning disable S3928 // Parameter names used into ArgumentException constructors should match an existing one 
                throw new ArgumentNullException(nameof(cinemachineCamera),
                    "CinemachineCamera requires a CinemachineFollow component for this script to work.");
#pragma warning restore S3928 // Parameter names used into ArgumentException constructors should match an existing one 
            }

            _followOffset = _cinemachineFollow.FollowOffset;

            // Initialize FOV from current camera setting to prevent snapping on start
            if (cinemachineCamera.Lens.FieldOfView > 0)
            {
                targetFieldOfView = cinemachineCamera.Lens.FieldOfView;
            }

            _inputActions = new CameraInputActions();
        }

        private void Update()
        {
            HandleCameraMovement();
            HandleCameraRotation_Horizontal();
            HandleCameraRotation_Vertical();
            // s - HandleCameraZoom_FieldOfView
            HandleCameraZoom_MoveForward();
        }

        /// <summary>
        /// Handles Keyboard, Edge Scroll, and Drag Pan movement logic.
        /// </summary>
        private void HandleCameraMovement()
        {
            Vector3 inputDir = Vector3.zero;

            // 1. Handle Keyboard Input (WASD)
            Vector2 moveInput = _inputActions.CameraControls.Move.ReadValue<Vector2>();
            inputDir.x = moveInput.x;
            inputDir.z = moveInput.y;

            // 2. Handle Edge Scrolling
            if (useEdgeScrolling)
            {
                inputDir = CalculateEdgeScrolling(inputDir);
            }

            // 3. Handle Drag Panning
            if (useDragPan)
            {
                inputDir = CalculateDragPanning(inputDir);
            }

            // Apply the calculated movement
            ApplyMovementVector(inputDir);
        }

        /// <summary>
        /// Calculates modification to input direction based on mouse screen position.
        /// </summary>
        private Vector3 CalculateEdgeScrolling(Vector3 currentInputDir)
        {
            Vector2 mousePos = _inputActions.CameraControls.MousePosition.ReadValue<Vector2>();

            if (mousePos.x < edgeScrollSize)
                currentInputDir.x = -1f;

            if (mousePos.y < edgeScrollSize)
                currentInputDir.z = -1f;

            if (mousePos.x > Screen.width - edgeScrollSize)
                currentInputDir.x = +1f;

            if (mousePos.y > Screen.height - edgeScrollSize)
                currentInputDir.z = +1f;

            return currentInputDir;
        }

        /// <summary>
        /// Calculates modification to input direction based on mouse drag delta.
        /// </summary>
        private Vector3 CalculateDragPanning(Vector3 currentInputDir)
        {
            // Check if Right Mouse Button is held down
            if (_inputActions.CameraControls.PanDrag.IsPressed())
            {
                Vector2 mouseDelta = _inputActions.CameraControls.MouseDelta.ReadValue<Vector2>();

                // Invert movement (drag left moves camera right)
                currentInputDir.x = -mouseDelta.x * dragPanSpeed;
                currentInputDir.z = -mouseDelta.y * dragPanSpeed;
            }

            return currentInputDir;
        }

        /// <summary>
        /// Translates input direction relative to camera orientation and moves the transform.
        /// </summary>
        private void ApplyMovementVector(Vector3 inputDir)
        {
            Vector3 cameraForward = transform.forward;
            cameraForward.y = 0;
            cameraForward.Normalize();

            Vector3 cameraRight = transform.right;
            cameraRight.y = 0;
            cameraRight.Normalize();

            Vector3 moveDir = (cameraForward * inputDir.z) + (cameraRight * inputDir.x);

            transform.position += moveDir * (moveSpeed * Time.deltaTime);
        }

        private void HandleCameraRotation_Horizontal()
        {
            float rotateDir = _inputActions.CameraControls.RotateHorizontal.ReadValue<float>();

            // Apply rotation
            transform.eulerAngles += new Vector3(0f, rotateDir * rotateSpeedHorizontal * Time.deltaTime, 0f);
        }

        private void HandleCameraRotation_Vertical()
        {
            // Requires "RotateVertical" Action in Input System (Axis or Keys)
            float rotateDir = _inputActions.CameraControls.RotateVertical.ReadValue<float>();

            if (Mathf.Abs(rotateDir) > 0.01f)
            {
                float step = -rotateDir * rotateVerticalSpeed * Time.deltaTime;

                Vector3 newOffset = Quaternion.AngleAxis(step, Vector3.right) * _followOffset;

                float angleFromUp = Vector3.Angle(newOffset, Vector3.up);

                float minAllowedAngleFromUp = 90f - maxVerticalAngle;
                float maxAllowedAngleFromUp = 90f - minVerticalAngle;

                if (angleFromUp >= minAllowedAngleFromUp && angleFromUp <= maxAllowedAngleFromUp)
                {
                    _followOffset = newOffset;
                }
            }
        }


        private void HandleCameraZoom_FieldOfView()
        {
            float zoomInput = _inputActions.CameraControls.Zoom.ReadValue<float>();

            // Input System scroll values are usually 120 or -120 per notch, or normalized.
            // We normalize direction here.
            if (zoomInput > 0)
            {
                targetFieldOfView -= zoomAmount;
            }
            else if (zoomInput < 0)
            {
                targetFieldOfView += zoomAmount;
            }

            targetFieldOfView = Mathf.Clamp(targetFieldOfView, fieldOfViewMin, fieldOfViewMax);

            cinemachineCamera.Lens.FieldOfView = Mathf.Lerp(
                cinemachineCamera.Lens.FieldOfView,
                targetFieldOfView,
                zoomSpeed * Time.deltaTime
            );
        }

        /// <summary>
        /// Optional: Alternative Zoom method that physically moves the camera closer/further.
        /// </summary>
        private void HandleCameraZoom_MoveForward()
        {
            float zoomInput = _inputActions.CameraControls.Zoom.ReadValue<float>();
            Vector3 zoomDir = _followOffset.normalized;

            if (zoomInput > 0)
            {
                _followOffset -= zoomDir * zoomAmount;
            }
            else if (zoomInput < 0)
            {
                _followOffset += zoomDir * zoomAmount;
            }

            // Clamp vector magnitude
            if (_followOffset.magnitude < followOffsetMin)
            {
                _followOffset = zoomDir * followOffsetMin;
            }

            if (_followOffset.magnitude > followOffsetMax)
            {
                _followOffset = zoomDir * followOffsetMax;
            }

            // Apply
            _cinemachineFollow.FollowOffset = Vector3.Lerp(
                _cinemachineFollow.FollowOffset,
                _followOffset,
                Time.deltaTime * zoomSpeed
            );
        }

        public void EnableControl()
        {
            _inputActions.CameraControls.Enable();
        }

        public void DisableControl()
        {
            _inputActions.CameraControls.Disable();
        }
    }
}