using System;
using System.Linq;
using System.Collections.Generic;

namespace Stately
{
    public partial class StateMachine<T>
    {
        public void SetCooldownTimersProcessMode(FSMProcessMode mode)
        {
            timersProcessMode = mode;
        }
    
        public bool IsActive()
        {
            return !paused && started;
        }

        public float GetMinStateTime()
        {
            if (currentState == null)
                throw new InvalidOperationException("No current state. Call Start() first.");
            return currentState.MinTime;
        }

        public float GetRemainingTime() => 
            currentState != null && currentState.Timeout > 0f ? Math.Max(0f, currentState.Timeout - stateTime) : -1f;

        public float GetTimeoutProgress()
        {
            if (currentState == null || currentState.Timeout <= 0f)
                return -1f;
            return Math.Clamp(stateTime / currentState.Timeout, 0f, 1f);
        }

        public T GetPreviousId() => hasPreviousState ? previousId : default;

        public string GetCurrentStateName() => currentState?.Id.ToString() ?? "Null";

        public bool MinTimeExceeded() => currentState != null && stateTime > currentState.MinTime;

        public bool IsInStateWithTag(string tag)
        {
            return currentState?.Tags.Contains(tag) ?? false;
        }

        public bool IsCurrentState(T id)
        {
            return currentState != null && currentState.Id.Equals(id);
        }

        public bool IsPreviousState(T id)
        {
            return hasPreviousState && previousId.Equals(id);
        }

        public List<T> GetValidTransitions()
        {
            if (currentState == null)
                return new();
        
            var valid = new List<T>();
        
            foreach (var transition in currentState.Transitions)
            {
                if (CanTransitionTo(transition.To))
                    valid.Add(transition.To);
            }
        
            return valid;
        }

        public bool IsTransitionOnCooldown(T from, T to)
        {
            if (!states.TryGetValue(from, out var state))
            {
                logger.LogError($"Can't find state with id: {from}");
                return false;
            }

            var transition = state.Transitions.Find(t => t.To.Equals(to));

            if (transition == null)
            {
                logger.LogError("Transition Does not exist");
                return false;
            }

            return transition.IsOnCooldown();
        }

        public void ResetTransitionCooldown(T from, T to)
        {
            if (!states.TryGetValue(from, out var state))
            {
                logger.LogError($"Can't find state with id: {from}");
                return;
            }

            var transition = state.Transitions.Find(t => t.To.Equals(to));

            if (transition == null)
            {
                logger.LogError("Transition Does not exist");
                return;
            }

            transition.Cooldown.Reset();
        }

        public bool IsStateOnCooldown(T id)
        {
            if (!states.TryGetValue(id, out var state))
            {
                logger.LogError($"Can't find state with id: {id}");
                return false;
            }

            return state.IsOnCooldown();
        }

        public void ResetStateCooldown(T id)
        {
            if (!states.TryGetValue(id, out var state))
            {
                logger.LogError($"Can't find state with id: {id}");
                return;
            }   

            state.Cooldown.Reset();
        }

        public void ResetAllCooldowns()
        {
            foreach (var kvp in states)
            {
                kvp.Value.Cooldown.Reset();

                foreach (var transition in kvp.Value.Transitions)
                    transition.Cooldown.Reset();
            }

            foreach (var transition in globalTransitions)
                transition.Cooldown.Reset();
        }

        public int GetActiveCooldownCount()
        {
            int count = 0;

            foreach (var kvp in states)
            {
                if (kvp.Value.IsOnCooldown())
                    count++;
            
                foreach (var transition in kvp.Value.Transitions)
                {
                    if (transition.IsOnCooldown())
                        count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Checks if the specified state (or any of its ancestors) is currently active
        /// </summary>
        public bool IsInHierarchy(T stateId)
        {
            var activeHierarchy = GetActiveHierarchy();
            return activeHierarchy.Contains(stateId);
        }
    
        /// <summary>
        /// Gets the currently active direct child of the specified parent
        /// Returns default(T) if parent is not active or has no active child
        /// </summary>
        public T GetActiveChild(T parentId)
        {
            var activeHierarchy = GetActiveHierarchy();
            int parentIndex = activeHierarchy.IndexOf(parentId);
        
            // Parent not in active hierarchy or is the leaf
            if (parentIndex == -1 || parentIndex == activeHierarchy.Count - 1)
                return default;
        
            return activeHierarchy[parentIndex + 1];
        }
    
        /// <summary>
        /// Gets the nesting depth of the specified state (0 = root, 1 = child, 2 = grandchild, etc.)
        /// </summary>
        public int GetDepth(T stateId)
        {
            return GetAncestors(stateId).Count;
        }
    
        /// <summary>
        /// Gets the root (top-level) state for the specified state
        /// </summary>
        public T GetRootState(T stateId)
        {
            var ancestors = GetAncestors(stateId);
            return ancestors.Count > 0 ? ancestors[ancestors.Count - 1] : stateId;
        }
    
        /// <summary>
        /// Checks if the specified state is a parent (has children)
        /// </summary>
        public bool IsParentState(T stateId)
        {
            return states.TryGetValue(stateId, out var state) && state.IsParent;
        }
    
        /// <summary>
        /// Checks if the specified state is a child (has a parent)
        /// </summary>
        public bool IsChildState(T stateId)
        {
            return states.TryGetValue(stateId, out var state) && state.IsChild;
        }
    
        /// <summary>
        /// Checks if the specified state is a leaf (has no children)
        /// </summary>
        public bool IsLeafState(T stateId)
        {
            return states.TryGetValue(stateId, out var state) && state.IsLeaf;
        }
    }
}