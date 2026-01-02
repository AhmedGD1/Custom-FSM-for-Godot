using System;
using System.Collections.Generic;
using System.Linq;

namespace Godot.FSM;

public class StateMachine<T> : IDisposable where T : Enum
{
    public event Action<T, T> StateChanged;
    public event Action<T, T> TransitionTriggered;
    public event Action<T> TimeoutBlocked;
    public event Action<T> StateTimeout;

    private const int MaxQueuedTransitions = 20;
    private const string DataPerTransition = "45u349gng66__transition_data__8934u89grg85";

    private Dictionary<T, State<T>> states = new();
    private Dictionary<string, object> globalData = new();

    private List<Transition<T>> globalTransitions = new();
    private List<Transition<T>> cachedSortedTransitions = new();
    private Queue<T> pendingTransitions = new();

    private Dictionary<string, List<Action>> eventListeners = new();
    private Queue<string> pendingEvents = new();

    private State<T> currentState;

    private T initialId;
    private T previousId;

    private bool initialized;
    private bool hasPreviousState;
    private bool paused;
    private bool isTransitioning;
    private bool isProcessingEvent;
    private bool disposed = false;

    private float stateTime;
    private float lastStateTime;

    private ILogger logger;

    public StateMachine(ILogger logger = null)
    {
        this.logger = logger ?? new DefaultLogger();
    }

