using ScenarioKit.Events;
using UnityEngine;
using UnityEngine.UI;

namespace ScenarioKit.Sources
{
    [RequireComponent(typeof(Button))]
    public class ButtonEventSource : ScenarioEventSource
    {
        [Header("Событие")]
        [SerializeField]
        [Tooltip("Событие, которое произойдёт при нажатии на кнопку.")]
        private ScenarioEventKey _onClick;

        private Button _button;

        public ScenarioEventKey OnClick => _onClick;

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

        private void ButtonClickedHandler()
        {
            Raise(_onClick);
        }
    }
}
