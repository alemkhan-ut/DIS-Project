using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScenarioKit.Events
{
    public static class ScenarioEventBus
    {
        public static event Action<ScenarioEventKey> RaisedEvent;
        public static event Action StateResetEvent;

        private static readonly WorldState _worldState = new WorldState();

        public static IWorldState State => _worldState;

        public static void Raise(ScenarioEventKey key)
        {
            if (key == null)
            {
                return;
            }

            RaisedEvent?.Invoke(key);
        }

        public static void SetState(ScenarioStateKey key, bool value)
        {
            if (key == null)
            {
                return;
            }

            _worldState.Set(key, value);
        }

        public static void ResetState()
        {
            _worldState.Clear();
            StateResetEvent?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            RaisedEvent = null;
            StateResetEvent = null;
            ResetState();
        }

        private class WorldState : IWorldState
        {
            private readonly Dictionary<ScenarioStateKey, bool> _values = new Dictionary<ScenarioStateKey, bool>();

            public bool Get(ScenarioStateKey key)
            {
                if (key == null)
                {
                    return false;
                }

                bool value;
                return _values.TryGetValue(key, out value) && value;
            }

            public void Set(ScenarioStateKey key, bool value)
            {
                _values[key] = value;
            }

            public void Clear()
            {
                _values.Clear();
            }
        }
    }
}
