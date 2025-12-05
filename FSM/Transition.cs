using System;
using System.Collections.Generic;

namespace Godot.FSM;

public class Transition<T> where T : Enum
{
    private static long globalInsertionCounter = 0;

    public T From { get; private set; }
    public T To { get; private set; }

    public Predicate<StateMachine<T>> Condition { get; private set; }
    public Predicate<StateMachine<T>> Guard { get; private set; }

    public string EventName { get; private set; }
    public float OverrideMinTime { get; private set; } = -1f;
    public int Priority { get; private set; }
    public long InsertionIndex { get; private set; }

    public bool ForceInstantTransition { get; private set; }

    public Action Triggered { get; private set; }

    public Transition(T from, T to)
    {
        From = from;
        To = to;

        InsertionIndex = globalInsertionCounter++;
    }


    public Transition<T> OnTriggered(Action callback)
    {
        Triggered = callback;
        return this;
    }

    public Transition<T> OnEvent(string eventName)
    {
        EventName = eventName;
        return this;
    }

    public Transition<T> SetCondition(Predicate<StateMachine<T>> condition)
    {
        Condition = condition;
        return this;   
    }

    public Transition<T> SetGuard(Predicate<StateMachine<T>> guard)
    {
        Guard = guard;
        return this;
    }

    public Transition<T> SetMinTime(float minTime)
    {
        OverrideMinTime = Math.Max(0f, minTime);
        return this;
    }

    public Transition<T> SetPriority(int priority)
    {
        Priority = priority;
        return this;
    }

    public Transition<T> SetOnTop()
    {
        Priority = int.MaxValue;
        return this;
    }

    public Transition<T> ForceInstant()
    {
        ForceInstantTransition = true;
        return this;
    }

    public Transition<T> BreakInstant()
    {
        ForceInstantTransition = false;
        return this;
    }

    internal static int Compare(Transition<T> a, Transition<T> b)
    {
        int priorityCompare = b.Priority.CompareTo(a.Priority);
        return priorityCompare != 0 ? priorityCompare : a.InsertionIndex.CompareTo(b.InsertionIndex);
    }
}

