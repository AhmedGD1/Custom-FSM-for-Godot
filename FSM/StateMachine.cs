using System;
using System.Linq;
using System.Collections.Generic;

namespace Godot.FSM;


/// <summary>
/// Logger to tag errors (consider using an adaper to make it actually work)
/// </summary>
public interface ILogger
{
    void LogError(string text);
    void LogWarning(string text);
}

public class DefaultLogger : ILogger
{
    public void LogError(string text) => GD.PushError(text);
    public void LogWarning(string text) => GD.PushWarning(text);
}

public enum FSMProcessMode
{
    Physics,
    Idle
}


public enum FSMLockMode
{
    None,
    Full,
    Transition
}

public class StateMachine<T> where T : Enum
{
    private const int MAX_QUEUED_TRANSITIONS = 20;
    private const string TRANSITION_PER_DATA = "__transition_data__";

    public event Action<T, T> StateChanged;
    public event Action<T> TimeoutBlocked;
    public event Action<T> StateTimeout;
    public event Action<T, T> TransitionTriggered;

    private readonly Dictionary<T, State<T>> states = new();
    private readonly Dictionary<string, object> globalData = new();

    private readonly List<Transition<T>> globalTransitions = new();
    private readonly List<Transition<T>> cachedSortedTransitions = new();
    private readonly List<Transition<T>> activeTransitions = new();
    private readonly Queue<T> pendingTransitions = new();
    
    private readonly Dictionary<string, List<Action>> eventListeners = new();
    private readonly Queue<string> pendingEvents = new();

    private State<T> currentState;

    private T initialId;
    private T previousId;

    private bool initialized;
    private bool hasPreviousState;
    private bool paused;
    private bool transitionDirty = true;
    private bool isTransitioning;
    private bool isProcessingEvent;

    private float stateTime;
    private float lastStateTime;

    private ILogger logger;

    public StateMachine(ILogger logger = null)
    {
        this.logger = logger ?? new DefaultLogger();
    }

    public State<T> AddState(T id)
    {
        if (states.ContainsKey(id))
        {
            logger.LogError($"State with id: {id} already exists");
            return null;
        }

        var state = new State<T>(id);
        states[id] = state;

        if (!initialized)
        {
            initialId = id;
            initialized = true;
        }

        state.SetRestart(initialId);
        return state;
    }

    public void Start()
    {
        if (initialized)
            ChangeStateInternal(initialId, ignoreExit: true);
    }

    public bool RemoveState(T id)
    {
        if (!states.ContainsKey(id))
        {
            logger.LogWarning($"State with id: {id} does not exist to be removed !");
            return false;
        }

        states.Remove(id);

        if (initialId.Equals(id))
            initialized = false;
        
        if (currentState?.Id.Equals(id) ?? false)
        {
            if (states.Count > 0)
            {
                SetInitialId(states.Values.First().Id);
                Reset();
            }
            else
            {
                currentState = null;
                initialized = false;
                hasPreviousState = false;
            }
        }

        foreach (var state in states.Values)
            state.Transitions.RemoveAll(t => t.To.Equals(id));
        globalTransitions.RemoveAll(t => t.To.Equals(id));

        ReSortTransitions();
        return true;
    }

    public bool Reset()
    {
        if (states.Count == 0)
        {
            logger.LogWarning("State Machine can Reset while being Empty !");
            return false;
        }

        if (!initialized)
        {
            logger.LogWarning("State Machine not initialized - call SetInitialId() first");
            return false;
        }

        ChangeStateInternal(initialId);
        hasPreviousState = false;
        previousId = default;
        return true;
    }

    public void SetInitialId(T id)
    {
        if (!states.ContainsKey(id))
        {
            logger.LogError($"State with this id does not exist");
            return;
        }

        initialId = id;
        initialized = true;
    }

    public void RestartCurrentState(bool ignoreExit = false, bool ignoreEnter = false)
    {
        if (currentState == null)
        {
            logger.LogWarning("Can't restart current state as it does not exist");
            return;
        }

        ResetStateTime();

        if (!ignoreExit && !currentState.IsLocked()) currentState.Exit?.Invoke();
        if (!ignoreEnter) currentState.Enter?.Invoke();
    }

