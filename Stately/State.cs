using System;
using System.Collections.Generic;

namespace Stately
{
    public class State<T> where T : Enum
    {
        public Cooldown Cooldown => cooldown;
        public IReadOnlyCollection<string> Tags => tags;

        public bool IsParent => ChildrenIds.Count > 0;
        public bool IsChild => !ParentId.Equals(default(T));
        public bool IsRoot => !IsChild;
        public bool IsLeaf => !IsParent;

        public T Id { get; private set; }
        public T TimeoutTargetId { get; set; }

        public List<Transition<T>> Transitions { get; set; } = new();

        public float MinTime { get; private set; }
        public float Timeout { get; private set; } = -1f;

        public Action<float> Update { get; private set; }

        public Action Enter { get; private set; }
        public Action Exit { get; private set; }

        public Action OnTimeoutTriggered { get; private set; }
        public Action TimeoutExpired { get; private set; }

        public FSMProcessMode ProcessMode { get; private set; }
        public FSMLockMode LockMode { get; private set; }

        public T ParentId { get; internal set; }
        public T DefaultChildId { get; private set; }
        public List<T> ChildrenIds { get; private set; } = new();

        private HashSet<string> tags = new();
        private Dictionary<Type, object> data = new();

        private readonly Cooldown cooldown = new();

        public State(T id)
        {
            Id = id;
        }

        public State<T> SetDefaultChild(T childId)
        {
            DefaultChildId = childId;
            return this;
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

        /// <summary>
        /// Called when state timeout while it's locked;
        /// Call OnTimeout() method instead if state isn't locked;
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public State<T> OnTimeoutExpired(Action method)
        {
            TimeoutExpired = method;
            return this;
        }

        public State<T> MinDuration(float duration)
        {
            MinTime = MathF.Max(0f, duration);
            return this;
        }

        public State<T> TimeoutAfter(float duration)
        {
            Timeout = duration;
            return this;
        }

        public State<T> TimeoutAfter(float duration, T to)
        {
            TimeoutTargetId = to;
            return TimeoutAfter(duration);
        }

        public State<T> ProcessIn(FSMProcessMode mode)
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

        public bool HasTag(string tag)
        {
            return tags.Contains(tag);
        }

        public State<T> SetData<TData>(TData value)
        {
            data[typeof(TData)] = value;
            return this;
        }

        public bool TryGetData<TData>(out TData value)
        {
            if (data.TryGetValue(typeof(TData), out var result) && result is TData castValue)
            {
                value = castValue;
                return true;
            }
            value = default;
            return false;
        }

        public bool RemoveData<TData>()
        {
            return data.Remove(typeof(TData));
        }

        public TData GetData<TData>()
        {
            if (data.TryGetValue(typeof(TData), out var result) && result is TData castValue)
                return castValue;
            return default;
        }

        public bool IsLocked() => LockMode != FSMLockMode.None;
        public bool IsFullyLocked() => LockMode == FSMLockMode.Full;
        public bool TransitionBlocked() => LockMode == FSMLockMode.Transition;

        public bool HasData<TData>() => data.ContainsKey(typeof(TData));
        public bool HasDataWithValue<TData>(object value) => data.TryGetValue(typeof(TData), out var result) && result.Equals(value);

        public State<T> From(StateTemplate<T> template)
        {
            template?.ApplyTo(this);
            return this;
        }

        public State<T> SetCooldown(float duration)
        {
            cooldown.SetDuration(duration);
            return this;
        }

        public bool IsOnCooldown()
        {
            return cooldown.IsActive;
        }

        internal void StartCooldown()
        {
            cooldown.Start();
        }

        internal void UpdateCooldown(float delta)
        {
            cooldown.Update(delta);
        }

        internal void SetDataDirect(Type type, object value)
        {
            data[type] = value;
        }
    }
}
