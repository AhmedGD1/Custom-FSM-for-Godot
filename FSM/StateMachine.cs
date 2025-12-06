using System;
using System.Linq;
using System.Collections.Generic;

namespace Godot.FSM;


/// <summary>
/// Logger to tag errors (consider using an adaper to make it actually work)
/// </summary>
public interface ILogger
{
    /// <summary>
    /// Checks if a global transition exists to the specified state.
    /// </summary>
    /// <param name="id">The target state identifier.</param>
    /// <returns>True if a global transition exists, false otherwise.</returns>
    /// Logs an error message.
    /// </summary>
    /// <param name="text">The error message to log.</param>
    void LogError(string text);
    
    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="text">The warning message to log.</param>
    void LogWarning(string text);
}

/// <summary>
/// Default logger implementation that uses Godot's built-in logging functions.
/// </summary>
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

/// <summary>
/// Defines when the state machine processes updates.
/// </summary>
public enum FSMProcessMode
{
    /// <summary>
    /// Process during physics frames (_PhysicsProcess).
    /// </summary>
    Physics,
    
    /// <summary>
    /// Process during idle frames (_Process).
    /// </summary>
    Idle
}

/// <summary>
/// Defines how transitions are locked in a state.
/// </summary>
public enum FSMLockMode
{
    /// <summary>
    /// No locking - transitions can occur freely.
    /// </summary>
    None,
    
    /// <summary>
    /// Fully locked - no transitions or exit callbacks.
    /// </summary>
    Full,
    
    /// <summary>
    /// Transitions locked but exit callbacks still execute.
    /// </summary>
    Transition
}

/// <summary>
/// A generic finite state machine implementation with support for transitions, events, timeouts, and data management.
/// </summary>
/// <typeparam name="T">The enum type representing state identifiers.</typeparam>
public class StateMachine<T> where T : Enum
{
    private const int MAX_QUEUED_TRANSITIONS = 20;
    private const string TRANSITION_PER_DATA = "__transition_data__";

    /// <summary>
    /// Event triggered when the state changes. Parameters are (previousState, newState).
    /// </summary>
    public event Action<T, T> StateChanged;
    
    /// <summary>
    /// Event triggered when a timeout is blocked due to a locked state.
    /// </summary>
    public event Action<T> TimeoutBlocked;
    
    /// <summary>
    /// Event triggered when a state times out.
    /// </summary>
    public event Action<T> StateTimeout;
    
    /// <summary>
    /// Event triggered when a transition occurs. Parameters are (fromState, toState).
    /// </summary>
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

    /// <summary>
    /// Initializes a new instance of the StateMachine class.
    /// </summary>
    /// <param name="logger">Optional custom logger. If null, uses DefaultLogger.</param>
    public StateMachine(ILogger logger = null)
    {
        this.logger = logger ?? new DefaultLogger();
    }

    /// <summary>
    /// Adds a new state to the state machine.
    /// </summary>
    /// <param name="id">The unique identifier for the state.</param>
    /// <returns>The created State object, or null if a state with this id already exists.</returns>
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

    /// <summary>
    /// Starts the state machine by transitioning to the initial state.
    /// </summary>
    public void Start()
    {
        if (initialized)
            ChangeStateInternal(initialId, ignoreExit: true);
    }

    /// <summary>
    /// Removes a state from the state machine.
    /// </summary>
    /// <param name="id">The identifier of the state to remove.</param>
    /// <returns>True if the state was successfully removed, false otherwise.</returns>
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

    /// <summary>
    /// Resets the state machine to the initial state.
    /// </summary>
    /// <returns>True if reset was successful, false if the state machine is empty or not initialized.</returns>
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

    /// <summary>
    /// Sets the initial state that the state machine will start in.
    /// </summary>
    /// <param name="id">The identifier of the state to set as initial.</param>
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

    /// <summary>
    /// Restarts the current state by calling its exit and enter callbacks.
    /// </summary>
    /// <param name="ignoreExit">If true, skips the exit callback.</param>
    /// <param name="ignoreEnter">If true, skips the enter callback.</param>
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

    /// <summary>
    /// Resets the state time counter to zero, storing the previous time.
    /// </summary>
    public void ResetStateTime()
    {
        lastStateTime = stateTime;
        stateTime = 0f;
    }

    /// <summary>
    /// Attempts to change to a specified state if the condition is met.
    /// </summary>
    /// <param name="id">The identifier of the target state.</param>
    /// <param name="condition">Optional condition function that must return true for the transition to occur.</param>
    /// <param name="data">Optional data to pass with the transition.</param>
    /// <returns>True if the state change was successful, false otherwise.</returns>
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

    /// <summary>
    /// Attempts to transition back to the previous state.
    /// </summary>
    /// <returns>True if the transition was successful, false if there is no previous state or the current state is locked.</returns>
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

