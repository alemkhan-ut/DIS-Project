using UnityEngine;
using UnityEngine.InputSystem;

namespace ScenarioKit.Demo
{
    [RequireComponent(typeof(Rigidbody))]
    public class SimplePlayerMover : MonoBehaviour
    {
        [Header("Движение")]
        [SerializeField]
        [Tooltip("Скорость движения игрока, метров в секунду.")]
        private float _speed = 5f;

        private Rigidbody _rigidbody;

        public float Speed => _speed;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            Vector3 direction = GetInputDirection();
            Vector3 velocity = direction * _speed;
            velocity.y = _rigidbody.linearVelocity.y;
            _rigidbody.linearVelocity = velocity;
        }

        private Vector3 GetInputDirection()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return Vector3.zero;
            }

            float x = 0f;
            float z = 0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                x -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                x += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                z -= 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                z += 1f;
            }

            return Vector3.ClampMagnitude(new Vector3(x, 0f, z), 1f);
        }
    }
}
