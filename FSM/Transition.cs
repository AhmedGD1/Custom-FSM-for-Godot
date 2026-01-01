using System;
using System.Collections.Generic;

namespace Godot.FSM;

public class Transition<T> where T : Enum
{
    private static int globalInsertionCounter = 0;
    private long InsertionIndex { get; set; }

    public T From { get; private set; }
    public T To { get; private set; }

    /// <summary>
    /// Guard: Pre-condition check evaluated FIRST, before time requirements.
    /// Use for early rejection (e.g., "is player alive?", "is feature unlocked?")
    /// Return FALSE to prevent this transition from being considered at all.
    /// Guards are checked in both regular and event-based transitions.
    /// </summary>
    public Predicate<StateMachine<T>> Guard { get; private set; }

    /// <summary>
    /// Condition: Main transition trigger evaluated AFTER guard and time requirements.
    /// Use for the actual transition logic (e.g., "is health < 20?", "is enemy visible?")
    /// Return TRUE to trigger the transition.
    /// Only used in regular transitions (not event-based transitions).
    /// </summary>
    public Predicate<StateMachine<T>> Condition { get; private set; }

    public Action OnTriggered { get; private set; }

    public string EventName { get; private set; }

    public float OverrideMinTime { get; private set; } = -1f;
    public int Priority { get; private set; }

    public bool ForceInstantTransition { get; private set; }

    public Transition(T from, T to)
    {
        From = from;
        To = to;

        InsertionIndex = globalInsertionCounter++;
    }

    public Transition<T> OnTrigger(Action method)
    {
        OnTriggered = method;
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

    public Transition<T> SetMinTime(float value)
    {
        OverrideMinTime = value;
        return this;
    }

    public Transition<T> SetPriority(int priority)
    {
        Priority = priority;
        return this;
    }

    public Transition<T> SetMaxPriority()
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

    internal static int Compare(Transition<T> transition1, Transition<T> transition2)
    {
        int priorityCompare = transition2.Priority.CompareTo(transition1.Priority);
        return priorityCompare != 0 ? priorityCompare : transition1.InsertionIndex.CompareTo(transition2.InsertionIndex);
    }
}

