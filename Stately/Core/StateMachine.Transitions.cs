using System;
using System.Collections.Generic;

namespace Stately
{
    public partial class StateMachine<T>
    {
        public Transition<T> ConfigTransition(T from, T to, Action<Transition<T>> configure)
        {
            if (configure == null)
            {
                logger.LogError("Configure can not be null");
                return null;
            }

            Transition<T> transition = null;
            if (states.TryGetValue(from, out var state))
            {
                transition = state.Transitions.Find(t => t.To.Equals(to));
            }

            if (transition == null)
            {
                transition = AddTransition(from, to);
                if (transition == null)
                    return null;
            }

            configure(transition);
            return transition;
        }

        public Transition<T> ConfigGlobalTransition(T to, Action<Transition<T>> configure)
        {
            if (configure == null)
            {
                logger.LogError("Configure action cannot be null");
                return null;
            }

            var transition = globalTransitions.Find(t => t.To.Equals(to));

            if (transition == null)
            {
                transition = AddGlobalTransition(to);
                if (transition == null)
                    return null;
            }

            configure(transition);
            return transition;
        }

        public Transition<T> AddTransition(T from, T to)
        {
            if (!states.TryGetValue(from, out var fromState))
            {
                logger.LogError($"Can not transition as (From state) does not exist");
                return null;
            }

            if (!states.ContainsKey(to))
            {
                logger.LogError($"Can not transition as (To state) does not exist");
                return null;
            }

            // NEW: Resolve parent states to their leaves
            T fromLeaf = ResolveToLeaf(from);
            T toLeaf = ResolveToLeaf(to);

            // Validate resolved states
            if (!states.ContainsKey(fromLeaf) || !states.ContainsKey(toLeaf))
            {
                logger.LogError($"Failed to resolve states to leaves");
                return null;
            }

            // Add transition using resolved leaf states
            var resolvedFromState = states[fromLeaf];
            var transition = resolvedFromState.AddTransition(toLeaf);
            SortTransitions();

            return transition;
        }

        public Transition<T> AddResetTransition(T from)
        {
            if (!initialized)
            {
                logger.LogError("Cannot add reset transition: no initial state set. Call AddState() first.");
                return null;
            }

            if (!states.ContainsKey(from))
            {
                logger.LogError($"Cannot add reset transition: source state {from} does not exist");
                return null;
            }

            return AddTransition(from, initialId);
        }

        public Transition<T> AddSelfTransition(T id)
        {
            return AddTransition(id, id);
        }

        public void AddTransitions(T[] from, T to, Predicate<StateMachine<T>> condition)
        {
            if (from == null) 
            {
                logger.LogError("from array is null");
                return;
            }

            for (int i = 0; i < from.Length; i++)
                AddTransition(from[i], to)?.When(condition);
        }

        public void AddTagTransition(string tag, T to, Predicate<StateMachine<T>> condition)
        {
            foreach (var kvp in states)
            {
                if (kvp.Value.HasTag(tag))
                {
                    AddTransition(kvp.Value.Id, to).When(condition);
                }
            }
        }

        public void AddTagTransition(string tag, T to, string eventName)
        {
            foreach (var kvp in states)
            {
                if (kvp.Value.HasTag(tag))
                {
                    AddTransition(kvp.Value.Id, to).OnEvent(eventName);
                }
            }
        }

        public void AddTagTransition<TEvent>(string tag, T to, TEvent eventName) where TEvent : Enum
        {
            AddTagTransition(tag, to, eventName.ToString());
        }

        public Transition<T> AddGlobalTransition(T to)
        {
            if (!states.ContainsKey(to))
            {
                logger.LogError("To State Does not exist");
                return null;
            }

            var transition = new Transition<T>(default, to);
            globalTransitions.Add(transition);
            SortTransitions();

            return transition;
        }