    public void ResetStateTime()
    {
        lastStateTime = stateTime;
        stateTime = 0f;
    }

    public bool TryChangeState(T id, Func<bool> condition = null, object data = null)
    {
        if (!(condition?.Invoke() ?? true))
            return false;

        if (!states.ContainsKey(id))
            return false;

       
        if (data != null)
            SetData(TRANSITION_PER_DATA, data);
        ChangeStateInternal(id);
        return true;
    }

    public bool TryGoBack()
    {
        if (!hasPreviousState || !states.ContainsKey(previousId) || (currentState?.IsLocked() ?? false))
        {
            logger.LogError("Can't go back to previous state");
            return false;
        }

        ChangeStateInternal(previousId);
        return true;
    }

    private void ChangeStateInternal(T id, bool ignoreExit = false)
    {
        if (isTransitioning)
        {
            if (pendingTransitions.Count >= MAX_QUEUED_TRANSITIONS)
            {
                logger.LogError($"Too many queued transitions ({MAX_QUEUED_TRANSITIONS})! Possible infinite loop?");
                return;
            }
            pendingTransitions.Enqueue(id);
            return;
        }

        if (!states.TryGetValue(id, out var value))
        {
            logger.LogWarning($"Can not change state to {id} as it does not exist");
            return;
        }

        isTransitioning = true;

        try
        {
            bool canExit = !ignoreExit && currentState != null && !currentState.IsLocked();
            if (canExit) currentState.Exit?.Invoke();

            lastStateTime = stateTime;
            stateTime = 0f;

            if (currentState != null)
            {
                previousId = currentState.Id;
                hasPreviousState = true;
            }

            currentState = value;
            currentState.Enter?.Invoke();

            ReSortTransitions();

            if (initialized)
                StateChanged?.Invoke(previousId, currentState.Id);
        
            while (pendingTransitions.Count > 0)
            {
                var nextId = pendingTransitions.Dequeue();
                isTransitioning = false;
                ChangeStateInternal(nextId);
                isTransitioning = true;
            }
        }
        finally
        {
            isTransitioning = false;
            RemoveGlobalData(TRANSITION_PER_DATA);
        }
    }

    public Transition<T> AddTransition(T from, T to)
    {
        if (!states.TryGetValue(from, out var state))
        {
            logger.LogError($"Can not transition as (From state) does not exist");
            return null;
        }

        if (!states.ContainsKey(to))
        {
            logger.LogError($"Can not transition as (To state) does not exist");
            return null;
        }

        var transition = state.AddTransition(to);
        ReSortTransitions();
        return transition;
    }

    public void AddTransitions(T[] from, T to, Predicate<StateMachine<T>> condition)
    {
        if (from == null) 
        {
            logger.LogError("from array is null");
            return;
        }
        
        for (int i = 0; i < from.Length; i++)
            AddTransition(from[i], to)?.SetCondition(condition);
    }

    public Transition<T> AddGlobalTransition(T to)
    {
        if (!states.ContainsKey(to))
        {
            logger.LogError($"Can not transition as (To state) does not exist");
            return null;
        }

        var transition = new Transition<T>(default, to);
        globalTransitions.Add(transition);

        ReSortTransitions();
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
            logger.LogError($"No Transition Was Found Between: {from} -> {to}");
        
        ReSortTransitions();
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

        ReSortTransitions();
        return true;
    }

    public void ClearTransitionsFrom(T id)
    {
        if (!states.TryGetValue(id, out var state))
        {
            logger.LogWarning($"State with id: {id} does not exist");
            return;
        }
        state.Transitions.Clear();
        ReSortTransitions();
    }

    public void ClearTransitions()
    {
        foreach (var state in states.Values)
            state.Transitions.Clear();
        ReSortTransitions();
    }

    public void ClearGlobalTransitions()
    {
        globalTransitions.Clear();
        ReSortTransitions();
    }

    private void ReSortTransitions()
    {
        transitionDirty = true;
    }

    public void SendEvent(string eventName)
    {
        if (string.IsNullOrEmpty(eventName))
            return;
        pendingEvents.Enqueue(eventName);
    }

