using System;
using UnityEngine;

namespace RiftForge
{
    /// <summary>
    /// Moves a three-dimensional player on the ground plane using an injectable input source.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        private float moveSpeed = 5f;

        [SerializeField, Min(0f)]
        private float arenaRadius;

        private CharacterController characterController;
        private IPlayerInputSource inputSource;

        /// <summary>
        /// Gets or sets the movement speed in world units per second.
        /// </summary>
        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = Mathf.Max(0f, value);
        }

        /// <summary>Optional XZ movement boundary. Zero keeps the original unbounded foundation behavior.</summary>
        public float ArenaRadius
        {
            get => arenaRadius;
            set => arenaRadius = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            inputSource ??= new InputSystemPlayerInputSource();
        }

        private void Update()
        {
            Vector3 movement = CalculateMovementVector(inputSource.ReadMove());
            characterController.Move(movement * (moveSpeed * Time.deltaTime));
            if (arenaRadius > 0f)
            {
                Vector2 horizontal = new Vector2(transform.position.x, transform.position.z);
                if (horizontal.magnitude > arenaRadius)
                {
                    Vector2 bounded = horizontal.normalized * arenaRadius;
                    characterController.Move(new Vector3(bounded.x - horizontal.x, 0f, bounded.y - horizontal.y));
                }
            }
        }

        /// <summary>
        /// Replaces the input provider, allowing gameplay code and tests to supply another device source.
        /// </summary>
        /// <param name="source">The input source to read each frame.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
        public void SetInputSource(IPlayerInputSource source)
        {
            inputSource = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>
        /// Maps two-dimensional input to the XZ ground plane without allowing diagonal input to exceed unit length.
        /// </summary>
        /// <param name="input">Horizontal and forward movement input.</param>
        /// <returns>A ground-plane movement vector with magnitude no greater than one.</returns>
        public static Vector3 CalculateMovementVector(Vector2 input)
        {
            Vector2 normalizedInput = Vector2.ClampMagnitude(input, 1f);
            return new Vector3(normalizedInput.x, 0f, normalizedInput.y);
        }
    }
}
