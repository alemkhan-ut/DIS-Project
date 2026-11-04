using UnityEngine;

namespace ScenarioKit.Demo
{
    public class ResetPose : MonoBehaviour
    {
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private Rigidbody _rigidbody;

        private void Awake()
        {
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
            TryGetComponent(out _rigidbody);
        }

        public void ResetToInitial()
        {
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.position = _initialPosition;
                _rigidbody.rotation = _initialRotation;
            }

            transform.SetPositionAndRotation(_initialPosition, _initialRotation);
        }
    }
}
