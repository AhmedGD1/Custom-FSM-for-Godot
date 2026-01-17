using System;

namespace Stately
{
    public partial class StateMachine<T>
    {
        public void TriggerEvent(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                logger.LogError("Event Name is invalid");
                return;
            }

            pendingEvents.Enqueue(eventName);
        }

        public void TriggerEvent<TEvent>(TEvent eventName) where TEvent : Enum
        {
            TriggerEvent(eventName.ToString());
        }

        public void OnEvent<TEvent>(TEvent eventName, Action callback) where TEvent : Enum
        {
            OnEvent(eventName.ToString(), callback);
        }

        public void OnEvent(string eventName, Action callback)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                logger.LogError("Event Name is invalid");
                return;
            }

            if (!eventListeners.ContainsKey(eventName))
                eventListeners[eventName] = new();
            eventListeners[eventName].Add(callback);
        }

        public bool RemoveEventListener(string eventName, Action callback)
        {
            if (eventListeners.TryGetValue(eventName, out var listener))
            {
                listener.Remove(callback);

                if (listener.Count == 0)
                    eventListeners.Remove(eventName);
                return true;
            }
            return false;
        }

        public void ClearEventListeners()
        {
            eventListeners.Clear();
        }   

        private void ProcessEvents()
        {
            while (pendingEvents.Count > 0)
            {
                string eventName = pendingEvents.Dequeue();

                if (eventListeners.TryGetValue(eventName, out var listener))
                {
                    for (int i = listener.Count - 1; i >= 0; i--)
                    {
                        listener[i]?.Invoke();
                    }
                }

                if (cachedSortedTransitions.Count > 0 && !isProcessingEvent)
                    CheckEventTransitions(eventName);
            }
        }

        private void CheckEventTransitions(string eventName)
        {
            if (isProcessingEvent)
                return;

            isProcessingEvent = true;

            try
            {
                foreach (var transition in cachedSortedTransitions)
                {
                    if (string.IsNullOrEmpty(transition.EventName))
                        continue;

                    if (transition.EventName != eventName)
                        continue;

                    if (transition.IsOnCooldown())
                        continue;

                    bool isInFromState = IsInFromStateHierarchy(transition.From);
                    if (!isInFromState)
                        continue;

                    bool guardPassed = transition.Guard?.Invoke(this) ?? true;

                    float requiredTime = transition.OverrideMinTime > 0f ? transition.OverrideMinTime : currentState.MinTime;
                    bool timeRequirementMet = transition.ForceInstantTransition || stateTime > requiredTime;

                    if (guardPassed && timeRequirementMet)
                    {
                        // Resolve target to leaf
                        T targetLeaf = ResolveToLeaf(transition.To);

                        if (states.TryGetValue(targetLeaf, out var targetState) && targetState.IsOnCooldown())
                            continue;

                        transition.StartCooldown();
                        PerformTransition(targetLeaf);

                        TransitionTriggered?.Invoke(transition.From, targetLeaf);
                        transition.OnTriggered?.Invoke();
                        return;
                    }
                }
            }
            finally
            {
                isProcessingEvent = false;
            }
        }
    }
}