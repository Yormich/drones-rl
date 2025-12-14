using System;
using UnityEngine;

namespace DroneMovement
{
    [Serializable]
    public class PIDController
    {
        [Header("Gains")]
        [Tooltip("Proportional Gain: Reaction to current error")]
        [SerializeField] private float pGain = 1.0f;

        [Tooltip("Integral Gain: Reaction to accumulated error (steady-state fix)")]
        [SerializeField] private float iGain = 0.1f;

        [Tooltip("Derivative Gain: Reaction to rate of change (damping)")]
        [SerializeField] private float dGain = 0.2f;

        [Header("Limits")]
        [SerializeField] private float maxOutput = 100f;
        [SerializeField] private float integralLimit = 5f;

        private float p, i, d;
        private float prevError;

        public float MaxOutput => maxOutput;

        /// <summary>
        /// Calculates PID output. Uses DeltaAngle for rotational logic to ensure shortest path rotation.
        /// </summary>
        public float Update(float currentError, float deltaTime)
        {
            if (deltaTime <= 0f) return 0f;

            // Proportional term
            p = pGain * currentError;

            // Integral term with windup guarding
            i += currentError * deltaTime * iGain;
            i = Mathf.Clamp(i, -integralLimit, integralLimit);

            // Derivative term
            float errorRateOfChange = (currentError - prevError) / deltaTime;
            d = dGain * errorRateOfChange;

            prevError = currentError;

            // Combine terms and clamp output to maxOutput limits
            return Mathf.Clamp(p + i + d, -maxOutput, maxOutput);
        }

        public void Reset()
        {
            p = 0; 
            i = 0; 
            d = 0; 
            prevError = 0f;
        }
    }
}