    public void OnEvent(string eventName, Action callback)
    {
        if (string.IsNullOrEmpty(eventName))
            return;
        
        if (!eventListeners.ContainsKey(eventName))
            eventListeners[eventName] = new();
        eventListeners[eventName].Add(callback);
    }

    public void RemoveEventListener(string eventName, Action callback)
    {
        if (eventListeners.TryGetValue(eventName, out var listeners))
        {
            listeners.Remove(callback);
            if (listeners.Count == 0)
                eventListeners.Remove(eventName);
        }
    }

    private void ProcessEvents()
    {
        while (pendingEvents.Count > 0)
        {
            var eventName = pendingEvents.Dequeue();

            if (eventListeners.TryGetValue(eventName, out var listeners))
            {
                int count = listeners.Count;

                for (int i = 0; i < count; i++)
                    listeners[i]?.Invoke();
            }

            if (cachedSortedTransitions.Count > 0)
                CheckEventTransitions(eventName);
        }
    }

    private void CheckEventTransitions(string eventName)
    {
        if (isProcessingEvent) return;

        isProcessingEvent = true;

        try
        {
            foreach (var transition in cachedSortedTransitions)
            {
                if (string.IsNullOrEmpty(transition.EventName))
                    continue;

                if (transition.EventName != eventName)
                    continue;

                float requiredTime = transition.OverrideMinTime > 0f ? transition.OverrideMinTime : currentState.MinTime;
                bool timeRequirementMet = stateTime > requiredTime || transition.ForceInstantTransition;
                bool guardPassed = transition.Guard?.Invoke(this) ?? true;

                if (timeRequirementMet && guardPassed && (transition.Condition?.Invoke(this) ?? true))
                {
                    ChangeStateInternal(transition.To);
                    transition.Triggered?.Invoke();
                    TransitionTriggered?.Invoke(transition.From, transition.To);
                    return;
                }
            }
        }
        finally
        {
            isProcessingEvent = false;
        }
    }

    public void Process(FSMProcessMode mode, double delta)
    {
        if (paused || currentState == null) return;

        if (currentState.ProcessMode == mode)
        {
            stateTime += (float)delta;
            currentState.Update?.Invoke(delta);
            CheckTransitions();
        }
    }

    private void CheckTransitions()
    {
        if (currentState == null) return;

        ProcessEvents();

        bool timeoutTriggered = currentState.Timeout > 0f && stateTime >= currentState.Timeout;

        if (timeoutTriggered)
        {
            if (currentState.IsFullyLocked())
            {
                TimeoutBlocked?.Invoke(currentState.Id);
                return;
            }

            var restartId = currentState.RestartId;
            var fromId = currentState.Id;

            if (!states.ContainsKey(restartId))
            {
                logger.LogError($"RestartId {restartId} doesn't exist for state {fromId}");
                return;
            }

            currentState.Callback?.Invoke();
            StateTimeout?.Invoke(fromId);
            ChangeStateInternal(restartId);
            TransitionTriggered?.Invoke(fromId, restartId);
            return;
        }

        if (currentState.TransitionBlocked()) return;

        RebuildTransitionCache();

        if (cachedSortedTransitions.Count > 0)
            CheckTransitionLoop();
    }

    private void CheckTransitionLoop()
    {
        foreach (var transition in activeTransitions)
        {
            if (!transition.Guard?.Invoke(this) ?? true)
                continue;
            
            float requiredTime = transition.OverrideMinTime > 0f ? transition.OverrideMinTime : currentState.MinTime;
            
            if (stateTime <= requiredTime && !transition.ForceInstantTransition)
                continue;

            if (transition.Condition?.Invoke(this) ?? false)
            {
                ChangeStateInternal(transition.To);
                transition.Triggered?.Invoke();
                TransitionTriggered?.Invoke(transition.From, transition.To);
                return;
            }
        }
    }

    private void RebuildTransitionCache()
    {
        if (!transitionDirty) return;

        cachedSortedTransitions.Clear();
        cachedSortedTransitions.AddRange(currentState.Transitions);
        cachedSortedTransitions.AddRange(globalTransitions);
        cachedSortedTransitions.Sort(Transition<T>.Compare);

        activeTransitions.Clear();
        foreach (var transition in cachedSortedTransitions)
            if (string.IsNullOrEmpty(transition.EventName))
                activeTransitions.Add(transition);

        transitionDirty = false;
    }

