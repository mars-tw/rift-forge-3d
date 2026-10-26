using UnityEngine;

namespace RiftForge
{
    /// <summary>
    /// Smoothly follows and looks at a target from a configurable trailing distance and height.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField]
        private Transform target;

        [SerializeField, Min(0f)]
        private float distance = 8f;

        [SerializeField, Min(0f)]
        private float height = 6f;

        [SerializeField, Min(0f)]
        private float damping = 5f;

        private Vector3 basePosition;
        private Quaternion baseRotation;
        private bool hasBase;
        private float trauma;
        private float baseFieldOfView = -1f;
        private float fovKick;

        /// <summary>Current shake trauma in [0, 1]. Shake strength is trauma squared.</summary>
        public float Trauma => trauma;

        /// <summary>
        /// Gets the transform currently followed by this rig.
        /// </summary>
        public Transform Target => target;

        /// <summary>
        /// Gets or sets the trailing distance from the target.
        /// </summary>
        public float Distance
        {
            get => distance;
            set => distance = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Gets or sets the vertical offset above the target.
        /// </summary>
        public float Height
        {
            get => height;
            set => height = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Gets or sets the exponential follow damping coefficient.
        /// </summary>
        public float Damping
        {
            get => damping;
            set => damping = Mathf.Max(0f, value);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (!hasBase)
            {
                basePosition = transform.position;
                baseRotation = transform.rotation;
                hasBase = true;
            }

            float blend = CalculateDampingFactor(damping, Time.deltaTime);
            Vector3 desiredPosition = GetDesiredPosition();
            basePosition = Vector3.LerpUnclamped(basePosition, desiredPosition, blend);

            Vector3 lookDirection = target.position - basePosition;
            if (lookDirection.sqrMagnitude > Mathf.Epsilon)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
                baseRotation = Quaternion.SlerpUnclamped(baseRotation, desiredRotation, blend);
            }

            // Shake runs on unscaled time so hit-stop and slow motion still read as impact.
            float unscaled = Time.unscaledDeltaTime;
            trauma = DecayTrauma(trauma, unscaled);
            float strength = trauma * trauma;
            float time = Time.unscaledTime * 31f;
            Vector3 offset = new Vector3(Mathf.PerlinNoise(time, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, time) - 0.5f,
                Mathf.PerlinNoise(time, time * 0.5f) - 0.5f) * (0.9f * strength);
            float roll = (Mathf.PerlinNoise(time * 0.8f, 2.1f) - 0.5f) * 6f * strength;
            transform.position = basePosition + offset;
            transform.rotation = baseRotation * Quaternion.Euler(0f, 0f, roll);

            Camera attached = GetComponent<Camera>();
            if (attached != null)
            {
                if (baseFieldOfView < 0f) baseFieldOfView = attached.fieldOfView;
                fovKick = Mathf.MoveTowards(fovKick, 0f, unscaled * 18f);
                attached.fieldOfView = baseFieldOfView - fovKick;
            }
        }

        /// <summary>Add camera shake. Values accumulate and are clamped to one.</summary>
        public void AddTrauma(float amount)
        {
            trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, amount));
        }

        /// <summary>Briefly narrows the field of view (degrees) for a punch-in on big hits.</summary>
        public void KickFieldOfView(float degrees)
        {
            fovKick = Mathf.Max(fovKick, Mathf.Clamp(degrees, 0f, 12f));
        }

        /// <summary>Snap the smoothed base pose (used for intro camera swoops).</summary>
        public void SnapTo(Vector3 position, Quaternion rotation)
        {
            basePosition = position;
            baseRotation = rotation;
            hasBase = true;
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Linear trauma decay of 1.6 per second, clamped to [0, 1].</summary>
        public static float DecayTrauma(float value, float deltaTime)
        {
            if (deltaTime <= 0f) return Mathf.Clamp01(value);
            return Mathf.Clamp01(value - 1.6f * deltaTime);
        }

        /// <summary>
        /// Assigns the transform that the rig follows.
        /// </summary>
        /// <param name="newTarget">The target transform, or null to stop following.</param>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Calculates a frame-rate-independent interpolation factor for exponential damping.
        /// </summary>
        /// <param name="dampingCoefficient">The non-negative damping coefficient.</param>
        /// <param name="deltaTime">The elapsed time in seconds.</param>
        /// <returns>A blend factor in the inclusive range from zero to one.</returns>
        public static float CalculateDampingFactor(float dampingCoefficient, float deltaTime)
        {
            if (dampingCoefficient <= 0f || deltaTime <= 0f)
            {
                return 0f;
            }

            return 1f - Mathf.Exp(-dampingCoefficient * deltaTime);
        }

        private Vector3 GetDesiredPosition()
        {
            return target.position - (target.forward * distance) + (Vector3.up * height);
        }

        private void OnValidate()
        {
            distance = Mathf.Max(0f, distance);
            height = Mathf.Max(0f, height);
            damping = Mathf.Max(0f, damping);
        }
    }
}