        public bool RemoveTransition(T from, T to)
        {
            if (!states.TryGetValue(from, out var state))
            {
                logger.LogWarning($"State with id: {from} does not exist");
                return false;
            }

            int removed = state.Transitions.RemoveAll(t => t.To.Equals(to));

            if (removed == 0)
                logger.LogWarning($"No Transition Was Found Between: {from} -> {to}");
        
            SortTransitions();
            return removed > 0;
        }

        public bool RemoveGlobalTransition(T to)
        {
            int removed = globalTransitions.RemoveAll(t => t.To.Equals(to));

            if (removed == 0)
            {
                logger.LogWarning($"No Global Transition Was Found to state: {to}");
                return false;
            }

            SortTransitions();
            return removed > 0;
        }

        public void ClearTransitionsFrom(T id)
        {
            if (!states.TryGetValue(id, out var state))
            {
                logger.LogWarning($"State with id: {id} does not exist");
                return;
            }

            state.Transitions.Clear();
            SortTransitions();
        }

        public void ClearTransitions()
        {
            foreach (var kvp in states)
                kvp.Value.Transitions.Clear();
            SortTransitions();
        }

        public void ClearGlobalTransitions()
        {
            globalTransitions.Clear();
            SortTransitions();
        }

        public void SortTransitions()
        {
            cachedSortedTransitions.Clear();

            if (currentState != null)
                cachedSortedTransitions.AddRange(currentState.Transitions);
        
            cachedSortedTransitions.AddRange(globalTransitions);
            cachedSortedTransitions.Sort(Transition<T>.Compare);
        }

        public bool HasTransition(T from, T to)
        {
            if (!states.TryGetValue(from, out var state))
                return false;
            return state.Transitions.Find(t => t.To.Equals(to)) != null;
        }

        public bool HasTransitionTo(T to)
        {
            return currentState?.Transitions.Find(t => t.To.Equals(to)) != null;
        }

        public bool HasGlobalTransition(T to)
        {
            return globalTransitions.Find(t => t.To.Equals(to)) != null;
        }

        public bool HasAnyGlobalTransitions() => globalTransitions.Count > 0;


        private void PerformTransition(T id, bool bypassExit = false, bool bypassHistory = false)
        {
            var targetLeaf = ResolveToLeaf(id);

            if (!states.ContainsKey(targetLeaf))
            {
                logger.LogError($"Target state {targetLeaf} does not exist");
                return;
            }

            if (isTransitioning)
            {
                if (pendingTransitions.Count >= MaxTransitionQueueSize)
                {
                    logger.LogError($"Too many queued transitions ({MaxTransitionQueueSize})! Possible infinite loop?");
                    return;
                }

                pendingTransitions.Enqueue(id);
                return;
            }

            isTransitioning = true;

            try
            {
                var currentHierarchy = GetActiveHierarchy();
                var targetHierarchy = GetHeirarchyPath(targetLeaf);

                int commonAncestorIndex = FindCommonAncestorIndex(currentHierarchy, targetHierarchy);

                bool recordHistory = history.IsActive && !bypassHistory && currentState != null;

                if (!bypassExit && currentState != null && !currentState.IsLocked())
                {
                    if (recordHistory)
                        history.CreateNewEntry(currentState.Id, stateTime);

                    // Exit from deepest to shallowest (leaf to root)
                    for (int i = currentHierarchy.Count - 1; i > commonAncestorIndex; i--)
                    {
                        var state = GetState(currentHierarchy[i]);
                        state.Exit?.Invoke();
                    }
                }
            
                ResetStateTime();

                if (currentState != null && !bypassExit)
                {
                    previousId = currentState.Id;
                    hasPreviousState = true;
                    currentState.StartCooldown();
                }

                // Enter states from common ancestor down to target leaf
                for (int i = commonAncestorIndex + 1; i < targetHierarchy.Count; i++)
                {
                    var state = GetState(targetHierarchy[i]);
                    state.Enter?.Invoke();
                }
            
                // Set current state to leaf
                currentState = GetState(targetLeaf);
            
                SortTransitions();
            
                if (initialized)
                    StateChanged?.Invoke(previousId, currentState.Id);
            
                // Process pending transitions
                while (pendingTransitions.Count > 0)
                {
                    var nextId = pendingTransitions.Dequeue();
                    isTransitioning = false;
                    PerformTransition(nextId);
                    isTransitioning = true;
                }
            }
            finally
            {
                isTransitioning = false;
                transitionData = default;
            }
        }


