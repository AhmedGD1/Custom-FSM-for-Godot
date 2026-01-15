using System;
using System.Linq;
using System.Collections.Generic;

namespace Stately;

public partial class StateMachine<T>
{
    public State<T> AddState(T id)
    {
        if (states.TryGetValue(id, out State<T> value))
        {
            logger.LogWarning($"State With Id: {id}, Already Exists");
            return value;
        }

        var state = new State<T>(id);
        states[id] = state;

        if (!initialized)
            SetInitialId(id);

        if (initialized && states.ContainsKey(initialId))
            state.TimeoutTargetId = initialId;
        return state;
    }

    public State<T> AddChildState(T parentId, T childId)
    {
        if (!states.TryGetValue(parentId, out var parent))
        {
            logger.LogError($"Parent state {parentId} does not exist");
            return null;
        }
        
        if (states.TryGetValue(childId, out State<T> value))
        {
            logger.LogWarning($"State {childId} already exists");
            return value;
        }

        var childState = new State<T>(childId);
        states[childId] = childState;

        childState.ParentId = parentId;
        parent.ChildrenIds.Add(childId);

        if (parent.DefaultChildId.Equals(default(T)))
        {
            parent.SetDefaultChild(childId);
        }

        if (initialized && states.ContainsKey(initialId))
        {
            childState.TimeoutTargetId = parent.TimeoutTargetId;
        }

        return childState;
    }

    public State<T> ConfigState(T id, Action<State<T>> configure)
    {
        if (configure == null)
        {
            logger.LogError("Configure action can not be null");
            return null;
        }

        State<T> state;
        if (!states.TryGetValue(id, out state))
        {
            state = AddState(id);
        }

        configure(state);
        return state;
    }

    public bool RemoveState(T id)
    {
        if (isTransitioning)
        {
            logger.LogError("Cannot remove state during transition");
            return false;
        }

        if (!states.TryGetValue(id, out State<T> state))
        {
            logger.LogWarning($"State With Id: {id}, does not exist");
            return false;
        }

        if (states.Count == 1)
        {
            logger.LogError("Cannot remove the last state in the state machine");
            return false;
        }
        
        // Check if state has children
        if (state.IsParent && state.ChildrenIds.Count > 0)
        {
            logger.LogError($"Cannot remove parent state {id} - remove children first");
            return false;
        }

        bool wasCurrentState = currentState != null && currentState.Id.Equals(id);
        
        // Update initial state if removing it
        if (initialId.Equals(id))
        {
            var newId = states.Keys.FirstOrDefault(k => !k.Equals(id));
            initialId = newId;
        }
        
        // Remove from parent's children list
        if (state.IsChild)
        {
            var parent = GetState(state.ParentId);
            if (parent != null)
            {
                parent.ChildrenIds.Remove(id);
                
                // Update default child if we're removing it
                if (!parent.DefaultChildId.Equals(default(T)) && parent.DefaultChildId.Equals(id))
                {
                    parent.SetDefaultChild(parent.ChildrenIds.FirstOrDefault());
                }
            }
        }
        
        // Remove all transitions to/from this state
        foreach (var kvp in states)
            kvp.Value.RemoveTransition(id);
        globalTransitions.RemoveAll(t => t.To.Equals(id));

        states.Remove(id);
        
        if (wasCurrentState)
        {
            currentState = states[initialId];
            Reset();
        }
        
        SortTransitions();
        return true;
    }

    public void SetInitialId(T id)
    {
        if (!states.TryGetValue(id, out State<T> value))
        {
            logger.LogError($"State With Id: {id}, does not exist");
            return;
        }

        initialized = true;
        initialId = id;
        currentState = value;
    }

    public bool Reset()
    {
        if (states.Count == 0)
        {
            logger.LogWarning("Can not reset an empty state machine");
            return false;
        }

        if (!initialized)
        {
            logger.LogError("State Machine is not initialized. Call SetInitialId() first");
            return false;
        }

        history.ClearHistory();
        PerformTransition(initialId);
        hasPreviousState = false;
        previousId = default;

        return true;
    }

