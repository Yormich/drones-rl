using UnityEngine;

namespace ContinuousWorld.Visuals
{
    public class DroneFollowCamera : MonoBehaviour
    {
        [SerializeField] private float distance = 5.0f;
        [SerializeField] private float height = 2.0f;
        [SerializeField] private float smoothSpeed = 10f;
        [SerializeField] private float rotationSmoothSpeed = 5f;

        private Transform _target;

        public void SetTarget(Transform target)
        {
            _target = target;

            // Snap immediately to start position to prevent camera flying across map on reset
            if (_target != null)
            {
                Vector3 desiredPos = _target.position - _target.forward * distance + Vector3.up * height;
                transform.position = desiredPos;
                transform.LookAt(_target);
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 forwardFlat = _target.forward;
            forwardFlat.y = 0;
            forwardFlat.Normalize();

            Vector3 desiredPosition = _target.position - (forwardFlat * distance) + (Vector3.up * height);

            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

            Quaternion targetRotation = Quaternion.LookRotation(_target.position - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothSpeed * Time.deltaTime);
        }
    }
}