        private int FindCommonAncestorIndex(List<T> hierarchy1, List<T> hierarchy2)
        {
            int minLength = Math.Min(hierarchy1.Count, hierarchy2.Count);
            int commonIndex = -1;
        
            for (int i = 0; i < minLength; i++)
            {
                if (hierarchy1[i].Equals(hierarchy2[i]))
                    commonIndex = i;
                else
                    break;
            }
        
            return commonIndex;
        }

        private void CheckTransitions()
        {
            if (currentState == null)
                return;
        
            ProcessEvents();

            bool timeoutTriggered = currentState.Timeout > 0f && stateTime >= currentState.Timeout;

            if (timeoutTriggered)
            {
                OnStateTimeoutTriggered();
                return;
            }

            if (currentState.TransitionBlocked())
                return;

            if (cachedSortedTransitions.Count > 0)
                CheckTransitionLoop();
        }

        private void OnStateTimeoutTriggered()
        {
            currentState.TimeoutExpired?.Invoke();

            if (currentState.IsFullyLocked())
            {
                TimeoutBlocked?.Invoke(currentState.Id);
                return;
            }

            var timeoutId = currentState.TimeoutTargetId;
            var fromId = currentState.Id;

            if (!states.ContainsKey(timeoutId))
            {
                logger.LogError($"State With Id: {fromId}, does not have a timeout id");
                return;
            }

            var timeoutTransition = currentState.Transitions.Find(t => t.To.Equals(timeoutId));
            if (timeoutTransition != null && timeoutTransition.IsOnCooldown())
            {
                TimeoutBlocked?.Invoke(currentState.Id);
                return;
            }

            if (states.TryGetValue(timeoutId, out var state) && state.IsOnCooldown())
            {
                TimeoutBlocked?.Invoke(currentState.Id);
                return;
            }

            if (timeoutTransition != null)
                timeoutTransition.StartCooldown();

            currentState.OnTimeoutTriggered?.Invoke();
            StateTimeout?.Invoke(fromId);
            TransitionTriggered?.Invoke(fromId, timeoutId);
            PerformTransition(timeoutId);
        }

        private void CheckTransitionLoop()
        {
            foreach (var transition in cachedSortedTransitions)
            {
                if (transition.IsOnCooldown())
                    continue;

                bool guardPassed = transition.Guard?.Invoke(this) ?? true;

                if (!guardPassed)
                    continue;

                float requiredTime = transition.OverrideMinTime > 0f ? transition.OverrideMinTime : currentState.MinTime;

                if (stateTime < requiredTime && !transition.ForceInstantTransition)
                    continue;

                bool isInFromState = IsInFromStateHierarchy(transition.From);

                if (!isInFromState)
                    continue;

                if (transition.Condition?.Invoke(this) ?? false)
                {
                    // Resolve target to leaf
                    T targetLeaf = ResolveToLeaf(transition.To);

                    if (states.TryGetValue(targetLeaf, out var targetState) && targetState.IsOnCooldown())
                        continue;

                    transition.StartCooldown();
                    PerformTransition(targetLeaf);

                    transition.OnTriggered?.Invoke();
                    TransitionTriggered?.Invoke(transition.From, targetLeaf);

                    return;
                }
            }
        }

        private bool IsInFromStateHierarchy(T fromStateId)
        {
            // Global transition (from default enum value)
            if (fromStateId.Equals(default(T)))
                return true;

            // Direct match
            if (currentState.Id.Equals(fromStateId))
                return true;

            // Check if we're a child/descendant of fromStateId
            return IsInHierarchy(fromStateId);
        }
    }

}
