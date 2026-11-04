using ScenarioKit.Events;
using UnityEngine;
using UnityEngine.UI;

namespace ScenarioKit.Sources
{
    [RequireComponent(typeof(Toggle))]
    public class ToggleStateSource : MonoBehaviour
    {
        [Header("Состояние")]
        [SerializeField]
        [Tooltip("Состояние мира, в которое пишется значение переключателя.")]
        private ScenarioStateKey _state;

        private Toggle _toggle;

        public ScenarioStateKey State => _state;

        private void Awake()
        {
            _toggle = GetComponent<Toggle>();
        }

        private void Start()
        {
            ToggleValueChangedHandler(_toggle.isOn);
        }

        private void OnEnable()
        {
            _toggle.onValueChanged.AddListener(ToggleValueChangedHandler);
            ScenarioEventBus.StateResetEvent += StateResetEventHandler;
        }

        private void OnDisable()
        {
            _toggle.onValueChanged.RemoveListener(ToggleValueChangedHandler);
            ScenarioEventBus.StateResetEvent -= StateResetEventHandler;
        }

        private void StateResetEventHandler()
        {
            ScenarioEventBus.SetState(_state, _toggle.isOn);
        }

        private void ToggleValueChangedHandler(bool isOn)
        {
            ScenarioEventBus.SetState(_state, isOn);
        }
    }
}