    /// <summary>
    /// Internal method to handle state transitions with queuing support.
    /// </summary>
    /// <param name="id">The identifier of the target state.</param>
    /// <param name="ignoreExit">If true, skips the exit callback of the current state.</param>
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

    /// <summary>
    /// Adds a transition between two states.
    /// </summary>
    /// <param name="from">The source state identifier.</param>
    /// <param name="to">The target state identifier.</param>
    /// <returns>The created Transition object, or null if either state doesn't exist.</returns>
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

    /// <summary>
    /// Adds transitions from multiple source states to a single target state with a shared condition.
    /// </summary>
    /// <param name="from">Array of source state identifiers.</param>
    /// <param name="to">The target state identifier.</param>
    /// <param name="condition">The condition that must be met for the transitions to occur.</param>
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

    /// <summary>
    /// Adds a global transition that can be triggered from any state.
    /// </summary>
    /// <param name="to">The target state identifier.</param>
    /// <returns>The created Transition object, or null if the target state doesn't exist.</returns>
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

    /// <summary>
    /// Removes a transition between two states.
    /// </summary>
    /// <param name="from">The source state identifier.</param>
    /// <param name="to">The target state identifier.</param>
    /// <returns>True if the transition was removed, false otherwise.</returns>
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

    /// <summary>
    /// Removes a global transition to the specified state.
    /// </summary>
    /// <param name="to">The target state identifier.</param>
    /// <returns>True if the global transition was removed, false otherwise.</returns>
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

    /// <summary>
    /// Clears all transitions from a specific state.
    /// </summary>
    /// <param name="id">The state identifier.</param>
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

    /// <summary>
    /// Clears all transitions from all states (does not affect global transitions).
    /// </summary>
    public void ClearTransitions()
    {
        foreach (var state in states.Values)
            state.Transitions.Clear();
        ReSortTransitions();
    }

    /// <summary>
    /// Clears all global transitions.
    /// </summary>
    public void ClearGlobalTransitions()
    {
        globalTransitions.Clear();
        ReSortTransitions();
    }

    /// <summary>
    /// Marks transitions as dirty to trigger re-sorting on next check.
    /// </summary>
    private void ReSortTransitions()
    {
        transitionDirty = true;
    }

    /// <summary>
    /// Queues an event to be processed during the next update cycle.
    /// </summary>
    /// <param name="eventName">The name of the event to send.</param>
    public void SendEvent(string eventName)
    {
        if (string.IsNullOrEmpty(eventName))
            return;
        pendingEvents.Enqueue(eventName);
    }

    /// <summary>
    /// Registers a callback to be invoked when a specific event is sent.
    /// </summary>
    /// <param name="eventName">The name of the event to listen for.</param>
    /// <param name="callback">The callback to invoke when the event occurs.</param>
    public void OnEvent(string eventName, Action callback)
    {
        if (string.IsNullOrEmpty(eventName))
            return;
        
        if (!eventListeners.ContainsKey(eventName))
            eventListeners[eventName] = new();
        eventListeners[eventName].Add(callback);
    }

    /// <summary>
    /// Removes a registered event listener.
    /// </summary>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="callback">The callback to remove.</param>
    public void RemoveEventListener(string eventName, Action callback)
    {
        if (eventListeners.TryGetValue(eventName, out var listeners))
        {
            listeners.Remove(callback);
            if (listeners.Count == 0)
                eventListeners.Remove(eventName);
        }
    }

    /// <summary>
    /// Processes all pending events and triggers their registered callbacks and transitions.
    /// </summary>
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

    /// <summary>
    /// Checks if any event-based transitions should trigger for the given event.
    /// </summary>
    /// <param name="eventName">The name of the event to check.</param>
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

    /// <summary>
    /// Main process method to update the state machine. Should be called from _Process or _PhysicsProcess.
    /// </summary>
    /// <param name="mode">The process mode (Physics or Idle).</param>
    /// <param name="delta">The time elapsed since the last frame.</param>
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

    /// <summary>
    /// Checks and processes all transitions including timeouts and condition-based transitions.
    /// </summary>
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

    /// <summary>
    /// Iterates through active transitions and triggers the first valid one.
    /// </summary>
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

    /// <summary>
    /// Rebuilds the cached transition list if marked dirty, sorting by priority.
    /// </summary>
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

    /// <summary>
    /// Stores global data that can be accessed from any state.
    /// </summary>
    /// <param name="id">The key to store the data under.</param>
    /// <param name="data">The data to store.</param>
    /// <returns>True if the data was stored successfully, false if the id is null or empty.</returns>
    public bool SetData(string id, object data)
    {
        if (string.IsNullOrEmpty(id)) 
            return false;
        globalData[id] = data;
        return true;
    }

