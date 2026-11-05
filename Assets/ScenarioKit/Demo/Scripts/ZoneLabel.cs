using UnityEngine;
using UnityEngine.UI;

namespace ScenarioKit.Demo
{
    /// <summary>
    /// Отображает буквенное обозначение зоны внутри триггера (A, B, C, D и т.д.).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ZoneLabel : MonoBehaviour
    {
        [Header("Текст")]
        [SerializeField]
        [Tooltip("Буквенное обозначение зоны (например, 'A', 'B', 'C', 'D').")]
        private string _text = "A";

        [Header("Ссылки")]
        [SerializeField]
        [Tooltip("Трансформ объекта метки для ориентации на камеру.")]
        private Transform _labelTransform;

        [SerializeField]
        [Tooltip("Компонент Text для отображения буквы.")]
        private Text _textComponent;

        [Header("Отображение")]
        [SerializeField]
        [Tooltip("Поворачивать метку к главной камере.")]
        private bool _faceCamera = true;

        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                ApplyText();
            }
        }

        public Transform LabelTransform
        {
            get => _labelTransform;
            set => _labelTransform = value;
        }

        public Text TextComponent
        {
            get => _textComponent;
            set
            {
                _textComponent = value;
                ApplyText();
            }
        }

        public bool FaceCamera
        {
            get => _faceCamera;
            set => _faceCamera = value;
        }

        private void Awake()
        {
            ApplyText();
        }

        private void OnEnable()
        {
            ApplyText();
        }

        private void OnValidate()
        {
            ApplyText();
        }

        private void LateUpdate()
        {
            if (_faceCamera && _labelTransform != null)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    _labelTransform.rotation = cam.transform.rotation;
                }
            }
        }

        public void ApplyText()
        {
            if (_textComponent != null && _textComponent.text != _text)
            {
                _textComponent.text = _text;
            }
        }
    }
}