    public void RestartCurrentState(bool callEnter = true, bool callExit = false)
    {
        if (currentState?.IsLocked() ?? true)
        {
            logger.LogWarning("Can not restart current state since it is locked");
            return;
        }

        if (callEnter) currentState.Enter?.Invoke();
        if (callExit) currentState.Exit?.Invoke();

        ResetStateTime();
    }

    public State<T> GetState(T id)
    {
        return states.TryGetValue(id, out var state) ? state : null;
    }

    public State<T> GetStateWithTag(string tag)
    {
        foreach (var kvp in states)
        {
            if (kvp.Value.Tags.Contains(tag))
                return kvp.Value;
        }
        return null;
    }

    public List<State<T>> GetStatesWithTag(string tag)
    {
        var result = new List<State<T>>();

        foreach (var kvp in states)
        {
            if (kvp.Value.Tags.Contains(tag))
                result.Add(kvp.Value);
        }

        return result;
    }

    public State<T> GetParentState(T childId)
    {
        if (!states.TryGetValue(childId, out var child) || !child.IsChild)
            return null;
        return states.TryGetValue(child.ParentId, out var parent) ? parent : null;
    }

    public List<State<T>> GetChildren(T parentId)
    {
        if (!states.TryGetValue(parentId, out var parent))
            return new();
        
        return parent.ChildrenIds
            .Select(id => states.TryGetValue(id, out var state) ? state : null)
            .Where(s => s != null)
            .ToList();
    }

    public List<T> GetAncestors(T childId)
    {
        var ancestors = new List<T>();
        var current = GetState(childId);

        while (current != null && current.IsChild)
        {
            ancestors.Add(current.ParentId);
            current = GetState(current.ParentId);
        }

        return ancestors;
    }

    public List<T> GetHeirarchyPath(T id)
    {
        var path = new List<T>();
        var current = GetState(id);

        while (current != null)
        {
            path.Insert(0, current.Id);
            current = current.IsChild ? GetState(current.ParentId) : null;
        }

        return path;
    }

    public List<T> GetActiveHierarchy()
    {
        if (currentState == null)
            return new();
        return GetHeirarchyPath(currentState.Id);
    }

    public T ResolveToLeaf(T id)
    {
        if (!states.TryGetValue(id, out var state))
        {
            logger.LogError($"State {id} does not exist");
            return id;
        }

        while (state.IsParent)
        {
            if (state.DefaultChildId.Equals(default(T)))
            {
                logger.LogError($"Parent state {id} has no default child set");
                return id; // Return as-is, will fail in transition
            }

            id = state.DefaultChildId;

            if (!states.TryGetValue(id, out state))
            {
                logger.LogError($"Default child {id} does not exist");
                return id;
            }
        }

        return id;
    }

    /// <summary>
    /// Validates that all parent states have default children set
    /// </summary>
    public bool ValidateHierarchy(out List<string> errors)
    {
        errors = new List<string>();
        
        foreach (var kvp in states)
        {
            var state = kvp.Value;
            
            // Parent must have default child
            if (state.IsParent && state.DefaultChildId.Equals(default(T)))
            {
                errors.Add($"Parent state {state.Id} has no default child set");
            }
            
            // Default child must exist
            if (!state.DefaultChildId.Equals(default(T)))
            {
                if (!states.ContainsKey(state.DefaultChildId))
                {
                    errors.Add($"State {state.Id} has invalid default child {state.DefaultChildId}");
                }
                else if (!state.ChildrenIds.Contains(state.DefaultChildId))
                {
                    errors.Add($"State {state.Id} default child {state.DefaultChildId} is not in children list");
                }
            }
            
            // Parent reference must be valid
            if (state.IsChild)
            {
                if (!states.ContainsKey(state.ParentId))
                {
                    errors.Add($"State {state.Id} has invalid parent {state.ParentId}");
                }
            }
            
            // Check for cycles
            var ancestors = new HashSet<T>();
            var current = state;
            while (current != null && current.IsChild)
            {
                if (ancestors.Contains(current.Id))
                {
                    errors.Add($"Circular parent relationship detected at {current.Id}");
                    break;
                }
                ancestors.Add(current.Id);
                current = !current.ParentId.Equals(default(T)) ? GetState(current.ParentId) : null;
            }
        }
        
        return errors.Count == 0;
    }
}