    /// <summary>
    /// Removes global data by key.
    /// </summary>
    /// <param name="id">The key of the data to remove.</param>
    /// <returns>True if the data was removed, false if it doesn't exist.</returns>
    public bool RemoveGlobalData(string id)
    {
        if (!globalData.ContainsKey(id))
            return false;
        globalData.Remove(id);
        return true;
    }

    /// <summary>
    /// Attempts to retrieve global data by key and cast it to the specified type.
    /// </summary>
    /// <typeparam name="TData">The expected type of the data.</typeparam>
    /// <param name="id">The key of the data to retrieve.</param>
    /// <param name="data">The retrieved data if successful.</param>
    /// <returns>True if the data exists and is of the correct type, false otherwise.</returns>
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

    /// <summary>
    /// Attempts to retrieve data that was passed with the current transition.
    /// </summary>
    /// <typeparam name="TData">The expected type of the data.</typeparam>
    /// <param name="data">The retrieved data if successful.</param>
    /// <returns>True if transition data exists and is of the correct type, false otherwise.</returns>
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

    /// <summary>
    /// Checks if the state machine is currently active (not paused).
    /// </summary>
    /// <returns>True if active, false if paused.</returns>
    public bool IsActive() => !paused;
    
    /// <summary>
    /// Pauses the state machine, preventing updates and transitions.
    /// </summary>
    public void Pause() => paused = true;
    
    /// <summary>
    /// Resumes the state machine from a paused state.
    /// </summary>
    /// <param name="resetTime">If true, resets the state time counter.</param>
    public void Resume(bool resetTime = false)
    {   
        if (resetTime)
            ResetStateTime();
        paused = false;
    }

    /// <summary>
    /// Gets how long the state machine was in the previous state before transitioning
    /// </summary>
    /// <returns>The time spent in the previous state, or -1 if there is no previous state.</returns>
    public float GetPreviousStateTime() => hasPreviousState ? lastStateTime : -1f;
    
    /// <summary>
    /// Gets the time elapsed in the current state.
    /// </summary>
    /// <returns>The current state time in seconds.</returns>
    public float GetStateTime() => stateTime;
    
    /// <summary>
    /// Gets the minimum time required before transitions can occur from the current state.
    /// </summary>
    /// <returns>The minimum state time, or -1 if there is no current state.</returns>
    public float GetMinStateTime() => currentState?.MinTime ?? -1f;
    
    /// <summary>
    /// Gets the remaining time until the current state times out.
    /// </summary>
    /// <returns>The remaining time in seconds, or -1 if the current state has no timeout.</returns>
    public float GetRemainingTime() => currentState?.Timeout > 0f ? Math.Max(0f, currentState.Timeout - stateTime) : -1f;

    /// <summary>
    /// Gets a state by its identifier.
    /// </summary>
    /// <param name="id">The state identifier.</param>
    /// <returns>The State object, or null if not found.</returns>
    public State<T> GetState(T id) => states.TryGetValue(id, out var result) ? result : null;

    /// <summary>
    /// Gets the first state that has the specified tag.
    /// </summary>
    /// <param name="tag">The tag to search for.</param>
    /// <returns>The State object with the tag, or null if not found.</returns>
    public State<T> GetStateWithTag(string tag)
    {
        foreach (var kvp in states)
            if (kvp.Value.HasTag(tag))
                return kvp.Value;
        return null;
    }

    /// <summary>
    /// Gets the timeout progress as a normalized value between 0 and 1.
    /// </summary>
    /// <returns>The progress (0-1), or -1 if the current state has no timeout.</returns>
    public float GetTimeoutProgress()
    {
        if (currentState == null || currentState.Timeout <= 0f)
            return -1f;
        return Math.Clamp(stateTime / currentState.Timeout, 0f, 1f);
    }

    /// <summary>
    /// Gets the identifier of the current state.
    /// </summary>
    /// <returns>The current state identifier, or default if no state is active.</returns>
    public T GetCurrentId() => currentState != null ? currentState.Id : default;
    
    /// <summary>
    /// Gets the identifier of the initial state.
    /// </summary>
    /// <returns>The initial state identifier, or default if not initialized.</returns>
    public T GetInitialId() => initialized ? initialId : default;
    
    /// <summary>
    /// Attempts to get the identifier of the previous state.
    /// </summary>
    /// <param name="id">The previous state identifier if available.</param>
    /// <returns>True if there is a previous state, false otherwise.</returns>
    public bool TryGetPreviousId(out T id)
    {
        id = hasPreviousState ? previousId : default;
        return hasPreviousState;
    }

    /// <summary>
    /// Checks if a transition exists between two states.
    /// </summary>
    /// <param name="from">The source state identifier.</param>
    /// <param name="to">The target state identifier.</param>
    /// <returns>True if the transition exists, false otherwise.</returns>
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
    
    /// <summary>
}
