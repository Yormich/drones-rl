using System.Collections;
using UnityEngine;

namespace GridWorld.Visuals
{
    [RequireComponent(typeof(Renderer))]
    public class ColorFlashFeedback : MonoBehaviour
    {
        [Header("Settings")]
        [ColorUsage(true, true)]
        [SerializeField] private Color successColor = new Color(0, 1, 0, 1);

        [ColorUsage(true, true)]
        [SerializeField] private Color failureColor = new Color(1, 0, 0, 1);

        [SerializeField] private float fadeDuration = 1.0f;
        [SerializeField] private float pulseSpeed = 5.0f; // Speed of the pulsing while colliding

        [Header("Shader Fix")]
        [SerializeField] private string colorPropertyName = "_BaseColor";

        private Renderer _renderer;
        private Material[] _targetMaterials;
        private Color[] _originalColors;
        private Coroutine _currentCoroutine;
        private int _colorPropertyID;

        // State tracking for continuous collision
        private bool _isContinuousFlashing = false;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _targetMaterials = _renderer.materials;

            if (_targetMaterials == null || _targetMaterials.Length == 0)
            {
                this.enabled = false;
                return;
            }

            _originalColors = new Color[_targetMaterials.Length];

            // Validate property name using first material
            Material refMat = _targetMaterials[0];
            if (!refMat.HasProperty(colorPropertyName))
            {
                if (refMat.HasProperty("_BaseColor")) colorPropertyName = "_BaseColor";
                else if (refMat.HasProperty("BaseColor")) colorPropertyName = "BaseColor";
            }
            _colorPropertyID = Shader.PropertyToID(colorPropertyName);

            // Cache originals
            for (int i = 0; i < _targetMaterials.Length; i++)
            {
                if (_targetMaterials[i].HasProperty(_colorPropertyID))
                    _originalColors[i] = _targetMaterials[i].GetColor(_colorPropertyID);
            }
        }

        // --- One Shot Methods (Instant Feedback) ---
        public void FlashSuccess() => TriggerOneShot(successColor);
        public void FlashFailure() => TriggerOneShot(failureColor);

        // --- Continuous Methods (While Colliding) ---
        public void StartContinuousFailure()
        {
            if (_isContinuousFlashing) return; // Already flashing
            _isContinuousFlashing = true;

            if (_currentCoroutine != null) StopCoroutine(_currentCoroutine);
            _currentCoroutine = StartCoroutine(ContinuousPulseRoutine(failureColor));
        }

        public void StopContinuousFailure()
        {
            if (!_isContinuousFlashing) return;
            _isContinuousFlashing = false;
            // The coroutine will exit its loop naturally and fade out
        }

        private void TriggerOneShot(Color targetColor)
        {
            // If we are continuously flashing (e.g. stuck in a wall), ignore single shots
            if (_isContinuousFlashing) return;

            if (!this.enabled || !gameObject.activeInHierarchy) return;

            if (_currentCoroutine != null) StopCoroutine(_currentCoroutine);
            _currentCoroutine = StartCoroutine(FlashOneShotRoutine(targetColor));
        }

        private IEnumerator FlashOneShotRoutine(Color flashColor)
        {
            float elapsedTime = 0f;
            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsedTime / fadeDuration);
                ApplyColor(flashColor, t); // t goes 0 to 1, we want Flash -> Original
                yield return null;
            }
            ResetColors();
            _currentCoroutine = null;
        }

        private IEnumerator ContinuousPulseRoutine(Color flashColor)
        {
            float timer = 0f;

            while (_isContinuousFlashing)
            {
                timer += Time.deltaTime * pulseSpeed;

                float t = Mathf.PingPong(timer, 1f);

                ApplyLerp(flashColor, 1 - t);
                yield return null;
            }

            yield return FlashOneShotRoutine(flashColor);
        }

        private void ApplyColor(Color target, float time)
        {
            // time 0 = target, time 1 = original
            ApplyLerp(target, time);
        }

        private void ApplyLerp(Color flashColor, float t)
        {
            for (int i = 0; i < _targetMaterials.Length; i++)
            {
                if (_targetMaterials[i].HasProperty(_colorPropertyID))
                {
                    Color c = Color.Lerp(flashColor, _originalColors[i], t);
                    _targetMaterials[i].SetColor(_colorPropertyID, c);
                }
            }
        }

        private void ResetColors()
        {
            for (int i = 0; i < _targetMaterials.Length; i++)
            {
                if (_targetMaterials[i].HasProperty(_colorPropertyID))
                    _targetMaterials[i].SetColor(_colorPropertyID, _originalColors[i]);
            }
        }
    }
}