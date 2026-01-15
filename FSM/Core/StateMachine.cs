using System;
using System.Collections.Generic;

namespace Stately;

public partial class StateMachine<T> : IDisposable where T : Enum
{
    public event Action<T, T> StateChanged;
    public event Action<T, T> TransitionTriggered;
    public event Action<T> TimeoutBlocked;
    public event Action<T> StateTimeout;

    private const int MaxTransitionQueueSize = 20;

    public StateHistory<T> StateHistory => history;
    public State<T> CurrentState => currentState;
    public float StateTime => stateTime;

    private StateHistory<T> history = new();

    private Dictionary<T, State<T>> states = new();
    private Dictionary<Type, object> globalData = new();

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
    private bool disposed;
    private bool started;

    private float stateTime;

    private ILogger logger;
    private object transitionData;

    private FSMProcessMode timersProcessMode = FSMProcessMode.Idle;

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
        history.ClearHistory();

        disposed = true;
    }

    public void Start()
    {
        if (initialized)
            PerformTransition(initialId, bypassExit: true);
        started = true;
    }

    public void Stop()
    {
        if (!started)
            return;
        
        currentState?.Exit?.Invoke();
        currentState = null;
        started = false;
        paused = false;
        stateTime = 0f;
    }

    public void UpdateIdle(float delta)
    {
        Process(FSMProcessMode.Idle, delta);
    }

    public void UpdateFixed(float delta)
    {
        Process(FSMProcessMode.Fixed, delta);
    }

    private void Process(FSMProcessMode mode, float delta)
    {
        if (!started)
        {
            logger.LogError("State Machine hasn't started yet. Make sure to call The Start() method first");
            return;
        }

        if (paused || currentState == null)
            return;

        if (timersProcessMode == mode)
            UpdateCooldownTimers(delta);

        if (currentState.ProcessMode == mode)
        {
            stateTime += delta;
            currentState.Update?.Invoke(delta);

            CheckTransitions();
        }
    }

    private void UpdateCooldownTimers(float delta)
    {
        history.UpdateElapsedTime(delta);

        currentState?.UpdateCooldown(delta);

        foreach (var kvp in states)
        {
            if (kvp.Value != currentState)
                kvp.Value.UpdateCooldown(delta);
        }
        
        foreach (var transition in cachedSortedTransitions)
            transition.UpdateCooldown(delta);
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
    public void LogError(string text) => Console.WriteLine($"[ERROR] {text}");
    public void LogWarning(string text) => Console.WriteLine($"[WARN] {text}");
}
