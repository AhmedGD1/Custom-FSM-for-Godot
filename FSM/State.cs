using System;
using System.Collections.Generic;

namespace Godot.FSM;

public class State<T> where T : Enum
{
    public T Id { get; private set; }
    public T TimeoutTargetId { get; private set; }

    public List<Transition<T>> Transitions { get; set; } = new();

    public float MinTime { get; private set; }
    public float Timeout { get; private set; } = 1f;

    public Action<float> Update { get; private set; }
    public Action Enter { get; private set; }
    public Action Exit { get; private set; }
    public Action OnTimeoutTriggered { get; private set; }

    public FSMProcessMode ProcessMode { get; private set; }
    public FSMLockMode LockMode { get; private set; }

    public IReadOnlyCollection<string> Tags => tags;
    public IReadOnlyDictionary<string, object> Data => data;

    private HashSet<string> tags = new();
    private Dictionary<string, object> data = new();

    public State(T id)
    {
        Id = id;
    }

    public Transition<T> AddTransition(T to)
    {
        Transition<T> transition = new Transition<T>(Id, to);
        Transitions.Add(transition);

        return transition;
    }

    public bool RemoveTransition(T to)
    {
        int removed = 0;
        removed = Transitions.RemoveAll(t => t.To.Equals(to));

        return removed > 0;
    }

    public State<T> OnUpdate(Action<float> update)
    {
        Update = update;
        return this;
    }

    public State<T> OnEnter(Action enter)
    {
        Enter = enter;
        return this;
    }

    public State<T> OnExit(Action exit)
    {
        Exit = exit;
        return this;
    }

    public State<T> OnTimeout(Action method)
    {
        OnTimeoutTriggered = method;
        return this;
    }

    public State<T> SetMinTime(float duration)
    {
        MinTime = MathF.Max(0f, duration);
        return this;
    }

    public State<T> SetTimeout(float duration)
    {
        Timeout = duration;
        return this;
    }

    public State<T> SetTimeoutId(T id)
    {
        TimeoutTargetId = id;
        return this;
    }

    public State<T> SetProcessMode(FSMProcessMode mode)
    {
        ProcessMode = mode;
        return this;
    }

    public State<T> Lock(FSMLockMode mode = FSMLockMode.Full)
    {
        LockMode = mode;
        return this;
    }

    public State<T> Unlock()
    {
        LockMode = FSMLockMode.None;
        return this;
    }

    public State<T> AddTags(params string[] what)
    {
        foreach (string tag in what)
            tags.Add(tag);
        return this;
    }

    public State<T> SetData(string id, object value)
    {
        data[id] = value;
        return this;
    }

    public bool TryGetData<TData>(string id, out TData value)
    {
        if (data.TryGetValue(id, out var result) && result is TData castValue)
        {
            value = castValue;
            return true;
        }
        
        value = default;
        return false;
    }

    public bool RemoveData(string id)
    {
        return data.Remove(id);
    }

   public bool IsLocked() => LockMode != FSMLockMode.None;
   public bool IsFullyLocked() => LockMode == FSMLockMode.Full;
   public bool TransitionBlocked() => LockMode == FSMLockMode.Transition;

   public bool HasData(string id) => data.ContainsKey(id);
   public bool HasData(object dataValue) => data.ContainsValue(dataValue);

}
