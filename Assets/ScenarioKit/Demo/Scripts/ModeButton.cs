using System;
using ScenarioKit.Modes;
using UnityEngine;
using UnityEngine.UI;

namespace ScenarioKit.Demo
{
    [RequireComponent(typeof(Button))]
    public class ModeButton : MonoBehaviour
    {
        [Header("Подпись")]
        [SerializeField]
        [Tooltip("Текст кнопки, в него выводится название режима.")]
        private Text _label;

        private Button _button;
        private ScenarioMode _mode;
        private Action<ScenarioMode> _onSelected;

        public ScenarioMode Mode => _mode;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            _button.onClick.AddListener(ButtonClickedHandler);
        }

        private void OnDisable()
        {
            _button.onClick.RemoveListener(ButtonClickedHandler);
        }

        public void Setup(ScenarioMode mode, Action<ScenarioMode> onSelected)
        {
            _mode = mode;
            _onSelected = onSelected;

            if (_label != null)
            {
                _label.text = mode != null ? mode.DisplayName : string.Empty;
            }
        }

        private void ButtonClickedHandler()
        {
            if (_mode != null)
            {
                _onSelected?.Invoke(_mode);
            }
        }
    }
}
