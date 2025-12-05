using System;
using System.Collections.Generic;

namespace Godot.FSM;

public class State<T> where T : Enum
{
    public T Id { get; private set; }
    public T RestartId { get; private set; }

    public List<Transition<T>> Transitions { get; set; } = new();

    public float MinTime { get; private set; }
    public float Timeout { get; private set; } = -1f;

    public Action<double> Update { get; private set; }
    public Action Enter { get; private set; }
    public Action Exit { get; private set; }
    public Action Callback { get; private set; }

    public FSMProcessMode ProcessMode { get; private set; }
    public FSMLockMode LockMode { get; private set; }

    private readonly HashSet<string> tags = new();
    private readonly Dictionary<string, object> data = new();

    public IReadOnlyCollection<string> Tags => tags;
    public IReadOnlyDictionary<string, object> Data => data;

    public State(T id)
    {
        Id = id;
    }

    public Transition<T> AddTransition(T to)
    {
        if (Transitions.Find(t => t.To.Equals(to)) != null)
            return null;

        Transition<T> transition = new Transition<T>(Id, to);
        Transitions.Add(transition);

        return transition;
    }

    public bool RemoveTransitions(T to)
    {
        int removed = 0;
        removed = Transitions.RemoveAll(t => t.To.Equals(to));

        return removed > 0;
    }

    public State<T> OnUpdate(Action<double> update)
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
        Callback = method;
        return this;
    }

    public State<T> SetMinTime(float value)
    {
        MinTime = Math.Max(0f, value);
        return this;
    }

    public State<T> SetTimeout(float value)
    {
        Timeout = value;
        return this;
    }

    public State<T> SetRestart(T id)
    {
        RestartId = id;
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

    public State<T> UnLock()
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

    public State<T> RegisterData(string id, object value)
    {
        if (data.ContainsKey(id))
            return this;
        data[id] = value;
        return this;
    }

    public bool RemoveData(string id)
    {
        return data.Remove(id);
    }

    public bool TryGetData<TData>(string id, out TData value)
    {
        if (data.TryGetValue(id, out var obj) && obj is TData typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }

    public bool IsLocked() => LockMode != FSMLockMode.None;
    public bool IsFullyLocked() => LockMode == FSMLockMode.Full;
    public bool TransitionBlocked() => LockMode == FSMLockMode.Transition;

    public bool HasTag(string tag) => tags.Contains(tag);
    public bool HasData(string id) => data.ContainsKey(id);
    public bool HasData(object value) => data.ContainsValue(value);
}