    public void Dispose()
    {
        if (disposed) return;

        ClearEventListeners();
        ClearTransitions();
        ClearGlobalTransitions();
        states.Clear();

        disposed = true;
    }

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
            state.SetTimeoutId(initialId);
        return state;
    }

    public void Start()
    {
        if (initialized)
            ChangeStateInternal(initialId, bypassExit: true);
    }

    public bool RemoveState(T id)
    {
        if (!states.TryGetValue(id, out State<T> state))
        {
            logger.LogWarning($"State With Id: {id}, does not exist");
            return false;
        }

        // Can't remove the only state
        if (states.Count == 1)
        {
            logger.LogError("Cannot remove the last state in the state machine");
            return false;
        }

        // If removing the initial state, pick a new one
        if (initialId.Equals(id))
        {
            // Find first state that isn't the one being removed
            var newId = states.Keys.FirstOrDefault(k => !k.Equals(id));
            SetInitialId(newId);
        }
        
        // If it's the current state, reset
        if (currentState != null && currentState.Id.Equals(state.Id))
        {
            Reset();
        }

        // Remove all transitions pointing to this state
        foreach (var kvp in states)
            kvp.Value.RemoveTransition(id);
        globalTransitions.RemoveAll(t => t.To.Equals(id));

        states.Remove(id);
        SortTransitions();
        
        return true;
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

        ChangeStateInternal(initialId);
        hasPreviousState = false;
        previousId = default;

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

    public void ResetStateTime()
    {
        lastStateTime = stateTime;
        stateTime = 0f;
    }

    public bool TryChangeState(T to, Func<bool> condition = null, object data = null)
    {
        if (!(condition?.Invoke() ?? true) || !MintimeExceeded())
            return false;
        
        if (!states.ContainsKey(to))
            return false;
        
        if (data != null)
            SetData(DataPerTransition, data);
        
        ChangeStateInternal(to);
        return true;
    }

    public bool TryGoBack()
    {
        if (!hasPreviousState || !states.ContainsKey(previousId) || (currentState?.IsLocked() ?? true))
            return false;
        
        ChangeStateInternal(previousId);
        return true;
    }

    public void ChangeStateInternal(T id, bool bypassExit = false)
    {
        if (isTransitioning)
        {
            if (pendingTransitions.Count >= MaxQueuedTransitions)
            {
                logger.LogError($"Too many queued transitions ({MaxQueuedTransitions})! Possible infinite loop?");
                return;
            }

            pendingTransitions.Enqueue(id);
            return;
        }

        isTransitioning = true;

        try
        {
            bool canExit = !bypassExit && currentState != null && !currentState.IsLocked();

            if (canExit)
                currentState.Exit?.Invoke();
            
            ResetStateTime();

            if (currentState != null)
            {
                previousId = currentState.Id;
                hasPreviousState = true;
            }

            currentState = states[id];
            currentState.Enter?.Invoke();

            SortTransitions();

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
            RemoveData(DataPerTransition);
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
            AddTransition(from[i], to)?.SetCondition(condition);
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

    public void SendEvent(string eventName)
    {
        if (string.IsNullOrEmpty(eventName))
        {
            logger.LogError("Event Name is invalid");
            return;
        }

        pendingEvents.Enqueue(eventName);
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
                foreach (var callback in listener)
                    callback?.Invoke();
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
                
                bool guardPassed = transition.Guard?.Invoke(this) ?? true;
                
                float requiredTime = transition.OverrideMinTime > 0f ? transition.OverrideMinTime : currentState.MinTime;
                bool timeRequirementMet = transition.ForceInstantTransition || stateTime > requiredTime;

                if (guardPassed && timeRequirementMet)
                {
                    ChangeStateInternal(transition.To);
                    TransitionTriggered?.Invoke(transition.From, transition.To);

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

    public void Process(FSMProcessMode mode, float delta)
    {
        if (paused || currentState == null)
            return;

        if (currentState.ProcessMode == mode)
        {
            stateTime += delta;
            currentState.Update?.Invoke(delta);

            CheckTransitions();
        }
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

        currentState.OnTimeoutTriggered?.Invoke();
        StateTimeout?.Invoke(fromId);
        TransitionTriggered?.Invoke(fromId, timeoutId);
        ChangeStateInternal(timeoutId);
    }

    private void CheckTransitionLoop()
    {
        foreach (var transition in cachedSortedTransitions)
        {
            bool guardPassed = transition.Guard?.Invoke(this) ?? true;

            if (!guardPassed)
                continue;
            
            float requiredTime = transition.OverrideMinTime > 0f ? transition.OverrideMinTime : currentState.MinTime;

            if (stateTime < requiredTime && !transition.ForceInstantTransition)
                continue;
            
            if (transition.Condition?.Invoke(this) ?? false)
            {
                ChangeStateInternal(transition.To);
                transition.OnTriggered?.Invoke();
                TransitionTriggered?.Invoke(transition.From, transition.To);

                return;
            }
        }
    }

    public void SetData(string id, object value)
    {
        if (string.IsNullOrEmpty(id))
        {
            logger.LogError("ID is invalid");
            return;
        }
        globalData[id] = value;
    }

    private bool RemoveData(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            logger.LogError("ID is invalid");
            return false;
        }

        return globalData.Remove(id);
    }

    public bool TryGetData<TData>(string id, out TData value)
    {
        if (globalData.TryGetValue(id, out var result) && result is TData castValue)
        {
            value = castValue;
            return true;
        }
        value = default;
        return false;
    }

    public TData GetPerTransitionData<TData>()
    {
        return globalData.TryGetValue(DataPerTransition, out var result) && result is TData data ? data : default;
    }

    public bool IsActive() => !paused;
    public void TogglePaused(bool toggle) => paused = toggle;
    public void Pause() => paused = true;
    public void Resume() => paused = false;

    public float GetLastStateTime() => lastStateTime;
    public float GetStateTime() => stateTime;

    public float GetMinStateTime()
    {
        if (currentState == null)
            throw new NullReferenceException();
        return currentState.MinTime;
    }

    public float GetRemainingTime() => 
        currentState != null && currentState.Timeout > 0f ? Math.Max(0f, currentState.Timeout - stateTime) : -1f;
    
    public State<T> GetState(T id)
    {
        return states.TryGetValue(id, out var state) ? state : null;
    }

    public State<T> GetStateByTag(string tag)
    {
        foreach (var kvp in states)
        {
            if (kvp.Value.Tags.Contains(tag))
                return kvp.Value;
        }
        return null;
    }

    public float GetTimeoutProgress()
    {
        if (currentState == null || currentState.Timeout <= 0f)
            return -1f;
        return Math.Clamp(stateTime / currentState.Timeout, 0f, 1f);
    }

    public T GetCurrentId() => currentState != null ? currentState.Id : default;
    public T GetPreviousId() => hasPreviousState ? previousId : default;

    public string GetCurrentStateName() => currentState?.Id.ToString() ?? "Null";

    public bool MintimeExceeded() => currentState != null && stateTime > currentState.MinTime;

    public bool HasTransition(T from, T to)
    {
        if (!states.TryGetValue(from, out var state))
            return false;
        return state.Transitions.Find(t => t.To.Equals(to)) != null;
    }

    public bool HasGlobalTransition(T to)
    {
        return globalTransitions.Find(t => t.To.Equals(to)) != null;
    }

    public bool HasAnyGlobalTransitions() => globalTransitions.Count > 0;

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

    public List<State<T>> GetStatesWithTag(string tag)
    {
        List<State<T>> result = new();

        foreach (var kvp in states)
        {
            if (kvp.Value.Tags.Contains(tag))
                result.Add(kvp.Value);
        }

        return result;
    }

    public List<Transition<T>> GetAvailableTransitions()
    {
        if (currentState == null)
            return null;
        return currentState.Transitions;
    }

    public bool IsValid()
    {
        return currentState != null;
    }
}

public enum FSMProcessMode
{
    Idle,
    Fixed
}

public enum FSMLockMode
{
    None,
    Full,
    Transition
}

public interface ILogger
{
    void LogError(string text);
    void LogWarning(string text);
}

public class DefaultLogger : ILogger
{
    /// <summary>
    /// Logs an error message using GD.PushError.
    /// </summary>
    /// <param name="text">The error message to log.</param>
    public void LogError(string text) => GD.PushError(text);
    
    /// <summary>
    /// Logs a warning message using GD.PushWarning.
    /// </summary>
    /// <param name="text">The warning message to log.</param>
    public void LogWarning(string text) => GD.PushWarning(text);
}

