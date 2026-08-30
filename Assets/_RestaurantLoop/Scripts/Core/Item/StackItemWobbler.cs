using UnityEngine;

namespace RestaurantLoop.Core
{
    /// <summary>
    /// Applies a lightweight, procedural sway to a StackItem's child visuals only.
    /// The StackItem root remains fully controlled by conveyor and gameplay systems.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StackItemVisuals))]
    public sealed class StackItemWobbler : MonoBehaviour
    {
        private const float TwoPi = Mathf.PI * 2f;

        [Header("Visual Target")]
        [Tooltip("Child pivot rotated by the wobble. If empty, StackItemVisuals.VisualContainer is used.")]
        [SerializeField] private Transform visualContainer;

        [Header("Stack Intensity")]
        [Tooltip("Counts at or below this value remain perfectly stable.")]
        [SerializeField, Min(0)] private int minStackCountForWobble = 4;
        [Tooltip("Reference count used to normalize wobble intensity.")]
        [SerializeField, Min(1)] private int maxStackSize = 40;
        [SerializeField] private AnimationCurve wobbleIntensityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Tooltip("How quickly intensity responds when the stack count changes.")]
        [SerializeField, Min(0.01f)] private float intensityResponse = 7f;

        [Header("Motion")]
        [SerializeField, Range(0f, 25f)] private float maxWobbleAngle = 8f;
        [Tooltip("Base oscillation frequency in cycles per second.")]
        [SerializeField, Range(0.1f, 6f)] private float wobbleSpeed = 2.5f;
        [Tooltip("Scales the secondary local Z-axis tilt relative to the X-axis sway.")]
        [SerializeField, Range(0f, 1.5f)] private float zTiltBias = 0.7f;
        [Tooltip("Small noise-based angle variation in degrees.")]
        [SerializeField, Range(0f, 2f)] private float jitterAmount = 0.35f;

        private Quaternion baseLocalRotation = Quaternion.identity;
        private float phase;
        private float secondaryPhase;
        private float noiseSeed;
        private float noiseTime;
        private float currentIntensity;
        private float targetIntensity;
        private int currentCount;
        private bool wobbleActive;

        private void Awake()
        {
            ResolveVisualContainer();
            CaptureBaseRotation();
        }

        private void OnEnable()
        {
            // Re-randomize pooled instances so neighboring stacks never synchronize.
            phase = Random.Range(0f, TwoPi);
            secondaryPhase = Random.Range(0f, TwoPi);
            noiseSeed = Random.Range(0f, 1000f);
            noiseTime = Random.Range(0f, 100f);
        }

        private void LateUpdate()
        {
            if (visualContainer == null)
            {
                ResolveVisualContainer();
                if (visualContainer == null) return;
                CaptureBaseRotation();
            }

            if (!wobbleActive || currentCount <= minStackCountForWobble)
            {
                ResetRotationImmediately();
                return;
            }

            float deltaTime = Time.deltaTime;
            float intensityBlend = 1f - Mathf.Exp(-intensityResponse * deltaTime);
            currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, intensityBlend);

            if (currentIntensity <= 0.0001f)
            {
                ResetRotationImmediately();
                return;
            }

            // Lower stacks sway more slowly while high stacks approach the configured frequency.
            float frequencyScale = Mathf.Lerp(0.45f, 1f, currentIntensity);
            float phaseStep = wobbleSpeed * frequencyScale * TwoPi * deltaTime;
            phase += phaseStep;
            noiseTime += deltaTime * wobbleSpeed * 0.35f;

            float angle = maxWobbleAngle * currentIntensity;
            float organicNoise = (Mathf.PerlinNoise(noiseSeed, noiseTime) - 0.5f) * 2f * jitterAmount;
            float xTilt = Mathf.Sin(phase) * angle + organicNoise * currentIntensity;
            float zTilt = Mathf.Sin(phase * 0.73f + secondaryPhase) * angle * zTiltBias -
                          organicNoise * 0.5f * currentIntensity;

            visualContainer.localRotation = baseLocalRotation * Quaternion.Euler(xTilt, 0f, zTilt);
        }

        private void OnDisable()
        {
            ResetImmediately();
        }

        /// <summary>Updates intensity after serving or modifying this stack.</summary>
        public void SetStackCount(int count)
        {
            currentCount = Mathf.Max(0, count);
            targetIntensity = CalculateIntensity(currentCount);

            // The threshold is a hard stability contract, not a gradual tail.
            if (currentCount <= minStackCountForWobble)
            {
                currentIntensity = 0f;
                ResetRotationImmediately();
            }
        }

        /// <summary>Enables sway only while the item is tracked on the conveyor.</summary>
        public void SetWobbleActive(bool active)
        {
            wobbleActive = active;
            if (!active)
            {
                currentIntensity = 0f;
                ResetRotationImmediately();
                return;
            }

            targetIntensity = CalculateIntensity(currentCount);
        }

        /// <summary>Prepares a newly spawned or reinitialized pooled stack.</summary>
        public void ResetState(int count)
        {
            wobbleActive = false;
            currentIntensity = 0f;
            SetStackCount(count);
            ResetRotationImmediately();
        }

        /// <summary>Immediately clears all transient wobble state for pooling or mode changes.</summary>
        public void ResetImmediately()
        {
            wobbleActive = false;
            currentIntensity = 0f;
            targetIntensity = 0f;
            currentCount = 0;
            ResetRotationImmediately();
        }

        private float CalculateIntensity(int count)
        {
            if (count <= minStackCountForWobble) return 0f;

            int safeMaximum = Mathf.Max(minStackCountForWobble + 1, maxStackSize);
            float normalizedCount = Mathf.InverseLerp(minStackCountForWobble, safeMaximum, count);
            AnimationCurve curve = wobbleIntensityCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
            return Mathf.Clamp01(curve.Evaluate(normalizedCount));
        }

        private void ResolveVisualContainer()
        {
            if (visualContainer != null) return;
            StackItemVisuals visuals = GetComponent<StackItemVisuals>();
            if (visuals != null) visualContainer = visuals.VisualContainer;
        }

        private void CaptureBaseRotation()
        {
            if (visualContainer != null) baseLocalRotation = visualContainer.localRotation;
        }

        private void ResetRotationImmediately()
        {
            if (visualContainer != null) visualContainer.localRotation = baseLocalRotation;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maxStackSize = Mathf.Max(minStackCountForWobble + 1, maxStackSize);
            wobbleIntensityCurve ??= AnimationCurve.Linear(0f, 0f, 1f, 1f);
            ResolveVisualContainer();
        }
#endif
    }
}