    public bool SetData(string id, object data)
    {
        if (string.IsNullOrEmpty(id)) 
            return false;
        globalData[id] = data;
        return true;
    }

    public bool RemoveGlobalData(string id)
    {
        if (!globalData.ContainsKey(id))
            return false;
        globalData.Remove(id);
        return true;
    }

    public bool TryGetData<TData>(string id, out TData data)
    {
        if (globalData.TryGetValue(id, out var value) && value is TData castValue)
        {
            data = castValue;
            return true;
        }
        data = default;
        return false;
    }

    public bool TryGetPerTransitionData<TData>(out TData data)
    {
        if (globalData.TryGetValue(TRANSITION_PER_DATA, out var value) && value is TData castValue)
        {
            data = castValue;
            return true;
        }
        data = default;
        return false;
    }

    public bool IsActive() => !paused;
    public void Pause() => paused = true;
    public void Resume(bool resetTime = false)
    {   
        if (resetTime)
            ResetStateTime();
        paused = false;
    }

    /// <summary>
    /// Gets how long the state machine was in the previous state before transitioning
    /// </summary>
    public float GetPreviousStateTime() => hasPreviousState ? lastStateTime : -1f;
    public float GetStateTime() => stateTime;
    public float GetMinStateTime() => currentState?.MinTime ?? -1f;
    public float GetRemainingTime() => currentState?.Timeout > 0f ? Math.Max(0f, currentState.Timeout - stateTime) : -1f;

    public State<T> GetState(T id) => states.TryGetValue(id, out var result) ? result : null;

    public State<T> GetStateWithTag(string tag)
    {
        foreach (var kvp in states)
            if (kvp.Value.HasTag(tag))
                return kvp.Value;
        return null;
    }

    public float GetTimeoutProgress()
    {
        if (currentState == null || currentState.Timeout <= 0f)
            return -1f;
        return Math.Clamp(stateTime / currentState.Timeout, 0f, 1f);
    }

    public T GetCurrentId() => currentState != null ? currentState.Id : default;
    public T GetInitialId() => initialized ? initialId : default;
    
    public bool TryGetPreviousId(out T id)
    {
        id = hasPreviousState ? previousId : default;
        return hasPreviousState;
    }

    public bool HasTransition(T from, T to)
    {
        if (!states.TryGetValue(from, out var state))
            return false;
        
        for (int i = 0; i < state.Transitions.Count; i++)
        {
            if (state.Transitions[i].To.Equals(to))
                return true;
        }
        return false;
    }
    
    public bool HasGlobalTransition(T id)
    {
        for (int i = 0; i < globalTransitions.Count; i++)
        {
            if (globalTransitions[i].To.Equals(id))
                return true;
        }
        return false;
    }

    public bool HasTransitionFrom(T from) => states.TryGetValue(from, out var state) && state.Transitions.Count > 0;
    
    public bool HasState(T id) => states.ContainsKey(id);
    public bool IsCurrentState(T id) => currentState?.Id.Equals(id) ?? false;
    public bool IsPreviousState(T id) => hasPreviousState && Equals(previousId, id);
    public bool IsInStateWithTag(string tag) => currentState?.Tags.Contains(tag) ?? false;
 
    public bool CanEnterState(T id) => states.ContainsKey(id) && !(currentState?.IsFullyLocked() ?? false);

    public string DebugCurrentTransition()
    {
        if (currentState == null) return "Not Started";
        return hasPreviousState 
            ? $"{previousId} -> {currentState.Id}" 
            : $"[Initial] -> {currentState.Id}";
    }

    public string DebugAllTransitions()
    {
        var result = new List<string>();
        foreach (var state in states.Values)
            foreach (var t in state.Transitions)
                result.Add($"{t.From} -> {t.To} (Priority: {t.Priority})");

        foreach (var t in globalTransitions)
            result.Add($"GLOBAL -> {t.To} (Priority: {t.Priority})");

        return string.Join("\n", result);
    }

    public string DebugAllStates()
    {
        var result = new List<string>();
        foreach (var state in states.Values)
            result.Add(state.Id.ToString());
        return string.Join("\n", result);
    }


}

