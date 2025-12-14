using UnityEngine;
using System.Collections;

namespace Visuals
{
    [RequireComponent(typeof(LineRenderer))]
    public class PeriodicBeam : MonoBehaviour
    {
        [Header("Beam Settings")]
        [Tooltip("How high the beam shoots into the sky.")]
        [SerializeField] private float beamHeight = 100f;

        [Tooltip("Width of the beam.")]
        [SerializeField] private float beamWidth = 0.5f;

        [Tooltip("Color of the beam.")]
        [SerializeField] private Color beamColor = Color.cyan;

        [Header("Animation Settings")]
        [Tooltip("Time the beam stays fully visible before fading.")]
        [SerializeField] private float sustainDuration = 1.0f;

        [Tooltip("How long it takes to fade out.")]
        [SerializeField] private float fadeDuration = 1.5f;

        [Tooltip("Time between beam shots.")]
        [SerializeField] private float timeBetweenPulses = 2.0f;

        private LineRenderer _lineRenderer;

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            InitializeLineRenderer();
        }

        private void OnEnable()
        {
            StartCoroutine(BeamRoutine());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _lineRenderer.enabled = false;
        }

        private void InitializeLineRenderer()
        {
            // Use a default soft particle material so it looks like light
            // "Sprites/Default" is a safe built-in shader that supports transparency
            if (_lineRenderer.material == null || _lineRenderer.material.name.StartsWith("Default-Line"))
            {
                _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }

            _lineRenderer.startWidth = beamWidth;
            _lineRenderer.endWidth = beamWidth;
            _lineRenderer.positionCount = 2;
            _lineRenderer.useWorldSpace = true; // Important so it stays upright even if parent rotates
            _lineRenderer.enabled = false;      // Start hidden
        }

        private IEnumerator BeamRoutine()
        {
            while (true)
            {
                Vector3 startPos = transform.position;

                Vector3 endPos = startPos + Vector3.up * beamHeight;

                _lineRenderer.SetPosition(0, startPos);
                _lineRenderer.SetPosition(1, endPos);

                SetBeamAlpha(1f);
                _lineRenderer.enabled = true;

                yield return new WaitForSeconds(sustainDuration);

                float timer = 0f;
                while (timer < fadeDuration)
                {
                    timer += Time.deltaTime;
                    float progress = timer / fadeDuration;

                    float currentAlpha = Mathf.Lerp(1f, 0f, progress);
                    SetBeamAlpha(currentAlpha);

                    yield return null;
                }

                _lineRenderer.enabled = false;
                yield return new WaitForSeconds(timeBetweenPulses);
            }
        }

        private void SetBeamAlpha(float alpha)
        {
            _lineRenderer.startColor = new Color(beamColor.r, beamColor.g, beamColor.b, alpha);
            _lineRenderer.endColor = new Color(beamColor.r, beamColor.g, beamColor.b, 0f);
        }
    }
}